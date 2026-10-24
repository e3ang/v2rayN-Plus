namespace ServiceLib.ViewModels;

/// <summary>
/// Groups sing-box clash API connections by process, keeps running per-app totals,
/// and lets the user pin an app to block / direct / proxy through a routing rule.
/// </summary>
public partial class AppTrafficViewModel : MyReactiveObject
{
    public const string AppRuleRemarks = "AppControl";
    private const string UnknownProcess = "(unknown)";

    private readonly Func<Task> _reload;
    private readonly Dictionary<string, AppTrafficModel> _apps = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (ulong Up, ulong Down)> _lastConnBytes = [];
    private Dictionary<string, string> _appRules = new(StringComparer.OrdinalIgnoreCase);

    public AppTrafficViewModel(Func<Task> reload)
    {
        _config = AppManager.Instance.Config;
        _reload = reload;

        var canAct = this.WhenAnyValue(
            x => x.SelectedSource,
            selectedSource => selectedSource != null && selectedSource.ProcessName != UnknownProcess);

        BlockCmd = ReactiveCommand.CreateFromTask(() => SetAppRule(Global.BlockTag), canAct);
        DirectCmd = ReactiveCommand.CreateFromTask(() => SetAppRule(Global.DirectTag), canAct);
        ProxyCmd = ReactiveCommand.CreateFromTask(() => SetAppRule(Global.ProxyTag), canAct);
        ResetRuleCmd = ReactiveCommand.CreateFromTask(() => SetAppRule(null), canAct);
        CloseConnectionsCmd = ReactiveCommand.CreateFromTask(CloseAppConnections, canAct);
        ResetStatsCmd = ReactiveCommand.Create(ResetStats);

        _ = LoadAppRules();
        Task.Run(PollTask);
    }

    public ObservableCollection<AppTrafficModel> AppItems { get; } = [];

    [Reactive] public partial AppTrafficModel? SelectedSource { get; set; }

    public ReactiveCommand<RxVoid, RxVoid> BlockCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> DirectCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ProxyCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ResetRuleCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> CloseConnectionsCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ResetStatsCmd { get; }

    private async Task PollTask()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
            {
                if (!AppManager.Instance.IsRunningCore(ECoreType.sing_box))
                {
                    continue;
                }
                var ret = await ClashApiManager.Instance.GetConnections();
                if (ret?.connections == null)
                {
                    continue;
                }
                RxSchedulers.MainThreadScheduler.Schedule(() => Accumulate(ret.connections));
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("AppTrafficViewModel", ex);
        }
    }

    private static string GetProcessName(string? processPath)
    {
        if (processPath.IsNullOrEmpty())
        {
            return UnknownProcess;
        }
        var name = Path.GetFileName(processPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
        return name.IsNullOrEmpty() ? processPath : name;
    }

    private AppTrafficModel GetOrAddApp(string name)
    {
        if (!_apps.TryGetValue(name, out var app))
        {
            app = new AppTrafficModel { ProcessName = name };
            UpdateRuleText(app);
            _apps[name] = app;
            AppItems.Add(app);
        }
        return app;
    }

    private void Accumulate(List<ConnectionItem> connections)
    {
        var tickBytes = new Dictionary<string, (ulong Up, ulong Down, int Count)>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>();

        foreach (var conn in connections)
        {
            if (conn.id.IsNullOrEmpty())
            {
                continue;
            }
            seen.Add(conn.id);

            var name = GetProcessName(conn.metadata?.processPath);
            var app = GetOrAddApp(name);
            if (conn.metadata?.processPath.IsNotEmpty() == true)
            {
                app.ProcessPath = conn.metadata.processPath;
            }

            // a connection's counters only grow; a smaller value means the core restarted and reused the id
            _lastConnBytes.TryGetValue(conn.id, out var last);
            var dUp = conn.upload >= last.Up ? conn.upload - last.Up : conn.upload;
            var dDown = conn.download >= last.Down ? conn.download - last.Down : conn.download;
            _lastConnBytes[conn.id] = (conn.upload, conn.download);

            tickBytes.TryGetValue(name, out var t);
            tickBytes[name] = (t.Up + dUp, t.Down + dDown, t.Count + 1);
        }

        foreach (var id in _lastConnBytes.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _lastConnBytes.Remove(id);
        }

        foreach (var app in _apps.Values)
        {
            tickBytes.TryGetValue(app.ProcessName, out var t);
            app.Upload += t.Up;
            app.Download += t.Down;
            app.Connections = t.Count;
            app.UploadTraffic = Utils.HumanFyBytes((long)app.Upload);
            app.DownloadTraffic = Utils.HumanFyBytes((long)app.Download);
            app.Speed = t.Up + t.Down > 0
                ? $"↑{Utils.HumanFyBytes((long)t.Up)}/s ↓{Utils.HumanFyBytes((long)t.Down)}/s"
                : string.Empty;
        }
    }

    private void UpdateRuleText(AppTrafficModel app)
    {
        app.Rule = _appRules.GetValueOrDefault(app.ProcessName) switch
        {
            Global.BlockTag => ResUI.TbAppRuleBlock,
            Global.DirectTag => ResUI.TbAppRuleDirect,
            Global.ProxyTag => ResUI.TbAppRuleProxy,
            _ => ResUI.TbAppRuleDefault,
        };
    }

    private static List<RulesItem> GetRules(RoutingItem routing)
    {
        return (routing.RuleSet.IsNullOrEmpty() ? null : JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet)) ?? [];
    }

    private static bool IsAppRule(RulesItem rule, string? processName = null)
    {
        return rule.Remarks == AppRuleRemarks
               && rule.Process?.Count == 1
               && (processName == null || rule.Process[0].Equals(processName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task LoadAppRules()
    {
        var routing = await ConfigHandler.GetDefaultRouting(_config);
        var rules = routing == null ? [] : GetRules(routing);
        _appRules = rules
            .Where(r => IsAppRule(r) && r.Enabled)
            .GroupBy(r => r.Process![0], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().OutboundTag ?? Global.ProxyTag, StringComparer.OrdinalIgnoreCase);

        RxSchedulers.MainThreadScheduler.Schedule(() =>
        {
            // blocked apps have no live connections, keep them listed so they can be unblocked
            foreach (var name in _appRules.Keys)
            {
                GetOrAddApp(name);
            }
            foreach (var app in _apps.Values)
            {
                UpdateRuleText(app);
            }
        });
    }

    private async Task SetAppRule(string? outboundTag)
    {
        var name = SelectedSource?.ProcessName;
        if (name.IsNullOrEmpty() || name == UnknownProcess)
        {
            return;
        }

        var routing = await ConfigHandler.GetDefaultRouting(_config);
        if (routing == null)
        {
            return;
        }
        var rules = GetRules(routing);
        rules.RemoveAll(r => IsAppRule(r, name));
        if (outboundTag != null)
        {
            // first so it wins over domain / ip rules of the routing set
            rules.Insert(0, new RulesItem
            {
                Id = Utils.GetGuid(false),
                Remarks = AppRuleRemarks,
                OutboundTag = outboundTag,
                Process = [name],
                Enabled = true,
            });
        }
        routing.RuleNum = rules.Count;
        routing.RuleSet = JsonUtils.Serialize(rules, false);
        if (await ConfigHandler.SaveRoutingItem(_config, routing) != 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            return;
        }

        await LoadAppRules();
        NoticeManager.Instance.Enqueue(ResUI.TbAppRuleSaved);
        await _reload();
    }

    private async Task CloseAppConnections()
    {
        var name = SelectedSource?.ProcessName;
        var ret = await ClashApiManager.Instance.GetConnections();
        foreach (var conn in ret?.connections ?? [])
        {
            if (conn.id.IsNotEmpty() && GetProcessName(conn.metadata?.processPath).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                await ClashApiManager.Instance.CloseConnection(conn.id);
            }
        }
    }

    private void ResetStats()
    {
        foreach (var app in _apps.Values)
        {
            app.Upload = 0;
            app.Download = 0;
            app.UploadTraffic = Utils.HumanFyBytes(0);
            app.DownloadTraffic = Utils.HumanFyBytes(0);
        }
    }
}

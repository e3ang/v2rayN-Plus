namespace ServiceLib.Services;

/// <summary>
/// Some networks freeze an SSH connection for tens of seconds. sing-box's SSH outbound has no
/// keepalive, so it keeps waiting on the frozen connection even after the path opens again.
/// This probes through the local proxy while an SSH server is active and, when it stops
/// answering, reloads the core so a fresh SSH session is dialed (what OpenSSH does with
/// ServerAliveInterval). It keeps retrying while the freeze lasts.
/// </summary>
public class SshWatchdogService(Config config, Func<Task> reload)
{
    private const string _tag = "SshWatchdog";

    public static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);

    private readonly Config _config = config;
    private readonly Func<Task> _reload = reload;
    private readonly SshWatchdogState _state = new();

    public void Start()
    {
        Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        using var timer = new PeriodicTimer(ProbeInterval);
        while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
        {
            try
            {
                if (_config.AutoSwitchItem?.SshWatchdog != true
                    || !AppManager.Instance.IsRunningCore(ECoreType.sing_box)
                    || (await AppManager.Instance.GetProfileItem(_config.IndexId))?.ConfigType != EConfigType.SSH)
                {
                    _state.Reset();
                    continue;
                }

                var ok = await ProbeAsync();
                switch (_state.OnProbe(ok, DateTime.UtcNow))
                {
                    case SshWatchdogAction.Reconnect:
                        Logging.SaveLog($"{_tag}: SSH tunnel not answering, reconnecting");
                        NoticeManager.Instance.Enqueue(ResUI.MsgSshFrozenReconnecting);
                        await _reload();
                        break;

                    case SshWatchdogAction.Recovered:
                        Logging.SaveLog($"{_tag}: SSH tunnel back");
                        NoticeManager.Instance.Enqueue(ResUI.MsgSshReconnected);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }
        }
    }

    private async Task<bool> ProbeAsync()
    {
        var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
        var url = _config.SpeedTestItem.SpeedPingTestUrl.IsNullOrEmpty()
            ? Global.SpeedPingTestUrls.First()
            : _config.SpeedTestItem.SpeedPingTestUrl;
        try
        {
            using var handler = new SocketsHttpHandler
            {
                Proxy = new WebProxy($"socks5://{Global.Loopback}:{port}"),
                UseProxy = true,
            };
            using var client = new HttpClient(handler) { Timeout = ProbeTimeout };
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            // any HTTP answer means data flows through the tunnel
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public enum SshWatchdogAction
{
    None,
    Reconnect,
    Recovered,
}

/// <summary>
/// Decision part of the watchdog, kept free of I/O so it can be tested.
/// </summary>
public class SshWatchdogState
{
    public const int FailuresBeforeReconnect = 3;
    public static readonly TimeSpan MinReconnectGap = TimeSpan.FromSeconds(30);

    private int _failures;
    private DateTime _lastReconnect = DateTime.MinValue;
    private bool _reconnecting;

    public SshWatchdogAction OnProbe(bool ok, DateTime now)
    {
        if (ok)
        {
            _failures = 0;
            if (_reconnecting)
            {
                _reconnecting = false;
                return SshWatchdogAction.Recovered;
            }
            return SshWatchdogAction.None;
        }

        _failures++;
        if (_failures < FailuresBeforeReconnect || now - _lastReconnect < MinReconnectGap)
        {
            return SshWatchdogAction.None;
        }

        // a reload drops every open connection, so retry slowly while a freeze lasts
        _failures = 0;
        _lastReconnect = now;
        _reconnecting = true;
        return SshWatchdogAction.Reconnect;
    }

    public void Reset()
    {
        _failures = 0;
        _reconnecting = false;
    }
}

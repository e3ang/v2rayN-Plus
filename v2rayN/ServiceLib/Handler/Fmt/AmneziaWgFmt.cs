namespace ServiceLib.Handler.Fmt;

/// <summary>
/// Turns an AmneziaWG / WireGuard .conf (as exported by AmneziaVPN or wg-quick) into a mihomo
/// config, since neither Xray nor sing-box speak AmneziaWG. v2rayN then runs it as a custom
/// mihomo profile and injects the local port, TUN and clash API like for any custom config.
/// </summary>
public static class AmneziaWgFmt
{
    private const string ProxyName = "awg";

    // Interface keys that go into amnezia-wg-option as numbers / as strings
    private static readonly string[] _intKeys = ["Jc", "Jmin", "Jmax", "S1", "S2", "S3", "S4"];
    private static readonly string[] _strKeys = ["H1", "H2", "H3", "H4", "I1", "I2", "I3", "I4", "I5", "J1", "J2", "J3"];

    public static bool IsWireGuardConf(string text)
    {
        return text.Contains("[Interface]", StringComparison.OrdinalIgnoreCase)
               && text.Contains("[Peer]", StringComparison.OrdinalIgnoreCase);
    }

    public static string? ToMihomoYaml(string confText)
    {
        var sections = ParseIni(confText);
        var iface = sections.GetValueOrDefault("interface");
        var peer = sections.GetValueOrDefault("peer");
        if (iface == null || peer == null
            || !iface.TryGetValue("PrivateKey", out var privateKey)
            || !peer.TryGetValue("PublicKey", out var publicKey)
            || !peer.TryGetValue("Endpoint", out var endpoint))
        {
            return null;
        }

        var (server, port) = SplitEndpoint(endpoint);
        if (server.IsNullOrEmpty() || port <= 0)
        {
            return null;
        }

        var proxy = new Dictionary<string, object>
        {
            ["name"] = ProxyName,
            ["type"] = "wireguard",
            ["server"] = server,
            ["port"] = port,
            ["private-key"] = privateKey,
            ["public-key"] = publicKey,
            ["udp"] = true,
        };

        foreach (var addr in SplitList(iface.GetValueOrDefault("Address")))
        {
            var key = addr.Contains(':') ? "ipv6" : "ip";
            proxy.TryAdd(key, addr.Contains('/') ? addr : $"{addr}/{(key == "ip" ? 32 : 128)}");
        }
        if (peer.TryGetValue("PresharedKey", out var psk) && psk.IsNotEmpty())
        {
            proxy["pre-shared-key"] = psk;
        }
        var allowedIps = SplitList(peer.GetValueOrDefault("AllowedIPs"));
        proxy["allowed-ips"] = allowedIps.Count > 0 ? allowedIps : ["0.0.0.0/0", "::/0"];
        if (int.TryParse(iface.GetValueOrDefault("MTU"), out var mtu) && mtu > 0)
        {
            proxy["mtu"] = mtu;
        }
        if (int.TryParse(peer.GetValueOrDefault("PersistentKeepalive"), out var keepalive) && keepalive > 0)
        {
            proxy["persistent-keepalive"] = keepalive;
        }
        var dns = SplitList(iface.GetValueOrDefault("DNS"));
        if (dns.Count > 0)
        {
            proxy["remote-dns-resolve"] = true;
            proxy["dns"] = dns;
        }

        var awg = new Dictionary<string, object>();
        foreach (var key in _intKeys)
        {
            if (int.TryParse(iface.GetValueOrDefault(key), out var n))
            {
                awg[key.ToLowerInvariant()] = n;
            }
        }
        foreach (var key in _strKeys)
        {
            if (iface.TryGetValue(key, out var v) && v.IsNotEmpty())
            {
                awg[key.ToLowerInvariant()] = v;
            }
        }
        if (int.TryParse(iface.GetValueOrDefault("Itime"), out var itime))
        {
            awg["itime"] = itime;
        }
        if (awg.Count > 0)
        {
            proxy["amnezia-wg-option"] = awg;
        }

        var root = new Dictionary<string, object>
        {
            ["mode"] = "rule",
            ["proxies"] = new List<object> { proxy },
            ["rules"] = new List<string> { $"MATCH,{ProxyName}" },
        };
        return YamlUtils.ToYaml(root);
    }

    private static Dictionary<string, Dictionary<string, string>> ParseIni(string text)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? current = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var name = line[1..^1].Trim();
                // only the first [Peer] is used, mihomo's single-peer form
                current = sections.ContainsKey(name) ? null : sections[name] = new(StringComparer.OrdinalIgnoreCase);
                continue;
            }
            var eq = line.IndexOf('=');
            if (current == null || eq <= 0)
            {
                continue;
            }
            current[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return sections;
    }

    private static List<string> SplitList(string? value)
    {
        return value.IsNullOrEmpty()
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static (string? Host, int Port) SplitEndpoint(string endpoint)
    {
        var idx = endpoint.LastIndexOf(':');
        if (idx <= 0 || !int.TryParse(endpoint[(idx + 1)..], out var port))
        {
            return (null, 0);
        }
        return (endpoint[..idx].Trim('[', ']'), port);
    }
}

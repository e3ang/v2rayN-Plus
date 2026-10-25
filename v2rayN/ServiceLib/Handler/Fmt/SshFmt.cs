namespace ServiceLib.Handler.Fmt;

/// <summary>
/// ssh://user:password@host:port?pk=&lt;private key&gt;&amp;hk=&lt;host keys&gt;#remarks
/// pk / hk follow the Hiddify link; the longer sing-box field names are accepted too.
/// </summary>
public class SshFmt : BaseFmt
{
    public static ProfileItem? Resolve(string str, out string msg)
    {
        msg = ResUI.ConfigurationFormatIncorrect;

        var parsedUrl = Utils.TryUri(str);
        if (parsedUrl == null || parsedUrl.Port <= 0)
        {
            return null;
        }

        ProfileItem item = new()
        {
            ConfigType = EConfigType.SSH,
            Remarks = parsedUrl.GetComponents(UriComponents.Fragment, UriFormat.Unescaped),
            Address = parsedUrl.IdnHost,
            Port = parsedUrl.Port,
        };

        var userInfo = parsedUrl.UserInfo;
        var colon = userInfo.IndexOf(':');
        item.Username = Utils.UrlDecode(colon >= 0 ? userInfo[..colon] : userInfo);
        item.Password = colon >= 0 ? Utils.UrlDecode(userInfo[(colon + 1)..]) : string.Empty;

        var query = Utils.ParseQueryString(parsedUrl.Query);
        string? Get(params string[] keys) => keys.Select(k => GetQueryDecoded(query, k)).FirstOrDefault(v => v.IsNotEmpty()).NullIfEmpty();

        item.SetProtocolExtra(item.GetProtocolExtra() with
        {
            SshPrivateKey = Get("pk", "private_key"),
            SshPrivateKeyPath = Get("pkp", "private_key_path"),
            SshPrivateKeyPassphrase = Get("pp", "private_key_passphrase"),
            SshHostKey = Get("hk", "host_key"),
            SshHostKeyAlgorithms = Get("hka", "host_key_algorithms"),
            SshClientVersion = Get("cv", "client_version"),
        });
        return item;
    }

    public static string? ToUri(ProfileItem? item)
    {
        if (item == null)
        {
            return null;
        }
        var remark = item.Remarks.IsNotEmpty() ? "#" + Utils.UrlEncode(item.Remarks) : string.Empty;
        var userInfo = Utils.UrlEncode(item.Username ?? string.Empty);
        if (item.Password.IsNotEmpty())
        {
            userInfo += ":" + Utils.UrlEncode(item.Password);
        }

        var extra = item.GetProtocolExtra();
        var dicQuery = new Dictionary<string, string>();
        void Add(string key, string? value)
        {
            if (value.IsNotEmpty())
            {
                dicQuery[key] = Utils.UrlEncode(value);
            }
        }
        Add("pk", extra.SshPrivateKey);
        Add("pkp", extra.SshPrivateKeyPath);
        Add("pp", extra.SshPrivateKeyPassphrase);
        Add("hk", extra.SshHostKey);
        Add("hka", extra.SshHostKeyAlgorithms);
        Add("cv", extra.SshClientVersion);

        var query = dicQuery.Count > 0
            ? "?" + string.Join("&", dicQuery.Select(x => $"{x.Key}={x.Value}"))
            : string.Empty;
        return $"{Global.ProtocolShares[EConfigType.SSH]}{userInfo}@{GetIpv6(item.Address)}:{item.Port}{query}{remark}";
    }
}

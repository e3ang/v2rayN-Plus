using System.Globalization;

namespace ServiceLib.Models.Dto;

/// <summary>
/// The "subscription-userinfo" response header most panels (3x-ui, Marzban, Remnawave) send:
/// upload=455727941; download=6174315083; total=1073741824000; expire=1671815872
/// </summary>
public record SubscriptionUsage(long Upload, long Download, long Total, long Expire)
{
    public const string HeaderName = "subscription-userinfo";

    public long Used => Upload + Download;

    public static SubscriptionUsage? Parse(string? value)
    {
        if (value.IsNullOrEmpty())
        {
            return null;
        }

        long upload = 0, download = 0, total = 0, expire = 0;
        var found = false;
        foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length != 2 || !double.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
            {
                continue;
            }
            var n = (long)num;
            switch (kv[0].ToLowerInvariant())
            {
                case "upload": upload = n; found = true; break;
                case "download": download = n; found = true; break;
                case "total": total = n; found = true; break;
                case "expire": expire = n; found = true; break;
            }
        }
        return found ? new SubscriptionUsage(upload, download, total, expire) : null;
    }

    /// <summary>
    /// e.g. "6.2 GB / 1000 GB · 12d"
    /// </summary>
    public string Format(DateTimeOffset now)
    {
        var sb = new StringBuilder();
        sb.Append(Utils.HumanFy(Used));
        sb.Append(" / ");
        sb.Append(Total > 0 ? Utils.HumanFy(Total) : "∞");

        if (Expire > 0)
        {
            var left = DateTimeOffset.FromUnixTimeSeconds(Expire) - now;
            sb.Append(" · ");
            sb.Append(left.TotalSeconds <= 0
                ? ResUI.TbSubExpired
                : string.Format(ResUI.TbSubDaysLeft, Math.Max(1, (int)Math.Ceiling(left.TotalDays))));
        }
        return sb.ToString();
    }

    public static string FormatHeader(string? value)
    {
        return Parse(value)?.Format(DateTimeOffset.UtcNow) ?? string.Empty;
    }
}

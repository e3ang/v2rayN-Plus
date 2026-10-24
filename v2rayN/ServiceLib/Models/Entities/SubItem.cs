namespace ServiceLib.Models.Entities;

[Serializable]
public class SubItem
{
    [PrimaryKey]
    public string Id { get; set; }

    public string Remarks { get; set; }

    public string Url { get; set; }

    public string MoreUrl { get; set; }

    public bool Enabled { get; set; } = true;

    public string UserAgent { get; set; } = string.Empty;

    public string? RequestHeaders { get; set; }

    public int Sort { get; set; }

    public string? Filter { get; set; }

    public int AutoUpdateInterval { get; set; }

    public long UpdateTime { get; set; }

    public string? ConvertTarget { get; set; }

    public string? PrevProfile { get; set; }

    public string? NextProfile { get; set; }

    public int? PreSocksPort { get; set; }

    public string? Memo { get; set; }

    public ECoreType? CustomCoreType { get; set; }

    // raw "subscription-userinfo" header from the last successful update
    public string? UserInfo { get; set; }

    // send x-hwid / x-device-os / x-ver-os / x-device-model like Happ, for panels that limit devices
    public bool SendHwid { get; set; }

    [Ignore]
    public string UsageText => SubscriptionUsage.FormatHeader(UserInfo);
}

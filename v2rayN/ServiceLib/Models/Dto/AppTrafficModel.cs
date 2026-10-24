namespace ServiceLib.Models.Dto;

public partial class AppTrafficModel : ReactiveObject
{
    public string ProcessName { get; init; } = string.Empty;

    [Reactive] public partial string? ProcessPath { get; set; }
    [Reactive] public partial string? Rule { get; set; }
    [Reactive] public partial int Connections { get; set; }
    [Reactive] public partial string? UploadTraffic { get; set; }
    [Reactive] public partial string? DownloadTraffic { get; set; }
    [Reactive] public partial string? Speed { get; set; }

    public ulong Upload { get; set; }
    public ulong Download { get; set; }
}

using Microsoft.Win32;

namespace ServiceLib.Helper;

/// <summary>
/// Device headers in the format Happ / Remnawave panels use to count and limit devices.
/// The id is a hash of the OS machine id, so it is stable across reinstalls and reveals nothing.
/// </summary>
public static class HwidHelper
{
    private static readonly Lazy<string> _hwid = new(BuildHwid);

    public static string Hwid => _hwid.Value;

    public static IEnumerable<(string Name, string Value)> GetDeviceHeaders()
    {
        yield return ("x-hwid", Hwid);
        yield return ("x-device-os", Utils.IsWindows() ? "Windows" : Utils.IsMacOS() ? "macOS" : "Linux");
        yield return ("x-ver-os", Environment.OSVersion.Version.ToString());
        yield return ("x-device-model", "PC");
    }

    private static string BuildHwid()
    {
        var machineId = ReadMachineId();
        if (machineId.IsNullOrEmpty())
        {
            // no OS id readable: fall back to the install, which is still stable between runs
            machineId = Utils.StartupPath();
        }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"v2rayN-hwid:{machineId}"));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private static string? ReadMachineId()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", false);
                return key?.GetValue("MachineGuid") as string;
            }
            foreach (var path in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path).Trim();
                }
            }
            if (OperatingSystem.IsMacOS())
            {
                var output = Utils.GetCliWrapOutput("ioreg", ["-rd1", "-c", "IOPlatformExpertDevice"]).GetAwaiter().GetResult();
                var match = Regex.Match(output ?? string.Empty, "\"IOPlatformUUID\" = \"([^\"]+)\"");
                return match.Success ? match.Groups[1].Value : null;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("HwidHelper", ex);
        }
        return null;
    }
}

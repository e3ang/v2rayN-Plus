namespace ServiceLib.Tests.Services;

public class SshWatchdogStateTests
{
    private static readonly DateTime T0 = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task SingleFailure_DoesNotReconnect()
    {
        var s = new SshWatchdogState();
        await s.OnProbe(false, T0).Should().BeEqualTo(SshWatchdogAction.None);
        await s.OnProbe(true, T0.AddSeconds(5)).Should().BeEqualTo(SshWatchdogAction.None);
    }

    [Test]
    public async Task ThreeFailures_Reconnect_ThenRecovered()
    {
        var s = new SshWatchdogState();
        await s.OnProbe(false, T0).Should().BeEqualTo(SshWatchdogAction.None);
        await s.OnProbe(false, T0.AddSeconds(5)).Should().BeEqualTo(SshWatchdogAction.None);
        await s.OnProbe(false, T0.AddSeconds(10)).Should().BeEqualTo(SshWatchdogAction.Reconnect);
        await s.OnProbe(true, T0.AddSeconds(15)).Should().BeEqualTo(SshWatchdogAction.Recovered);
        await s.OnProbe(true, T0.AddSeconds(20)).Should().BeEqualTo(SshWatchdogAction.None);
    }

    [Test]
    public async Task LongFreeze_RetriesAtMostEvery30Seconds()
    {
        var s = new SshWatchdogState();
        var reconnects = 0;
        for (var i = 0; i < 24; i++) // two minutes of failed probes, 5 s apart
        {
            if (s.OnProbe(false, T0.AddSeconds(i * 5)) == SshWatchdogAction.Reconnect)
            {
                reconnects++;
            }
        }
        await reconnects.Should().BeEqualTo(4);
    }
}

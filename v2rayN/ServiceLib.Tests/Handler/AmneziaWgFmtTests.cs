namespace ServiceLib.Tests.Handler;

public class AmneziaWgFmtTests
{
    // keys are random test values, not a real peer
    internal const string SampleConf = """
        [Interface]
        PrivateKey = 0Mj8nO6SgRbUuvCs+fpLR3DnVx4MBxcYxGWvRxk7c3s=
        Address = 10.8.1.2/32, fd00::2/128
        DNS = 1.1.1.1, 1.0.0.1
        MTU = 1280
        Jc = 4
        Jmin = 40
        Jmax = 70
        S1 = 0
        S2 = 0
        H1 = 1
        H2 = 2
        H3 = 3
        H4 = 4

        [Peer]
        PublicKey = 3gmz3kGIx7S3lJl3Pt1TvJ0JqDsJ0d0CZT9XG6mJ2Ec=
        PresharedKey = Dk7ynn1Mz7f2oVwO6c5X8T5mJ6N0lW3T0b5P3Kq0dYA=
        AllowedIPs = 0.0.0.0/0, ::/0
        Endpoint = 203.0.113.10:51820
        PersistentKeepalive = 25
        """;

    [Test]
    public async Task ToMihomoYaml_AmneziaConf_MapsAllFields()
    {
        await AmneziaWgFmt.IsWireGuardConf(SampleConf).Should().BeTrue();

        var yaml = AmneziaWgFmt.ToMihomoYaml(SampleConf);
        await yaml.Should().NotBeNull();

        var root = YamlUtils.FromYaml<Dictionary<string, object>>(yaml!);
        var proxy = (Dictionary<object, object>)((List<object>)root["proxies"])[0];
        await proxy["type"].ToString().Should().BeEqualTo("wireguard");
        await proxy["server"].ToString().Should().BeEqualTo("203.0.113.10");
        await proxy["port"].ToString().Should().BeEqualTo("51820");
        await proxy["ip"].ToString().Should().BeEqualTo("10.8.1.2/32");
        await proxy["ipv6"].ToString().Should().BeEqualTo("fd00::2/128");
        await proxy["mtu"].ToString().Should().BeEqualTo("1280");
        var awg = (Dictionary<object, object>)proxy["amnezia-wg-option"];
        await awg["jc"].ToString().Should().BeEqualTo("4");
        await awg["jmax"].ToString().Should().BeEqualTo("70");
        await awg["h4"].ToString().Should().BeEqualTo("4");
        await yaml!.Should().Contain("MATCH,awg");
    }

    [Test]
    public async Task ToMihomoYaml_MissingPeer_ReturnsNull()
    {
        await AmneziaWgFmt.ToMihomoYaml("[Interface]\nPrivateKey = abc").Should().BeNull();
    }
}

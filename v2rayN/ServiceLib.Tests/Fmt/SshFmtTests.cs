namespace ServiceLib.Tests.Fmt;

public class SshFmtTests
{
    [Test]
    public async Task ShareLink_RoundTrip_KeepsAllFields()
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.SSH,
            Remarks = "my ssh",
            Address = "144.91.98.5",
            Port = 1252,
            Username = "tun",
            Password = "p@ss:w/rd+",
        };
        item.SetProtocolExtra(item.GetProtocolExtra() with
        {
            SshPrivateKey = "-----BEGIN OPENSSH PRIVATE KEY-----\nAAA+/=\n-----END OPENSSH PRIVATE KEY-----",
            SshPrivateKeyPath = "~/.ssh/id_ed25519",
            SshHostKey = "ssh-ed25519 AAAA",
        });

        var link = FmtHandler.GetShareUri(item)!;
        await link.Should().StartWith("ssh://");

        var back = FmtHandler.ResolveConfig(link, out _)!;
        await back.Should().NotBeNull();
        await back.ConfigType.Should().BeEqualTo(EConfigType.SSH);
        await back.Address.Should().BeEqualTo("144.91.98.5");
        await back.Port.Should().BeEqualTo(1252);
        await back.Username.Should().BeEqualTo("tun");
        await back.Password.Should().BeEqualTo("p@ss:w/rd+");
        await back.Remarks.Should().BeEqualTo("my ssh");
        var extra = back.GetProtocolExtra();
        await extra.SshPrivateKey.Should().BeEqualTo(item.GetProtocolExtra().SshPrivateKey);
        await extra.SshPrivateKeyPath.Should().BeEqualTo("~/.ssh/id_ed25519");
        await extra.SshHostKey.Should().BeEqualTo("ssh-ed25519 AAAA");
    }

    [Test]
    public async Task Resolve_LongParamNames_AreAccepted()
    {
        var back = FmtHandler.ResolveConfig("ssh://root@example.com:22?private_key_path=%2Froot%2Fkey&host_key=abc#x", out _)!;
        await back.GetProtocolExtra().SshPrivateKeyPath.Should().BeEqualTo("/root/key");
        await back.GetProtocolExtra().SshHostKey.Should().BeEqualTo("abc");
        await back.Password.Should().BeEqualTo(string.Empty);
    }

    [Test]
    public async Task AmneziaConf_IsDetected_PlainWireGuardIsNot()
    {
        await AmneziaWgFmt.IsAmneziaConf(ServiceLib.Tests.Handler.AmneziaWgFmtTests.SampleConf).Should().BeTrue();
        var plain = "[Interface]\nPrivateKey = a\nAddress = 10.0.0.2/32\n[Peer]\nPublicKey = b\nEndpoint = 1.2.3.4:51820";
        await AmneziaWgFmt.IsAmneziaConf(plain).Should().BeFalse();
        await AmneziaWgFmt.GetEndpoint(plain).Should().BeEqualTo("1.2.3.4:51820");
    }

    [Test]
    public async Task AwgProfile_SharesOriginalConf()
    {
        var item = new ProfileItem { ConfigType = EConfigType.Custom };
        item.SetProtocolExtra(item.GetProtocolExtra() with { AwgConf = "[Interface]\nJc = 4" });
        await FmtHandler.GetShareUri(item).Should().BeEqualTo("[Interface]\nJc = 4");
    }
}

namespace ServiceLib.Tests.CoreConfig.Singbox;

public class SingboxSshOutboundTests
{
    private static Outbound4Sbox GenerateProxy(ProfileItem node)
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box);

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Msg);
        }

        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        return cfg.outbounds.First(o => o.tag == Global.ProxyTag);
    }

    [Test]
    public async Task Ssh_PasswordOnly_EmitsExpectedFields()
    {
        var node = CoreConfigTestFactory.CreateSshNode(password: "p@ss");
        var proxy = GenerateProxy(node);

        await proxy.type.Should().BeEqualTo("ssh");
        await proxy.server.Should().BeEqualTo(node.Address);
        await proxy.server_port.Should().BeEqualTo(node.Port);
        await proxy.user.Should().BeEqualTo(node.Username);
        await proxy.password.Should().BeEqualTo("p@ss");
        await proxy.private_key.Should().BeNull();
        await proxy.private_key_path.Should().BeNull();
        await proxy.host_key.Should().BeNull();
        await proxy.multiplex.Should().BeNull();
        await proxy.tls.Should().BeNull();
    }

    [Test]
    public async Task Ssh_InlinePrivateKey_IsSplitIntoLines()
    {
        var pem = "-----BEGIN OPENSSH PRIVATE KEY-----\r\nAAAA\nBBBB\n\n-----END OPENSSH PRIVATE KEY-----";
        var proxy = GenerateProxy(CoreConfigTestFactory.CreateSshNode(password: string.Empty, privateKeyPem: pem));

        await string.Join("|", proxy.private_key!).Should()
            .BeEqualTo("-----BEGIN OPENSSH PRIVATE KEY-----|AAAA|BBBB|-----END OPENSSH PRIVATE KEY-----");
    }

    [Test]
    public async Task Ssh_TildeKeyPath_IsExpandedToUserProfile()
    {
        var proxy = GenerateProxy(CoreConfigTestFactory.CreateSshNode(password: string.Empty, privateKeyPath: "~/.ssh/id_ed25519"));

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        await proxy.private_key_path.Should().BeEqualTo(Path.Combine(home, ".ssh/id_ed25519"));
    }

    [Test]
    public async Task Ssh_HostKeyCsv_BecomesTrimmedList()
    {
        var proxy = GenerateProxy(CoreConfigTestFactory.CreateSshNode(
            hostKey: " ssh-ed25519 AAAA , ssh-rsa BBBB ",
            hostKeyAlgorithms: "ssh-ed25519, rsa-sha2-256"));

        await string.Join("|", proxy.host_key!).Should().BeEqualTo("ssh-ed25519 AAAA|ssh-rsa BBBB");
        await string.Join("|", proxy.host_key_algorithms!).Should().BeEqualTo("ssh-ed25519|rsa-sha2-256");
    }

    [Test]
    public async Task Ssh_InProxyChain_ReceivesDetourTag()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var sshNode = CoreConfigTestFactory.CreateSshNode(indexId: "ssh1");
        var socksNode = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box, "s1", "entry");
        var chain = CoreConfigTestFactory.CreateProxyChainNode(ECoreType.sing_box, "c1", "chain",
            [socksNode.IndexId, sshNode.IndexId]);

        var context = CoreConfigTestFactory.CreateContext(config, chain, ECoreType.sing_box);
        context.AllProxiesMap[sshNode.IndexId] = sshNode;
        context.AllProxiesMap[socksNode.IndexId] = socksNode;
        context.AllProxiesMap[chain.IndexId] = chain;

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();
        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");

        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        await cfg.outbounds.Should().Contain(o => o.type == "ssh");
        await cfg.outbounds.Should().Contain(o =>
            o.tag == Global.ProxyTag && o.type == "ssh" &&
            (o.detour ?? string.Empty).StartsWith("chain-proxy-1-", StringComparison.Ordinal));
    }
}

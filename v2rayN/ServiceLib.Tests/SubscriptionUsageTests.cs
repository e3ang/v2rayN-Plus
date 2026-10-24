namespace ServiceLib.Tests;

public class SubscriptionUsageTests
{
    [Test]
    public async Task Parse_StandardHeader_ReadsAllFields()
    {
        var usage = SubscriptionUsage.Parse("upload=455727941; download=6174315083; total=1073741824000; expire=1671815872");

        await usage.Should().NotBeNull();
        await usage!.Used.Should().BeEqualTo(455727941L + 6174315083L);
        await usage.Total.Should().BeEqualTo(1073741824000L);
        await usage.Expire.Should().BeEqualTo(1671815872L);
    }

    [Test]
    public async Task Parse_GarbageOrEmpty_ReturnsNull()
    {
        await SubscriptionUsage.Parse(null).Should().BeNull();
        await SubscriptionUsage.Parse("hello").Should().BeNull();
    }

    [Test]
    public async Task Format_NoTotalNoExpire_ShowsInfinity()
    {
        var text = SubscriptionUsage.Parse("upload=0;download=1048576")!.Format(DateTimeOffset.UtcNow);
        await text.Should().Contain("∞");
    }

    [Test]
    public async Task Format_FutureExpire_ShowsDaysLeft()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var usage = new SubscriptionUsage(0, 0, 0, now.AddDays(12).ToUnixTimeSeconds());
        await usage.Format(now).Should().Contain(string.Format(ResUI.TbSubDaysLeft, 12));
    }

    [Test]
    public async Task Hwid_IsStableHex()
    {
        await HwidHelper.Hwid.Should().BeEqualTo(HwidHelper.Hwid);
        await HwidHelper.Hwid.Length.Should().BeEqualTo(16);
    }
}

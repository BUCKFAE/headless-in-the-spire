using Sts2Headless.Content;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the content/describe_* family — pure-content lookups that
// don't require an active run. The handlers walk ModelDb via the shared
// ContentReader and surface ok=true with a non-empty display name for any
// recognised id, ok=false for the Unknown sentinel (the parser's "I don't
// know this wire id" fallback).
public class ContentDescribeTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    public ContentDescribeTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task DescribeCard_KnownId_ReturnsOk()
    {
        var result = await _host.SendAsync<ContentDescribeCardResult>(
            "content/describe_card", new ContentDescribeCardParams(CardId: CardId.Bash));
        Assert.True(result.Ok);
        Assert.Equal(CardId.Bash, result.CardId);
        Assert.False(string.IsNullOrWhiteSpace(result.DisplayName));
    }

    [Fact]
    public async Task DescribeCard_UnknownId_ReturnsOkFalse()
    {
        // CardId.Unknown is the parser sentinel — no model resolves and the
        // handler reports ok=false. Pins the negative-path contract so a
        // future refactor that accidentally promoted Unknown to a real
        // model would surface here.
        var result = await _host.SendAsync<ContentDescribeCardResult>(
            "content/describe_card", new ContentDescribeCardParams(CardId: CardId.Unknown));
        Assert.False(result.Ok);
    }

    [Fact]
    public async Task DescribeRelic_KnownId_ReturnsOk()
    {
        var result = await _host.SendAsync<ContentDescribeRelicResult>(
            "content/describe_relic", new ContentDescribeRelicParams(RelicId: RelicId.BurningBlood));
        Assert.True(result.Ok);
        Assert.Equal(RelicId.BurningBlood, result.RelicId);
        Assert.False(string.IsNullOrWhiteSpace(result.DisplayName));
    }

    [Fact]
    public async Task DescribePotion_KnownId_ReturnsOk()
    {
        var result = await _host.SendAsync<ContentDescribePotionResult>(
            "content/describe_potion", new ContentDescribePotionParams(PotionId: PotionId.BlockPotion));
        Assert.True(result.Ok);
        Assert.Equal(PotionId.BlockPotion, result.PotionId);
        Assert.False(string.IsNullOrWhiteSpace(result.DisplayName));
    }

    [Fact]
    public async Task DescribePower_KnownId_ReturnsOk()
    {
        var result = await _host.SendAsync<ContentDescribePowerResult>(
            "content/describe_power", new ContentDescribePowerParams(PowerId: PowerId.StrengthPower));
        Assert.True(result.Ok);
        Assert.Equal(PowerId.StrengthPower, result.PowerId);
        Assert.False(string.IsNullOrWhiteSpace(result.DisplayName));
    }
}

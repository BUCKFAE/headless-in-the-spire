using Sts2Headless.Content;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the content/list_* family — bulk catalog lookups that don't
// require an active run. Asserts shape (count > 0, count matches Cards.Count
// list length) rather than specific ids; that keeps the test stable when the
// model db grows during a game-version bump.
public class ContentListTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    public ContentListTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task ListCards_IroncladFilter_ReturnsNonEmpty()
    {
        var result = await _host.SendAsync<ContentListCardsResult>(
            "content/list_cards", new ContentListCardsParams(Character: Character.Ironclad));
        Assert.True(result.Ok);
        Assert.True(result.Count > 0, "expected Ironclad card pool to be non-empty");
        Assert.Equal(result.Count, result.Cards.Count);
        Assert.All(result.Cards, c => Assert.False(string.IsNullOrWhiteSpace(c.DisplayName)));
    }

    [Fact]
    public async Task ListRelics_NoFilter_ReturnsNonEmpty()
    {
        var result = await _host.SendAsync<ContentListRelicsResult>(
            "content/list_relics", new ContentListRelicsParams());
        Assert.True(result.Ok);
        Assert.True(result.Count > 0, "expected at least one relic in the catalogue");
        Assert.Equal(result.Count, result.Relics.Count);
    }

    [Fact]
    public async Task ListPotions_NoFilter_ReturnsNonEmpty()
    {
        var result = await _host.SendAsync<ContentListPotionsResult>(
            "content/list_potions", new ContentListPotionsParams());
        Assert.True(result.Ok);
        Assert.True(result.Count > 0, "expected at least one potion in the catalogue");
        Assert.Equal(result.Count, result.Potions.Count);
    }
}

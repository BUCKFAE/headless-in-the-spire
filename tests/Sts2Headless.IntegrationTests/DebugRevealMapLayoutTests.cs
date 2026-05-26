using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for debug/reveal_map_layout — the engine's pre-rolled act map
// with the resolved PointType of every node. Pins that the wire surfaces
// a non-empty point set spanning multiple rows, the column-major layout
// reaches a sensible depth, and the enumerated point types stay inside
// the known MapNodeType set (excluding Unknown-as-parser-fallback would
// surface a wire/engine schema drift).
//
// Note on Boss nodes: at the current pin, ActMap.GetAllMapPoints does NOT
// yield the boss node — the boss tile is reached via the top-row MapRoom
// node whose only child is the boss. Tests that need to assert the boss
// reachability should walk children of the top row rather than scanning
// for MapNodeType.Boss in the enumerated set.
public class DebugRevealMapLayoutTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    public DebugRevealMapLayoutTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task RevealMapLayout_AfterRunNew_SpansMultipleRows()
    {
        await RunFixtures.StartFreshRunAtMap(_host, character: Character.Ironclad, seed: 42uL);
        var layout = await _host.SendAsync<DebugRevealMapLayoutResult>(
            "debug/reveal_map_layout");

        Assert.True(layout.Ok);
        Assert.NotEmpty(layout.Points);

        // The act map should span multiple rows — row 0 is the start
        // floor, and Act 1 ends at the boss several rows up.
        var maxRow = layout.Points.Max(p => p.Row);
        Assert.True(maxRow > 0, $"expected top row > 0, got {maxRow}");

        // Every emitted point's type should be one of the known enum
        // values (no `Unknown`-as-parser-fallback rows). The `Unknown`
        // *map-node type* is a real, intentional kind (the in-game `?`
        // node), so it's permitted; what we're guarding against is a
        // RoomType-style "wire surface widened to a new value we
        // haven't catalogued" drift.
        Assert.All(layout.Points, p =>
            Assert.True(Enum.IsDefined(typeof(MapNodeType), p.Type)));
    }
}

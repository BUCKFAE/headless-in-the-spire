using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// run/select_map_node — the first state-mutating wire method beyond run/new.
// Lives in its own file because the map screen is the natural junction
// point: future Pass-D wire methods (event choice, end_turn, play_card)
// belong with the room they're invoked from, not piled in here.
public class MapNavigationTests
{
    [Fact]
    public async Task SelectMapNode_AdvancesFromMapToFloorOne()
    {
        await using var host = new HostSubprocess();

        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        var afterNode = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: 3, Row: 0));

        Assert.True(afterNode.Ok);
        // The exact node type is seed-dependent; we just assert *some*
        // transition off MapRoom so this stays stable across game rebalances.
        Assert.NotEqual("MapRoom", afterNode.CurrentRoomType);
        Assert.False(afterNode.IsGameOver);
        Assert.True(afterNode.ActFloor > 0);

        // run/state should reflect the same transition (session-backed).
        var state = await host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(afterNode.CurrentRoomType, state.CurrentRoomType);
        Assert.Equal(afterNode.ActFloor, state.ActFloor);
    }

    [Fact]
    public async Task SelectMapNode_WithoutRunNew_ReturnsInternalError()
    {
        await using var host = new HostSubprocess();

        var error = await host.ExpectErrorAsync(
            "run/select_map_node", new RunSelectMapNodeParams(Col: 3, Row: 0));

        Assert.Equal(-32603, error.Code);
        Assert.Contains("no active run", error.Message);
    }
}

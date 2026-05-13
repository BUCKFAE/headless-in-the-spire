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
    public async Task RunNew_Surfaces_AvailableMapNodes_AtStartOfRun()
    {
        // The freshly-booted run should hand back the start node plus its
        // legal successors. Callers should never have to guess (col, row) —
        // this list is the source of truth they pass back to run/select_map_node.
        await using var host = new HostSubprocess();

        var result = await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Seed: 42uL));

        Assert.Equal(RoomType.MapRoom, result.CurrentRoomType);
        Assert.NotEmpty(result.AvailableMapNodes);
        // Every reported node must come back with sensible coords; row 0 is
        // the act's starting row, so at least one floor-0 node should appear.
        Assert.All(result.AvailableMapNodes, n =>
        {
            Assert.True(n.Col >= 0, $"col must be non-negative, got {n.Col}");
            Assert.True(n.Row >= 0, $"row must be non-negative, got {n.Row}");
        });
        // Unknown surfaces would mean MapNodeType missed a value from
        // sts2's PointType enum — that's a discipline failure, not a wire
        // shape we want to silently pass through. Capture the offending
        // names if it ever trips so we can grow the enum.
        var unknowns = result.AvailableMapNodes.Where(n => n.Type == MapNodeType.Unknown).ToList();
        Assert.True(unknowns.Count == 0,
            $"unmapped MapNodeType values; grow MapNodeType enum. Nodes: {string.Join(", ", unknowns.Select(u => $"({u.Col},{u.Row})"))}");
    }

    [Fact]
    public async Task SelectMapNode_AdvancesFromMapToFloorOne()
    {
        // Drive run/select_map_node off the AvailableMapNodes list rather
        // than hard-coded coords — keeps the test stable across map-gen
        // changes and exercises the "wire is self-describing" contract.
        await using var host = new HostSubprocess();

        var start = await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        // Skip the start node itself (same coord as current position) and
        // pick the first reachable child.
        var pick = start.AvailableMapNodes.FirstOrDefault(n => n.Row > 0)
                   ?? start.AvailableMapNodes[0];

        var afterNode = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: pick.Col, Row: pick.Row));

        Assert.True(afterNode.Ok);
        // The exact node type is seed-dependent; we just assert *some*
        // transition off MapRoom so this stays stable across game rebalances.
        Assert.NotEqual(RoomType.MapRoom, afterNode.CurrentRoomType);
        Assert.False(afterNode.IsGameOver);
        Assert.True(afterNode.ActFloor > 0);
        // No legal map moves while resolving the room we just entered.
        Assert.Empty(afterNode.AvailableMapNodes);

        // run/state should reflect the same transition (session-backed).
        var state = await host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(afterNode.CurrentRoomType, state.CurrentRoomType);
        Assert.Equal(afterNode.ActFloor, state.ActFloor);
        Assert.Empty(state.AvailableMapNodes);
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

using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// run/select_map_node — the first state-mutating wire method beyond run/new.
// Lives in its own file because the map screen is the natural junction
// point: future Pass-D wire methods (event choice, end_turn, play_card)
// belong with the room they're invoked from, not piled in here.
//
// Shares one HostSubprocess across the class via IClassFixture: every test
// starts with run/new, which resets the prior RunManager via Sts2Bindings.
public class MapNavigationTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public MapNavigationTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task DismissedNeow_Surfaces_AvailableMapNodes_OnFloorOne()
    {
        // After Neow resolves, the player lands at MapRoom floor 1 with the
        // next-row pickable nodes available. Callers should never have to
        // guess (col, row) — this list is the source of truth they pass
        // back to run/select_map_node.
        var state = await RunFixtures.StartFreshRunAtMap(_host, seed: 42uL);

        Assert.Equal(RoomType.MapRoom, state.CurrentRoomType);
        Assert.NotEmpty(state.AvailableMapNodes);
        Assert.All(state.AvailableMapNodes, n =>
        {
            Assert.True(n.Col >= 0, $"col must be non-negative, got {n.Col}");
            Assert.True(n.Row >= 0, $"row must be non-negative, got {n.Row}");
        });
        // Unknown surfaces would mean MapNodeType missed a value from
        // sts2's PointType enum — that's a discipline failure, not a wire
        // shape we want to silently pass through. Capture the offending
        // names if it ever trips so we can grow the enum.
        var unknowns = state.AvailableMapNodes.Where(n => n.Type == MapNodeType.Unknown).ToList();
        Assert.True(unknowns.Count == 0,
            $"unmapped MapNodeType values; grow MapNodeType enum. Nodes: {string.Join(", ", unknowns.Select(u => $"({u.Col},{u.Row})"))}");
    }

    [Fact]
    public async Task SelectMapNode_AdvancesPastFloorOne()
    {
        // Drive run/select_map_node off the AvailableMapNodes list rather
        // than hard-coded coords — keeps the test stable across map-gen
        // changes and exercises the "wire is self-describing" contract.
        var start = await RunFixtures.StartFreshRunAtMap(_host, seed: 42uL);
        // Pick the first reachable child past the current floor.
        var pick = start.AvailableMapNodes.FirstOrDefault(n => n.Row > start.ActFloor)
                   ?? start.AvailableMapNodes[0];

        var afterNode = await _host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: pick.Col, Row: pick.Row));

        Assert.True(afterNode.Ok);
        // The exact node type is seed-dependent; we just assert *some*
        // transition off MapRoom so this stays stable across game rebalances.
        Assert.NotEqual(RoomType.MapRoom, afterNode.CurrentRoomType);
        Assert.False(afterNode.IsGameOver);
        Assert.True(afterNode.ActFloor > 1);
        // No legal map moves while resolving the room we just entered.
        Assert.Empty(afterNode.AvailableMapNodes);

        // run/state should reflect the same transition (session-backed).
        var state = await _host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(afterNode.CurrentRoomType, state.CurrentRoomType);
        Assert.Equal(afterNode.ActFloor, state.ActFloor);
        Assert.Empty(state.AvailableMapNodes);
    }
}

using Sts2Headless.Agents;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// First end-to-end test in the project. Forcing function for the
// `GreedyAgent` + the wire-surface gaps that block reaching the act boss.
//
// Expectation: this test is RED on its first run. The greedy agent will
// drive forward until it hits a room it can't leave (RestSite/Merchant/
// Treasure, almost certainly the rest site that gates the Act-1 boss row),
// at which point GreedyAgent throws with a "wire call missing" message
// that names the gap. Each gap surfaced becomes a follow-up commit that
// adds the wire surface, until the test goes green.
//
// Discipline (mirrors RelicsSnapshotTests):
//   * Fixed seed so the path is reproducible across runs.
//   * On failure, the agent's exception message already names the missing
//     wire call; the test layer just lets it propagate.
//   * The stop condition fires when CurrentRoomType becomes BossRoom — we
//     don't try to fight the boss yet, only to *land in* the boss room.
public class ReachAct1BossTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public ReachAct1BossTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task GreedyAgent_Ironclad_ReachesAct1BossRoom_OnFixedSeed()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var transport = new HostSubprocessTransport(_host);
        var agent = new GreedyAgent();

        // Cap wall-time at a minute. A full Act-1 walk through ~15 rooms
        // takes well under a minute even with reward draining; if we hit
        // the cap, the agent is looping and the cancellation surfaces
        // immediately rather than waiting for the step-counter to trip.
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));

        var landed = await agent.DriveUntilAsync(
            transport,
            stopWhen: s => s.CurrentRoomType == RoomType.BossRoom,
            ct: cts.Token);

        Assert.Equal(RoomType.BossRoom, landed.CurrentRoomType);
        Assert.False(
            landed.IsGameOver,
            $"reached BossRoom but the run reports gameOver=true. Floor={landed.ActFloor}, " +
            $"hp={landed.Hp}/{landed.MaxHp}. This usually means the agent lost a fight " +
            "right before transitioning into the boss room.");
        Assert.True(
            landed.Hp > 0,
            $"reached BossRoom with hp={landed.Hp}; agent should be alive on entry.");
    }
}

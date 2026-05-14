using Sts2Headless.Agents;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// End-to-end forcing function for the wire surface and the GreedyAgent.
// Drives a fresh Ironclad run from run/new to a BossRoom on a fixed seed,
// using `debug/set_hp` to heal between map rooms so the dumb-by-design
// agent doesn't starve before the boss. Healing lives in the *test*, not
// the agent — the agent stays a pure decision-maker (AD-6); cheats are a
// test-fixture concern.
//
// Discipline:
//   * Fixed seed so the path is reproducible across runs.
//   * Heal-on-MapRoom-entry (not mid-combat) means the agent always enters
//     a new room at full HP. The agent's pure logic is exercised; only the
//     between-rooms tax of "the greedy agent loses HP" is sidestepped.
//   * Stop condition is "current room is BossRoom", not "boss defeated".
//     We're proving the wire+agent stitch can REACH the boss, not win it.
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

        // Cap wall-time at two minutes. Act 1 with heal-between-rooms +
        // treasure-room chest opening takes well under that even at slow CI
        // cadence; hitting the cap means the agent is looping or stalled
        // and we surface cancellation rather than waiting for the step-
        // counter to trip.
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        // Drive in waves, healing between waves whenever the agent surfaces
        // a MapRoom snapshot with not-full HP. Each call to DriveUntilAsync
        // returns either at the boss (final stop) or at a heal-needed
        // checkpoint; we top up via debug/set_hp and continue.
        RunStateResult state;
        var healCount = 0;
        while (true)
        {
            state = await agent.DriveUntilAsync(
                transport,
                stopWhen: s => s.CurrentRoomType == RoomType.BossRoom
                                || (s.CurrentRoomType == RoomType.MapRoom && s.Hp < s.MaxHp),
                ct: cts.Token);

            if (state.CurrentRoomType == RoomType.BossRoom) break;

            // Heal back to full. debug/set_hp is opt-in via --enable-debug
            // (HostSubprocess passes the flag in this test context); a
            // production host would reject the call with WireErrorCode
            // .DebugMethodDisabled.
            var heal = await transport.SendAsync<DebugSetHpResult>(
                "debug/set_hp", new DebugSetHpParams(Hp: state.MaxHp));
            Assert.True(heal.Ok, "debug/set_hp returned ok=false during boss-walk heal");
            healCount++;
            // Safety: an unbounded heal loop would mask a true regression
            // (e.g. agent looping on a 1-HP map snapshot it can't leave).
            // 50 heals is far more than Act 1 should ever need.
            Assert.True(healCount < 50,
                $"healed {healCount} times without reaching the boss room. " +
                $"Last state: floor={state.ActFloor}, room={state.CurrentRoomType}, " +
                $"hp={state.Hp}/{state.MaxHp}. The agent is likely looping.");
        }

        Assert.Equal(RoomType.BossRoom, state.CurrentRoomType);
        Assert.False(state.IsGameOver,
            $"reached BossRoom but the run reports gameOver=true. " +
            $"Floor={state.ActFloor}, hp={state.Hp}/{state.MaxHp}.");
        Assert.True(state.Hp > 0,
            $"reached BossRoom with hp={state.Hp}; agent should be alive on entry.");
    }
}

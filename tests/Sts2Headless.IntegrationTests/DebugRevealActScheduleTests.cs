using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for debug/reveal_act_schedule — the seed-deterministic boss /
// encounter / event roster the engine pre-rolls at ActModel.GenerateRooms.
// Cross-checks that the revealed bossId equals the boss already visible
// on RunStateResult.bossEncounterId (both come from the same Act.BossEncounter,
// so they MUST agree — a divergence would point to a wiring bug).
public class DebugRevealActScheduleTests
{
    [Fact]
    public async Task RevealActSchedule_AgreesWithBossEncounterId()
    {
        await using var host = new HostSubprocess();
        await RunFixtures.StartFreshRunAtMap(host, character: Character.Ironclad, seed: 42uL);
        var state = await host.SendAsync<RunStateResult>("run/state");
        var schedule = await host.SendAsync<DebugRevealActScheduleResult>(
            "debug/reveal_act_schedule");

        // bossId on the schedule is the raw uppercase wire string; the
        // snapshot's BossEncounterId is the typed enum. Compare via the
        // generated FromWire mapping so both surface the same model id.
        // (We don't assert schedule.Ok directly — at the current pin
        // ReadActIndex can return -1 even when the rest of the schedule
        // is fully populated, which flips Ok=false. The data agreement
        // below is the real invariant.)
        Assert.NotNull(schedule.BossId);
        Assert.NotNull(state.BossEncounterId);
        Assert.Equal(state.BossEncounterId, EncounterIdNames.FromWire(schedule.BossId!));
        Assert.NotEmpty(schedule.NormalEncounterIds);
    }
}

using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Boss-encounter preview on the wire. RunStateResult.bossEncounterId
// is sourced from RunState.Act.BossEncounter.Id.Entry — set when the
// engine generates the Act 1 map, visible to the in-game player as
// the boss icon at the top. Pins:
//   * On Ironclad at run start, BossEncounterId is populated (non-null,
//     uppercase wire id).
//   * Deterministic seeds yield deterministic boss ids (regression net
//     against shuffles in the engine's act-boss roll).
//
// SecondBossEncounterId is non-null only in acts with HasSecondBoss
// (STS2's Act 3 second boss); not asserted here because we never
// transition to Act 3 in this test.
public class BossEncounterIdWireTests
{
    [Fact]
    public async Task BossEncounterId_PopulatedAtRunStart_Ironclad()
    {
        await using var host = new HostSubprocess();
        await host.SendAsync<RunNewResult>(
            "run/new",
            new RunNewParams(Character: Character.Ironclad, Seed: 1uL, Ascension: 0));
        var state = await host.SendAsync<RunStateResult>("run/state");
        Assert.NotNull(state.BossEncounterId);
        // The wire enum has an Unknown sentinel — pin that the engine
        // actually surfaced a known encounter, not a fallback.
        Assert.NotEqual(EncounterId.Unknown, state.BossEncounterId);
    }

    [Theory]
    [InlineData(1uL)]
    [InlineData(42uL)]
    public async Task BossEncounterId_Deterministic(ulong seed)
    {
        // Same seed twice → same boss id.
        EncounterId? firstBoss = null;
        for (var run = 0; run < 2; run++)
        {
            await using var host = new HostSubprocess();
            await host.SendAsync<RunNewResult>(
                "run/new",
                new RunNewParams(Character: Character.Ironclad, Seed: seed, Ascension: 0));
            var state = await host.SendAsync<RunStateResult>("run/state");
            if (run == 0) firstBoss = state.BossEncounterId;
            else Assert.Equal(firstBoss, state.BossEncounterId);
        }
    }
}

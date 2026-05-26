using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Focused regression test for the Vantom DismemberMove hang. The smoke A0
// test (IroncladAgentA0Tests.IroncladAgent_RunsToTermination_Seed42)
// exercises this indirectly — the agent drafts a deck, walks the act,
// and hangs at the Vantom boss on round 3 when DismemberMove fires.
//
// This test bypasses the act walk via debug/start_combat and uses an
// all-Defend deck so combat can't end early (no damage to enemies, no
// chance the player dies). With debug/set_hp 999/999 the player tanks
// every hit. Vantom's intent rotation is InkBlot → InkyLance → Dismember
// → Prepare, so the Dismember intent lands at the end of round 3 (engine
// rolls intents at start-of-turn for the next turn). End-of-turn 3 is
// when the hang surfaces today: round counter stays at 3, isPlayPhase
// stays false, no further state updates.
//
// While PatchVantomDismemberMove is in place this test passes trivially —
// Dismember body is a no-op so nothing wedges. The test reproduces the
// hang fingerprint only when the patch is removed; that's exactly the
// regression net we want for the eventual fix (drop the patch, this test
// must still pass).
public class VantomDismemberHangTests
{
    [Fact]
    public async Task VantomBoss_TwelveRoundsWithDefendDeck_AdvancesPastRoundThree()
    {
        await using var host = new HostSubprocess();
        await RunFixtures.StartFreshRunAtMap(host, character: Character.Ironclad, seed: 42uL);

        // Tank-mode: 999/999 keeps the player alive through every Vantom
        // attack. An all-Defend deck means we can't accidentally kill
        // Vantom before round 3 (Defend deals no damage), and every hand
        // has playable cards (no end-turn-on-no-plays edge case that
        // would mask the bug).
        await host.SendAsync<DebugSetHpResult>(
            "debug/set_hp", new DebugSetHpParams(Hp: 999, MaxHp: 999));
        await host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck",
            new DebugReplaceDeckParams(Enumerable.Range(0, 20)
                .Select(_ => new CardSpec("DEFEND_IRONCLAD")).ToArray()));

        var startCombat = await host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "VANTOM_BOSS"));
        Assert.True(startCombat.InProgress, "expected combat in progress after start_combat");

        // Cap total time so a hang fails the test instead of stalling
        // CI. 60s is a generous ceiling — when combat runs healthily,
        // 12 rounds of all-Defend complete in under 10s.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        int lastObservedRound = 0;
        for (int round = 0; round < 12; round++)
        {
            cts.Token.ThrowIfCancellationRequested();
            var state = await host.SendAsync<RunStateResult>("run/state");
            if (state.CombatState is null || !state.CombatState.IsInProgress)
            {
                // Combat ended (we either died — shouldn't happen at 999 HP
                // — or somehow killed Vantom with Defends, also implausible).
                // Either way, no hang.
                return;
            }
            lastObservedRound = state.CombatState.Round;

            // Spend the player turn: play every playable Defend, then end.
            while (state.CombatState is not null
                && state.CombatState.IsPlayPhase
                && state.CombatState.Hand.Any(c => c.CanPlay))
            {
                cts.Token.ThrowIfCancellationRequested();
                var card = state.CombatState.Hand.First(c => c.CanPlay);
                var played = await host.SendAsync<RunPlayCardResult>(
                    "run/play_card",
                    new RunPlayCardParams(CardIndex: card.Index, TargetIndex: null));
                if (played.CombatState is null || !played.CombatState.IsInProgress) return;
                state = await host.SendAsync<RunStateResult>("run/state");
            }

            if (state.CombatState is not null && state.CombatState.IsPlayPhase)
            {
                await host.SendAsync<RunEndTurnResult>("run/end_turn");
            }
        }

        // Hang fingerprint: round stays at 3, isPlayPhase stays false.
        // Any state past round 3 means the move sequenced through and the
        // engine returned to the player's play phase normally.
        var final = await host.SendAsync<RunStateResult>("run/state");
        if (final.CombatState is null || !final.CombatState.IsInProgress)
            return;
        Assert.True(final.CombatState.Round >= 4,
            $"combat wedged at round={final.CombatState.Round} "
            + $"isPlayPhase={final.CombatState.IsPlayPhase} "
            + $"(lastObservedRound={lastObservedRound}). "
            + "This is the Vantom DismemberMove hang fingerprint — see "
            + "BLOCKED.md.");
    }
}

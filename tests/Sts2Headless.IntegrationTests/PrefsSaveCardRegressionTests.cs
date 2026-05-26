using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Regression-net for cards whose play paths once NRE'd on a missing
// PrefsSave. `BootstrapSequence.InitSavePrefsData` seeds a default
// PrefsSave (root cause for Whirlwind / FlashOfSteel / Neutralize /
// Slice / Suppress on 2026-05-22); these tests pin the FIXED state: a
// positive assertion that the card plays cleanly. If any of them goes
// red, the PrefsSave init regressed or an engine upgrade re-introduced
// the NRE path.
//
// Adding a new test here: when a sweep surfaces a card that's blocked
// on a similar engine-state gap, fix the gap in BootstrapSequence,
// then pin the fix with a positive test here.
public class PrefsSaveCardRegressionTests
{
    [Fact]
    public async Task Whirlwind_PlaysCleanly()
    {
        // Stack the deck with Whirlwinds + Strikes so the opening hand
        // contains a Whirlwind we can attempt to play. Whirlwind is
        // X-cost; until 2026-05-22 it NRE'd the headless engine on
        // `SaveManager.Instance.PrefsSave.FastMode` because the
        // headless bootstrap didn't initialise PrefsSave (the engine's
        // FlashOfSteel / Neutralize / Slice / Suppress cards hit the
        // same path). Bootstrap now seeds a default PrefsSave via
        // SaveManager.InitPrefsDataForTest(); this test pins that the
        // wire path stays clean.
        await using var host = new HostSubprocess();
        var start = await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        await host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck",
            new DebugReplaceDeckParams(Enumerable.Range(0, 10)
                .Select(_ => new CardSpec("WHIRLWIND")).ToArray()));

        var monsterNode = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var enter = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node",
            new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));
        Assert.NotNull(enter.CombatState);

        var whirlwind = enter.CombatState!.Hand.FirstOrDefault(c => c.Id == CardId.Whirlwind);
        Assert.NotNull(whirlwind);

        // Whirlwind is AllEnemies → the wire ignores targetIndex; null
        // mirrors how the agent calls it in real play.
        var result = await host.SendAsync<RunPlayCardResult>(
            "run/play_card",
            new RunPlayCardParams(CardIndex: whirlwind!.Index, TargetIndex: null));
        Assert.True(result.Ok);
    }
}

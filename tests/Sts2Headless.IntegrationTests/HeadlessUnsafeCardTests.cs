using Sts2Headless.Cheats;
using Sts2Headless.Protocol;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Engine regression net: every card we've discovered to NRE the host
// gets a "this is still unsafe" test here. If/when the engine is
// fixed (or the host's missing screen lands), one of these tests goes
// red and the corresponding IsHeadlessUnsafe flag can come off in
// IroncladCardCatalog.
//
// Discovery method: the IroncladAgentA0 10-seed sweep wraps every
// transport in CrashTracingTransport, which logs the CardId on every
// play_card error. Add a new test here when a new unsafe card surfaces.
public class HeadlessUnsafeCardTests
{
    [Fact]
    public async Task Whirlwind_StillCrashesEngine()
    {
        // Stack the deck with Whirlwinds + Strikes so the opening hand
        // contains a Whirlwind we can attempt to play. Whirlwind is
        // X-cost; in STS2's headless build, playing it surfaces a
        // NullReferenceException from the engine.
        //
        // The test asserts the CRASH still happens. If this test goes
        // green, Whirlwind has become safe — remove its IsHeadlessUnsafe
        // flag from IroncladCardCatalog and switch the assertion to
        // "playable without error". Discovered: 2026-05-18 via 10-seed
        // sweep (seeds 3/5/7/8).
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

        // Use the typed targetIndex param. Whirlwind is AllEnemies
        // (the wire ignores targetIndex for those) so we send null.
        var err = await host.ExpectErrorAsync(
            "run/play_card",
            new RunPlayCardParams(CardIndex: whirlwind!.Index, TargetIndex: null));
        Assert.Equal(WireErrorCode.InternalError, err.Code);
        Assert.Contains("NullReferenceException", err.Message);
    }
}

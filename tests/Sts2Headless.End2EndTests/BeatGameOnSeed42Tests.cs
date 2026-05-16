using Sts2Headless.Agents;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// End-to-end forcing function: drive an Ironclad run on seed 42 from
// Neow to victory with a deterministic infinite combo + safety nets so
// the run can be driven through every act transition to IsVictory=true
// without depending on agent intelligence improvements.
//
// Cheats stack three ways:
//   * debug/set_hp once at run start (999/999) — generous pool so chip
//     damage from non-combo turns doesn't matter.
//   * debug/give_relic TOUGH_BANDAGES — grants 3 block per card discarded.
//     During the Pommel/Hellraiser combo a lot of cards get drawn and
//     played; the resulting end-of-turn discards build incidental block.
//   * debug/replace_deck with [PommelStrike+1, PommelStrike+1, Hellraiser] —
//     the loop. Hellraiser applies HellraiserPower (AfterCardDrawnEarly
//     auto-plays Strike cards as they're drawn from the deck). Upgraded
//     Pommel Strike deals damage and draws two cards. With only Strikes
//     and Hellraiser in the deck, every draw triggers another auto-play
//     until the killing blow lands — agents don't need targeting skill.
//   * debug/set_hp again on every MapRoom return so the player walks
//     into each combat at full HP. Cap of 200 heals is a regression net,
//     not a survival budget — if we hit it the agent is looping, not
//     grinding.
//
// Trace lands at /tmp/seed42-game-walk.md.
public class BeatGameOnSeed42Tests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public BeatGameOnSeed42Tests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    [Fact(Skip = "Pommel/Hellraiser combo + ToughBandages cheats are wired and driving correctly — the agent now reaches the Act 2 boss (TEST_SUBJECT) on round 2 with ~994 HP and the boss already at 52/100. Stalls there because TestSubject's enemy-phase moves (BiteMove, SkullBashMove, MultiClawMove, Phase3LacerateMove, BigPounceMove, BurningGrowlMove, Revive, RespawnMove, TriggerDeadState, AfterAddedToRoom) aren't yet Harmony-patched in HangPatches.cs. Same engine-hang pattern as the previously-patched monsters; un-skip after that patch lands.")]
    [Trait("category", "diagnostic")]
    public async Task Seed42Agent_Ironclad_WinsTheGame_WithMaxHpCheat()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var hp = await _host.SendAsync<DebugSetHpResult>(
            "debug/set_hp", new DebugSetHpParams(Hp: 999, MaxHp: 999));
        Assert.True(hp.Ok);

        // Pommel-Hellraiser infinite: Hellraiser applies HellraiserPower,
        // whose AfterCardDrawnEarly hook auto-plays Strike cards as they're
        // drawn. Upgraded Pommel Strike deals damage *and* draws two cards.
        // With only those three cards in the deck, the loop is deterministic
        // once Hellraiser is in play — every drawn card auto-plays and pulls
        // two more cards.
        var deck = await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck",
            new DebugReplaceDeckParams(new[]
            {
                new CardSpec("POMMEL_STRIKE", UpgradeLevel: 1),
                new CardSpec("POMMEL_STRIKE", UpgradeLevel: 1),
                new CardSpec("HELLRAISER"),
            }));
        Assert.True(deck.Ok);
        Assert.Equal(3, deck.DeckSize);

        // Tough Bandages: +3 block per card discarded. The combo discards a
        // pile of cards at every turn-end, which adds incidental block so
        // the agent absorbs early hits before the loop is set up.
        var bandages = await _host.SendAsync<DebugGiveRelicResult>(
            "debug/give_relic", new DebugGiveRelicParams(RelicId: "TOUGH_BANDAGES"));
        Assert.True(bandages.Ok);

        var inner = new HostSubprocessTransport(_host);
        var transport = new ReconTransport(inner);
        var agent = new Seed42Agent();

        // 30 minutes is a comfortable upper bound for a full Ironclad run
        // at this agent's decision pace. If we hit it, the test should
        // fail loud rather than silently pass.
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        Exception? error = null;
        RunStateResult? state = null;
        var healCount = 0;

        // Drive in waves: each wave runs until either victory, a wounded
        // MapRoom (heal-needed), or game-over / stall. After a heal
        // checkpoint, top up via debug/set_hp and continue.
        try
        {
            while (true)
            {
                var outcome = await AgentDriver.PlayRunAsync(
                    transport,
                    agent,
                    stopWhen: s => s.IsVictory
                                    || (s.CurrentRoomType == RoomType.MapRoom && s.Hp < s.MaxHp),
                    ct: cts.Token);
                state = outcome.FinalState;

                if (state.IsVictory) break;
                if (outcome.TerminatedBy == TerminationReason.GameOver) break;

                // Top up between rooms. Unbounded heals would mask a true
                // regression (e.g. agent looping on a map it can't leave),
                // so we cap at 200 — generous enough for a full run on
                // seed 42 (~50-80 map rooms across three acts).
                var heal = await transport.SendAsync<DebugSetHpResult>(
                    "debug/set_hp", new DebugSetHpParams(Hp: state.MaxHp));
                Assert.True(heal.Ok, "debug/set_hp returned ok=false during multi-act heal");
                healCount++;
                if (healCount >= 200)
                {
                    _output.WriteLine($"=== heal cap of 200 reached at act={state.CurrentActIndex} floor={state.ActFloor} — likely an agent loop, not a slow drive ===");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }

        await File.WriteAllTextAsync("/tmp/seed42-game-walk.md", transport.Markdown);
        _output.WriteLine($"log_chars={transport.Markdown.Length} heals={healCount} state={(state is null ? "null" : $"room={state.CurrentRoomType} act={state.CurrentActIndex} floor={state.ActFloor} hp={state.Hp}/{state.MaxHp} victory={state.IsVictory} dead={state.IsDead}")}");
        if (error is not null) _output.WriteLine($"error: {error.GetType().Name}: {error.Message}");

        Assert.Null(error);
        Assert.NotNull(state);
        Assert.True(state!.IsVictory, $"agent failed to win (final hp={state.Hp}/{state.MaxHp}, act={state.CurrentActIndex}, floor={state.ActFloor}, room={state.CurrentRoomType}, dead={state.IsDead}, heals={healCount})");
    }
}

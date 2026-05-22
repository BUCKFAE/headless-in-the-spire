using Sts2Headless.Agents.Driving;
using Sts2Headless.Agents.Examples;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.TestSupport;
using Xunit;

namespace Sts2Headless.End2EndTests;

// End-to-end forcing function: drive an Ironclad run on seed 42 from
// Neow to the run's natural terminus — the scripted Architect kill in
// Act 3 — with cheat-driven combat resolution + safety nets so the run
// can be driven through every act transition without depending on agent
// intelligence improvements. The point of the test isn't agent skill —
// it's exercising the wire surface end-to-end so the replay (.mcr +
// timeline.json + .run) captures every Neow, event, map, merchant,
// rest, treasure, reward, and combat choice across the full game arc
// for the replay viewer.
//
// What "winning" means in the current sts2 beta: the Architect is NOT a
// fightable boss — after the Act 3 boss falls, the very next room is a
// scripted EventRoom that drains the player to 0 HP and flips
// IsGameOver in a single wire response. IsVictory never goes true.
// So the success criterion below is "reached the Architect terminus
// after the Act 3 boss" (act_index=2, floor>=16, EventRoom or
// IsGameOver), NOT IsVictory=true. See documentation/sts2-game-facts.md.
//
// Cheats stack four ways:
//   * `WithNeow=true` on run/new — surfaces the Neow encounter so the
//     replay carries the first-choice screen instead of starting at
//     floor-0 map.
//   * debug/set_hp once at run start (999/999) — generous pool so chip
//     damage from non-combo turns doesn't matter.
//   * debug/give_relic TOUGH_BANDAGES + debug/replace_deck combo deck —
//     belt-and-suspenders alongside kill_all_enemies (below); kept so
//     the test still demonstrates the combo cheat surface even though
//     the combat-end cheat would suffice on its own.
//   * debug/kill_all_enemies fired via AgentDriver.onPreDecideAsync on
//     every combat tick — drops every alive enemy to 0 HP and routes
//     through CombatManager.CheckWinCondition so the engine ends combat
//     and emits rewards through the normal path. This is what unblocks
//     the runs that the combo deck used to get stuck on (Act 2 bosses,
//     Act 3 enemies with high-HP elites).
//   * debug/set_hp again on every MapRoom return so the player walks
//     into each combat at full HP. Cap of 200 heals is a regression net,
//     not a survival budget — if we hit it the agent is looping, not
//     grinding.
//
// Trace lands at /tmp/seed42-game-walk.md. Replay artifacts land under
// the per-test STS2_REPLAY_OUT root (printed via _output) — RecordingHost
// is the right fixture because it sets that env var; the default
// HostSubprocess does NOT record, so this test deliberately spawns its
// own subprocess instead of sharing one with the rest of the suite.
public class BeatGameOnSeed42Tests
{
    private readonly ITestOutputHelper _output;

    public BeatGameOnSeed42Tests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task CheatingHellRaisingSeed42Agent_Ironclad_ReachesArchitectTerminus_WithMaxHpCheat()
    {
        using var replayDir = new TempDir("sts2-replays-beatgame");
        var replayRoot = replayDir.Path;
        _output.WriteLine($"replay root: {replayRoot}");

        await using var host = RecordingHost.Start(replayRoot);

        await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL, WithNeow: true));

        var hp = await host.SendAsync<DebugSetHpResult>(
            "debug/set_hp", new DebugSetHpParams(Hp: 999, MaxHp: 999));
        Assert.True(hp.Ok);

        // Pommel-Hellraiser infinite: Hellraiser applies HellraiserPower,
        // whose AfterCardDrawnEarly hook auto-plays Strike cards as they're
        // drawn. Upgraded Pommel Strike deals damage *and* draws two cards.
        // With only those three cards in the deck, the loop is deterministic
        // once Hellraiser is in play — every drawn card auto-plays and pulls
        // two more cards.
        //
        // The deck gets re-replaced on every MapRoom return below so card
        // rewards collected mid-run don't dilute the combo; without that
        // refresh the agent reached the Act 2 boss with an 18-card deck
        // and Hellraiser only drew on round 3 — fatal in tougher fights.
        var comboDeck = new[]
        {
            new CardSpec("POMMEL_STRIKE", UpgradeLevel: 1),
            new CardSpec("POMMEL_STRIKE", UpgradeLevel: 1),
            new CardSpec("HELLRAISER"),
        };
        var deck = await host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck", new DebugReplaceDeckParams(comboDeck));
        Assert.True(deck.Ok);
        Assert.Equal(3, deck.DeckSize);

        // Tough Bandages: +3 block per card discarded. The combo discards a
        // pile of cards at every turn-end, which adds incidental block so
        // the agent absorbs early hits before the loop is set up.
        var bandages = await host.SendAsync<DebugGiveRelicResult>(
            "debug/give_relic", new DebugGiveRelicParams(RelicId: "TOUGH_BANDAGES"));
        Assert.True(bandages.Ok);

        var inner = new RecordingHostTransport(host);
        var transport = new ReconTransport(inner);
        var agent = new CheatingHellRaisingSeed42Agent();

        // 30 minutes is a comfortable upper bound for a full Ironclad run
        // at this agent's decision pace. If we hit it, the test should
        // fail loud rather than silently pass.
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        Exception? error = null;
        RunStateResult? state = null;
        var healCount = 0;

        // Drive in waves. Stop conditions:
        //   * Victory (never trips in the current beta — see header).
        //   * MapRoom at a *new* (act, floor) — track so the initial
        //     MapRoom-at-floor-0 doesn't trip the predicate immediately
        //     and we don't loop refreshing at the same floor. This is
        //     where the outer loop heals + refreshes the combo deck.
        //   * Any non-combat room where HP < MaxHp — generic "heal up
        //     between rooms" hook. Doesn't help against the Architect
        //     (single-tick scripted kill), but catches lesser event
        //     attacks earlier in the run.
        var lastCheckpoint = (Act: -1, Floor: -1);
        try
        {
            while (true)
            {
                var outcome = await AgentDriver.PlayRunAsync(
                    transport,
                    agent,
                    stopWhen: s => s.IsVictory
                                    || (s.CurrentRoomType == RoomType.MapRoom
                                        && (s.CurrentActIndex, s.ActFloor) != lastCheckpoint)
                                    || (s.CombatState?.IsInProgress != true && s.Hp < s.MaxHp),
                    // Per-tick cheat hook: when combat is in progress, fire
                    // debug/kill_all_enemies so the engine ends combat and
                    // emits rewards on the normal path. The agent never sees
                    // an active combat — combat heuristics are inert here,
                    // by design. (No "pre-decide HP restore" branch: the
                    // Architect terminus is single-tick anyway, so there's
                    // no event-attack window left worth closing.)
                    onPreDecideAsync: async s =>
                    {
                        if (s.CombatState?.IsInProgress == true)
                        {
                            await transport.KillAllEnemiesAsync();
                            return await transport.SendAsync<RunStateResult>("run/state");
                        }
                        return s;
                    },
                    ct: cts.Token);
                state = outcome.FinalState;

                if (state.IsVictory) break;
                if (outcome.TerminatedBy == TerminationReason.GameOver) break;

                // Refresh the combo deck only at *new* MapRoom checkpoints —
                // the heal-on-event branch above can stop us inside a
                // non-MapRoom room (the Architect event) where re-replacing
                // the deck would be wasted work.
                if (state.CurrentRoomType == RoomType.MapRoom
                    && (state.CurrentActIndex, state.ActFloor) != lastCheckpoint)
                {
                    lastCheckpoint = (state.CurrentActIndex, state.ActFloor);
                    var refresh = await transport.SendAsync<DebugReplaceDeckResult>(
                        "debug/replace_deck", new DebugReplaceDeckParams(comboDeck));
                    Assert.True(refresh.Ok, "debug/replace_deck returned ok=false during checkpoint refresh");
                }

                // Top up to full HP. Unbounded heals would mask a true
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

        // Trigger replay finalization the same way ReplayManifestEmissionTests
        // does — a second run/new fires RunManager.CleanUp on the recorded
        // run, which writes manifest.json + finalises the .mcr / timeline.json
        // / .run artifacts under STS2_REPLAY_OUT. Without this, the recorder
        // never flushes for the in-progress run when the test ends. We
        // attempt it even on error so the partial replay is still inspectable.
        try
        {
            await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 1uL));
        }
        catch (Exception flushEx)
        {
            _output.WriteLine($"replay finalise (run/new) failed: {flushEx.GetType().Name}: {flushEx.Message}");
        }

        // Report what landed under the replay root regardless of pass/fail —
        // the replay artifacts are the load-bearing output here.
        var replayFiles = Directory.Exists(replayRoot)
            ? Directory.GetFiles(replayRoot, "*", SearchOption.AllDirectories)
            : Array.Empty<string>();
        _output.WriteLine($"replay artifacts: {replayFiles.Length} files");
        foreach (var f in replayFiles.OrderBy(s => s))
            _output.WriteLine($"  {Path.GetRelativePath(replayRoot, f)}  ({new FileInfo(f).Length} bytes)");

        Assert.Null(error);
        Assert.NotNull(state);

        // The Architect terminus, made concrete:
        //   * act_index == 2 (third act, 0-indexed). Anything lower
        //     means we never crossed the Act 2 → Act 3 transition.
        //   * floor >= 16 — the post-Act-3-boss EventRoom slot. Act 3
        //     has 15 mappable floors plus the boss-then-Architect
        //     terminus pair; floor 16 is the Architect.
        //   * IsGameOver == true — the Architect's scripted attack
        //     flipped it. No "still alive on the map" scenario.
        // Together these say "we beat the playable game" as far as the
        // current beta allows (see documentation/sts2-game-facts.md).
        Assert.Equal(2, state!.CurrentActIndex);
        Assert.True(state.ActFloor >= 16,
            $"didn't reach the Architect terminus (final act={state.CurrentActIndex}, floor={state.ActFloor}, room={state.CurrentRoomType}, hp={state.Hp}/{state.MaxHp}, dead={state.IsDead}, heals={healCount})");
        Assert.True(state.IsGameOver,
            $"expected Architect to have flipped IsGameOver (final act={state.CurrentActIndex}, floor={state.ActFloor}, hp={state.Hp}/{state.MaxHp})");

        // Replay artifacts are the load-bearing output here — the whole
        // point of stacking cheats is to feed the replay viewer a
        // realistic full-game corpus.
        Assert.True(replayFiles.Length > 0, $"recording substrate produced no files under {replayRoot} — replay pipeline regressed");
        Assert.Contains(replayFiles, f => f.EndsWith("manifest.json"));
        Assert.Contains(replayFiles, f => f.EndsWith(".mcr"));
        // At least one .mcr per act — proves we actually drove through
        // each act rather than stalling somewhere mid-Act-2.
        Assert.Contains(replayFiles, f => f.Contains("/act1-") && f.EndsWith(".mcr"));
        Assert.Contains(replayFiles, f => f.Contains("/act2-") && f.EndsWith(".mcr"));
        Assert.Contains(replayFiles, f => f.Contains("/act3-") && f.EndsWith(".mcr"));
    }
}

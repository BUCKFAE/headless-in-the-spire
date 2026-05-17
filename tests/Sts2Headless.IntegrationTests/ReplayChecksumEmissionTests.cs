using System.Text.Json.Nodes;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.Replay;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Locks in the spike fix for `ChecksumTracker.IsEnabled`. The engine
// boots the tracker disabled in our config (TestMode.IsOn = true AND
// NetService.Type = Singleplayer fails both gates of
// `!TestMode.IsOn && Type.IsMultiplayer()`), which strips
// `CombatReplayWriter.RecordChecksum` of its subscription source and
// leaves `.mcr.checksumData` empty. `ReplayRecorder.EnableEngineRecording`
// now flips it back; without that flip, the assertion below regresses to
// `checksum_count: 0` like the original seed-42 sample.
//
// The test drives one combat to completion under a RecordingHost so a
// real `.mcr` lands on disk with real engine-emitted checksums. We
// assert >0 rather than an exact count because the count depends on
// the path the combat takes (player-turn boundaries + per-action
// completion + enemy-turn boundaries), and a brittle equality check
// would tax every game-version bump unnecessarily.
public class ReplayChecksumEmissionTests
{
    [Fact]
    public async Task RecordedCombat_Produces_Nonzero_Checksums()
    {
        using var tempReplays = new TempReplayRoot();
        await using var host = RecordingHost.Start(tempReplays.Path);

        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));

        var snap = await host.SendAsync<RunStateResult>("run/state");
        var monsterNode = snap.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var entered = await host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: monsterNode.Col, Row: monsterNode.Row));

        CombatState? combat = entered.CombatState;
        RewardsState? rewards = entered.RewardsState;
        for (var safety = 0; safety < 40 && rewards is null; safety++)
        {
            var attack = combat?.Hand.FirstOrDefault(c =>
                c.CanPlay && c.Cost <= combat.Energy && c.TargetType == TargetType.AnyEnemy);
            if (attack is not null)
            {
                var afterPlay = await host.SendAsync<RunPlayCardResult>(
                    "run/play_card", new RunPlayCardParams(CardIndex: attack.Index, TargetIndex: 0));
                combat = afterPlay.CombatState;
                rewards = afterPlay.RewardsState;
            }
            else
            {
                var afterEnd = await host.SendAsync<RunEndTurnResult>("run/end_turn");
                combat = afterEnd.CombatState;
                rewards = afterEnd.RewardsState;
            }
        }
        Assert.NotNull(rewards);

        // Force the recorder to flush the manifest. Two paths land us
        // there — start a fresh run (CleanUp fires the BeforeRunManagerCleanUp
        // prefix on the prior run) or dispose the host. The first is
        // cheaper and keeps the test inside the SendAsync envelope.
        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 7uL));

        var manifestPaths = Directory.GetFiles(tempReplays.Path, ReplayLayout.ManifestFileName, SearchOption.AllDirectories);
        Assert.NotEmpty(manifestPaths);
        var manifest = ReplayManifest.Deserialize(File.ReadAllText(manifestPaths[0]));

        Assert.NotEmpty(manifest.Combats);
        var combatWithChecksums = manifest.Combats.FirstOrDefault(c => c.ChecksumCount > 0);
        Assert.NotNull(combatWithChecksums);

        // A combat that flushed through OnCombatWriteReplay (CombatManager's
        // natural end-of-combat hook) without the player dying must surface
        // as Victory in the manifest. The test drove the combat to rewards
        // without dying, so every entry should be Victory (none Unknown,
        // none Defeat, none Abandoned).
        Assert.All(manifest.Combats, c =>
        {
            Assert.Equal(ReplayCombatOutcome.Victory, c.Outcome);
        });

        // Phase A.1: every .mcr should have a sibling timeline.json
        // emitted by CombatTimelineEmitter at flush time. The viewer
        // reads this file; if the recorder ever drops the emit step
        // this assertion is what surfaces the regression.
        var runDir = Path.GetDirectoryName(manifestPaths[0])!;
        var mcrPath = Path.Combine(runDir, combatWithChecksums!.McrFile.Replace('/', Path.DirectorySeparatorChar));
        var timelinePath = mcrPath + CombatTimelineEmitter.TimelineFileExtension;
        Assert.True(File.Exists(timelinePath), $"expected timeline.json next to {combatWithChecksums.McrFile}, got nothing");
        var timeline = JsonNode.Parse(File.ReadAllText(timelinePath))!.AsObject();
        Assert.Equal(CombatTimelineEmitter.SchemaVersion, (int)timeline["schema_version"]!);
        var checksumsArray = timeline["checksums"]!.AsArray();
        // The timeline's checksum array mirrors the manifest's count —
        // both come from the same .mcr.
        Assert.Equal(combatWithChecksums.ChecksumCount, checksumsArray.Count);

        // Each checksum entry must carry a `state` block with at least
        // one creature (player or monster). This is the data the
        // viewer uses to render per-turn HP; if it ever regresses to
        // empty, item 4 from the user redesign is broken.
        var firstChecksum = checksumsArray[0]!.AsObject();
        var state = firstChecksum["state"]!.AsObject();
        var creatures = state["creatures"]!.AsArray();
        Assert.NotEmpty(creatures);
        // A live combat always has the player creature in the snapshot.
        var playerEntry = creatures.FirstOrDefault(c => (string)c!["kind"]! == "player");
        Assert.NotNull(playerEntry);
        Assert.True((int)playerEntry!["current_hp"]! > 0);
        // Each turn fires checksums from multiple call sites
        // (player-turn-start / enemy-turn-start / enemy-turn-end /
        // phase-one-end / phase-two-end), so checksum_count routinely
        // exceeds action_count by a factor of 2–3. The only invariant
        // worth locking in here is "non-zero" — anything more specific
        // taxes every game-version bump. The exact ratio is observable
        // in the per-combat timeline once the re-executor lands.

        // run.json should appear too. The engine's own OnEnded is
        // gated on TestMode.IsOff (death path) and ShouldSave (always)
        // and neither is on in our headless config, so
        // ReplayRecorder.WriteRunHistoryIfMissing fills the gap by
        // calling RunHistoryUtilities.CreateRunHistoryEntry itself at
        // cleanup. The viewer's full-run timeline depends on this
        // file existing.
        var runJsonPath = Path.Combine(runDir, ReplayLayout.RunHistoryFileName);
        Assert.True(File.Exists(runJsonPath), $"expected run.json at {runJsonPath} — WriteRunHistoryIfMissing regression");
        var runJson = JsonNode.Parse(File.ReadAllText(runJsonPath))!.AsObject();
        Assert.True((int)runJson["schema_version"]! > 0);
        Assert.False((bool)runJson["win"]!); // not a heart victory
        // The recorded run ended mid-walk (test drove to rewards then
        // started a fresh run/new). Without an actual death or victory,
        // the recorder classifies it as abandoned — which suppresses
        // the bogus "killed by X" stamp the engine's
        // CreateRunHistoryEntry would otherwise apply just because
        // the current room was a combat.
        Assert.True((bool)runJson["was_abandoned"]!);
        // map_point_history is the spine of the viewer's full-run
        // view. A truncated recording at floor 2 still has at least
        // the Neow floor + first combat floor — assert non-empty.
        Assert.NotEmpty(runJson["map_point_history"]!.AsArray());
    }

    private sealed class TempReplayRoot : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sts2-replays-" + Guid.NewGuid().ToString("N"));
        public TempReplayRoot() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, recursive: true); } catch { /* best-effort */ } }
    }
}

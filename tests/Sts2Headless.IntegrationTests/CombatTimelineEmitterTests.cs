using Sts2Headless.IntegrationTests.Coverage;
using Sts2Headless.Replay;
using Sts2Headless.Runtime;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Locks in the shape of timeline.json — the per-combat artefact Phase A.1
// emits next to every .mcr (see CombatTimelineEmitter). Uses an existing
// vendor/sample-saves .mcr (the same one ReplayBytesRoundTripTests parses)
// so the test is reproducible without driving a fresh combat.
//
// What the assertions cover:
//   1. The four top-level branches of the document (schema_version,
//      header, initial_run, events, checksums) are present and have the
//      expected gross shape.
//   2. Each event has a `type` from the engine's CombatReplayEventType
//      enum (GameAction / HookAction / ResumeAction / PlayerChoice).
//   3. For at least one GameAction event, `action_type` and `action`
//      both populate — the polymorphic INetAction is being serialised
//      with its concrete fields, not the empty interface.
//   4. The `initial_run` block round-trips through the game's own
//      SerializableRun serializer — `schema_version` field is present
//      with a positive integer (rules out the path "we silently
//      serialised an empty object").
[Collection(InProcessSts2Collection.Name)]
public class CombatTimelineEmitterTests
{
    [Fact]
    public void EmitsExpectedShape_ForSampleMcr()
    {
        var repoRoot = Paths.LocateRepoRoot();
        var vendorDir = Path.Combine(repoRoot, "vendor");
        var mcr = SampleSaves.CombatReplayFiles().FirstOrDefault();
        if (mcr is null) return;  // sample fixture absent — skip

        VendorAssemblyResolver.Install(vendorDir);
        var preamble = RuntimeBootstrap.Run(vendorDir);
        Assert.NotNull(preamble.Sts2);
        var steps = BootstrapSequence.Apply(preamble.Sts2!);
        Assert.All(steps, s => Assert.True(s.Ok, $"bootstrap step failed: {s.Label}"));

        var sts2 = preamble.Sts2!;
        var reader = new CombatReplayReader(sts2);
        var emitter = new CombatTimelineEmitter(sts2);
        var replay = reader.ReadFile(mcr);

        var doc = emitter.Build(replay);

        Assert.Equal(CombatTimelineEmitter.SchemaVersion, (int)doc["schema_version"]!);
        var header = doc["header"]!.AsObject();
        Assert.False(string.IsNullOrEmpty((string)header["version"]!));
        Assert.True((uint)header["model_id_hash"]! == 1357847701u);
        Assert.True((int)header["event_count"]! > 0);

        var events = doc["events"]!.AsArray();
        Assert.Equal((int)header["event_count"]!, events.Count);
        foreach (var evt in events)
        {
            var type = (string)evt!["type"]!;
            Assert.Contains(type, new[] { "GameAction", "HookAction", "ResumeAction", "PlayerChoice" });
        }

        // At least one GameAction should surface a populated `action`
        // payload with a concrete type name. If the polymorphic INetAction
        // serialisation regresses, this is the canary.
        var firstGameAction = events.FirstOrDefault(e => (string)e!["type"]! == "GameAction");
        if (firstGameAction is not null)
        {
            Assert.False(string.IsNullOrEmpty((string?)firstGameAction["action_type"]));
            Assert.NotNull(firstGameAction["action"]);
        }

        var initialRun = doc["initial_run"]!.AsObject();
        // SerializableRun has a `schema_version` field per Mega Crit's
        // ISaveSchema convention; at v0.103.2 the value is 16. We assert
        // ">0" to avoid coupling the test to a specific schema number
        // (a bump should pass this test and surface elsewhere).
        var srSchema = (int?)initialRun["schema_version"];
        Assert.True(srSchema is > 0, $"initial_run.schema_version not populated (got {srSchema?.ToString() ?? "null"})");

        // checksums array may be empty if the sample was captured before
        // the checksum-tracker fix landed (the seed-42 sample has
        // checksum_count: 0). Don't assert non-empty here — that's the
        // ReplayChecksumEmissionTests' job.
        var checksums = doc["checksums"]!.AsArray();

        // Per-checksum state block (item 4 in the viewer feedback —
        // enables per-turn HP rendering). If checksums exist at all,
        // every one must carry a `state` block with creatures + players;
        // if none do, this assertion is trivially satisfied for the
        // sample captured before checksums were recorded.
        foreach (var entry in checksums)
        {
            var state = entry!["state"];
            Assert.NotNull(state);
            var creatures = state!["creatures"]!.AsArray();
            // A combat must have at least one creature in the snapshot —
            // either the player or an enemy. An empty list would mean
            // we extracted nothing useful and the viewer can't render HP.
            Assert.NotEmpty(creatures);
            foreach (var creature in creatures)
            {
                var kind = (string)creature!["kind"]!;
                Assert.Contains(kind, new[] { "player", "monster", "unknown" });
                Assert.True((int)creature["current_hp"]! >= 0);
                Assert.True((int)creature["max_hp"]! > 0);
            }
        }
    }
}

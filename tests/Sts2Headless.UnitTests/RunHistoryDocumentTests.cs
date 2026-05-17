using Sts2Headless.Protocol.Methods;
using Sts2Headless.Runtime;
using Xunit;

namespace Sts2Headless.UnitTests;

// Validates the typed mirror of RunHistory (AD-8). Parses every .run
// file under vendor/sample-saves/ and asserts:
//   1. They deserialise without exception (or surface the offender).
//   2. schema_version pins to 9 (matches the version stamped into the
//      game's release_info.json at v0.103.2). A schema_version bump in
//      a future game pin surfaces here as a value-mismatch assertion
//      rather than silently parsing through.
//   3. Every observed enum value maps to a non-Unknown variant — the
//      `Unknown` sentinels are the forward-compat escape hatch for
//      future game-side additions; if anything in the current fixture
//      maps to Unknown, the enum is missing a value.
//
// Skipped when the fixture is absent (CI without vendor/).
public class RunHistoryDocumentTests
{
    [Fact]
    public void All_Sample_RunHistory_Files_Parse_With_Schema_Version_9()
    {
        var runs = SampleSaves.RunHistoryFiles().ToList();
        if (runs.Count == 0) return;

        var failures = new List<string>();
        foreach (var path in runs)
        {
            try
            {
                var doc = RunHistoryDocument.ParseFile(path);
                Assert.Equal(9, doc.SchemaVersion);
                Assert.False(string.IsNullOrEmpty(doc.BuildId));
                Assert.False(string.IsNullOrEmpty(doc.Seed));
                Assert.NotEqual(GameMode.Unknown, doc.GameMode);
                Assert.NotEqual(PlatformType.Unknown, doc.PlatformType);
                foreach (var act in doc.MapPointHistory)
                {
                    foreach (var mp in act)
                    {
                        Assert.NotEqual(RunHistoryMapPointType.Unknown, mp.MapPointType);
                        if (mp.Rooms is null) continue;
                        foreach (var room in mp.Rooms)
                        {
                            Assert.NotEqual(RunHistoryRoomType.Unknown, room.RoomType);
                        }
                    }
                }
                foreach (var player in doc.Players)
                {
                    // Character is an opaque content-id ("CHARACTER.IRONCLAD")
                    // in RunHistory rather than our wire-level Character enum
                    // ("ironclad"); validate by shape rather than enum value.
                    Assert.StartsWith("CHARACTER.", player.Character, StringComparison.Ordinal);
                    if (player.Badges is null) continue;
                    foreach (var badge in player.Badges)
                    {
                        Assert.NotEqual(BadgeRarity.Unknown, badge.Rarity);
                    }
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(path)}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0,
            $"Failed to parse {failures.Count}/{runs.Count} .run files:\n  " + string.Join("\n  ", failures.Take(10)));
    }

    [Fact]
    public void Parsed_RunHistory_Round_Trips_Through_Serializer()
    {
        var runs = SampleSaves.RunHistoryFiles().ToList();
        if (runs.Count == 0) return;

        // Pick a small but representative one (the first abandoned run)
        // and round-trip it through serialize/deserialize. We can't
        // assert byte-equal output because the game's writer emits
        // fields in a specific order + with specific null-omission
        // rules that we may or may not match; but the structural
        // equality of the parsed document must survive.
        var path = runs[0];
        var first = RunHistoryDocument.ParseFile(path);
        var serialised = System.Text.Json.JsonSerializer.Serialize(first, RunHistoryDocument.JsonOptions);
        var second = RunHistoryDocument.Parse(serialised);

        Assert.Equal(first.SchemaVersion, second.SchemaVersion);
        Assert.Equal(first.Seed, second.Seed);
        Assert.Equal(first.BuildId, second.BuildId);
        Assert.Equal(first.GameMode, second.GameMode);
        Assert.Equal(first.WasAbandoned, second.WasAbandoned);
        Assert.Equal(first.Win, second.Win);
        Assert.Equal(first.RunTime, second.RunTime);
        Assert.Equal(first.MapPointHistory.Count, second.MapPointHistory.Count);
        Assert.Equal(first.Players.Count, second.Players.Count);
    }
}

using System.Text.Json;
using Sts2Headless.Eval.Json;
using Sts2Headless.Utils;

namespace Sts2Headless.Eval;

// Loaders for the committed seed banks plus an ad-hoc inline / from-file
// path for callers who want their own. Committed banks are read lazily
// from `documentation/eval/seeds/<name>.json` and cached for the life of
// the process.
public static class SeedBanks
{
    public const string CommittedDirRelative = "documentation/eval/seeds";

    public static SeedBank Smoke     => Lazy.Value.Smoke;
    public static SeedBank Reference => Lazy.Value.Reference;
    public static SeedBank Deep      => Lazy.Value.Deep;

    // Load a bank from an absolute or repo-relative file path. The file
    // must match the SeedBank JSON shape.
    public static SeedBank FromFile(string path)
    {
        var resolved = Path.IsPathRooted(path) ? path : Path.Combine(Paths.LocateRepoRoot(), path);
        if (!File.Exists(resolved))
            throw new FileNotFoundException($"Seed bank not found: {resolved}", resolved);
        var bytes = File.ReadAllBytes(resolved);
        var bank = JsonSerializer.Deserialize<SeedBank>(bytes, EvalJson.Wire)
            ?? throw new InvalidDataException($"Seed bank deserialised to null: {resolved}");
        return bank;
    }

    // For tests / one-offs. Banks built this way carry version "inline";
    // EvaluationReportIo refuses to emit a reproducibility stamp without
    // a tracked bank, so an inline bank is fine for sanity checks but
    // not for a published leaderboard.
    public static SeedBank Inline(IEnumerable<ulong> seeds, string? name = null) =>
        new(
            Name:             name ?? "inline",
            Version:          "inline",
            CreatedAt:        null,
            GameVersion:      null,
            GenerationMethod: null,
            Seeds:            seeds.ToList());

    private static readonly Lazy<CommittedBanks> Lazy = new(LoadCommitted);

    private sealed record CommittedBanks(SeedBank Smoke, SeedBank Reference, SeedBank Deep);

    private static CommittedBanks LoadCommitted()
    {
        var root = Paths.LocateRepoRoot();
        var dir = Path.Combine(root, CommittedDirRelative);
        return new CommittedBanks(
            Smoke:     LoadOne(dir, "smoke"),
            Reference: LoadOne(dir, "reference"),
            Deep:      LoadOne(dir, "deep"));
    }

    private static SeedBank LoadOne(string dir, string bankName)
    {
        var path = Path.Combine(dir, $"{bankName}.json");
        if (!File.Exists(path))
        {
            // Missing banks are not fatal at process start — callers that
            // never touch them shouldn't pay for IO errors. We surface
            // the missing file only when the bank is requested.
            return new SeedBank(
                Name:             bankName,
                Version:          "missing",
                CreatedAt:        null,
                GameVersion:      null,
                GenerationMethod: $"missing seed bank file: {path}",
                Seeds:            []);
        }
        return JsonSerializer.Deserialize<SeedBank>(File.ReadAllBytes(path), EvalJson.Wire)
            ?? throw new InvalidDataException($"Seed bank deserialised to null: {path}");
    }
}

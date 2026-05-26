using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Eval.Execution;

// Cartesian product → Cell list, with capability filtering folded in.
//
// Skip rules (capability mismatch is a *skip*, not a crash):
//   * Manifest.SupportedCharacters omits this cell's character.
//   * Manifest.SupportedAscensions omits this cell's ascension.
//   * Manifest.SupportedModifiers is non-null and disjoint from the
//     cell's modifier set.
//
// Skipped cells get logged via the `onSkip` callback so summary.md
// can render a "this agent did not play these N cells" note without
// pretending they were attempted.
//
// The replay-directory layout is decided here, once, so the executor
// is free to read it back off `Cell.RelativeReplayDir` without
// reconstructing it from axes (which would risk drift between the
// path encoded in CellResult.ReplayPath and the path the recorder
// actually writes to).
public static class MatrixExpander
{
    public static IReadOnlyList<Cell> Expand(
        EvaluationHarnessConfig    config,
        Action<MatrixSkip>?        onSkip = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Agents.Count == 0) throw new ArgumentException("EvaluationHarnessConfig.Agents must have at least one entry.", nameof(config));
        if (config.Seeds.Seeds.Count == 0) throw new ArgumentException("EvaluationHarnessConfig.Seeds must contain at least one seed.", nameof(config));
        if (config.Characters.Count == 0) throw new ArgumentException("EvaluationHarnessConfig.Characters must have at least one entry.", nameof(config));
        if (config.Ascensions.Count == 0) throw new ArgumentException("EvaluationHarnessConfig.Ascensions must have at least one entry.", nameof(config));

        // Single-axis matrices collapse the path key so a default-Ironclad
        // smoke eval gets `cells/<agent>/s<seed>/` (matching the spec's
        // indicative tree) instead of `cells/<agent>/Ironclad-A0-s<seed>/`
        // (necessary when the matrix legitimately spans characters).
        var includeCharInPath = config.Characters.Count > 1;
        var includeAscInPath  = config.Ascensions.Count > 1;
        var modSig = ModifierSignature(config.Modifiers);

        var cells = new List<Cell>();
        foreach (var manifest in config.Agents)
        {
            foreach (var character in config.Characters)
            {
                foreach (var ascension in config.Ascensions)
                {
                    if (!manifest.SupportedCharacters.Contains(character))
                    {
                        onSkip?.Invoke(new MatrixSkip(manifest, character, ascension, config.Modifiers, "character not in SupportedCharacters"));
                        continue;
                    }
                    if (!manifest.SupportedAscensions.Contains(ascension))
                    {
                        onSkip?.Invoke(new MatrixSkip(manifest, character, ascension, config.Modifiers, "ascension not in SupportedAscensions"));
                        continue;
                    }
                    if (manifest.SupportedModifiers is { } supportedMods)
                    {
                        var unsupportedMod = config.Modifiers.FirstOrDefault(m => !supportedMods.Contains(m));
                        if (unsupportedMod != default && !supportedMods.Contains(unsupportedMod))
                        {
                            onSkip?.Invoke(new MatrixSkip(manifest, character, ascension, config.Modifiers, $"modifier {unsupportedMod} not in SupportedModifiers"));
                            continue;
                        }
                    }

                    foreach (var seed in config.Seeds.Seeds)
                    {
                        var dir = BuildRelativeDir(manifest, character, ascension, seed, includeCharInPath, includeAscInPath, modSig);
                        cells.Add(new Cell(
                            Manifest:          manifest,
                            Seed:              seed,
                            Character:         character,
                            Ascension:         ascension,
                            Modifiers:         config.Modifiers,
                            RelativeReplayDir: dir,
                            Budgets:           manifest.Budgets ?? config.Budgets));
                    }
                }
            }
        }
        return cells;
    }

    private static string BuildRelativeDir(
        AgentManifest manifest,
        Character     character,
        int           ascension,
        ulong         seed,
        bool          includeChar,
        bool          includeAsc,
        string        modSig)
    {
        var agentSlug = Slug(manifest.Name);
        var leaf = (includeChar, includeAsc, modSig.Length > 0) switch
        {
            (false, false, false) => $"s{seed}",
            (false, false, true)  => $"s{seed}-{modSig}",
            (true,  false, false) => $"{character}-s{seed}",
            (true,  false, true)  => $"{character}-s{seed}-{modSig}",
            (false, true,  false) => $"A{ascension}-s{seed}",
            (false, true,  true)  => $"A{ascension}-s{seed}-{modSig}",
            (true,  true,  false) => $"{character}-A{ascension}-s{seed}",
            (true,  true,  true)  => $"{character}-A{ascension}-s{seed}-{modSig}",
        };
        return Path.Combine("cells", agentSlug, leaf).Replace('\\', '/');
    }

    private static string ModifierSignature(IReadOnlyList<ModifierId> mods)
    {
        if (mods.Count == 0) return "";
        var sorted = mods.Select(m => m.ToString()).OrderBy(s => s, StringComparer.Ordinal);
        return "mods-" + string.Join("+", sorted);
    }

    private static string Slug(string s)
    {
        // Conservative slug: keep [a-zA-Z0-9._-], swap others to '-'. Agent
        // names already lean on this shape (e.g. "ironclad-conservative")
        // so the transform is usually a no-op.
        var chars = s.Select(c =>
            (char.IsLetterOrDigit(c) || c is '.' or '_' or '-') ? c : '-').ToArray();
        return new string(chars);
    }
}

public sealed record MatrixSkip(
    AgentManifest             Manifest,
    Character                 Character,
    int                       Ascension,
    IReadOnlyList<ModifierId> Modifiers,
    string                    Reason);

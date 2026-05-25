using System.Text;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.Runtime;
using Sts2Headless.Runtime.Bindings;
using Sts2Headless.Runtime.Loading;

namespace Sts2Headless.Commands;

// Phase-4 cataloging probe: drive the engine's natural reward chain (no
// try/catch around CardPileCmd.Add / OnSelectWrapper / OnSkipped /
// SyncLocalObtainedCard) and dump every gap that surfaces. Companion to
// `--probe-natural-chain`, which covers the end-of-turn chain.
//
// Walks: start run → first MapRoom Monster → drive combat with production
// PlayCard/EndTurn (those paths are already proven by Phase 3) → once
// rewards surface, call Sts2Bindings.SelectRewardAndCatalog /
// SkipRewardAndCatalog one reward at a time. Output is markdown, same shape
// as ProbeNaturalChainCommand's gap doc.
internal static class ProbeRewardsNaturalChainCommand
{
    private const string OutputRelative = "documentation/research/reward-chain-gaps.md";
    private const ulong Seed = 42uL;

    public static int Run(string vendorDir, string repoRoot)
    {
        Console.WriteLine("probe-rewards-natural-chain:");

        var preamble = RuntimeBootstrap.Run(vendorDir);
        if (preamble.SetupError is not null)
        {
            Console.Error.WriteLine($"  bootstrap setup failed: {preamble.SetupError}");
            return 1;
        }
        foreach (var p in preamble.Patches)
            if (!p.Patched) Console.Error.WriteLine($"  WARN: patch '{p.Target}' did not apply ({p.Detail})");
        foreach (var s in BootstrapSequence.Apply(preamble.Sts2!))
            if (!s.Ok) Console.Error.WriteLine($"  WARN: bootstrap step '{s.Label}' did not succeed ({s.Detail})");

        Sts2Bindings bindings;
        try { bindings = Sts2Bindings.Bind(preamble.Sts2!, preamble.SyncContext); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  bind failed: {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
            return 1;
        }

        Console.WriteLine($"  starting run (seed={Seed}, NetId=1)...");
        RunHandle handle;
        try { handle = bindings.StartRun(Character.Ironclad, Seed); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  StartRun threw: {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
            return 1;
        }

        Console.WriteLine("  navigating to first combat...");
        var snap = bindings.ReadSnapshot(handle);
        if (snap.CurrentRoomType != RoomType.MapRoom)
        {
            Console.Error.WriteLine($"  expected MapRoom after StartRun, got {snap.CurrentRoomType}");
            return 1;
        }
        var monster = snap.AvailableMapNodes.FirstOrDefault(n => n.Type == MapNodeType.Monster && n.Row > 0);
        if (monster is null)
        {
            Console.Error.WriteLine("  no Monster node available on the first map row");
            return 1;
        }
        try { bindings.EnterMapCoord(handle, monster.Col, monster.Row); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  EnterMapCoord threw: {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
            return 1;
        }

        // Drive combat to completion via production paths so rewards surface.
        Console.WriteLine("  driving combat to rewards (production EndTurn / PlayCard)...");
        snap = bindings.ReadSnapshot(handle);
        var safety = 0;
        while (snap.RewardsState is null && safety++ < 50)
        {
            var combat = snap.CombatState;
            if (combat is null || !combat.IsInProgress) break;
            var attack = combat.Hand.FirstOrDefault(c => c.CanPlay && c.Cost <= combat.Energy
                && c.TargetType == TargetType.AnyEnemy);
            try
            {
                if (attack is not null) bindings.PlayCard(handle, attack.Index, 0);
                else bindings.EndTurn(handle);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  combat-drive threw: {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
                return 1;
            }
            snap = bindings.ReadSnapshot(handle);
        }
        if (snap.RewardsState is null)
        {
            Console.Error.WriteLine($"  combat did not surface rewards within {safety} actions");
            return 1;
        }

        var rewards = snap.RewardsState.Available;
        Console.WriteLine($"  rewards pending: {rewards.Count}");
        for (var i = 0; i < rewards.Count; i++)
            Console.WriteLine($"    [{i}] kind={rewards[i].Kind} canSkip={rewards[i].CanSkip}"
                + (rewards[i].Kind == RewardKind.Card ? $" cards={rewards[i].Cards?.Count ?? 0}" : "")
                + (rewards[i].GoldAmount is int g ? $" gold={g}" : "")
                + (rewards[i].RelicId is string r ? $" relic={r}" : "")
                + (rewards[i].PotionId is string p ? $" potion={p}" : ""));

        // Catalog each reward through the natural chain. Strategy:
        //   - For non-card rewards: SelectRewardAndCatalog (no skip path).
        //   - For card rewards: take the first card via SelectRewardAndCatalog
        //     so CardPileCmd.Add fires. If a separate skippable card reward is
        //     present, also catalog the skip path.
        //
        // Per-reward catalogs accumulate into a single output document.
        var catalogs = new List<(string Label, Sts2Bindings.CatalogResult Result)>();

        // Walk by index from the back so removals don't shift earlier indexes.
        // (SelectRewardAndCatalog / SkipRewardAndCatalog mutate the pending list.)
        var snapshot = rewards.ToArray();
        for (var i = snapshot.Length - 1; i >= 0; i--)
        {
            var r = snapshot[i];
            try
            {
                Sts2Bindings.CatalogResult cat;
                if (r.Kind == RewardKind.Card)
                {
                    var hasCards = (r.Cards?.Count ?? 0) > 0;
                    if (!hasCards)
                    {
                        Console.WriteLine($"  [{i}] card reward had no cards — skipping catalog");
                        continue;
                    }
                    cat = bindings.SelectRewardAndCatalog(handle, i, 0);
                    catalogs.Add(($"select-card[{i}] (cardIndex=0)", cat));
                }
                else
                {
                    cat = bindings.SelectRewardAndCatalog(handle, i, null);
                    catalogs.Add(($"select-{r.Kind.ToString().ToLowerInvariant()}[{i}]", cat));
                }
                Console.WriteLine($"    → {cat.TerminalState}: gaps={cat.UniqueGaps.Count}");
                foreach (var g in cat.UniqueGaps)
                    Console.WriteLine($"      [{g.Phase}] {ShortName(g.ExceptionType)}: {g.Message}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  [{i}] catalog call threw outside catch: {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
            }
        }

        var outputPath = Path.Combine(repoRoot, OutputRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, FormatCatalog(catalogs, Seed));
        Console.WriteLine($"  wrote {OutputRelative}");

        var totalUnique = catalogs.Sum(c => c.Result.UniqueGaps.Count);
        return totalUnique == 0 ? 0 : 2;
    }

    private static string ShortName(string fullyQualified)
    {
        var idx = fullyQualified.LastIndexOf('.');
        return idx < 0 ? fullyQualified : fullyQualified[(idx + 1)..];
    }

    private static string FormatCatalog(
        List<(string Label, Sts2Bindings.CatalogResult Result)> catalogs, ulong seed)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# reward-chain gaps");
        sb.AppendLine();
        sb.AppendLine($"Generated by `just runner::probe::rewards-natural-chain` (seed={seed}). Walks from a fresh Ironclad run through the first reachable combat into the post-combat reward set, then drives each reward through the engine's natural claim path — `CardPileCmd.Add(card, PileType.Deck)` for card rewards, `Reward.OnSelectWrapper()` for non-card rewards, `RewardSynchronizer.SyncLocalObtainedCard` for the obtain-listener fan-out — with **no try/catch wrappers**. Each section below records the gaps that surfaced for one reward.");
        sb.AppendLine();
        sb.AppendLine("Re-run after every reward-related stub or binding change; the goal is **0 unique gaps** across all sections so Phase 4 can drop the production safety nets without regressing.");
        sb.AppendLine();

        var totalUnique = catalogs.Sum(c => c.Result.UniqueGaps.Count);
        sb.AppendLine("## summary");
        sb.AppendLine();
        sb.AppendLine($"- Rewards cataloged: `{catalogs.Count}`");
        sb.AppendLine($"- Total unique gaps across all rewards: `{totalUnique}`");
        sb.AppendLine();

        if (catalogs.Count == 0)
        {
            sb.AppendLine("## no rewards surfaced");
            sb.AppendLine();
            sb.AppendLine("Combat ended but no reward set was generated. Check that `TryGeneratePendingRewards` is running.");
            return sb.ToString();
        }

        if (totalUnique == 0)
        {
            sb.AppendLine("## ✓ no gaps — every reward chain runs end-to-end");
            sb.AppendLine();
            sb.AppendLine("Phase 4 is safe to proceed: production SelectReward / SkipReward / ClaimCardReward can drop their try/catch wrappers and switch to CardPileCmd.Add without regressing this scenario. Extend the probe (different combats, skippable card rewards, treasure-room relics) to harden coverage further.");
            sb.AppendLine();
        }

        for (var i = 0; i < catalogs.Count; i++)
        {
            var (label, c) = catalogs[i];
            sb.AppendLine($"## {i + 1}. `{label}`");
            sb.AppendLine();
            sb.AppendLine($"- TerminalState: `{c.TerminalState}`");
            sb.AppendLine($"- Synchronous gaps: `{c.Gaps.Count}` ({c.UniqueGaps.Count} unique)");
            sb.AppendLine();

            if (c.UniqueGaps.Count == 0)
            {
                sb.AppendLine("✓ no synchronous gaps.");
                sb.AppendLine();
            }
            else
            {
                foreach (var g in c.UniqueGaps)
                {
                    sb.AppendLine($"### `{ShortName(g.ExceptionType)}` (caught during `{g.Phase}`)");
                    sb.AppendLine();
                    sb.AppendLine($"**Message:** `{g.Message}`");
                    sb.AppendLine();
                    sb.AppendLine("**Stack (top frames):**");
                    sb.AppendLine();
                    sb.AppendLine("```");
                    foreach (var f in g.StackFrames) sb.AppendLine(f);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }

            if (!string.IsNullOrWhiteSpace(c.CapturedStderr))
            {
                var lines = c.CapturedStderr.Split('\n')
                    .Where(l => l.Contains("[godot:err]") || l.Contains("[godot:push-error]"))
                    .Take(40)
                    .ToArray();
                if (lines.Length > 0)
                {
                    sb.AppendLine("**Engine-logged stderr (truncated to 40 prefixed lines):**");
                    sb.AppendLine();
                    sb.AppendLine("```");
                    foreach (var l in lines) sb.AppendLine(l.TrimEnd('\r'));
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }
}

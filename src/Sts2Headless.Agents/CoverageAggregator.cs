using System.Text;
using System.Text.Json;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents;

// Aggregates CoverageReport snapshots across many runs and renders gap
// reports against the *IdNames.AllWireNames universe.
//
// Use shape:
//
//   var agg = new CoverageAggregator();
//   foreach (var seed in seeds) {
//       var rec = new CoverageRecorder();
//       await AgentDriver.PlayRunAsync(host, agent, coverageRecorder: rec, ...);
//       agg.Add(rec.Snapshot(), runLabel: $"seed-{seed}");
//   }
//   File.WriteAllText("documentation/coverage/latest.md", agg.RenderMarkdown());
//   File.WriteAllText("documentation/coverage/latest.json", agg.RenderJson());
//
// The aggregator is stateful but not thread-safe — concurrent runs should
// each use their own recorder and call Add(...) once their report is final.
public sealed class CoverageAggregator
{
    private readonly HashSet<string> _cardsSeen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _cardsPlayed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _relicsSeen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _potionsSeen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _potionsUsed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _monstersFaced = new(StringComparer.Ordinal);
    private readonly HashSet<string> _powersSeen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _eventOptionsSeen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _eventOptionsTaken = new(StringComparer.Ordinal);
    private readonly HashSet<string> _relicsTriggered = new(StringComparer.Ordinal);
    private readonly HashSet<string> _cardsTriggered = new(StringComparer.Ordinal);
    private readonly HashSet<string> _monstersTriggered = new(StringComparer.Ordinal);
    private readonly HashSet<string> _potionsTriggered = new(StringComparer.Ordinal);
    private readonly HashSet<string> _powersTriggered = new(StringComparer.Ordinal);
    private readonly HashSet<string> _hooksFired = new(StringComparer.Ordinal);
    private readonly List<string> _runLabels = new();

    public int RunCount => _runLabels.Count;
    public IReadOnlyList<string> RunLabels => _runLabels;

    public void Add(CoverageReport report, string runLabel)
    {
        if (report is null) throw new ArgumentNullException(nameof(report));
        _runLabels.Add(runLabel);
        foreach (var x in report.CardsSeen) _cardsSeen.Add(x);
        foreach (var x in report.CardsPlayed) _cardsPlayed.Add(x);
        foreach (var x in report.RelicsSeen) _relicsSeen.Add(x);
        foreach (var x in report.PotionsSeen) _potionsSeen.Add(x);
        foreach (var x in report.PotionsUsed) _potionsUsed.Add(x);
        foreach (var x in report.MonstersFaced) _monstersFaced.Add(x);
        foreach (var x in report.PowersSeen) _powersSeen.Add(x);
        foreach (var x in report.EventOptionsSeen) _eventOptionsSeen.Add(x);
        foreach (var x in report.EventOptionsTaken) _eventOptionsTaken.Add(x);
        foreach (var x in report.RelicsTriggered) _relicsTriggered.Add(x);
        foreach (var x in report.CardsTriggered) _cardsTriggered.Add(x);
        foreach (var x in report.MonstersTriggered) _monstersTriggered.Add(x);
        foreach (var x in report.PotionsTriggered) _potionsTriggered.Add(x);
        foreach (var x in report.PowersTriggered) _powersTriggered.Add(x);
        foreach (var x in report.HooksFired) _hooksFired.Add(x);
    }

    // Render as markdown — one section per kind with seen/played + missing
    // counts. Event-option lines are observed/selected (not in the
    // manifest universe because event-option text keys aren't a 1:1 with
    // the EventId enum — multiple options per event, plus loc-bound naming).
    public string RenderMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Coverage report");
        sb.AppendLine();
        sb.AppendLine($"Runs aggregated: **{RunCount}**");
        if (RunCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Runs:");
            foreach (var label in _runLabels) sb.AppendLine($"- {label}");
        }
        sb.AppendLine();

        // Each "manifest-anchored" axis compares the observed set to the
        // *IdNames universe. The Triggered axis (where present) is strictly
        // tighter than Seen — a model has to be active in a run to fire,
        // so seen−triggered measures owned/visible-but-inactive content.
        RenderManifestSection(sb, "Cards",       observed: _cardsSeen,       universe: CardIdNames.AllWireNames,        secondaryName: "played",    secondary: _cardsPlayed,    tertiaryName: "triggered", tertiary: _cardsTriggered);
        RenderManifestSection(sb, "Relics",      observed: _relicsSeen,      universe: RelicIdNames.AllWireNames,       secondaryName: "triggered", secondary: _relicsTriggered, tertiaryName: null,        tertiary: null);
        RenderManifestSection(sb, "Potions",     observed: _potionsSeen,     universe: PotionIdNames.AllWireNames,      secondaryName: "used",      secondary: _potionsUsed,    tertiaryName: "triggered", tertiary: _potionsTriggered);
        RenderManifestSection(sb, "Monsters",    observed: _monstersFaced,   universe: MonsterIdNames.AllWireNames,     secondaryName: "triggered", secondary: _monstersTriggered, tertiaryName: null,     tertiary: null);
        RenderManifestSection(sb, "Powers",      observed: _powersSeen,      universe: PowerIdNames.AllWireNames,       secondaryName: "triggered", secondary: _powersTriggered,   tertiaryName: null,     tertiary: null);

        // Event-option text keys aren't in the EventId manifest (events
        // have multiple options per event; the manifest catalogues events,
        // not options). Render as a free-form section: how many distinct
        // text keys appeared, how many were selected.
        sb.AppendLine($"## Event options");
        sb.AppendLine();
        sb.AppendLine($"- observed:    **{_eventOptionsSeen.Count}** distinct text keys");
        sb.AppendLine($"- selected:    **{_eventOptionsTaken.Count}** distinct text keys");
        sb.AppendLine();

        // Hook firings across all kinds: not bound to a universe (the
        // AbstractModel hook surface isn't a content kind we manifest),
        // but the count of distinct hooks observed is a useful "are we
        // exercising late-combat / merchant / rest-site code paths?"
        // signal.
        sb.AppendLine($"## Hook firings (all kinds)");
        sb.AppendLine();
        sb.AppendLine($"- distinct hooks observed: **{_hooksFired.Count}**");
        if (_hooksFired.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("```");
            foreach (var h in _hooksFired.OrderBy(s => s, StringComparer.Ordinal)) sb.AppendLine(h);
            sb.AppendLine("```");
        }
        sb.AppendLine();

        return sb.ToString();
    }

    public string RenderJson()
    {
        var doc = new
        {
            runCount = RunCount,
            runLabels = _runLabels,
            cards = ManifestStats(_cardsSeen, CardIdNames.AllWireNames, _cardsPlayed, _cardsTriggered),
            relics = ManifestStats(_relicsSeen, RelicIdNames.AllWireNames, _relicsTriggered, tertiary: null),
            potions = ManifestStats(_potionsSeen, PotionIdNames.AllWireNames, _potionsUsed, _potionsTriggered),
            monsters = ManifestStats(_monstersFaced, MonsterIdNames.AllWireNames, _monstersTriggered, tertiary: null),
            powers = ManifestStats(_powersSeen, PowerIdNames.AllWireNames, _powersTriggered, tertiary: null),
            eventOptionsSeen = _eventOptionsSeen.OrderBy(s => s, StringComparer.Ordinal),
            eventOptionsTaken = _eventOptionsTaken.OrderBy(s => s, StringComparer.Ordinal),
            hooksFired = _hooksFired.OrderBy(s => s, StringComparer.Ordinal),
        };
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object ManifestStats(
        IReadOnlyCollection<string> observed,
        IReadOnlyCollection<string> universe,
        IReadOnlyCollection<string>? secondary,
        IReadOnlyCollection<string>? tertiary)
    {
        var universeSet = new HashSet<string>(universe, StringComparer.Ordinal);
        var seenInUniverse = observed.Where(universeSet.Contains).ToHashSet(StringComparer.Ordinal);
        var unknownObserved = observed.Where(x => !universeSet.Contains(x)).OrderBy(s => s, StringComparer.Ordinal).ToList();
        var missing = universe.Except(seenInUniverse, StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
        return new
        {
            universe = universe.Count,
            seen = seenInUniverse.Count,
            secondary = secondary?.Count,
            tertiary = tertiary?.Count,
            // The Secondary / Tertiary axes (cards-played / potions-used,
            // cards-triggered / potions-triggered) — full lists, so the
            // human reading the report can spot "the greedy agent only
            // plays Strike + Defend across 5 seeds" without cross-
            // referencing logs.
            secondaryIds = secondary?.OrderBy(s => s, StringComparer.Ordinal).ToList(),
            tertiaryIds = tertiary?.OrderBy(s => s, StringComparer.Ordinal).ToList(),
            missing,
            // Ids that appeared in observations but aren't in the on-disk
            // manifest. Non-empty here is a hint that the game DLL was
            // bumped without regenerating the manifest, or the host
            // surfaced an id we don't recognise.
            unknownObserved,
        };
    }

    private static void RenderManifestSection(
        StringBuilder sb,
        string title,
        IReadOnlyCollection<string> observed,
        IReadOnlyCollection<string> universe,
        string? secondaryName,
        IReadOnlyCollection<string>? secondary,
        string? tertiaryName,
        IReadOnlyCollection<string>? tertiary)
    {
        var universeSet = new HashSet<string>(universe, StringComparer.Ordinal);
        var seenInUniverse = observed.Where(universeSet.Contains).ToHashSet(StringComparer.Ordinal);
        var missing = universe.Except(seenInUniverse, StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
        var unknown = observed.Where(x => !universeSet.Contains(x)).OrderBy(s => s, StringComparer.Ordinal).ToList();

        sb.AppendLine($"## {title}");
        sb.AppendLine();
        sb.AppendLine($"- universe (manifest):  **{universe.Count}**");
        sb.AppendLine($"- seen:                 **{seenInUniverse.Count}** ({Percent(seenInUniverse.Count, universe.Count)})");
        if (secondaryName is not null && secondary is not null)
            sb.AppendLine($"- {secondaryName}:{new string(' ', Math.Max(1, 18 - secondaryName.Length))}**{secondary.Count}** ({Percent(secondary.Count, universe.Count)})");
        if (tertiaryName is not null && tertiary is not null)
            sb.AppendLine($"- {tertiaryName}:{new string(' ', Math.Max(1, 18 - tertiaryName.Length))}**{tertiary.Count}** ({Percent(tertiary.Count, universe.Count)})");
        sb.AppendLine($"- missing:              **{missing.Count}**");

        if (missing.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<details><summary>missing ids</summary>");
            sb.AppendLine();
            sb.AppendLine("```");
            // Cap the dump so the report stays readable on a 577-card kind
            // where coverage starts near zero — the first ~200 items are
            // already enough to chart progress.
            const int maxList = 200;
            foreach (var id in missing.Take(maxList)) sb.AppendLine(id);
            if (missing.Count > maxList) sb.AppendLine($"... ({missing.Count - maxList} more)");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("</details>");
        }

        if (unknown.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"- ⚠ unknown observed: **{unknown.Count}** id(s) appeared in observations but are not in the {title.ToLowerInvariant()} manifest. Re-run `just generate-content-ids`.");
            sb.AppendLine();
            sb.AppendLine("```");
            foreach (var id in unknown.Take(20)) sb.AppendLine(id);
            if (unknown.Count > 20) sb.AppendLine($"... ({unknown.Count - 20} more)");
            sb.AppendLine("```");
        }
        sb.AppendLine();
    }

    private static string Percent(int part, int total) =>
        total == 0 ? "n/a" : $"{(part * 100.0 / total):F1}%";
}

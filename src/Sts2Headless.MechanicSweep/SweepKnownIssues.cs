namespace Sts2Headless.MechanicSweep;

// Per-kind allowlists of "this id is known to crash in headless because
// the engine path it exercises requires state the host can't stage
// cleanly today (UI screens, populated reward pools, a character we
// haven't implemented yet, …)". Sweeps still RUN these ids unchanged —
// when the engine ships a fix or we add the missing wire surface, the
// fixture succeeds and the row flips to Played, which is the cue to
// remove the entry from this catalog.
//
// Lifecycle:
//   1. A sweep surfaces a new Crashed id with an engine-internal stack.
//   2. Investigate: is it a card-select-screen / reward-pool / off-class
//      shape (catalog-grade), or a real bug worth filing?
//   3. If catalog-grade: add a row with a one-line reason. The next
//      sweep classifies it as KnownUnsafe and the failing-assertion
//      pressure comes off.
//   4. After an engine bump: the row flips to Played → remove from
//      catalog. KnownIssuesParityTest catches stale entries (id no
//      longer in the manifest).
//
// Same posture as CardMechanics.IsHeadlessUnsafe in
// src/Sts2Headless.Agents/Authoring/CardMechanics.cs — that flag covers
// the agent's view of "don't draft this card"; this catalog covers the
// sweep's view of "this row would be Crashed but we know why".
public static class SweepKnownIssues
{
    public sealed record Issue(string Id, string Reason);

    // Cards whose OnPlay can't be exercised cleanly in headless even
    // after the standard fixture work in CardSweep. Currently empty:
    //
    // Trimmed 2026-05-22:
    //   * FLASH_OF_STEEL, NEUTRALIZE, SLICE, SUPPRESS, WHIRLWIND —
    //     all five NRE'd on `SaveManager.Instance.PrefsSave.FastMode`
    //     because the headless bootstrap didn't initialise PrefsSave.
    //     `BootstrapSequence.InitSavePrefsData` (calling the engine's
    //     `SaveManager.InitPrefsDataForTest()`) now seeds a default
    //     PrefsSave, and the five cards play cleanly.
    //   * MAD_SCIENCE — needed `TinkerTimeType` set to one of
    //     Attack / Skill / Power before play (engine throws AOOR on
    //     the default switch branch otherwise). Sts2Bindings.Cheats
    //     .ApplyPostCreateDefaults now defaults it to Attack with
    //     no rider in replace_deck.
    public static readonly IReadOnlyList<Issue> Cards = [];

    // Relics whose AfterObtained() can't be exercised cleanly in
    // single-player headless. Trimmed 2026-05-22: GLASS_EYE,
    // LOST_COFFER, ORRERY, DUSTY_TOME used to be here. The first
    // three NRE'd on CardReward.OnSelect because
    // `runState.CurrentMapPointHistoryEntry` was null (give_relic
    // ran before any map point was entered, leaving MapPointHistory
    // empty); Sts2Bindings.GiveRelic now seeds a stub entry. Dusty
    // Tome NRE'd because its per-run AncientCard field wasn't set;
    // GiveRelic now calls SetupForPlayer(Player) on relics that
    // declare it.
    public static readonly IReadOnlyList<Issue> Relics =
    [
        // MASSIVE_SCROLL is the "Multiplayer Cards" relic — its
        // AfterObtained filters the character's CardPool by
        // `MultiplayerConstraint == MultiplayerOnly`. Headless runs
        // in single-player (`Players.Count == 1`), so the player's
        // CardMultiplayerConstraint returns SingleplayerOnly and the
        // filtered set is empty. CardFactory.CreateForReward then
        // throws "couldn't generate valid rarity!". Unblocking this
        // would require multiplayer-mode support in headless (no
        // wire surface yet) — out of scope for the per-id sweep.
        new("MASSIVE_SCROLL",
            "InvalidOperationException 'couldn't generate valid rarity' — relic filters CardPool to MultiplayerOnly cards, which never exist in single-player headless. By-design game-mode mismatch, not a fixture issue."),
    ];

    // The empty kinds are listed explicitly so a future sweep that
    // discovers a known-unsafe pattern has somewhere to put the row
    // without needing to grow the dictionary mid-edit. Adding a kind
    // here is a no-op when its list is empty — the lookup just always
    // returns false.
    public static readonly IReadOnlyList<Issue> Potions      = [];
    public static readonly IReadOnlyList<Issue> Events       = [];
    public static readonly IReadOnlyList<Issue> Encounters   = [];
    public static readonly IReadOnlyList<Issue> Powers       = [];
    public static readonly IReadOnlyList<Issue> Afflictions  = [];
    public static readonly IReadOnlyList<Issue> Enchantments = [];

    // Kind keys match the per-sweep "kind" string used in SweepReport
    // (the singular noun: "card", "relic", "potion", …). Each sweep
    // passes its kind through SweepInternals.ClassifyWireError so the
    // catalog lookup happens in one place.
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> s_byKind =
        new(StringComparer.Ordinal)
        {
            ["card"]        = ToDict(Cards),
            ["relic"]       = ToDict(Relics),
            ["potion"]      = ToDict(Potions),
            ["event"]       = ToDict(Events),
            ["encounter"]   = ToDict(Encounters),
            ["power"]       = ToDict(Powers),
            ["affliction"]  = ToDict(Afflictions),
            ["enchantment"] = ToDict(Enchantments),
        };

    private static IReadOnlyDictionary<string, string> ToDict(IReadOnlyList<Issue> issues) =>
        issues.ToDictionary(i => i.Id, i => i.Reason, StringComparer.Ordinal);

    // True if `id` is catalogued as known-unsafe for `kind` and a
    // reason string is returned via `reason`. Sweeps use the boolean
    // to gate the Crashed→KnownUnsafe reclassification; the reason is
    // included in the row Detail so reports stay self-explanatory.
    public static bool TryGetReason(
        string kind,
        string id,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? reason)
    {
        if (s_byKind.TryGetValue(kind, out var dict) && dict.TryGetValue(id, out var r))
        {
            reason = r;
            return true;
        }
        reason = null;
        return false;
    }

    // Every (kind, id) pair in the catalog — used by the parity test
    // in tests/Sts2Headless.IntegrationTests/Coverage/ to assert each
    // entry still refers to a real wire id (catches deletions /
    // renames after a GAME_VERSION bump).
    public static IEnumerable<(string Kind, string Id, string Reason)> AllEntries()
    {
        foreach (var (kind, dict) in s_byKind)
            foreach (var (id, reason) in dict)
                yield return (kind, id, reason);
    }
}

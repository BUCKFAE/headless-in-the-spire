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
public static class SweepKnownIssues
{
    public sealed record Issue(string Id, string Reason);

    // Cards that remain Unplayable in the standard CardSweep fixture even
    // after the smoke fixture's resource boost (debug/set_energy 20 +
    // debug/gain_stars 20 — see CardSweep.ResourceBoost).
    //
    // Empty today. Two historical buckets that lived here have been
    // resolved:
    //
    //   * TargetType=AnyAlly (co-op) cards — `CardModel.CanPlay` refuses
    //     when `CombatState.PlayerCreatures.Where(IsAlive).Count() > 1` is
    //     false, which is structurally true in single-player STS2 (Pets /
    //     Allies aren't Players). Per 01-initial-goals.md ("Single-player
    //     only") they're out of scope. CardSweep skips them at iteration
    //     time via CardSweep.AnyAllyMultiplayerOnly so they don't surface
    //     as noisy Unplayable rows.
    //
    //   * IsPlayable-override cards (CLASH, PACTS_END) — the engine's
    //     IsPlayable virtual gates on bespoke combat state (all-Attack
    //     hand / 3+ Exhaust pile). CardSweep now stages a tailored deck
    //     for each (CardSweep.CustomStagingDeckFor) plus a per-card
    //     pre-stage hook (PreStageHandAsync); both cards Play.
    //
    // Re-add an entry here only when a sweep surfaces a NEW clean-refusal
    // shape (the engine deliberately said no) that the fixture genuinely
    // can't stage. The standard cure for "the fixture refuses my card" is
    // to extend CustomStagingDeckFor / PreStageHandAsync first.
    public static readonly IReadOnlyList<Issue> CardExpectedRefusals = [];

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
    // single-player headless. Currently empty:
    //
    // Trimmed 2026-05-22:
    //   * GLASS_EYE / LOST_COFFER / ORRERY — NRE'd on
    //     `runState.CurrentMapPointHistoryEntry` being null
    //     (give_relic ran before any map point was entered).
    //     Sts2Bindings.GiveRelic now seeds a stub entry.
    //   * DUSTY_TOME — needed SetupForPlayer(Player) to sample its
    //     per-run AncientCard. GiveRelic now calls SetupForPlayer
    //     on any relic that declares it.
    //   * MASSIVE_SCROLL — required MultiplayerOnly cards in the
    //     pool. Two Harmony patches together (IRunState
    //     .CardMultiplayerConstraint → None and CardFactory
    //     .FilterForPlayerCount → pass-through) leave the
    //     multiplayer-only subset reachable for the relic's
    //     downstream filter.
    public static readonly IReadOnlyList<Issue> Relics = [];

    // The empty kinds are listed explicitly so a future sweep that
    // discovers a known-unsafe pattern has somewhere to put the row
    // without needing to grow the dictionary mid-edit. Adding a kind
    // here is a no-op when its list is empty — the lookup just always
    // returns false.
    public static readonly IReadOnlyList<Issue> Potions      = [];
    public static readonly IReadOnlyList<Issue> Events       = [];
    public static readonly IReadOnlyList<Issue> Encounters   = [];

    // BATTLEWORN_DUMMY_TIME_LIMIT_POWER is the only power tied to a
    // specific event (the BATTLEWORN_DUMMY training-dummy fight). Its
    // AfterTurnEnd hook isinst-casts CombatState.Encounter to
    // BattlewornDummyEventEncounter; outside that event the cast yields
    // null and the subsequent PlayerCombatState.MaxEnergy hook walk
    // dereferences a field the engine never sets in the SLIMES_NORMAL
    // path. Applying this power to the player in any other combat is
    // unreachable in normal play — the engine doesn't defend against
    // a scenario its event flow guarantees won't happen.
    public static readonly IReadOnlyList<Issue> Powers =
    [
        new("BATTLEWORN_DUMMY_TIME_LIMIT_POWER",
            "event-tied to BATTLEWORN_DUMMY; AfterTurnEnd hook NREs on "
            + "Hook.ModifyMaxEnergy when applied outside the BattlewornDummy "
            + "event context (PlayerCombatState.MaxEnergy walk hits an "
            + "uninitialised event-only field)"),
    ];

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

    // Lookup for the expected-refusal catalog. Returns the reason when
    // the card is on the CardExpectedRefusals list; null when it's not.
    // Sweep call sites prefix the row Detail with "expected-refusal:
    // <reason>" when this returns non-null so a reader can tell the row
    // apart from an un-investigated Unplayable.
    private static readonly Dictionary<string, string> s_cardExpectedRefusals =
        CardExpectedRefusals.ToDictionary(i => i.Id, i => i.Reason, StringComparer.Ordinal);

    public static bool TryGetExpectedRefusal(
        string kind, string id,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? reason)
    {
        if (string.Equals(kind, "card", StringComparison.Ordinal)
            && s_cardExpectedRefusals.TryGetValue(id, out var r))
        {
            reason = r;
            return true;
        }
        reason = null;
        return false;
    }

    public static IEnumerable<(string Kind, string Id, string Reason)> AllExpectedRefusals()
    {
        foreach (var (id, reason) in s_cardExpectedRefusals)
            yield return ("card", id, reason);
    }
}

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

    // Cards that remain Unplayable in the standard CardSweep fixture even
    // after the smoke fixture's resource boost (debug/set_energy 20 +
    // debug/gain_stars 20 — see CardSweep.ResourceBoost). The remaining
    // refusals fall into two empirically-verified buckets:
    //
    //   * TargetType=AnyAlly cards (bitflag=64) — `CardModel.CanPlay`
    //     refuses when `CombatState.PlayerCreatures.Where(IsAlive).Count() > 1`
    //     is false. `PlayerCreatures` filters on `IsPlayer` (only Player-typed
    //     creatures, not Pets / summons / Allies), so in single-player STS2 the
    //     count is structurally 1 and the gate never passes. These cards are
    //     designed for co-op multiplayer — the targeted ally must be a second
    //     human-controlled Player. Out of scope per documentation/requirements
    //     /01-initial-goals.md ("Single-player only"). Necrobinder + Osty does
    //     NOT satisfy the gate (Osty is a Pet, stored as Monster in
    //     CombatState._allies, not in PlayerCreatures).
    //
    //   * IsPlayable virtual override (bitflag=8) — the card's IsPlayable
    //     getter requires runtime state the smoke fixture doesn't stage. Only
    //     three cards in the engine override IsPlayable (Clash / GrandFinale /
    //     PactsEnd); the first two are smoke-playable through the default
    //     deck, only PACTS_END's Exhaust-pile predicate requires bespoke
    //     staging (the fixture starts combat with an empty Exhaust pile).
    //
    // Each entry cites the bitflag, the engine code path, and where the IL
    // evidence lives — so a reader can verify (or correct) the reason
    // without re-running the investigation.
    //
    // These rows surface as Unplayable in the sweep — not failures,
    // because Unplayable doesn't fail the test. The catalog entry annotates
    // the row Detail with "expected-refusal: <reason>" so a reader can
    // distinguish "fixture-staging gap we know about" from "fixture-staging
    // gap we haven't analysed yet." Lifecycle:
    //   * If the engine ships a single-player codepath for AnyAlly cards,
    //     all eight rows flip to Played → remove the AnyAlly entries.
    //   * If the fixture is extended to stage PACTS_END's Exhaust pile, that
    //     row flips to Played → remove the PACTS_END entry.
    public static readonly IReadOnlyList<Issue> CardExpectedRefusals =
    [
        // AnyAlly cards — co-op multiplayer-only by engine design. CanPlay
        // bitflag=64 because PlayerCreatures.Where(IsAlive).Count() > 1
        // never holds in single-player. IL: CardModel.CanPlay around lines
        // 53-76 (TargetType==6 branch in src/Sts2Headless.Runtime against
        // Models/CardModel.CanPlay disassembly).
        new("BELIEVE_IN_YOU", "TargetType=AnyAlly (co-op): OnPlay grants the targeted ally energy via PlayerCmd.GainEnergy; single-player has no second Player to target"),
        new("COORDINATE",     "TargetType=AnyAlly (co-op): OnPlay applies CoordinatePower to the targeted ally; single-player has no second Player to target"),
        new("DEMONIC_SHIELD", "TargetType=AnyAlly (co-op): OnPlay self-damages 14 then grants Block to the targeted ally via CreatureCmd.GainBlock; not a Demon-Form mechanic. Single-player has no second Player to target"),
        new("IGNITION",       "TargetType=AnyAlly (co-op): OnPlay channels a Plasma orb into the targeted ally's slots via OrbCmd.Channel — ally must be Defect. Single-player has no second Player to target"),
        new("INTERCEPT",      "TargetType=AnyAlly (co-op): OnPlay grants Block + 1 CoveredPower to the targeted ally; single-player has no second Player to target"),
        new("LARGESSE",       "TargetType=AnyAlly (co-op): OnPlay generates a random Colorless card into the targeted ally's hand via CardFactory.GetDistinctForCombat + CardPileCmd.AddGeneratedCardToCombat; single-player has no second Player to target"),
        new("LIFT",           "TargetType=AnyAlly (co-op): OnPlay grants Block to the targeted ally via CreatureCmd.GainBlock; single-player has no second Player to target"),
        new("MIMIC",          "TargetType=AnyAlly (co-op): OnPlay grants Block scaling with the ally's stats via CreatureCmd.GainBlock + CalculatedBlock.Calculate (despite the name, the IL contains no 'last card played' lookup); single-player has no second Player to target"),

        // IsPlayable virtual override — requires runtime state the fixture
        // doesn't stage. Only three cards in the engine override IsPlayable
        // (Clash / GrandFinale / PactsEnd). GrandFinale is satisfiable in
        // the default fixture (its predicate is "draw pile empty", which
        // holds briefly post-draw); the other two need bespoke staging.
        new("CLASH", "IsPlayable override: Clash.get_IsPlayable requires every card in hand to satisfy CardModel.IsAttack; CardSweep's deck mixes STRIKE (Attack) + DEFEND (Skill) so the all-Attack hand check fails. Localized text: \"Can only be played if every card in your hand is an Attack.\""),
        new("PACTS_END", "IsPlayable override: PactsEnd.get_IsPlayable requires CardPile.GetCards(Owner, PileType.ExhaustPile).Count() >= DynamicVars.Cards.IntValue (~3); CardSweep starts combat with empty Exhaust pile. Localized text: \"Can only be played if you have {Cards} or more cards in your Exhaust Pile.\""),
    ];

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

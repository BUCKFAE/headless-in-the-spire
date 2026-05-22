using Sts2Headless.MechanicSweep.Sweeps;

namespace Sts2Headless.MechanicSweep;

// Catalogue of which content kinds have a per-id MechanicSweep wired up
// and which kinds are still planned. Kept in lockstep with
// GenerateContentIdsCommand.Kinds by EveryKindHasASweepTest — adding a
// new kind to the manifest and forgetting to either implement a sweep
// or mark it Planned fails that test.
//
// The Implemented vs Planned split is deliberate:
//   * Implemented kinds map to a real sweep class — adding one is a
//     compile-time reference (typeof(SomeSweep)), so renaming or
//     deleting the class fails the build, not just a string check.
//   * Planned kinds are TODOs that are visible in code review. Each
//     carries a one-line note (the blocker / next step) so the
//     registry doubles as a punch list.
//
// Lifecycle: when a planned sweep lands, move its row from Planned to
// Implemented in the same commit that adds the sweep class.
public static class SweepRegistry
{
    // A sweep that exists today. Adding one: write the sweep class
    // (src/Sts2Headless.MechanicSweep/Sweeps/<Kind>Sweep.cs), add a
    // [Fact] wrapper under tests/Sts2Headless.MechanicSweepTests/, and
    // move the kind from PlannedSweeps to here.
    public sealed record Implemented(string Kind, Type SweepType);

    // A sweep we know we want but haven't built. Note is a short
    // human-readable reason (missing cheat, blocked feature, …).
    public sealed record Planned(string Kind, string Note);

    public static readonly IReadOnlyList<Implemented> ImplementedSweeps =
    [
        new("Card", typeof(CardSweep)),
    ];

    public static readonly IReadOnlyList<Planned> PlannedSweeps =
    [
        new("Affliction",  "needs debug/apply_affliction cheat (or attach via a card)"),
        new("Enchantment", "needs debug/apply_enchantment cheat (or attach via a card)"),
        new("Encounter",   "per-encounter smoke — the old EveryEncounterSmokeTests shape, ported into this matrix"),
        new("Event",       "needs debug/start_event cheat"),
        new("Modifier",    "needs run modifier plumb-through (BLOCKED.md)"),
        new("Monster",     "often covered via Encounter sweep; may not need a standalone sweep"),
        new("Orb",         "blocked on Defect character implementation (BLOCKED.md)"),
        new("Potion",      "needs debug/give_potion cheat"),
        new("Power",       "needs debug/apply_power cheat"),
        new("Relic",       "shape: give_relic + drive a fixed deck for a turn"),
    ];
}

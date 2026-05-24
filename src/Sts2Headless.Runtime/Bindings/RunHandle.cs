using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime.Bindings;

// Wire-facing types that the binding layer produces and the host layer
// consumes. Kept separate from Sts2Bindings so callers can import just the
// shape without pulling in the reflection internals.

// A "live run" is a triple: the Player aggregate, the RunState owned by the
// game, and the RunManager singleton instance that mutates them. Wire code
// treats it opaquely; the binding layer is the only thing that destructures.
public sealed record RunHandle(object Player, object RunState, object RunManager);

// Snapshot of the run for read-only wire surfacing. ExpandableRecord pattern:
// add fields as we bind more reads, never break existing JSON shape.
// CurrentRoomType is the Protocol enum, mapped from sts2's `room.GetType().Name`
// at the binding layer — unknown sts2 rooms come back as RoomType.Unknown.
// AvailableMapNodes is the list of legal next moves from the current map
// position; empty when the player isn't standing on the map.
// AvailableEventOptions are the current-page picks for an active Event;
// empty unless CurrentRoomType == EventRoom.
// AvailableRestSiteOptions mirrors that pattern for RestSiteRoom — empty
// unless the player is standing on a rest site, otherwise carries the
// engine's option list (HEAL/SMITH/…).
// AvailableMerchantItems mirrors that pattern for MerchantRoom — empty
// unless the player is standing on a merchant, otherwise carries the
// inventory roll-up (cards / relics / potions / card-removal).
// AvailableTreasureRelics mirrors that pattern for TreasureRoom — empty
// unless the player is standing on a chest, otherwise carries the chest's
// offering (typically one relic). Eagerly populated by driving
// TreasureRoom.DoNormalRewards on first read, so callers can decide
// whether to take or skip before invoking run/leave_treasure_room.
// CombatState is the combat read-out; null unless CurrentRoomType == CombatRoom.
// RewardsState carries the post-combat reward decisions when the engine has
// any unclaimed; null whenever the wire has nothing pending for the caller.
// Relics is the player's bag (starter relic + everything obtained mid-run),
// surfaced on every snapshot since relics are run-scoped state rather than
// room-scoped (unlike combatState / rewardsState).
// OwnedPotions is the player's potion belt — same run-scoped lifetime as
// relics. Empty slots are omitted (no nulls); positions in this list
// reflect the engine's slot order so run/use_potion can index into it.
// CurrentActIndex is the 0-based act number the run is in (0 = Act 1).
// Sourced from RunState.CurrentActIndex; bumped by RunManager.EnterNextAct
// after an act boss is defeated and rewards drained. Surfaced so callers
// can disambiguate "floor 17 of which act?" and gate stop conditions on
// reaching the final act.
//
// IsGameOver / IsVictory / IsDead form a tri-state for run termination —
// flat IsGameOver alone conflates "won" and "died" (both flip the engine
// flag). Callers wanting to assert victory should test IsVictory; callers
// reacting to death should test IsDead. IsGameOver is preserved as the
// either-or convenience (IsGameOver == IsVictory || IsDead) so existing
// stop conditions keep working through the transition.
public sealed record RunSnapshot(
    int CurrentHp,
    int MaxHp,
    int Gold,
    int DeckSize,
    RoomType CurrentRoomType,
    int ActFloor,
    int CurrentActIndex,
    bool IsGameOver,
    bool IsVictory,
    bool IsDead,
    IReadOnlyList<MapNode> AvailableMapNodes,
    IReadOnlyList<EventOption> AvailableEventOptions,
    IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    IReadOnlyList<MerchantItem> AvailableMerchantItems,
    IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    CombatState? CombatState,
    RewardsState? RewardsState,
    IReadOnlyList<Relic> Relics,
    IReadOnlyList<OwnedPotion> OwnedPotions,
    // Act-level boss preview. RunState.Act.BossEncounter.Id.Entry.
    // Null before the first act map is generated. Read once per
    // snapshot — no cost to populate.
    string? BossEncounterId = null,
    string? SecondBossEncounterId = null);

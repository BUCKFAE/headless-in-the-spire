using System.Text.Json.Serialization;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Cheats;

// Wire DTOs for the cheat surface — every method here is a test affordance
// gated behind --enable-debug (AD-7) and lives in its own assembly so the
// Sts2Headless.Agents project, which only references Protocol, cannot
// reach a cheat type even by `using` it accidentally.
//
// Naming follows Protocol.Methods convention: <Method>Params / <Method>Result,
// PascalCase records with explicit [JsonPropertyName] aliases so the wire
// shape is grep-able from this file alone.

// ── debug/give_relic ─────────────────────────────────────────────────────

// Grants a relic to the active player via the engine path (RelicCmd.Obtain,
// the same path RelicReward.OnSelectWrapper uses). Lives behind --enable-debug
// because regression tests use this to inject relics with observable on-event
// side effects (e.g. LuckyFysh's +15 gold on AfterCardChangedPiles) so the
// test can pin engine-pipeline behaviour that direct mutation would silently
// bypass.
public sealed record DebugGiveRelicParams(
    [property: JsonPropertyName("relicId")] string RelicId);

public sealed record DebugGiveRelicResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("relicId")] string RelicId,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("maxHp")] int MaxHp,
    [property: JsonPropertyName("gold")] int Gold,
    [property: JsonPropertyName("deckSize")] int DeckSize);

// ── debug/give_potion ────────────────────────────────────────────────────

// Grants a potion to the active player via the engine path
// (PotionCmd.TryToProcure(PotionModel, Player, slot=-1)). Same posture as
// debug/give_relic: routes through the real obtain pipeline so on-pickup
// hooks (BeforePotionProcured / AfterPotionProcured) fire, but bypasses
// the merchant gold cost. The slot landing follows the engine's
// own slot-pick rule (first empty); slotIndex in the result names the
// slot the potion ended up in.
public sealed record DebugGivePotionParams(
    [property: JsonPropertyName("potionId")] string PotionId);

public sealed record DebugGivePotionResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("potionId")] string PotionId,
    // Index of the PotionSlots entry the granted potion landed in (the
    // engine's TryToProcure picks the first empty slot when slot=-1).
    // -1 if procurement succeeded but the slot couldn't be located
    // post-hoc (shouldn't happen in practice but the wire is honest).
    [property: JsonPropertyName("slotIndex")] int SlotIndex,
    // Total count of non-null entries in PotionSlots after the procure
    // — lets callers verify "the bag grew by 1" without re-reading state.
    [property: JsonPropertyName("potionCount")] int PotionCount);

// ── debug/set_hp ─────────────────────────────────────────────────────────

// Writes the player's current HP (and optionally Max HP) directly into the
// engine's backing fields. **Bypasses** the damage event path, on-hit relic
// listeners (Burning Blood's heal, etc.), the death pipeline, and any other
// side effect a "real" HP change would trigger.
//
// Validation:
//   * hp >= 0
//   * maxHp, when provided, >= 1
//   * hp <= maxHp (the resulting maxHp — either the provided value or the
//     current one if maxHp was omitted)
// Validation failures return WireErrorCode.InvalidParams (-32602).
//
// Setting hp to 0 does NOT trigger game-over by itself — the engine's
// IsGameOver flag is set elsewhere on the death pipeline. Callers wanting
// to test "what does run/state look like at zero HP" can use this; callers
// wanting to test the death transition itself need to drive damage events
// through combat.
public sealed record DebugSetHpParams(
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("maxHp")] int? MaxHp = null);

public sealed record DebugSetHpResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("maxHp")] int MaxHp,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver);

// ── debug/set_energy ─────────────────────────────────────────────────────

// Writes the player's current Energy (and optionally MaxEnergy) directly
// into the engine's PlayerCombatState / Player. Bypasses the normal
// EnergyChanged event chain on backing-field writes but uses the property
// setter so listeners that subscribe through CombatHistory still observe
// the change — same posture as debug/set_hp.
//
// Validation:
//   * At least one of `energy` / `maxEnergy` must be provided.
//   * Each value, when provided, >= 0.
//   * `maxEnergy` >= 1 (an energy cap of 0 leaves the player permanently
//     locked out and is almost certainly a typo).
//
// Use case: let MechanicSweep / regression tests stage a card whose
// `EnergyCost` exceeds the character's default 3-energy budget (BURY,
// METEOR_STRIKE, BANSHEES_CRY) without driving multi-combat AfterCardPlayed
// hooks just to bump max energy.
public sealed record DebugSetEnergyParams(
    [property: JsonPropertyName("energy")] int? Energy = null,
    [property: JsonPropertyName("maxEnergy")] int? MaxEnergy = null);

public sealed record DebugSetEnergyResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("energy")] int Energy,
    [property: JsonPropertyName("maxEnergy")] int MaxEnergy);

// ── debug/gain_stars ─────────────────────────────────────────────────────

// Grants N Stars (Regent's resource) by writing PlayerCombatState.Stars via
// its public setter. The setter fires StarsChanged through CombatHistory so
// listeners (relics like GalacticDust / MiniRegent that observe
// AfterStarsSpent for refunds, BeforeCardPlayed listeners on Stars-related
// powers) still see the change — same engine path PlayerCmd.GainStars takes
// modulo the relic-listener chain that GainStars walks before writing.
//
// Validation:
//   * `amount` >= 0 (granting negative stars would silently bypass
//     UnplayableReason.NotEnoughStars and is almost certainly a typo —
//     callers wanting to test spend should drive a real card play).
//
// Use case: let MechanicSweep stage Regent's Star budget so high-cost Stars
// cards (COMET, SEVEN_STARS, NEUTRON_AEGIS, DEVASTATE, DECISIONS_DECISIONS,
// ROYAL_GAMBLE, THE_SMITH) can exercise their OnPlay without waiting on the
// DivineRight one-shot relic + multi-turn Stars accrual the engine
// otherwise requires.
public sealed record DebugGainStarsParams(
    [property: JsonPropertyName("amount")] int Amount);

public sealed record DebugGainStarsResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("stars")] int Stars);

// ── debug/replace_deck ───────────────────────────────────────────────────

// A card the caller wants placed into the player's deck. `cardId` is the
// wire string id (matches the CardId enum's wire form, e.g. "POMMEL_STRIKE");
// `upgradeLevel` is 0 for the base card, 1 for the first upgrade, etc.
// Engine-side this routes through CardModel.UpgradeInternal +
// FinalizeUpgradeInternal applied upgradeLevel times.
public sealed record CardSpec(
    [property: JsonPropertyName("cardId")] string CardId,
    [property: JsonPropertyName("upgradeLevel")] int UpgradeLevel = 0);

// Replace the player's deck with a curated list. Hard write — every
// existing card is untracked from RunState, the deck is cleared, and
// the new cards are added through RunState.CreateCard + Deck.AddInternal.
// Bypasses on-deck-change listeners (matches the spirit of debug/set_hp
// and debug/give_relic, which also write through events).
//
// Use case: pin a combat to a specific opening hand so a test can assert
// behavior (e.g. force a deterministic infinite combo) without relying on
// the full game-progression path to produce a particular deck.
public sealed record DebugReplaceDeckParams(
    [property: JsonPropertyName("cards")] IReadOnlyList<CardSpec> Cards);

public sealed record DebugReplaceDeckResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("deckSize")] int DeckSize,
    [property: JsonPropertyName("cardIds")] IReadOnlyList<string> CardIds);

// ── debug/read_deck ──────────────────────────────────────────────────────

// Read every card in the player's deck as (cardId, upgradeLevel) pairs.
// Mirrors `debug/replace_deck` shape so tests can round-trip:
// `replace_deck([...])` → … → `read_deck()` and compare. Order is the
// deck's insertion order (the engine's Deck.Cards list).
//
// Use case: pinning the effect of an upgrade — e.g. asserting that after
// `run/select_rest_site_option(SMITH, cardSelectIndices: [[0]])` a card
// in the deck has upgradeLevel > 0. The core wire surface deliberately
// keeps the deck out of `RunStateResult` (it's a coverage-heavy field
// agents rarely consume); this debug method gives tests the inspection
// hook without putting the same data on every snapshot.
public sealed record DebugReadDeckParams();

public sealed record DebugReadDeckResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("deckSize")] int DeckSize,
    [property: JsonPropertyName("cards")] IReadOnlyList<CardSpec> Cards);

// ── debug/start_combat ───────────────────────────────────────────────────

// Force-start a specific combat against the chosen encounter, bypassing
// map progression. Mirrors the sts2-cli "/enter_room combat ..." path:
// resolves the EncounterModel via ModelDb.GetById, mutates it for the run,
// constructs CombatRoom(encounter, runState), and drives
// RunManager.Instance.EnterRoom to flip the room. Bypasses the natural
// map-selection event chain — listeners that react to "player entered a
// monster node from the map" will not fire.
//
// Use case: the EveryEncounterSmokeTests sweep places the Ironclad with a
// known deck in front of every encounter id sts2 ships, verifying that
// nothing in the combat surface (cards, monster intents, powers, scenes)
// crashes with MissingMethodException / MissingFieldException — the same
// shape as the treasure-room chest-open bug. Losses are expected for
// some encounters; the test signal is "no crash."
//
// `encounterId` is the wire string id (matches EncounterId enum's wire
// form, e.g. "SLIMES_NORMAL"). Unknown ids return InvalidParams.
public sealed record DebugStartCombatParams(
    [property: JsonPropertyName("encounterId")] string EncounterId);

public sealed record DebugStartCombatResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("encounterId")] string EncounterId,
    [property: JsonPropertyName("inProgress")] bool InProgress,
    [property: JsonPropertyName("enemyCount")] int EnemyCount);

// ── debug/afflict_card ───────────────────────────────────────────────────

// Apply an affliction to a card in the player's hand via the engine
// path (CardCmd.Afflict(AfflictionModel, CardModel, Decimal)). Same
// engine path cards / events use to apply afflictions naturally.
//
// Combat is required — afflictions attach to cards in hand, which only
// exist mid-combat. Call debug/start_combat first.
//
// The target card is identified by handIndex (the position in
// CombatState.Hand). amount defaults to 1 (matches the common per-
// affliction stack count).
public sealed record DebugAfflictCardParams(
    [property: JsonPropertyName("afflictionId")] string AfflictionId,
    [property: JsonPropertyName("handIndex")] int HandIndex = 0,
    [property: JsonPropertyName("amount")] int Amount = 1);

public sealed record DebugAfflictCardResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("afflictionId")] string AfflictionId,
    [property: JsonPropertyName("handIndex")] int HandIndex,
    // Wire id of the card that was afflicted (the one at HandIndex
    // pre-apply). Lets callers verify the right card got hit without a
    // second state read.
    [property: JsonPropertyName("cardId")] string CardId);

// ── debug/enchant_card ───────────────────────────────────────────────────

// Apply an enchantment to a card in the player's hand via the engine
// path (CardCmd.Enchant(EnchantmentModel, CardModel, Decimal)). Same
// shape as debug/afflict_card.
public sealed record DebugEnchantCardParams(
    [property: JsonPropertyName("enchantmentId")] string EnchantmentId,
    [property: JsonPropertyName("handIndex")] int HandIndex = 0,
    [property: JsonPropertyName("amount")] int Amount = 1);

public sealed record DebugEnchantCardResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("enchantmentId")] string EnchantmentId,
    [property: JsonPropertyName("handIndex")] int HandIndex,
    [property: JsonPropertyName("cardId")] string CardId);

// ── debug/apply_power ────────────────────────────────────────────────────

// Apply a power to a creature via the engine path
// (PowerCmd.Apply(PowerModel, target, amount, source, cardSource: null,
// useFinalAmount: false)). Same posture as debug/give_relic: routes
// through the real apply pipeline so Before/AfterPowerAmountChanged +
// any per-power on-apply hooks fire, but bypasses the "must come from a
// card" expectation.
//
// Combat is required — most powers only live for a single combat. Call
// debug/start_combat first; passing a power that's only meaningful
// outside combat will still apply but won't do much.
//
// Target:
//   * enemyIndex == null → apply to the player
//   * enemyIndex >= 0    → apply to enemies[enemyIndex] (alive only)
public sealed record DebugApplyPowerParams(
    [property: JsonPropertyName("powerId")] string PowerId,
    [property: JsonPropertyName("amount")] int Amount = 1,
    [property: JsonPropertyName("enemyIndex")] int? EnemyIndex = null);

public sealed record DebugApplyPowerResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("powerId")] string PowerId,
    // Resulting amount of the power on the target creature after Apply.
    // For stacking powers (Strength, Vulnerable) this grows with each
    // Apply call; for non-stacking (most "duration" buffs/debuffs) it
    // sets the duration.
    [property: JsonPropertyName("appliedAmount")] int AppliedAmount,
    // Where the power landed — "Player" for the player target, "Enemy:<index>"
    // for an enemy target. Lets callers verify the target resolution
    // without a follow-up state read.
    [property: JsonPropertyName("targetDescription")] string TargetDescription);

// ── debug/start_event ────────────────────────────────────────────────────

// Force-start a specific event against the active run, bypassing map
// progression. Mirrors debug/start_combat: resolves the EventModel via
// ModelDb.GetById<EventModel>(new ModelId("EVENT", id)), constructs a
// per-run mutable copy if the model exposes ToMutable, builds
// EventRoom(eventModel), and drives RunManager.EnterRoom. Listeners
// gated on "player entered the room via the map" will not fire.
//
// `eventId` is the wire string id (matches EventId enum's wire form,
// e.g. "MIND_BLOOM"). Unknown ids return InvalidParams. The wire
// result reports the post-EnterRoom room type and the count of options
// currently available — callers (sweeps, tests) can branch on those
// without paying a second run/state round-trip.
public sealed record DebugStartEventParams(
    [property: JsonPropertyName("eventId")] string EventId);

public sealed record DebugStartEventResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("eventId")] string EventId,
    // Room type after EnterRoom landed. Usually EventRoom; if the event
    // resolved immediately (single-option "you walk past it" shapes), it
    // may already be MapRoom by the time we read state.
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    // Number of options on the current event page (one per
    // AvailableEventOptions entry). 0 means the event is finished or
    // has nothing pickable; the caller can decide whether to proceed.
    [property: JsonPropertyName("optionsCount")] int OptionsCount);

// ── debug/kill_all_enemies ───────────────────────────────────────────────

// Drops every alive enemy in the current combat to 0 HP by writing the
// engine's Creature._currentHp backing field (Enemy : Creature in sts2's
// hierarchy). Bypasses the damage-event pipeline and on-kill listeners —
// matches the spirit of debug/set_hp, which writes the same backing field
// on the player. Use case: forcing function for end-to-end full-game
// playthroughs where the agent isn't strong enough to clear every combat
// honestly but the test needs to exercise the post-combat path (rewards,
// map progression, Neow/event choices in the replay).
//
// Side effects in the engine:
//   * Each enemy whose HP we zero is then ignored by the engine's "alive
//     enemies" predicate, so CombatManager flips IsInProgress=false on the
//     next tick.
//   * After the writes, the helper drains the action executor and runs
//     AutoAdvancePostCombat so rewards generate through the normal path
//     (same surface UsePotion / PlayCard land on). The caller's next
//     `run/state` will see the post-combat room or pending rewards.
//
// No params — the cheat operates on whatever combat is currently in
// progress on the active run. Calling outside combat is a no-op that
// returns killed=0, combatEnded=false (not an error: tests routinely fire
// this on every state observation and we don't want spurious failures).
public sealed record DebugKillAllEnemiesParams();

public sealed record DebugKillAllEnemiesResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("killed")] int Killed,
    [property: JsonPropertyName("combatEnded")] bool CombatEnded);

// ── debug/reveal_act_schedule ────────────────────────────────────────────

// Pre-rolled schedule for the current act — what the engine generated at
// ActModel.GenerateRooms (`Rng.NextItem` against each pool). Reveals
// information that's seed-deterministic but normally hidden from the
// player:
//   - BossId / SecondBossId / AncientId: the specific encounter/ancient
//     this run will face on the boss tile / at Neow.
//   - NormalEncounterIds / EliteEncounterIds / EventIds: the ordered
//     schedule the engine dequeues from on each node entry. Combined
//     with NormalEncountersVisited / EliteEncountersVisited / EventsVisited
//     counters, the caller can predict "the next normal encounter I
//     pick will be X".
//
// Notes:
//   - Monster slot HP / specifics are NOT pre-rolled — those happen in
//     EncounterModel.GenerateMonstersWithSlots at combat-start.
//   - Card rewards and event outcomes are rolled lazily on emission /
//     choice and aren't part of this schedule.
//
// Requires an active run (call run/new first). Returns ok=false with
// empty lists when no schedule is currently bound (e.g. immediately
// before the first GenerateRooms call).
public sealed record DebugRevealActScheduleParams();

public sealed record DebugRevealActScheduleResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("actIndex")] int ActIndex,
    [property: JsonPropertyName("bossId")] string? BossId,
    [property: JsonPropertyName("secondBossId")] string? SecondBossId,
    [property: JsonPropertyName("ancientId")] string? AncientId,
    [property: JsonPropertyName("normalEncounterIds")] IReadOnlyList<string> NormalEncounterIds,
    [property: JsonPropertyName("eliteEncounterIds")] IReadOnlyList<string> EliteEncounterIds,
    [property: JsonPropertyName("eventIds")] IReadOnlyList<string> EventIds,
    // How many entries of each list the engine has already consumed.
    // Combined with the lists above, this gives the caller a "what
    // comes next" answer without further calls.
    [property: JsonPropertyName("normalEncountersVisited")] int NormalEncountersVisited,
    [property: JsonPropertyName("eliteEncountersVisited")] int EliteEncountersVisited,
    [property: JsonPropertyName("eventsVisited")] int EventsVisited);

// ── debug/reveal_map_layout ──────────────────────────────────────────────

// Reveal the *entire* pre-rolled map layout for the current act, with
// the engine's resolved PointType for every node — including those the
// player still sees as `?` on the in-game map. The natural
// `availableMapNodes` field on RunStateResult only exposes the *current
// row's reachable children* and leaves Unknown nodes as Unknown; this
// debug method walks every point in every row of `RunState.Act.Map` via
// the engine's `ActMap.GetAllMapPoints()` enumerator and emits each node
// along with its outgoing edges into the next row.
//
// Caveat — Unknown nodes:
//   The engine assigns Type=Unknown at generation time and lazily rolls
//   the actual room type on first entry via
//   `RunManager.RollRoomTypeFor → UnknownMapPointOdds.Roll(history,
//   runState)`. That roll depends on the visit-history context, so the
//   resolved outcome is *not* knowable at layout-reveal time. Callers
//   reading `Type == Unknown` should treat it as "engine hasn't picked
//   yet"; the prior odds live in `UnknownMapPointOdds` and aren't
//   currently surfaced through this method (a future enhancement could
//   emit them alongside).
//
// Same posture as `debug/reveal_act_schedule`: gated by --enable-debug
// because it leaks seed-deterministic information the player can't see
// (rows beyond their current position, plus the resolved type of any
// pre-assigned Treasure / Merchant / RestSite nodes).
public sealed record MapNodeEdge(
    [property: JsonPropertyName("col")] int Col,
    [property: JsonPropertyName("row")] int Row);

public sealed record DebugRevealMapPoint(
    [property: JsonPropertyName("col")] int Col,
    [property: JsonPropertyName("row")] int Row,
    [property: JsonPropertyName("type")] MapNodeType Type,
    // Outgoing edges from this point to the next row. Empty for boss /
    // terminal nodes. Used by clients to render or walk the map without
    // a second GetPoint round-trip per node.
    [property: JsonPropertyName("children")] IReadOnlyList<MapNodeEdge> Children);

public sealed record DebugRevealMapLayoutParams();

public sealed record DebugRevealMapLayoutResult(
    [property: JsonPropertyName("ok")] bool Ok,
    // -1 when no map is bound (pre-EnterAct / between acts). In that case
    // Points is empty so the wire stays shape-stable.
    [property: JsonPropertyName("actIndex")] int ActIndex,
    [property: JsonPropertyName("points")] IReadOnlyList<DebugRevealMapPoint> Points);

// ── debug/peek_card_reward ───────────────────────────────────────────────

// Simulation-based peek at what card-reward triplet a victory in the
// specified encounter would offer. SCOPE NOTE: full simulation requires a
// SerializableRunState clone/restore round-trip; that infrastructure isn't
// wired in this slice. The peek currently returns the candidate POOL
// (CardCreationOptions.ForRoom(player, CombatRoom).GetPossibleCards) — the
// engine's filtered candidate set that real rewards sample from. This
// over-shares relative to a real triplet roll (callers see every legal
// card, not the 3 the engine would pick), but is fully read-only — no
// engine state is mutated. `encounterId` is informational only; when
// omitted, the host fills it from the schedule's next-pending normal
// encounter (when available). Notes carries the fidelity disclosure so
// callers can branch on shape.
public sealed record DebugPeekCardRewardParams(
    [property: JsonPropertyName("encounterId")] string? EncounterId = null);

public sealed record DebugPeekCardEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("cost")] int Cost,
    [property: JsonPropertyName("rarity")] string Rarity);

public sealed record DebugPeekCardRewardResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("encounterId")] string EncounterId,
    [property: JsonPropertyName("cards")] IReadOnlyList<DebugPeekCardEntry> Cards,
    // Fidelity disclosure: documents whether the result is the full
    // simulation, the pool fallback, or a hard soft-fail. Callers MUST
    // read this to know what they're getting.
    [property: JsonPropertyName("notes")] string Notes);

// ── debug/peek_event_outcome ─────────────────────────────────────────────

// Simulation-based peek at the side effects of picking option N on event
// `eventId`. SCOPE NOTE: full outcome simulation requires the same
// SerializableRunState clone/restore round-trip as peek_card_reward.
// Until that lands, this method only confirms the event id resolves and
// (best-effort) reports the canonical option count, leaving all deltas
// zero and diff lists empty. `notes` carries the fidelity disclosure.
public sealed record DebugPeekEventOutcomeParams(
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("optionIndex")] int OptionIndex);

public sealed record DebugPeekEventOutcomeResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("optionIndex")] int OptionIndex,
    [property: JsonPropertyName("hpDelta")] int HpDelta,
    [property: JsonPropertyName("goldDelta")] int GoldDelta,
    [property: JsonPropertyName("relicsGained")] IReadOnlyList<string> RelicsGained,
    [property: JsonPropertyName("relicsLost")] IReadOnlyList<string> RelicsLost,
    [property: JsonPropertyName("cardsAdded")] IReadOnlyList<string> CardsAdded,
    [property: JsonPropertyName("cardsRemoved")] IReadOnlyList<string> CardsRemoved,
    [property: JsonPropertyName("notes")] string Notes);


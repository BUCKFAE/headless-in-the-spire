using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

// Per-method request/response DTOs. The Envelope layer (Request/Response)
// carries these as JsonNode payloads; both the host's method handlers and
// the integration tests (de)serialise through these records so a renamed
// field is a compile error on both sides, not a silently-passing test.
//
// Naming: <Method>Params for inputs (omit if no params), <Method>Result for
// outputs. Property names map to JSON via [JsonPropertyName] — explicit on
// every field so the wire shape is grep-able from this file alone.
//
// All response shapes carry `ok` as a redundant boolean — clients should
// branch on the envelope's `error` field, but `ok: true` is a useful sanity
// hint in logs and matches the pattern sts2-cli established.

// ── Enums ────────────────────────────────────────────────────────────────

// Playable characters. Wire shape is lowercase ("ironclad", "silent", ...)
// to match sts2-cli convention; on the .NET side the standard PascalCase
// enum names are used.
//
// STS2 ships with five characters: the three returning ones (Ironclad,
// Silent, Defect) and the two new ones (Regent, Necrobinder). Watcher is
// an STS1-only character and is deliberately NOT in this enum — the
// sts2.dll has no corresponding type, and the bindings layer will not
// resolve. See documentation/sts2-game-facts.md.
//
// Every value here must be wired in CharacterExtensions.Sts2TypeName (the
// switch expression is no-default, so adding a value breaks compile until
// the binding is named) AND must resolve to a real type in sts2.dll at
// bootstrap time (Sts2Bindings.Bind throws if a character is missing).
[JsonConverter(typeof(JsonStringEnumConverter<Character>))]
public enum Character
{
    [JsonStringEnumMemberName("ironclad")] Ironclad,
    [JsonStringEnumMemberName("silent")] Silent,
    [JsonStringEnumMemberName("defect")] Defect,
    [JsonStringEnumMemberName("regent")] Regent,
    [JsonStringEnumMemberName("necrobinder")] Necrobinder,
}

// Room types we've seen the engine land at via the StartRun chain. Wire
// shape matches the sts2 type name exactly (PascalCase like "MapRoom"),
// which is what `room.GetType().Name` returns server-side. Enum values
// missing from this list arrive as `Unknown` so the wire never carries
// a free-form string for a type we haven't catalogued — that would be
// the spelling-mistake trap this enum exists to prevent.
//
// BossRoom is a wire-level synthetic — sts2 itself has no BossRoom type
// (the act boss is a regular CombatRoom whose monster is the act boss).
// The host flips CombatRoom → BossRoom in BuildSnapshot when the player
// stands on a Boss MapPoint, so callers using `currentRoomType == BossRoom`
// as a stop signal can distinguish the boss fight from a regular combat.
[JsonConverter(typeof(JsonStringEnumConverter<RoomType>))]
public enum RoomType
{
    Unknown,
    MapRoom,
    EventRoom,
    CombatRoom,
    RestSiteRoom,
    MerchantRoom,
    TreasureRoom,
    BossRoom,
}

// Map-node types we've seen sts2's MapPoint.PointType report. Distinct from
// RoomType: this is the *kind* of node painted on the act map (Monster,
// Elite, …), whereas RoomType is the runtime room you land in after picking
// the node (Elite resolves to CombatRoom, etc.). Grow the enum as integration
// tests surface new PointType names rather than widening the parser.
//
// Unknown is the in-game "?" marker — sts2's PointType.Unknown literally
// names the mystery node whose destination room is rolled on entry (can
// resolve to EventRoom, CombatRoom, …). Don't read Unknown as "parser
// fallback": it's a real, intentional map-node type. If we ever need a
// separate sentinel for unmapped PointType values, add `Unmapped` and update
// the fallback in Sts2Bindings.ToMapNode().
//
// Observed so far: Monster (row > 0 from start), Unknown (row 2+ mystery
// rooms), Boss (top row of the act, validated against seed 42's row=16
// pre-boss MapRoom whose only child is the boss node). Elite/Event/RestSite/
// Treasure/Merchant are still speculative — left in to document the schema
// callers can expect, validated when the corresponding nodes actually appear.
[JsonConverter(typeof(JsonStringEnumConverter<MapNodeType>))]
public enum MapNodeType
{
    Unknown,
    Monster,
    Elite,
    Event,
    RestSite,
    Treasure,
    Merchant,
    Boss,
}

// One legal next move from the current map position. Col/row feed straight
// back into run/select_map_node; Type lets clients distinguish "definitely
// combat" from "definitely shop" without booting the room first.
public sealed record MapNode(
    [property: JsonPropertyName("col")] int Col,
    [property: JsonPropertyName("row")] int Row,
    [property: JsonPropertyName("type")] MapNodeType Type);

// Card targeting modes. Mirrors sts2's TargetType enum at the wire layer; the
// binding maps `card.TargetType.ToString()` into this enum with the Unknown
// fallback discipline used elsewhere. AnyEnemy is the only mode that requires
// a caller-supplied targetIndex on run/play_card — for the others the engine
// resolves targets internally and the wire `targetIndex` is ignored.
[JsonConverter(typeof(JsonStringEnumConverter<TargetType>))]
public enum TargetType
{
    Unknown,
    None,
    AnyEnemy,
    AllEnemies,
    Self,
    AnyAlly,
    AllAllies,
    Caster,
}

// Intent shapes we've actually seen on monster NextMove.Intents. The wire
// enum is shallower than sts2's IntentType (which mixes "Attack" and
// "AttackDefend" and similar combined kinds); we surface the primary kind and
// let the per-intent damage/block fields carry the numbers. Same Unknown
// fallback discipline as RoomType — grow rather than widen on surprise.
[JsonConverter(typeof(JsonStringEnumConverter<IntentKind>))]
public enum IntentKind
{
    Unknown,
    Attack,
    Defend,
    Buff,
    Debuff,
    Sleep,
    Stun,
    Escape,
    Magic,
    AttackDefend,
    AttackBuff,
    AttackDebuff,
    StrongDebuff,
}

// One option offered by an Event the player is currently standing on.
//
// Index is the position in the current page's option list — pass it back via
// run/select_event_option. We don't surface a stable id because sts2 itself
// doesn't expose one beyond the loc text-key, and the index alone is what
// EventOption.Chosen() dispatches on.
//
// TextKey is the loc string the game uses to look up the option's title
// (e.g. "NEOW.pages.INITIAL.options.STONE_HUMIDIFIER"). It's the closest
// thing to a stable identifier and lets callers branch on "the relic option"
// without booking knowledge of the index ordering. Null when the option has
// no loc binding (a few procedurally-generated options).
//
// IsLocked mirrors the in-game "you can't pick this yet" flag — surfaced so
// callers can grey-out rather than guess. The host does not block locked
// picks server-side; if a caller still selects one, sts2's Chosen() will
// no-op and the room will stay put.
public sealed record EventOption(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("textKey")] string? TextKey,
    [property: JsonPropertyName("isLocked")] bool IsLocked);

// One status effect / buff / debuff on a creature. Id is the game's stable
// power key (e.g. "STRENGTH", "VULNERABLE"); Amount is the stack count. We
// don't surface a localized name — clients translate using the id.
public sealed record Power(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("amount")] int Amount);

// One choice offered by a rest site. OptionId is the engine's stable
// identifier ("HEAL", "SMITH", "DIG", …); IsEnabled mirrors the engine's
// per-option availability (HEAL is disabled at full HP, SMITH at empty deck,
// etc.). Clients should branch on OptionId for icon/label, on IsEnabled for
// clickability. Unknown OptionId strings are passed through verbatim — we
// don't enum the option space yet because new relics can introduce them
// mid-game (Lantern, Toolbox, …).
public sealed record RestSiteOption(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("optionId")] string OptionId,
    [property: JsonPropertyName("isEnabled")] bool IsEnabled);

// Kind of item offered by a merchant room. Wire shape matches the entry's
// engine type stripped of the "Merchant"/"Entry" prefix/suffix: MerchantCardEntry
// → "card", MerchantRelicEntry → "relic", MerchantPotionEntry → "potion",
// MerchantCardRemovalEntry → "card_removal". Same Unknown-fallback discipline
// as RoomType — an unrecognised entry type still surfaces (so the wire never
// silently hides a sold item) but with an opaque kind clients can't act on.
[JsonConverter(typeof(JsonStringEnumConverter<MerchantKind>))]
public enum MerchantKind
{
    [JsonStringEnumMemberName("unknown")] Unknown,
    [JsonStringEnumMemberName("card")] Card,
    [JsonStringEnumMemberName("relic")] Relic,
    [JsonStringEnumMemberName("potion")] Potion,
    [JsonStringEnumMemberName("card_removal")] CardRemoval,
}

// One item on offer at the current merchant. Index is the position in the
// inventory's AllEntries roll-up (the engine's stable iteration order:
// CharacterCards, ColorlessCards, Relics, Potions, CardRemoval); pass back
// via run/buy_merchant_item.itemIndex.
//
// Cost is gold (already after sale modifiers — IsOnSale just describes the
// price tag, not a separate discount the caller has to apply). IsStocked
// false means the slot was sold this visit; IsAffordable mirrors the engine's
// EnoughGold flag so callers don't recompute Player.Gold > Cost themselves.
//
// Kind-specific id fields are nullable — only the matching kind populates
// its slot. CardRemoval has no item id (it's a service, not a thing).
public sealed record MerchantItem(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("kind")] MerchantKind Kind,
    [property: JsonPropertyName("cost")] int Cost,
    [property: JsonPropertyName("isStocked")] bool IsStocked,
    [property: JsonPropertyName("isAffordable")] bool IsAffordable,
    [property: JsonPropertyName("cardId")] string? CardId = null,
    [property: JsonPropertyName("relicId")] string? RelicId = null,
    [property: JsonPropertyName("potionId")] string? PotionId = null);

// One relic carried by the player. Id is the game's stable relic key
// (e.g. "BURNING_BLOOD"); clients translate to a localised name themselves.
// Per-relic runtime state (DynamicVars: counters, charges, etc.) isn't
// surfaced yet — adding fields here is non-breaking and should be driven
// by a concrete caller need rather than mirroring the engine's full shape.
public sealed record Relic(
    [property: JsonPropertyName("id")] string Id);

// One relic the current treasure-room chest is offering. RelicId is the
// engine's canonical wire form (e.g. "GORGET"); empty list when the
// player isn't in a treasure room or the chest has no offering. The
// snapshot eagerly populates the offering on first read by driving
// TreasureRoom.DoNormalRewards reflectively, so callers see the actual
// relic before deciding whether to take or skip via
// run/leave_treasure_room.
public sealed record TreasureRelic(
    [property: JsonPropertyName("relicId")] string RelicId);

// One potion in a player's belt slot. Index is the slot position (pass
// back via run/use_potion.potionIndex); empty slots are omitted entirely
// rather than surfaced as nulls. Id is the engine's canonical wire id
// (e.g. "BLOCK_POTION", "ENERGY_POTION") — the same SCREAMING_SNAKE_CASE
// form cards/relics use, and the same form catalogued in
// PotionIdNames.AllWireNames. TargetType drives whether targetIndex is
// required on use (AnyEnemy → required; Self / None → ignored). CanUse
// reflects sts2's PassesCustomUsabilityCheck — most potions are always
// usable in combat, but a handful (FoulPotion etc.) gate themselves by
// run state.
public sealed record OwnedPotion(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("targetType")] TargetType TargetType,
    [property: JsonPropertyName("canUse")] bool CanUse);

// One element of an enemy's NextMove. Damage is per-hit (multiply by Hits for
// total); Block is the amount the enemy will gain. Kind is sts2's primary
// IntentType bucket — combined kinds (AttackDefend, AttackBuff, …) keep their
// composite name so callers can branch on the precise shape they see.
public sealed record Intent(
    [property: JsonPropertyName("kind")] IntentKind Kind,
    [property: JsonPropertyName("damage")] int? Damage,
    [property: JsonPropertyName("hits")] int? Hits,
    [property: JsonPropertyName("block")] int? Block);

// One card in the player's hand. Index is the array position (pass back via
// run/play_card.cardIndex); Id is the card's stable id string ("STRIKE_RED");
// Cost is the energy cost after combat modifiers (-1 means unplayable, the
// sts2 convention for X-cost or perma-disabled cards). TargetType drives
// whether targetIndex is required on play. Upgraded mirrors the engine's
// CardModel.IsUpgraded — true when the card is at the max upgrade level for
// its class; planners (BattleAgent / IroncladCardCatalog) branch on this to
// pick the upgraded stat row (Strike+1 = 9 dmg, Defend+1 = 8 block, …).
public sealed record Card(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("id")] CardId Id,
    [property: JsonPropertyName("cost")] int Cost,
    [property: JsonPropertyName("canPlay")] bool CanPlay,
    [property: JsonPropertyName("targetType")] TargetType TargetType,
    [property: JsonPropertyName("upgraded")] bool Upgraded);

// One enemy in the current combat. Index is the position in the alive-enemy
// list (pass back via run/play_card.targetIndex when the card's TargetType is
// AnyEnemy); MonsterId is the game's stable monster id ("LOUSE_RED" etc.) and
// is the closest thing to a stable name. IntendsAttack is the convenience
// summary "is this enemy about to attack me?" — Intents carries the
// per-intent detail.
public sealed record Enemy(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("monsterId")] string? MonsterId,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("maxHp")] int MaxHp,
    [property: JsonPropertyName("block")] int Block,
    [property: JsonPropertyName("intendsAttack")] bool IntendsAttack,
    [property: JsonPropertyName("intents")] IReadOnlyList<Intent> Intents,
    [property: JsonPropertyName("powers")] IReadOnlyList<Power> Powers);

// Kind of reward offered by the engine after a combat resolves. Wire shape
// matches the sts2 type name (CardReward → "card", GoldReward → "gold", …);
// keeping the lowercase wire form keeps the JSON readable. Same Unknown-
// fallback discipline as RoomType — an unrecognised reward stays selectable
// (the engine still resolves it via OnSelectWrapper) but its kind is opaque
// to clients until we add it here.
[JsonConverter(typeof(JsonStringEnumConverter<RewardKind>))]
public enum RewardKind
{
    [JsonStringEnumMemberName("unknown")] Unknown,
    [JsonStringEnumMemberName("card")] Card,
    [JsonStringEnumMemberName("gold")] Gold,
    [JsonStringEnumMemberName("relic")] Relic,
    [JsonStringEnumMemberName("potion")] Potion,
}

// One card option inside a CardReward. Index is the position in the reward's
// card list; pass back via run/select_reward.cardIndex when claiming a Card-
// kind reward. Id/Cost mirror the in-hand Card record so a single client
// helper can render either.
public sealed record CardRewardOption(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("id")] CardId Id,
    [property: JsonPropertyName("cost")] int Cost);

// One reward in the post-combat reward set. Index is the position in the
// pending list; pass back via run/select_reward.rewardIndex (and
// run/skip_reward when CanSkip is true). Kind-specific fields are nullable —
// only the matching kind populates its slot:
//   - Card  → Cards (one inner pick required, see CanSkip for skippability)
//   - Gold  → GoldAmount
//   - Potion → PotionId (sts2's stable potion id)
//   - Relic → RelicId  (sts2's stable relic id)
// Unknown-kind rewards still surface so a client can claim them blind via
// select_reward; the engine resolves the action either way.
public sealed record RewardOption(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("kind")] RewardKind Kind,
    [property: JsonPropertyName("canSkip")] bool CanSkip,
    [property: JsonPropertyName("goldAmount")] int? GoldAmount = null,
    [property: JsonPropertyName("potionId")] string? PotionId = null,
    [property: JsonPropertyName("relicId")] string? RelicId = null,
    [property: JsonPropertyName("cards")] IReadOnlyList<CardRewardOption>? Cards = null);

// Post-combat decision payload. Surfaced on snapshots whenever the engine
// has an unconsumed reward set — typically after combat ends and before the
// caller advances back to the map. While Available is non-empty the host
// holds back the auto-advance to MapRoom; once every reward is selected or
// skipped, the next snapshot returns rewardsState=null and the room has
// flipped back to MapRoom.
public sealed record RewardsState(
    [property: JsonPropertyName("available")] IReadOnlyList<RewardOption> Available);

// Combat-only state. Surfaced on snapshot responses only when CurrentRoomType
// == CombatRoom (or the room flipped back to MapRoom in the same tick via
// post-combat auto-advance — in which case CombatState is omitted on the
// follow-up snapshot). Round numbers start at 1 on the first player turn.
public sealed record CombatState(
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("energy")] int Energy,
    [property: JsonPropertyName("maxEnergy")] int MaxEnergy,
    [property: JsonPropertyName("playerBlock")] int PlayerBlock,
    [property: JsonPropertyName("isPlayPhase")] bool IsPlayPhase,
    [property: JsonPropertyName("isInProgress")] bool IsInProgress,
    [property: JsonPropertyName("drawPileCount")] int DrawPileCount,
    [property: JsonPropertyName("discardPileCount")] int DiscardPileCount,
    [property: JsonPropertyName("hand")] IReadOnlyList<Card> Hand,
    [property: JsonPropertyName("enemies")] IReadOnlyList<Enemy> Enemies,
    [property: JsonPropertyName("playerPowers")] IReadOnlyList<Power> PlayerPowers);

// ── host/ping ────────────────────────────────────────────────────────────

public sealed record HostPingResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("gameVersion")] string? GameVersion,
    [property: JsonPropertyName("gameSha256")] string? GameSha256);

// ── run/new ──────────────────────────────────────────────────────────────

// Fields optional on the wire — character defaults to Ironclad, seed to 1,
// withNeow to false. Defaults are applied in the handler, not the record,
// so the JSON schema matches "field absent" cleanly and the deserialiser
// doesn't need to know.
//
// withNeow=true opts into the Neow blessing event (lands CurrentRoom on the
// Neow EventRoom). No wire method yet exists to *dismiss* the event, so
// clients that opt in are accepting a room they can't currently leave;
// useful for state-shape tests, not for end-to-end runs.
public sealed record RunNewParams(
    [property: JsonPropertyName("character")] Character? Character = null,
    [property: JsonPropertyName("seed")] ulong? Seed = null,
    [property: JsonPropertyName("withNeow")] bool? WithNeow = null,
    // Ascension level. 0 (default) matches the previous wire behavior.
    // Higher levels enable harder content (ASCENDERS_BANE curse,
    // tougher monsters, …). The engine's RunState.CreateForTest takes
    // ascensionLevel directly; we plumb the wire value through
    // Sts2Bindings.StartRun → CreateForTest.
    [property: JsonPropertyName("ascension")] int? Ascension = null,
    // Run modifiers (DRAFT, SEALED_DECK, HOARDER, …). Currently used
    // only as replay-header metadata — the engine plumb-through that
    // would actually alter starting state is a follow-up. Today the
    // wire validates the list (every entry must be a known
    // ModifierId; ModifierId.Unknown is rejected as InvalidParams) and
    // records what the caller asked for in the replay header, but
    // gameplay is unaffected. Null/omitted = no modifiers (today's
    // behavior).
    [property: JsonPropertyName("modifiers")] IReadOnlyList<ModifierId>? Modifiers = null);

public sealed record RunNewResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("character")] Character Character,
    [property: JsonPropertyName("seed")] ulong Seed,
    [property: JsonPropertyName("playerType")] string PlayerType,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    // Legal next moves from the current map position. Empty when the player
    // isn't standing on the map (e.g. mid-combat, on Neow's event); callers
    // should re-issue run/state once the current room resolves.
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    // Legal event-option picks when the current room is an Event. Empty
    // unless currentRoomType == EventRoom; mirrors the "current page" of the
    // active Event, so callers should re-read after run/select_event_option
    // (multi-page events refresh this list each turn).
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    // Combat read-out when CurrentRoomType == CombatRoom. Null otherwise (the
    // wire's room gating mirrors availableMapNodes / availableEventOptions —
    // clients should branch on currentRoomType, not on whether this field
    // is non-null).
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("availableMerchantItems")] IReadOnlyList<MerchantItem> AvailableMerchantItems,
    [property: JsonPropertyName("availableTreasureRelics")] IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    // Pending post-combat rewards. Non-null when the engine has rewards the
    // caller hasn't yet selected/skipped — drives the run/select_reward and
    // run/skip_reward decisions. Null in every other state.
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    // Relics currently carried by the player. Includes the character's
    // starter relic and anything obtained mid-run; order matches sts2's
    // Player.Relics walk (acquisition order).
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics,
    [property: JsonPropertyName("ownedPotions")] IReadOnlyList<OwnedPotion> OwnedPotions,
    // Echo of RunNewParams.Modifiers, normalised to a non-null list
    // (empty when the caller omitted the field). Lets callers confirm
    // the wire shape they got back matches what they asked for.
    [property: JsonPropertyName("modifiers")] IReadOnlyList<ModifierId> Modifiers);

// ── run/state ────────────────────────────────────────────────────────────

// One coverage-instrumentation event captured by a Harmony postfix between
// the previous run/state read and this one. Today only relic hooks are
// patched — kind is always "relic" — but the field is shaped to grow
// without breaking the wire when card/power/potion patches land (each
// just adds a new kind value).
//
// Source is the model's canonical wire id (e.g. "LUCKY_FYSH"), Hook is the
// AbstractModel virtual that fired (e.g. "AfterCardChangedPiles"). The
// pair `(kind, source, hook)` is sufficient to attribute the firing to a
// specific (relic, response) pair; coverage tooling aggregates these into
// the Triggered axis.
//
// The buffer is drained on every run/state response — clients that read
// state twice in a row see the same trigger events ONLY for the first
// read. Skipping a run/state means losing that window's trigger events
// (the buffer caps at TriggerLog.Capacity to bound the leak); a future
// notification stream would remove that constraint without changing this
// field's shape.
[JsonConverter(typeof(JsonStringEnumConverter<TriggerKind>))]
public enum TriggerKind
{
    [JsonStringEnumMemberName("unknown")] Unknown,
    [JsonStringEnumMemberName("relic")] Relic,
    [JsonStringEnumMemberName("card")] Card,
    [JsonStringEnumMemberName("monster")] Monster,
    [JsonStringEnumMemberName("potion")] Potion,
    [JsonStringEnumMemberName("power")] Power,
    // Kinds added alongside the InstrumentationKindParityTest sweep —
    // every entry in GenerateContentIdsCommand.Kinds now maps to a
    // TriggerKind. Encounter is included even though sts2's
    // EncounterModel subtypes have zero AbstractModel hook overrides
    // today (the patcher walks the namespace, finds nothing, reports
    // 0 patched — that's a clean pass, not a failure).
    [JsonStringEnumMemberName("affliction")] Affliction,
    [JsonStringEnumMemberName("enchantment")] Enchantment,
    [JsonStringEnumMemberName("encounter")] Encounter,
    [JsonStringEnumMemberName("event")] Event,
    [JsonStringEnumMemberName("modifier")] Modifier,
    [JsonStringEnumMemberName("orb")] Orb,
}

public sealed record TriggerEvent(
    [property: JsonPropertyName("kind")] TriggerKind Kind,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("hook")] string Hook);

// No params record — run/state reads from session state.
public sealed record RunStateResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("character")] Character? Character,
    [property: JsonPropertyName("seed")] ulong Seed,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("maxHp")] int MaxHp,
    [property: JsonPropertyName("gold")] int Gold,
    [property: JsonPropertyName("deckSize")] int DeckSize,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("currentActIndex")] int CurrentActIndex,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("isVictory")] bool IsVictory,
    [property: JsonPropertyName("isDead")] bool IsDead,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("availableMerchantItems")] IReadOnlyList<MerchantItem> AvailableMerchantItems,
    [property: JsonPropertyName("availableTreasureRelics")] IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics,
    [property: JsonPropertyName("ownedPotions")] IReadOnlyList<OwnedPotion> OwnedPotions,
    // Coverage instrumentation. Empty for callers that don't bootstrap
    // RelicHookPatches; otherwise carries every relic-hook firing since
    // the previous run/state read. See TriggerEvent for the shape.
    // triggeredDropped > 0 indicates the buffer overflowed — callers
    // can surface that as a warning, or just treat it as "go read state
    // more often".
    [property: JsonPropertyName("triggeredSincePrev")] IReadOnlyList<TriggerEvent> TriggeredSincePrev,
    [property: JsonPropertyName("triggeredDropped")] long TriggeredDropped);

// ── run/select_map_node ──────────────────────────────────────────────────

public sealed record RunSelectMapNodeParams(
    [property: JsonPropertyName("col")] int Col,
    [property: JsonPropertyName("row")] int Row);

public sealed record RunSelectMapNodeResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("col")] int Col,
    [property: JsonPropertyName("row")] int Row,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("currentActIndex")] int CurrentActIndex,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("isVictory")] bool IsVictory,
    [property: JsonPropertyName("isDead")] bool IsDead,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("availableMerchantItems")] IReadOnlyList<MerchantItem> AvailableMerchantItems,
    [property: JsonPropertyName("availableTreasureRelics")] IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics,
    [property: JsonPropertyName("ownedPotions")] IReadOnlyList<OwnedPotion> OwnedPotions);

// ── run/select_event_option ──────────────────────────────────────────────

// optionIndex matches the EventOption.Index returned by the most recent
// run/state (or run/new) when the current room is an Event. Picking a
// locked option is permitted by the wire layer — sts2 simply ignores it —
// and the resulting AvailableEventOptions tells the caller whether the
// page advanced.
public sealed record RunSelectEventOptionParams(
    [property: JsonPropertyName("optionIndex")] int OptionIndex);

public sealed record RunSelectEventOptionResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("optionIndex")] int OptionIndex,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("currentActIndex")] int CurrentActIndex,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("isVictory")] bool IsVictory,
    [property: JsonPropertyName("isDead")] bool IsDead,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("availableMerchantItems")] IReadOnlyList<MerchantItem> AvailableMerchantItems,
    [property: JsonPropertyName("availableTreasureRelics")] IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics,
    [property: JsonPropertyName("ownedPotions")] IReadOnlyList<OwnedPotion> OwnedPotions);

// ── run/select_rest_site_option ──────────────────────────────────────────

// optionIndex matches the RestSiteOption.Index returned by the most recent
// snapshot when the current room is a RestSiteRoom. Picking a disabled
// option is allowed by the wire layer — sts2's RestSiteSynchronizer is what
// gates enabled/disabled — but a disabled pick is a likely no-op and the
// next snapshot's AvailableRestSiteOptions tells the caller whether the
// state advanced.
//
// cardSelectIndices: optional hint for options that prompt the player to
// pick cards from the deck. SMITH is the canonical case — it raises one
// CardSelectCmd.FromDeckForUpgrade prompt whose options are the deck's
// upgradable cards (engine pre-filters). For SMITH, send [[0]] to upgrade
// the first upgradable card; with a SmithCount-boosting relic, send
// [[0,1,...]] up to SmithCount picks. Each inner array is one prompt, in
// the order the engine raises them. Omitted hints fall back to the
// selector's first-N default — safe for SMITH because the engine already
// filtered the option list to "upgradable".
//
// HEAL/SMITH/DIG/HATCH/... all leave Options empty once accepted (single-
// pick default) and the host force-advances to MapRoom. A
// ShouldDisableRemainingRestSiteOptions hook can leave additional options
// enabled for multi-pick relics; the next snapshot's AvailableRestSiteOptions
// surfaces them and the agent can call this method again.
public sealed record RunSelectRestSiteOptionParams(
    [property: JsonPropertyName("optionIndex")] int OptionIndex,
    [property: JsonPropertyName("cardSelectIndices")] IReadOnlyList<IReadOnlyList<int>>? CardSelectIndices = null);

public sealed record RunSelectRestSiteOptionResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("optionIndex")] int OptionIndex,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("currentActIndex")] int CurrentActIndex,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("isVictory")] bool IsVictory,
    [property: JsonPropertyName("isDead")] bool IsDead,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("availableMerchantItems")] IReadOnlyList<MerchantItem> AvailableMerchantItems,
    [property: JsonPropertyName("availableTreasureRelics")] IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics,
    [property: JsonPropertyName("ownedPotions")] IReadOnlyList<OwnedPotion> OwnedPotions);

// ── run/leave_treasure_room ──────────────────────────────────────────────

// Drives the treasure-room exit chain. The offering is populated lazily
// on the first snapshot in which currentRoomType=TreasureRoom (so the
// caller sees availableTreasureRelics before they decide); this method
// either grants the offered relic via RelicCmd.Obtain (skip=false, the
// default) or closes the synchronizer session untouched (skip=true).
// Either way the chain finishes with DoExtraRewardsIfNeeded (act-3 /
// ascension extras) and EnterRoom(MapRoom). The returned snapshot
// reflects the post-leave state.
//
// `skip` is optional and defaults to false (claim the relic). Pass
// skip=true to walk past the chest — useful for relic-conflict avoidance,
// SilverCrucible-style "first chest is empty" modifiers, or any agent
// that prefers a known-bad offering over an empty Player.Relics slot.
public sealed record RunLeaveTreasureRoomParams(
    [property: JsonPropertyName("skip")] bool Skip = false);

public sealed record RunLeaveTreasureRoomResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("currentActIndex")] int CurrentActIndex,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("isVictory")] bool IsVictory,
    [property: JsonPropertyName("isDead")] bool IsDead,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("availableMerchantItems")] IReadOnlyList<MerchantItem> AvailableMerchantItems,
    [property: JsonPropertyName("availableTreasureRelics")] IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics,
    [property: JsonPropertyName("ownedPotions")] IReadOnlyList<OwnedPotion> OwnedPotions);

// ── run/end_turn ─────────────────────────────────────────────────────────

// No params record — run/end_turn acts on the current run's active combat.
// Errors when there is no active run, when the current room isn't a
// CombatRoom, or when combat has already ended.
public sealed record RunEndTurnResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("currentActIndex")] int CurrentActIndex,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("isVictory")] bool IsVictory,
    [property: JsonPropertyName("isDead")] bool IsDead,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("availableMerchantItems")] IReadOnlyList<MerchantItem> AvailableMerchantItems,
    [property: JsonPropertyName("availableTreasureRelics")] IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics,
    [property: JsonPropertyName("ownedPotions")] IReadOnlyList<OwnedPotion> OwnedPotions);

// ── run/play_card ────────────────────────────────────────────────────────

// cardIndex is the position in the current snapshot's combatState.hand list.
// targetIndex is required when the card's TargetType is AnyEnemy and is
// otherwise ignored; the index matches combatState.enemies (alive-only).
//
// cardSelectIndices: optional hint for cards that prompt the player to
// choose other cards mid-play (Headbutt picks one card from the discard
// pile to put on top of the draw pile; Armaments picks a card in hand to
// upgrade; Burning Pact picks one to discard). Each inner array is one
// prompt, in the order the engine raises them. Indices are 0-based into
// the options the engine offers (the discard pile / hand, etc.). When
// omitted, the host's ICardSelector picks the first valid card per
// prompt — deterministic and usually safe for cards whose effect doesn't
// depend on which target is chosen, but agents that care should send
// explicit indices.
public sealed record RunPlayCardParams(
    [property: JsonPropertyName("cardIndex")] int CardIndex,
    [property: JsonPropertyName("targetIndex")] int? TargetIndex = null,
    [property: JsonPropertyName("cardSelectIndices")] IReadOnlyList<IReadOnlyList<int>>? CardSelectIndices = null);

public sealed record RunPlayCardResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("cardIndex")] int CardIndex,
    [property: JsonPropertyName("targetIndex")] int? TargetIndex,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("currentActIndex")] int CurrentActIndex,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("isVictory")] bool IsVictory,
    [property: JsonPropertyName("isDead")] bool IsDead,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("availableMerchantItems")] IReadOnlyList<MerchantItem> AvailableMerchantItems,
    [property: JsonPropertyName("availableTreasureRelics")] IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics,
    [property: JsonPropertyName("ownedPotions")] IReadOnlyList<OwnedPotion> OwnedPotions);

// ── run/select_reward ────────────────────────────────────────────────────

// rewardIndex is the position in the most recent snapshot's
// rewardsState.available list. cardIndex is required when the picked reward's
// kind == Card and ignored otherwise. Selecting a reward consumes it; if any
// rewards remain after this call, rewardsState on the response is non-null.
// Once the last reward is consumed, the host advances back to MapRoom and
// rewardsState turns null on the next snapshot.
public sealed record RunSelectRewardParams(
    [property: JsonPropertyName("rewardIndex")] int RewardIndex,
    [property: JsonPropertyName("cardIndex")] int? CardIndex = null);

public sealed record RunSelectRewardResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("rewardIndex")] int RewardIndex,
    [property: JsonPropertyName("cardIndex")] int? CardIndex,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("currentActIndex")] int CurrentActIndex,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("isVictory")] bool IsVictory,
    [property: JsonPropertyName("isDead")] bool IsDead,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("availableMerchantItems")] IReadOnlyList<MerchantItem> AvailableMerchantItems,
    [property: JsonPropertyName("availableTreasureRelics")] IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics,
    [property: JsonPropertyName("ownedPotions")] IReadOnlyList<OwnedPotion> OwnedPotions);

// ── run/skip_reward ──────────────────────────────────────────────────────

// rewardIndex is the position in the most recent snapshot's
// rewardsState.available list. Skipping is only valid for rewards whose
// CanSkip flag is true (currently: card rewards that aren't forced); the
// host throws if the indexed reward isn't skippable so callers can't drift
// engine state.
public sealed record RunSkipRewardParams(
    [property: JsonPropertyName("rewardIndex")] int RewardIndex);

public sealed record RunSkipRewardResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("rewardIndex")] int RewardIndex,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("currentActIndex")] int CurrentActIndex,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("isVictory")] bool IsVictory,
    [property: JsonPropertyName("isDead")] bool IsDead,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("availableMerchantItems")] IReadOnlyList<MerchantItem> AvailableMerchantItems,
    [property: JsonPropertyName("availableTreasureRelics")] IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics,
    [property: JsonPropertyName("ownedPotions")] IReadOnlyList<OwnedPotion> OwnedPotions);

// ── run/buy_merchant_item ────────────────────────────────────────────────

// itemIndex matches the MerchantItem.Index returned by the most recent
// snapshot when CurrentRoomType == MerchantRoom. The host routes the buy
// through the entry's engine path (MerchantEntry.OnTryPurchaseWrapper);
// engine-side guards (insufficient gold, already-sold slot) surface as
// WireException(InvalidParams) so the caller sees a typed error rather
// than a silent no-op. After a successful purchase the item flips to
// IsStocked=false on the next snapshot; gold and relics/potions/deck are
// reflected on the standard snapshot fields.
public sealed record RunBuyMerchantItemParams(
    [property: JsonPropertyName("itemIndex")] int ItemIndex);

public sealed record RunBuyMerchantItemResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("itemIndex")] int ItemIndex,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("currentActIndex")] int CurrentActIndex,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("isVictory")] bool IsVictory,
    [property: JsonPropertyName("isDead")] bool IsDead,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("availableMerchantItems")] IReadOnlyList<MerchantItem> AvailableMerchantItems,
    [property: JsonPropertyName("availableTreasureRelics")] IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics,
    [property: JsonPropertyName("ownedPotions")] IReadOnlyList<OwnedPotion> OwnedPotions);

// ── run/leave_merchant_room ──────────────────────────────────────────────

// A merchant room has no engine auto-exit (unlike rest-site HEAL which
// flips the room itself). Calling this method drives the same pattern the
// rest-site slice uses: EnterRoom(new MapRoom()) on the RunManager, which
// reads CurrentRoomType back to MapRoom on the returned snapshot.
//
// No params — there's no decision to make on the way out beyond "leave".
// A caller that wants to buy something first must call run/buy_merchant_item
// before run/leave_merchant_room (purchasing in-room is unidirectional once
// the player walks out).
public sealed record RunLeaveMerchantRoomResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("currentActIndex")] int CurrentActIndex,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("isVictory")] bool IsVictory,
    [property: JsonPropertyName("isDead")] bool IsDead,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("availableMerchantItems")] IReadOnlyList<MerchantItem> AvailableMerchantItems,
    [property: JsonPropertyName("availableTreasureRelics")] IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics,
    [property: JsonPropertyName("ownedPotions")] IReadOnlyList<OwnedPotion> OwnedPotions);

// ── run/use_potion ───────────────────────────────────────────────────────

// potionIndex matches the OwnedPotion.Index returned by the most recent
// snapshot. targetIndex is required when the potion's TargetType is
// AnyEnemy and is otherwise ignored; the index matches
// combatState.enemies (alive-only). Using a potion outside combat is
// permitted by the engine for some potions (e.g. Strength persists into
// the next fight) but agents should generally hold off until in-combat.
public sealed record RunUsePotionParams(
    [property: JsonPropertyName("potionIndex")] int PotionIndex,
    [property: JsonPropertyName("targetIndex")] int? TargetIndex = null);

public sealed record RunUsePotionResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("potionIndex")] int PotionIndex,
    [property: JsonPropertyName("targetIndex")] int? TargetIndex,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("currentActIndex")] int CurrentActIndex,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("isVictory")] bool IsVictory,
    [property: JsonPropertyName("isDead")] bool IsDead,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("availableMerchantItems")] IReadOnlyList<MerchantItem> AvailableMerchantItems,
    [property: JsonPropertyName("availableTreasureRelics")] IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics,
    [property: JsonPropertyName("ownedPotions")] IReadOnlyList<OwnedPotion> OwnedPotions);

// ── run/enter_next_act ───────────────────────────────────────────────────

// No params — boss → next-act transition reads from session state.
//
// Only legal once the wire surface has reported CurrentRoomType=BossRoom
// with combat ended and the boss reward chain drained (i.e. rewardsState
// is null and the player has stepped off the boss tile into the post-
// boss MapRoom). After draining rewards the engine leaves the player in
// a stale MapRoom whose AvailableMapNodes is empty; calling this method
// drives RunManager.EnterNextAct, which bumps RunState.CurrentActIndex
// and regenerates the next act's map at the start node. Sts2-cli mirrors
// this at RunSimulator.cs:2221.
public sealed record RunEnterNextActResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("currentActIndex")] int CurrentActIndex,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("isVictory")] bool IsVictory,
    [property: JsonPropertyName("isDead")] bool IsDead,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("availableMerchantItems")] IReadOnlyList<MerchantItem> AvailableMerchantItems,
    [property: JsonPropertyName("availableTreasureRelics")] IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics,
    [property: JsonPropertyName("ownedPotions")] IReadOnlyList<OwnedPotion> OwnedPotions);

// ── run/proceed_event ────────────────────────────────────────────────────

// No params — finished-event auto-advance reads from session state.
//
// Only legal when the wire reports CurrentRoomType=EventRoom AND the
// local event's IsFinished flag is true (signalled by an empty
// AvailableEventOptions list while still in EventRoom). The engine
// occasionally leaves the room mid-transition after an event resolves;
// this method calls RunManager.ProceedFromTerminalRewardsScreen() and
// force-EnterRoom(MapRoom) if needed, mirroring sts2-cli's `Leave`
// pattern at RunSimulator.cs:1626-1646.
//
// Returns InvalidParams if called outside that window (not in EventRoom,
// or event still has live options).
public sealed record RunProceedEventResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("currentActIndex")] int CurrentActIndex,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("isVictory")] bool IsVictory,
    [property: JsonPropertyName("isDead")] bool IsDead,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("availableMerchantItems")] IReadOnlyList<MerchantItem> AvailableMerchantItems,
    [property: JsonPropertyName("availableTreasureRelics")] IReadOnlyList<TreasureRelic> AvailableTreasureRelics,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics,
    [property: JsonPropertyName("ownedPotions")] IReadOnlyList<OwnedPotion> OwnedPotions);

// ── debug/*: see Sts2Headless.Cheats ─────────────────────────────────────
//
// All debug/cheat wire DTOs (DebugSetHpParams/Result, DebugGiveRelicParams/Result,
// etc.) live in the Sts2Headless.Cheats project so the Sts2Headless.Agents
// project — which only references Sts2Headless.Protocol — cannot import a
// cheat type even by accident. The wire-method registration loop in
// HostMethods merges the core MethodCatalog with CheatMethodCatalog at host
// startup so the catalog stays a single AssertParity surface.

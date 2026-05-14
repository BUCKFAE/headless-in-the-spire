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
// enum names are used. Future-binding values (Silent, Defect, …) are listed
// even though the host only accepts Ironclad — they form the wire schema
// callers can target, and the host's "not yet supported" error message
// reports the requested value.
[JsonConverter(typeof(JsonStringEnumConverter<Character>))]
public enum Character
{
    [JsonStringEnumMemberName("ironclad")] Ironclad,
    [JsonStringEnumMemberName("silent")] Silent,
    [JsonStringEnumMemberName("defect")] Defect,
    [JsonStringEnumMemberName("watcher")] Watcher,
    [JsonStringEnumMemberName("regent")] Regent,
    [JsonStringEnumMemberName("necrobinder")] Necrobinder,
}

// Room types we've seen the engine land at via the StartRun chain. Wire
// shape matches the sts2 type name exactly (PascalCase like "MapRoom"),
// which is what `room.GetType().Name` returns server-side. Enum values
// missing from this list arrive as `Unknown` so the wire never carries
// a free-form string for a type we haven't catalogued — that would be
// the spelling-mistake trap this enum exists to prevent.
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
// rooms). Elite/Event/RestSite/Treasure/Merchant/Boss are speculative —
// left in to document the schema callers can expect, validated when the
// corresponding nodes actually appear.
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

// One relic carried by the player. Id is the game's stable relic key
// (e.g. "BURNING_BLOOD"); clients translate to a localised name themselves.
// Per-relic runtime state (DynamicVars: counters, charges, etc.) isn't
// surfaced yet — adding fields here is non-breaking and should be driven
// by a concrete caller need rather than mirroring the engine's full shape.
public sealed record Relic(
    [property: JsonPropertyName("id")] string Id);

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
// whether targetIndex is required on play.
public sealed record Card(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("cost")] int Cost,
    [property: JsonPropertyName("canPlay")] bool CanPlay,
    [property: JsonPropertyName("targetType")] TargetType TargetType);

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
    [property: JsonPropertyName("id")] string Id,
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
    [property: JsonPropertyName("withNeow")] bool? WithNeow = null);

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
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    // Pending post-combat rewards. Non-null when the engine has rewards the
    // caller hasn't yet selected/skipped — drives the run/select_reward and
    // run/skip_reward decisions. Null in every other state.
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    // Relics currently carried by the player. Includes the character's
    // starter relic and anything obtained mid-run; order matches sts2's
    // Player.Relics walk (acquisition order).
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics);

// ── run/state ────────────────────────────────────────────────────────────

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
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics);

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
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics);

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
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics);

// ── run/select_rest_site_option ──────────────────────────────────────────

// optionIndex matches the RestSiteOption.Index returned by the most recent
// snapshot when the current room is a RestSiteRoom. Picking a disabled
// option is allowed by the wire layer — sts2's RestSiteSynchronizer is what
// gates enabled/disabled — but a disabled pick is a likely no-op and the
// next snapshot's AvailableRestSiteOptions tells the caller whether the
// state advanced.
//
// HEAL exits the rest site to MapRoom cleanly. SMITH (and any other option
// that branches into a card-selection sub-flow) leaves the player blocked
// on a card-select wire surface we have not built yet; callers picking
// SMITH today will see CurrentRoomType stay at RestSiteRoom with an empty
// AvailableRestSiteOptions list. Routing through SMITH is the next slice.
public sealed record RunSelectRestSiteOptionParams(
    [property: JsonPropertyName("optionIndex")] int OptionIndex);

public sealed record RunSelectRestSiteOptionResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("optionIndex")] int OptionIndex,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics);

// ── run/end_turn ─────────────────────────────────────────────────────────

// No params record — run/end_turn acts on the current run's active combat.
// Errors when there is no active run, when the current room isn't a
// CombatRoom, or when combat has already ended.
public sealed record RunEndTurnResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics);

// ── run/play_card ────────────────────────────────────────────────────────

// cardIndex is the position in the current snapshot's combatState.hand list.
// targetIndex is required when the card's TargetType is AnyEnemy and is
// otherwise ignored; the index matches combatState.enemies (alive-only).
public sealed record RunPlayCardParams(
    [property: JsonPropertyName("cardIndex")] int CardIndex,
    [property: JsonPropertyName("targetIndex")] int? TargetIndex = null);

public sealed record RunPlayCardResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("cardIndex")] int CardIndex,
    [property: JsonPropertyName("targetIndex")] int? TargetIndex,
    [property: JsonPropertyName("currentRoomType")] RoomType CurrentRoomType,
    [property: JsonPropertyName("actFloor")] int ActFloor,
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics);

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
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics);

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
    [property: JsonPropertyName("isGameOver")] bool IsGameOver,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("availableMapNodes")] IReadOnlyList<MapNode> AvailableMapNodes,
    [property: JsonPropertyName("availableEventOptions")] IReadOnlyList<EventOption> AvailableEventOptions,
    [property: JsonPropertyName("availableRestSiteOptions")] IReadOnlyList<RestSiteOption> AvailableRestSiteOptions,
    [property: JsonPropertyName("combatState")] CombatState? CombatState,
    [property: JsonPropertyName("rewardsState")] RewardsState? RewardsState,
    [property: JsonPropertyName("relics")] IReadOnlyList<Relic> Relics);

// ── debug/give_relic ─────────────────────────────────────────────────────

// Test affordance — grants a relic to the active player via the engine
// path (RelicCmd.Obtain, the same path RelicReward.OnSelectWrapper uses).
// Lives in the `debug/` namespace to make its purpose explicit: regression
// tests use this to inject relics with observable on-event side effects
// (e.g. LuckyFysh's +15 gold on AfterCardChangedPiles) so the test can pin
// engine-pipeline behaviour that direct mutation would silently bypass.
public sealed record DebugGiveRelicParams(
    [property: JsonPropertyName("relicId")] string RelicId);

public sealed record DebugGiveRelicResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("relicId")] string RelicId,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("maxHp")] int MaxHp,
    [property: JsonPropertyName("gold")] int Gold,
    [property: JsonPropertyName("deckSize")] int DeckSize);


using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol.Methods;

// Typed mirror of the game's `RunHistory` JSON (schema_version 9 at the
// v0.103.2 pin). One `.run` file per completed/abandoned run lives under
// the player's save directory; this is the canonical post-run audit
// trail per AD-8.
//
// **Wire shape exception**: the rest of our wire protocol uses camelCase
// for property names (`currentRoomType`, `availableMapNodes`); this
// schema uses **snake_case** to pass through the game's own field names
// verbatim. AD-8's rule is "adopt the game's formats verbatim", and a
// case rename here would silently desync our mirror from the game's
// schema_version bumps. Documented as a deliberate local deviation.
//
// Naming policy is centralised in `RunHistoryDocument.JsonOptions` so
// any consumer (test, future wire method, CLI tool) uses the same
// snake_case ↔ PascalCase mapping.
//
// Enums added on demand from the values we've actually observed in
// `vendor/sample-saves/`. Each carries a leading `Unknown` sentinel so
// a new game-side value surfaces as Unknown rather than a parse error
// — the same posture as `RoomType` / `MapNodeType`.

public sealed record RunHistoryDocument(
    int SchemaVersion,
    int Ascension,
    string BuildId,
    GameMode GameMode,
    string KilledByEncounter,
    string KilledByEvent,
    IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> MapPointHistory,
    IReadOnlyList<RunHistoryModifier> Modifiers,
    PlatformType PlatformType,
    IReadOnlyList<RunHistoryPlayer> Players,
    long RunTime,
    string Seed,
    long StartTime,
    bool WasAbandoned,
    bool Win)
{
    // Reads the .run JSON file at the given path. Throws on malformed
    // JSON or schema_version mismatch (the parser is strict: an
    // unexpected schema is a bug to surface, not silently round through
    // a tolerant deserialiser).
    public static RunHistoryDocument ParseFile(string path)
    {
        var json = File.ReadAllText(path);
        return Parse(json);
    }

    public static RunHistoryDocument Parse(string json)
    {
        var doc = JsonSerializer.Deserialize<RunHistoryDocument>(json, JsonOptions)
            ?? throw new InvalidDataException(".run JSON deserialised to null");
        return doc;
    }

    // JSON options for snake_case ↔ PascalCase mapping. Used for read
    // and write — if you're touching a .run file, route through this.
    // Property names default to C# PascalCase; the SnakeCaseLower policy
    // converts at the serialiser boundary so the records stay clean.
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // The game writes some fields only when non-empty; tolerate them
        // being absent in the JSON.
        ReadCommentHandling = JsonCommentHandling.Skip,
    };
}

// game_mode at v0.103.2. Only "standard" observed across 110 sample
// runs; values added on demand.
[JsonConverter(typeof(JsonStringEnumConverter<GameMode>))]
public enum GameMode
{
    Unknown,
    [JsonStringEnumMemberName("standard")] Standard,
    [JsonStringEnumMemberName("daily")] Daily,
    [JsonStringEnumMemberName("custom")] Custom,
    [JsonStringEnumMemberName("endless")] Endless,
}

// platform_type. Only "steam" observed; the game's PlatformType enum
// covers more (Switch / GOG / Epic / …) but we add as we see them.
[JsonConverter(typeof(JsonStringEnumConverter<PlatformType>))]
public enum PlatformType
{
    Unknown,
    [JsonStringEnumMemberName("none")] None,
    [JsonStringEnumMemberName("steam")] Steam,
    [JsonStringEnumMemberName("gog")] Gog,
    [JsonStringEnumMemberName("epic")] Epic,
    [JsonStringEnumMemberName("switch")] Switch,
}

// map_point_type. Distinct from our existing `MapNodeType` (which is
// PascalCase, "Monster"/"Elite"/…, surfaced via run/state's
// AvailableMapNodes): this is the run-history serialisation of the
// SAME concept under different wire-name conventions. Observed
// values across the sample saves: ancient, boss, elite, monster,
// rest_site, shop, treasure, unknown.
[JsonConverter(typeof(JsonStringEnumConverter<RunHistoryMapPointType>))]
public enum RunHistoryMapPointType
{
    Unknown,
    [JsonStringEnumMemberName("ancient")] Ancient,
    [JsonStringEnumMemberName("boss")] Boss,
    [JsonStringEnumMemberName("elite")] Elite,
    [JsonStringEnumMemberName("monster")] Monster,
    [JsonStringEnumMemberName("rest_site")] RestSite,
    [JsonStringEnumMemberName("shop")] Shop,
    [JsonStringEnumMemberName("treasure")] Treasure,
    [JsonStringEnumMemberName("unknown")] UnknownPoint,
}

// rooms[].room_type — the kind of room visited inside a map point.
// Similar to our existing wire-level RoomType but in snake_case and
// without the "Room" suffix. Observed: boss, elite, event, monster,
// rest_site, shop, treasure. Note an Event map_point typically has no
// dedicated entry in map_point_history; events surface as rooms whose
// room_type is "event" inside a Monster / Unknown / RestSite map
// point.
[JsonConverter(typeof(JsonStringEnumConverter<RunHistoryRoomType>))]
public enum RunHistoryRoomType
{
    Unknown,
    [JsonStringEnumMemberName("boss")] Boss,
    [JsonStringEnumMemberName("elite")] Elite,
    [JsonStringEnumMemberName("event")] Event,
    [JsonStringEnumMemberName("monster")] Monster,
    [JsonStringEnumMemberName("rest_site")] RestSite,
    [JsonStringEnumMemberName("shop")] Shop,
    [JsonStringEnumMemberName("treasure")] Treasure,
}

// players[].badges[].rarity. Observed: bronze, silver, gold.
[JsonConverter(typeof(JsonStringEnumConverter<BadgeRarity>))]
public enum BadgeRarity
{
    Unknown,
    [JsonStringEnumMemberName("bronze")] Bronze,
    [JsonStringEnumMemberName("silver")] Silver,
    [JsonStringEnumMemberName("gold")] Gold,
}

// ── Per-act sub-records ──────────────────────────────────────────────

public sealed record MapPointHistoryEntry(
    RunHistoryMapPointType MapPointType,
    IReadOnlyList<MapPointRoom>? Rooms,
    IReadOnlyList<MapPointPlayerStats>? PlayerStats);

// rooms[] inside a map_point_history entry. model_id is the engine's
// internal model id (e.g. an encounter or event id); we leave it as a
// string since it can be any of EncounterId / EventId / etc. depending
// on room_type, and the cross-typing isn't worth the discrimination at
// this layer.
public sealed record MapPointRoom(
    RunHistoryRoomType RoomType,
    string? ModelId,
    int TurnsTaken);

// Per-player snapshot at the end of a map point. Many fields are
// optional / zero-by-default — the game omits empty collections, and
// numeric counters of 0 may or may not appear in the JSON. All
// collection fields default to empty so consumers can iterate freely
// without null checks.
public sealed record MapPointPlayerStats(
    long PlayerId,
    IReadOnlyList<HistoryChoiceEntry>? AncientChoice,
    IReadOnlyList<HistoryCardRef>? CardsGained,
    IReadOnlyList<HistoryCardRef>? CardsRemoved,
    IReadOnlyList<HistoryCardTransformation>? CardsTransformed,
    IReadOnlyList<HistoryEventChoice>? EventChoices,
    IReadOnlyList<HistoryRelicChoice>? RelicChoices,
    int CurrentGold,
    int CurrentHp,
    int DamageTaken,
    int GoldGained,
    int GoldLost,
    int GoldSpent,
    int GoldStolen,
    int HpHealed,
    int MaxHp,
    int MaxHpGained,
    int MaxHpLost);

// Used for ancient_choice + event_choices entries: a list of options
// with localisation keys and which one was picked. The `title` block
// carries a localisation reference (`{ key, table }`); we keep it as a
// nested record so the consumer doesn't have to flatten it manually.
public sealed record HistoryChoiceEntry(
    string TextKey,
    HistoryLocalisationKey Title,
    bool WasChosen);

public sealed record HistoryEventChoice(
    HistoryLocalisationKey Title);

// relic_choices use a flat shape distinct from ancient_choice: the
// `choice` field is a single string id (e.g. "RELIC.GOLDEN_PEARL"),
// not a list of localisation-keyed options. The game's serialiser
// emits both shapes from different code paths so we mirror both.
public sealed record HistoryRelicChoice(
    string? Choice,
    bool WasPicked);

public sealed record HistoryLocalisationKey(
    string Key,
    string Table);

public sealed record HistoryCardRef(
    string Id,
    int? FloorAddedToDeck);

public sealed record HistoryCardTransformation(
    HistoryCardRef OriginalCard,
    HistoryCardRef FinalCard);

// modifiers[] entry. Engine model is richer; we keep this as an opaque
// string + flag set until a use case demands more granularity.
public sealed record RunHistoryModifier(
    string? Id);

// ── Per-player end-of-run snapshot ──────────────────────────────────

public sealed record RunHistoryPlayer(
    long Id,
    // The game's RunHistory serialises the character as a full
    // content-id ("CHARACTER.IRONCLAD") matching its ModelId format,
    // whereas our wire-level `Character` enum uses the friendly form
    // ("ironclad"). The cross-encoding isn't worth retrofitting our
    // existing enum — same posture as our other content-id fields,
    // which are left as opaque strings in the wire records too.
    string Character,
    int MaxPotionSlotCount,
    IReadOnlyList<HistoryCardRef>? Deck,
    IReadOnlyList<HistoryCardRef>? Relics,
    IReadOnlyList<HistoryOwnedPotion>? Potions,
    IReadOnlyList<HistoryBadge>? Badges);

public sealed record HistoryOwnedPotion(
    string Id,
    int SlotIndex);

public sealed record HistoryBadge(
    string Id,
    BadgeRarity Rarity);

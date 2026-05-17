using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sts2Headless.Replay;

// Decodes a deserialised CombatReplay into `timeline.json` — the
// viewer-friendly per-combat artefact. One timeline.json per .mcr, written
// next to it in the same `combats/` directory.
//
// This is Phase A.1 of the replay-viewing work: it gives the viewer
// structured access to the event stream and the initial run state at
// combat start, without requiring engine re-execution to capture
// state-per-event. State-per-event is Phase A.2 — gated on solving the
// `await ProcessFrame` problem in the engine's `NMultiplayerTest.RunReplay`
// loop, which our headless host can't pump.
//
// Layout (1:1 with the four CombatReplayEventTypes the engine writes):
//
//   {
//     "schema_version": 1,
//     "header": { version, git_commit, model_id_hash,
//                 next_action_id, next_checksum_id, next_hook_id,
//                 event_count, checksum_count },
//     "initial_run": <SerializableRun JSON via game's own serializer>,
//     "choice_ids": [...],
//     "events": [{ "index": 0, "type": "GameAction"|"HookAction"|...,
//                  "player_id": <ulong?>, ...type-specific fields },
//                ...],
//     "checksums": [{ "id", "checksum", "context" }, ...]
//   }
//
// The `full_state` payload that's recorded alongside each checksum in
// the .mcr is deliberately omitted — it inflates the file 10–100× and
// the viewer doesn't render it. If the canary work (Phase A.2) needs it,
// add it behind a flag.
public sealed class CombatTimelineEmitter
{
    public const int SchemaVersion = 1;

    public const string TimelineFileExtension = ".timeline.json";

    // Reflection on the loaded sts2 assembly. Cached at construction
    // because the emitter is reused for every combat in a run.
    private readonly Type _combatReplayType;
    private readonly FieldInfo _versionField;
    private readonly FieldInfo _gitCommitField;
    private readonly FieldInfo _modelIdHashField;
    private readonly FieldInfo _choiceIdsField;
    private readonly FieldInfo _nextActionIdField;
    private readonly FieldInfo _nextChecksumIdField;
    private readonly FieldInfo _nextHookIdField;
    private readonly FieldInfo _serializableRunField;
    private readonly FieldInfo _eventsField;
    private readonly FieldInfo _checksumDataField;

    private readonly Type _combatReplayEventType;
    private readonly FieldInfo _eventTypeField;
    private readonly FieldInfo _eventPlayerIdField;
    private readonly FieldInfo _eventActionField;
    private readonly FieldInfo _eventHookIdField;
    private readonly FieldInfo _eventActionIdField;
    private readonly FieldInfo _eventGameActionTypeField;
    private readonly FieldInfo _eventChoiceIdField;
    private readonly FieldInfo _eventPlayerChoiceResultField;

    private readonly Type _replayChecksumDataType;
    private readonly FieldInfo _checksumDataChecksumField;
    private readonly FieldInfo _checksumDataContextField;
    private readonly FieldInfo _checksumDataFullStateField;

    private readonly Type _netChecksumDataType;
    private readonly FieldInfo _netChecksumIdField;
    private readonly FieldInfo _netChecksumChecksumField;

    // NetFullCombatState — the live combat snapshot recorded with each
    // checksum. We pull a slim subset (creature HP / block, player
    // energy + gold) so the viewer can render per-turn HP without
    // having to deserialise the full state itself. The full struct is
    // available in the .mcr if a future tool needs deeper inspection.
    private readonly Type _fullCombatStateType;
    private readonly PropertyInfo _fullCombatStateCreaturesProp;
    private readonly PropertyInfo _fullCombatStatePlayersProp;
    private readonly Type _creatureStateType;
    private readonly FieldInfo _creatureStateMonsterIdField;
    private readonly FieldInfo _creatureStatePlayerIdField;
    private readonly FieldInfo _creatureStateCurrentHpField;
    private readonly FieldInfo _creatureStateMaxHpField;
    private readonly FieldInfo _creatureStateBlockField;
    private readonly Type _playerStateType;
    private readonly FieldInfo _playerStatePlayerIdField;
    private readonly FieldInfo _playerStateEnergyField;
    private readonly FieldInfo _playerStateGoldField;

    // ModelId is the engine's `{ Category, Entry }` content-id pair —
    // already shows up in the GameAction decoding path as the
    // snake-case `{ category, entry }` JSON object. For the slim-state
    // path we just call `ModelId.ToString()` (which returns
    // "CATEGORY.ENTRY") so the viewer can render `MONSTER.CRAWLER`
    // verbatim. Cached MethodInfo so we don't resolve on every read.
    private readonly MethodInfo _modelIdToString;

    // The game's own JSON serializer is the only thing that handles
    // SerializableRun (and its 100+ nested types) correctly — Mega Crit's
    // schema has [JsonPropertyName] all over but also custom converters
    // and source-generated TypeInfo. Calling `ToJson<SerializableRun>`
    // gives us bytes that match the engine's `.run` shape conventions
    // (snake_case keys, omitted defaults). We then re-parse the result
    // into a JsonNode so we can splice it into the wider timeline doc.
    private readonly MethodInfo _gameToJsonForSerializableRun;

    // STJ options for the parts of timeline.json that we author (header,
    // events, checksums). Field-include is required because the engine's
    // INetAction implementations are public-fields-only structs with no
    // property surface. WriteIndented is on so the file is eyeball-readable
    // and diff-friendly; the viewer doesn't care about wire size at this
    // stage.
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        IncludeFields = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public CombatTimelineEmitter(Assembly sts2)
    {
        _combatReplayType = sts2.GetType("MegaCrit.Sts2.Core.Multiplayer.Replay.CombatReplay")
            ?? throw new InvalidOperationException("CombatReplay not found in sts2 assembly");
        _versionField = RequireField(_combatReplayType, "version");
        _gitCommitField = RequireField(_combatReplayType, "gitCommit");
        _modelIdHashField = RequireField(_combatReplayType, "modelIdHash");
        _choiceIdsField = RequireField(_combatReplayType, "choiceIds");
        _nextActionIdField = RequireField(_combatReplayType, "nextActionId");
        _nextChecksumIdField = RequireField(_combatReplayType, "nextChecksumId");
        _nextHookIdField = RequireField(_combatReplayType, "nextHookId");
        _serializableRunField = RequireField(_combatReplayType, "serializableRun");
        _eventsField = RequireField(_combatReplayType, "events");
        _checksumDataField = RequireField(_combatReplayType, "checksumData");

        _combatReplayEventType = sts2.GetType("MegaCrit.Sts2.Core.Multiplayer.Replay.CombatReplayEvent")
            ?? throw new InvalidOperationException("CombatReplayEvent not found in sts2 assembly");
        _eventTypeField = RequireField(_combatReplayEventType, "eventType");
        _eventPlayerIdField = RequireField(_combatReplayEventType, "playerId");
        _eventActionField = RequireField(_combatReplayEventType, "action");
        _eventHookIdField = RequireField(_combatReplayEventType, "hookId");
        _eventActionIdField = RequireField(_combatReplayEventType, "actionId");
        _eventGameActionTypeField = RequireField(_combatReplayEventType, "gameActionType");
        _eventChoiceIdField = RequireField(_combatReplayEventType, "choiceId");
        _eventPlayerChoiceResultField = RequireField(_combatReplayEventType, "playerChoiceResult");

        _replayChecksumDataType = sts2.GetType("MegaCrit.Sts2.Core.Multiplayer.Replay.ReplayChecksumData")
            ?? throw new InvalidOperationException("ReplayChecksumData not found in sts2 assembly");
        _checksumDataChecksumField = RequireField(_replayChecksumDataType, "checksumData");
        _checksumDataContextField = RequireField(_replayChecksumDataType, "context");
        _checksumDataFullStateField = RequireField(_replayChecksumDataType, "fullState");

        _netChecksumDataType = sts2.GetType("MegaCrit.Sts2.Core.Entities.Multiplayer.NetChecksumData")
            ?? throw new InvalidOperationException("NetChecksumData not found in sts2 assembly");
        _netChecksumIdField = RequireField(_netChecksumDataType, "id");
        _netChecksumChecksumField = RequireField(_netChecksumDataType, "checksum");

        _fullCombatStateType = sts2.GetType("MegaCrit.Sts2.Core.Entities.Multiplayer.NetFullCombatState")
            ?? throw new InvalidOperationException("NetFullCombatState not found in sts2 assembly");
        _fullCombatStateCreaturesProp = _fullCombatStateType.GetProperty("Creatures", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("NetFullCombatState.Creatures property not found");
        _fullCombatStatePlayersProp = _fullCombatStateType.GetProperty("Players", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("NetFullCombatState.Players property not found");

        // Nested struct types live under NetFullCombatState — Type.GetNestedType
        // covers them. Both are public on the wire, just nested in the C#
        // declaration.
        _creatureStateType = _fullCombatStateType.GetNestedType("CreatureState", BindingFlags.Public)
            ?? throw new InvalidOperationException("NetFullCombatState.CreatureState nested type not found");
        _creatureStateMonsterIdField = RequireField(_creatureStateType, "monsterId");
        _creatureStatePlayerIdField = RequireField(_creatureStateType, "playerId");
        _creatureStateCurrentHpField = RequireField(_creatureStateType, "currentHp");
        _creatureStateMaxHpField = RequireField(_creatureStateType, "maxHp");
        _creatureStateBlockField = RequireField(_creatureStateType, "block");

        _playerStateType = _fullCombatStateType.GetNestedType("PlayerState", BindingFlags.Public)
            ?? throw new InvalidOperationException("NetFullCombatState.PlayerState nested type not found");
        _playerStatePlayerIdField = RequireField(_playerStateType, "playerId");
        _playerStateEnergyField = RequireField(_playerStateType, "energy");
        _playerStateGoldField = RequireField(_playerStateType, "gold");

        var modelIdType = sts2.GetType("MegaCrit.Sts2.Core.Models.ModelId")
            ?? throw new InvalidOperationException("ModelId not found in sts2 assembly");
        _modelIdToString = modelIdType.GetMethod("ToString", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
            ?? throw new InvalidOperationException("ModelId.ToString() not found");

        var serializableRunType = sts2.GetType("MegaCrit.Sts2.Core.Saves.SerializableRun")
            ?? throw new InvalidOperationException("SerializableRun not found in sts2 assembly");
        var jsonUtilType = sts2.GetType("MegaCrit.Sts2.Core.Saves.JsonSerializationUtility")
            ?? throw new InvalidOperationException("JsonSerializationUtility not found in sts2 assembly");
        var toJsonGeneric = jsonUtilType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m is { Name: "ToJson", IsGenericMethodDefinition: true } && m.GetParameters().Length == 1)
            ?? throw new InvalidOperationException("JsonSerializationUtility.ToJson<T>(T) not found");
        _gameToJsonForSerializableRun = toJsonGeneric.MakeGenericMethod(serializableRunType);
    }

    // Builds the timeline document for a single CombatReplay. Returns
    // the JsonNode rather than writing — tests can inspect the structure
    // without going through disk.
    public JsonObject Build(object combatReplay)
    {
        if (combatReplay is null) throw new ArgumentNullException(nameof(combatReplay));

        var events = ReadEvents(combatReplay);
        var checksums = ReadChecksums(combatReplay);
        var initialRun = ReadInitialRun(combatReplay);

        var doc = new JsonObject
        {
            ["schema_version"] = SchemaVersion,
            ["header"] = new JsonObject
            {
                ["version"] = (string)_versionField.GetValue(combatReplay)!,
                ["git_commit"] = (string)_gitCommitField.GetValue(combatReplay)!,
                ["model_id_hash"] = (uint)_modelIdHashField.GetValue(combatReplay)!,
                ["next_action_id"] = (uint)_nextActionIdField.GetValue(combatReplay)!,
                ["next_checksum_id"] = (uint)_nextChecksumIdField.GetValue(combatReplay)!,
                ["next_hook_id"] = (uint)_nextHookIdField.GetValue(combatReplay)!,
                ["event_count"] = events.Count,
                ["checksum_count"] = checksums.Count,
            },
            ["initial_run"] = initialRun,
            ["choice_ids"] = ReadChoiceIds(combatReplay),
            ["events"] = events,
            ["checksums"] = checksums,
        };
        return doc;
    }

    // Writes the document to disk. Output path is `<mcrPath>.timeline.json`
    // unless overridden — same directory as the .mcr so the viewer can
    // pair them by name.
    public void EmitNextTo(object combatReplay, string mcrPath)
    {
        var output = mcrPath + TimelineFileExtension;
        Emit(combatReplay, output);
    }

    public void Emit(object combatReplay, string outputPath)
    {
        var doc = Build(combatReplay);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, doc.ToJsonString(JsonOptions));
    }

    private JsonNode ReadInitialRun(object combatReplay)
    {
        var sr = _serializableRunField.GetValue(combatReplay);
        if (sr is null) return JsonValue.Create((object?)null)!;
        // The game's serializer returns a complete JSON document string;
        // re-parse so we can embed it as a structured node.
        var raw = (string)(_gameToJsonForSerializableRun.Invoke(null, [sr])
            ?? throw new InvalidOperationException("SerializableRun serialised to null"));
        return JsonNode.Parse(raw) ?? throw new InvalidDataException("SerializableRun JSON re-parse returned null");
    }

    private JsonArray ReadChoiceIds(object combatReplay)
    {
        var list = _choiceIdsField.GetValue(combatReplay);
        var array = new JsonArray();
        if (list is null) return array;
        foreach (var id in (System.Collections.IEnumerable)list)
            array.Add(JsonValue.Create((uint)id));
        return array;
    }

    private JsonArray ReadEvents(object combatReplay)
    {
        var list = _eventsField.GetValue(combatReplay);
        var array = new JsonArray();
        if (list is null) return array;
        int index = 0;
        foreach (var evt in (System.Collections.IEnumerable)list)
        {
            array.Add(BuildEvent(evt, index));
            index++;
        }
        return array;
    }

    private JsonObject BuildEvent(object evt, int index)
    {
        var eventTypeRaw = _eventTypeField.GetValue(evt)!;
        var eventTypeName = eventTypeRaw.ToString()!;
        var playerId = (ulong?)_eventPlayerIdField.GetValue(evt);

        var node = new JsonObject
        {
            ["index"] = index,
            ["type"] = eventTypeName,
        };
        if (playerId.HasValue) node["player_id"] = playerId.Value;

        switch (eventTypeName)
        {
            case "GameAction":
                AddGameAction(node, evt);
                break;
            case "HookAction":
                AddHookAction(node, evt);
                break;
            case "ResumeAction":
                AddResumeAction(node, evt);
                break;
            case "PlayerChoice":
                AddPlayerChoice(node, evt);
                break;
            // CombatReplayEventType.None (the default 0 value) is never
            // recorded by the engine — every event the writer appends
            // sets one of the four real types. If we see one, the file
            // is corrupt; emit it as-is and let the viewer flag it
            // rather than crashing the whole timeline emission.
        }
        return node;
    }

    private void AddGameAction(JsonObject node, object evt)
    {
        var action = _eventActionField.GetValue(evt);
        if (action is null)
        {
            node["action_type"] = "Unknown";
            return;
        }
        node["action_type"] = action.GetType().Name;
        // Concrete-type serialisation so the polymorphic INetAction
        // surfaces its real fields (card, modelId, targetId, …) rather
        // than the empty interface.
        node["action"] = JsonSerializer.SerializeToNode(action, action.GetType(), JsonOptions);
    }

    private void AddHookAction(JsonObject node, object evt)
    {
        if (_eventHookIdField.GetValue(evt) is uint hookId) node["hook_id"] = hookId;
        var gameActionType = _eventGameActionTypeField.GetValue(evt);
        if (gameActionType is not null) node["game_action_type"] = gameActionType.ToString();
    }

    private void AddResumeAction(JsonObject node, object evt)
    {
        if (_eventActionIdField.GetValue(evt) is uint actionId) node["action_id"] = actionId;
    }

    private void AddPlayerChoice(JsonObject node, object evt)
    {
        if (_eventChoiceIdField.GetValue(evt) is uint choiceId) node["choice_id"] = choiceId;
        var result = _eventPlayerChoiceResultField.GetValue(evt);
        if (result is null) return;
        node["choice_result"] = JsonSerializer.SerializeToNode(result, result.GetType(), JsonOptions);
    }

    private JsonArray ReadChecksums(object combatReplay)
    {
        var list = _checksumDataField.GetValue(combatReplay);
        var array = new JsonArray();
        if (list is null) return array;
        foreach (var entry in (System.Collections.IEnumerable)list)
        {
            var netData = _checksumDataChecksumField.GetValue(entry)!;
            var id = (uint)_netChecksumIdField.GetValue(netData)!;
            var checksum = (uint)_netChecksumChecksumField.GetValue(netData)!;
            var context = (string)(_checksumDataContextField.GetValue(entry) ?? "");
            var node = new JsonObject
            {
                ["id"] = id,
                ["checksum"] = checksum,
                ["context"] = context,
            };
            var fullState = _checksumDataFullStateField.GetValue(entry);
            if (fullState is not null)
            {
                node["state"] = BuildSlimState(fullState);
            }
            array.Add(node);
        }
        return array;
    }

    // Extracts the viewer-relevant subset of NetFullCombatState: each
    // creature's HP / max-HP / block (tagged player or monster) and
    // each player's energy + gold. Powers / orbs / piles / potions /
    // relics are deliberately omitted — they're either already in
    // initial_run (relics, potions don't change mid-combat) or the
    // file-size blow-up isn't worth the marginal viewer value. If a
    // future feature needs them, widen this method.
    private JsonObject BuildSlimState(object fullCombatState)
    {
        var creatures = new JsonArray();
        var creatureList = _fullCombatStateCreaturesProp.GetValue(fullCombatState);
        if (creatureList is not null)
        {
            foreach (var creature in (System.Collections.IEnumerable)creatureList)
            {
                creatures.Add(BuildCreatureEntry(creature));
            }
        }

        var players = new JsonArray();
        var playerList = _fullCombatStatePlayersProp.GetValue(fullCombatState);
        if (playerList is not null)
        {
            foreach (var player in (System.Collections.IEnumerable)playerList)
            {
                players.Add(new JsonObject
                {
                    ["player_id"] = (ulong)_playerStatePlayerIdField.GetValue(player)!,
                    ["energy"] = (int)_playerStateEnergyField.GetValue(player)!,
                    ["gold"] = (int)_playerStateGoldField.GetValue(player)!,
                });
            }
        }

        return new JsonObject
        {
            ["creatures"] = creatures,
            ["players"] = players,
        };
    }

    private JsonObject BuildCreatureEntry(object creature)
    {
        var node = new JsonObject
        {
            ["current_hp"] = (int)_creatureStateCurrentHpField.GetValue(creature)!,
            ["max_hp"] = (int)_creatureStateMaxHpField.GetValue(creature)!,
            ["block"] = (int)_creatureStateBlockField.GetValue(creature)!,
        };
        var monsterId = _creatureStateMonsterIdField.GetValue(creature);
        var playerId = _creatureStatePlayerIdField.GetValue(creature);
        if (monsterId is not null)
        {
            node["kind"] = "monster";
            node["monster_id"] = FormatModelId(monsterId);
        }
        else if (playerId is not null)
        {
            node["kind"] = "player";
            node["player_id"] = (ulong)playerId;
        }
        else
        {
            // Both null is the engine's "neither" — shouldn't happen
            // in recorded replays but the wire encoding allows it.
            node["kind"] = "unknown";
        }
        return node;
    }

    private string FormatModelId(object modelId)
        => (string)(_modelIdToString.Invoke(modelId, null) ?? "");

    private static FieldInfo RequireField(Type type, string name)
        => type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
           ?? throw new InvalidOperationException($"{type.FullName}.{name} field not found");
}

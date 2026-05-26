using System.Reflection;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime.Bindings;

// Run-start operations. StartRun is the sts2-cli StartRun chain
// condensed: clean up any previous run, create the player (per character
// via the dictionary in _createCharacterRun), create the run state, set
// up the RunManager, align LocalContext.NetId, generate rooms, launch,
// finalize starting relics, EnterAct(0). Backed by the `_player*` /
// `_runManager*` / `_runState*` fields declared in Sts2Bindings.cs.
public sealed partial class Sts2Bindings
{
    // Full sts2-cli StartRun chain, condensed. Returns a triple the wire
    // layer can pass back in for subsequent calls. `character` selects
    // which Player.CreateForNewRun<T> closed generic to invoke — the
    // dictionary was built at Bind() so every Character enum value has a
    // registered factory or bootstrap would have failed. Every STS2 run
    // starts at the Neow blessing EventRoom (no opt-out — that's how the
    // game works); callers drive `run/select_event_option` to advance to
    // MapRoom. LocPatches + the Texture2D / StringName stubs are what let
    // the event populate options in the first place.
    public RunHandle StartRun(Character character, ulong seed, int ascensionLevel = 0)
    {
        if (ascensionLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(ascensionLevel),
                $"ascensionLevel must be non-negative, got {ascensionLevel}");
        if (!_createCharacterRun.TryGetValue(character, out var characterFactory))
            throw new InvalidOperationException(
                $"no binding for character {character} — Bind() should have rejected this at startup");
        // A new run cannot inherit pending rewards from a previous one — the
        // reward-set objects belong to the prior RunManager state and become
        // invalid after the second run/new wipes that state.
        _pendingRewards = null;

        // Reset RunManager if a previous run is still installed; SetUpTest
        // throws "State is already set." otherwise. sts2-cli does the same
        // thing at RunSimulator.CleanUp:3573.
        var existingManager = _runManagerInstance.GetValue(null);
        if (existingManager is not null
            && _runManagerIsInProgress.GetValue(existingManager) is true)
        {
            _runManagerCleanUp.Invoke(existingManager, new object?[] { /* graceful: */ true });
        }

        // Player.CreateForNewRun's second ulong is the player's NetId, not the
        // run seed (the seed lives on RunState — see CreateForTest below).
        // We pass 1uL — sts2-cli's "everything is player 1" contract.
        // NetSingleplayerGameService.NetId is a baked 1uL (read-only) and
        // keys the engine's multiplayer-aware paths (ActionQueueSet, Reward-
        // Synchronizer, RunHistory). With Player.NetId = LocalContext.NetId
        // = netService.NetId = 1uL, the natural enemy-turn / reward chains
        // run end-to-end without our manual fallbacks intervening.
        // (probe-natural-chain proved this; see natural-chain-gaps.md.)
        const ulong playerNetId = 1uL;
        var player = characterFactory.Invoke(null, new object?[] { _unlockStateAll, playerNetId })
            ?? throw new InvalidOperationException($"Player.CreateForNewRun<{character.Sts2TypeName()}> returned null");

        // CreateForTest takes IReadOnlyList<Player> — pass a strongly-typed
        // Player[] so the framework's parameter-binding sees a compatible
        // covariant cast rather than List<object>.
        var playerArray = Array.CreateInstance(_playerType, 1);
        playerArray.SetValue(player, 0);

        var runState = _runStateCreateForTest.Invoke(null, new Dictionary<string, object?>
        {
            ["players"] = playerArray,
            ["ascensionLevel"] = ascensionLevel,
            ["seed"] = $"sts2headless-{seed}",
        }) ?? throw new InvalidOperationException("RunState.CreateForTest returned null");

        var runManager = _runManagerInstance.GetValue(null)
            ?? throw new InvalidOperationException("RunManager.Instance returned null");
        var netService = Activator.CreateInstance(_netServiceType)
            ?? throw new InvalidOperationException($"{_netServiceType.FullName} default ctor returned null");

        _runManagerSetUpTest.Invoke(runManager, new Dictionary<string, object?>
        {
            ["state"] = runState,
            ["gameService"] = netService,
        });

        // Mirror sts2-cli's RunSimulator.cs:255: align LocalContext.NetId to
        // the local player's NetId. With Player.NetId = NetSingleplayerGame-
        // Service.NetId = 1uL above, the engine's multiplayer-aware lookups
        // (LocalContext.GetMe, RunHistory.GetPlayerStats, CardReward.OnSelect-
        // Wrapper) all resolve to this single player.
        var resolvedNetId = _playerNetId.GetValue(player)
            ?? throw new InvalidOperationException("Player.NetId returned null");
        WriteLocalContextNetId(resolvedNetId);

        var extra = _runStateExtraFields.GetValue(runState)
            ?? throw new InvalidOperationException("RunState.ExtraFields was null");
        _extraFieldsStartedWithNeow.SetValue(extra, true);

        _runManagerGenerateRooms.Invoke(runManager, null);
        _runManagerLaunch.Invoke(runManager, null);
        if (_runManagerFinalizeStartingRelics.Invoke(runManager, null) is Task finalize)
            finalize.GetAwaiter().GetResult();

        var enterActResult = _runManagerEnterAct.Invoke(runManager, new Dictionary<string, object?>
        {
            ["currentActIndex"] = 0,
            ["doTransition"] = false,
        });
        if (enterActResult is Task enterAct) enterAct.GetAwaiter().GetResult();

        return new RunHandle(player, runState, runManager);
    }

    // LocalContext.NetId is exposed as either a static property or a static
    // field depending on the game version. The Bind layer captures whichever
    // it found; this helper hides the discriminator at call sites.
    private void WriteLocalContextNetId(object netId)
    {
        switch (_localContextNetIdMember)
        {
            case PropertyInfo p:
                p.SetValue(null, netId);
                break;
            case FieldInfo f:
                f.SetValue(null, netId);
                break;
            default:
                throw new InvalidOperationException(
                    $"LocalContext.NetId binding is neither PropertyInfo nor FieldInfo (got {_localContextNetIdMember.GetType().Name})");
        }
    }
}

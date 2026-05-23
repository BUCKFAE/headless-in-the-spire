using System.Reflection;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.Runtime;
using Sts2Headless.Runtime.Bindings;
using Sts2Headless.Runtime.Loading;

namespace Sts2Headless.Commands;

// Diagnose-the-combat-stall probe. Drives the engine through one seed's
// natural agent-path (heal-between-rooms, play-then-end-turn) and on the
// first EndTurn that fails to converge, dumps the engine's reflective
// state — IsPlayPhase, IsInProgress, ActionExecutor.IsRunning, the live
// CombatState round/energy/hand/discard, every Wait*/Yield*-shaped method
// declared on the loaded sts2 assembly (to catch unpatched async hooks),
// and any captured engine-logged exception block. Output is plain text
// to stdout/stderr — not a checked-in markdown report — because this
// probe runs many times during the fix-and-verify loop.
internal static class ProbeCombatStallCommand
{
    public static int Run(string vendorDir, string[] args)
    {
        var seed = ParseSeed(args, defaultSeed: 1uL);
        var floorBudget = ParseInt(args, "--floor", defaultValue: 15);
        Console.WriteLine($"probe-combat-stall: seed={seed} floor-budget={floorBudget}");

        var preamble = RuntimeBootstrap.Run(vendorDir);
        if (preamble.SetupError is not null)
        {
            Console.Error.WriteLine($"  bootstrap setup failed: {preamble.SetupError}");
            return 1;
        }
        foreach (var p in preamble.Patches)
            if (!p.Patched) Console.Error.WriteLine($"  WARN: patch '{p.Target}' did not apply ({p.Detail})");
        foreach (var s in BootstrapSequence.Apply(preamble.Sts2!))
            if (!s.Ok) Console.Error.WriteLine($"  WARN: bootstrap step '{s.Label}' did not succeed ({s.Detail})");

        Sts2Bindings bindings;
        try { bindings = Sts2Bindings.Bind(preamble.Sts2!, preamble.SyncContext); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  bind failed: {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
            return 1;
        }

        RunHandle handle;
        try { handle = bindings.StartRun(Character.Ironclad, seed); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  StartRun threw: {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
            return 1;
        }

        var snap = bindings.ReadSnapshot(handle);
        Console.WriteLine($"  start floor={snap.ActFloor} room={snap.CurrentRoomType} hp={snap.CurrentHp}/{snap.MaxHp}");

        // Walk: at each step, pick an action based on room. Combat: play a
        // playable card if available else EndTurn; once IsPlayPhase drops to
        // false and stays there past EndTurn we treat it as a stall and dump.
        // No try/catch around production EndTurn — if it throws, the error
        // surfaces here unwrapped (that's diagnostic value).
        var startFloor = snap.ActFloor;
        var safety = 0;
        var lastLoggedRoom = RoomType.Unknown;
        var lastLoggedFloor = -1;
        while (safety++ < 2000)
        {
            snap = bindings.ReadSnapshot(handle);
            if (snap.CurrentRoomType != lastLoggedRoom || snap.ActFloor != lastLoggedFloor)
            {
                Console.WriteLine($"  enter floor={snap.ActFloor} room={snap.CurrentRoomType} hp={snap.CurrentHp}/{snap.MaxHp} options={snap.AvailableEventOptions.Count}");
                lastLoggedRoom = snap.CurrentRoomType;
                lastLoggedFloor = snap.ActFloor;
            }
            if (snap.IsGameOver) { Console.WriteLine($"  GAME-OVER floor={snap.ActFloor}"); return 0; }
            if (snap.ActFloor - startFloor >= floorBudget)
            {
                Console.WriteLine($"  budget exhausted at floor={snap.ActFloor} (no stall observed)");
                return 0;
            }

            switch (snap.CurrentRoomType)
            {
                case RoomType.MapRoom:
                    StepMap(bindings, handle, snap);
                    break;
                case RoomType.CombatRoom:
                case RoomType.BossRoom:
                    if (StepCombatOrDump(bindings, handle, snap, preamble.Sts2!)) return 0;
                    break;
                case RoomType.RestSiteRoom:
                    StepRest(bindings, handle, snap);
                    break;
                case RoomType.TreasureRoom:
                    bindings.LeaveTreasureRoom(handle);
                    break;
                case RoomType.MerchantRoom:
                    bindings.LeaveMerchantRoom(handle);
                    break;
                case RoomType.EventRoom:
                    if (snap.AvailableEventOptions.Count == 0)
                    {
                        Console.WriteLine($"  EVENT-NO-OPTIONS floor={snap.ActFloor}");
                        return 0;
                    }
                    Console.WriteLine("    event options: "
                        + string.Join(" | ", snap.AvailableEventOptions.Select(o =>
                            $"[{o.Index}{(o.IsLocked ? " locked" : "")}] {o.TextKey}")));
                    // Match GreedyAgent.StepEventAsync's "pick last unlocked"
                    // policy so the probe and the agent traverse the same path.
                    var pick = snap.AvailableEventOptions.LastOrDefault(o => !o.IsLocked)
                        ?? snap.AvailableEventOptions[^1];
                    bindings.SelectEventOption(handle, pick.Index);
                    break;
                default:
                    Console.WriteLine($"  unhandled room {snap.CurrentRoomType} floor={snap.ActFloor}");
                    return 0;
            }
        }
        Console.WriteLine($"  safety cap hit at floor={snap.ActFloor}");
        return 0;
    }

    private static void StepMap(Sts2Bindings bindings, RunHandle handle, RunSnapshot snap)
    {
        if (snap.CurrentHp < snap.MaxHp)
        {
            bindings.SetPlayerHp(handle, snap.MaxHp, null);
            return;
        }
        var pick = snap.AvailableMapNodes
            .Where(n => n.Type is MapNodeType.Monster or MapNodeType.Elite or MapNodeType.Event)
            .OrderBy(n => n.Type == MapNodeType.Monster ? 0 : (n.Type == MapNodeType.Elite ? 1 : 2))
            .ThenBy(n => n.Col)
            .FirstOrDefault()
            ?? snap.AvailableMapNodes.First();
        bindings.EnterMapCoord(handle, pick.Col, pick.Row);
    }

    private static void StepRest(Sts2Bindings bindings, RunHandle handle, RunSnapshot snap)
    {
        var heal = snap.AvailableRestSiteOptions.FirstOrDefault(o =>
            o.IsEnabled && string.Equals(o.OptionId, "HEAL", StringComparison.OrdinalIgnoreCase));
        if (heal is not null)
            bindings.SelectRestSiteOption(handle, heal.Index);
        else
            bindings.SelectRestSiteOption(handle, snap.AvailableRestSiteOptions.First(o => o.IsEnabled).Index);
    }

    // Returns true iff we hit a stall and dumped state (caller should stop).
    private static bool StepCombatOrDump(Sts2Bindings bindings, RunHandle handle, RunSnapshot snap, Assembly sts2)
    {
        var combat = snap.CombatState;
        if (combat is null)
        {
            Console.WriteLine($"  combat snapshot null at floor={snap.ActFloor}");
            return true;
        }

        // Combat-ended snapshot (engine finished combat but room is still
        // CombatRoom). If rewards are pending, drain them through the engine
        // path the agent would drive — exactly mirroring DrainRewardsAsync's
        // logic but in-process. Any failure here dumps and stops the probe.
        if (!combat.IsInProgress)
        {
            if (snap.RewardsState is { Available.Count: > 0 } rs)
            {
                Console.WriteLine($"  combat ended, draining {rs.Available.Count} rewards");
                return !DrainRewardsOrDump(bindings, handle, snap, sts2);
            }
            // Combat ended, no rewards — the engine's auto-advance should
            // have moved us out of CombatRoom by now. If we're still here it's
            // a wire-side bug worth dumping.
            Console.WriteLine("  STALL: combat ended, no rewards, room still CombatRoom");
            DumpStall(bindings, handle, snap, sts2, action: "combat ended, no rewards pending, no advance");
            return true;
        }

        // Play one card per call to keep the loop's state-read fresh; the
        // outer while drives the next iteration. This matches the agent's
        // shape so a stall here matches what e2e tests actually trigger.
        var playable = combat.Hand.FirstOrDefault(c => c.CanPlay && c.Cost >= 0 && c.Cost <= combat.Energy);
        if (playable is not null)
        {
            var target = playable.TargetType == TargetType.AnyEnemy ? (int?)0 : null;
            bindings.PlayCard(handle, playable.Index, target);
            return false;
        }

        var roundBefore = combat.Round;
        Console.WriteLine($"  end-turn floor={snap.ActFloor} round={combat.Round} enemies={combat.Enemies.Count}");
        bindings.EndTurn(handle);
        var after = bindings.ReadSnapshot(handle);
        var afterCombat = after.CombatState;
        if (afterCombat is null || !afterCombat.IsInProgress)
        {
            // Combat ended via the end-turn (e.g. damage-over-time killed the
            // last enemy). Outer loop will re-read and hit the ended-combat
            // branch above.
            return false;
        }
        if (afterCombat.Round > roundBefore || afterCombat.IsPlayPhase)
        {
            // Forward progress.
            return false;
        }
        Console.WriteLine($"  STALL: end_turn did not advance round (round still {afterCombat.Round})");
        DumpStall(bindings, handle, after, sts2, action: $"after end_turn (roundBefore={roundBefore})");
        return true;
    }

    // Mirrors GreedyAgent.DrainRewardsAsync but operates on the in-process
    // bindings. Returns true on success, false on stall (probe should stop).
    private static bool DrainRewardsOrDump(Sts2Bindings bindings, RunHandle handle, RunSnapshot snap, Assembly sts2)
    {
        var rs = snap.RewardsState;
        for (var i = 0; rs is not null && rs.Available.Count > 0 && i < 50; i++)
        {
            var pick = rs.Available[0];
            Console.WriteLine($"    reward[{pick.Index}] kind={pick.Kind} canSkip={pick.CanSkip}"
                + (pick.GoldAmount is int g ? $" gold={g}" : "")
                + (pick.Cards is { Count: > 0 } cs ? $" cards={cs.Count}" : ""));
            try
            {
                if (pick.Kind == RewardKind.Card && pick.CanSkip)
                    bindings.SkipReward(handle, pick.Index);
                else if (pick.Kind == RewardKind.Card)
                    bindings.SelectReward(handle, pick.Index,
                        (pick.Cards?.Count ?? 0) > 0 ? pick.Cards![0].Index : 0);
                else
                    bindings.SelectReward(handle, pick.Index, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    reward claim THREW: {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
                DumpStall(bindings, handle, bindings.ReadSnapshot(handle), sts2, action: $"reward[{pick.Index}] claim threw");
                return false;
            }
            snap = bindings.ReadSnapshot(handle);
            rs = snap.RewardsState;
        }
        if (rs is { Available.Count: > 0 })
        {
            Console.WriteLine("  STALL: reward drain bailed with rewards still pending");
            DumpStall(bindings, handle, snap, sts2, action: "rewards not consumed after 50 iterations");
            return false;
        }
        var after = bindings.ReadSnapshot(handle);
        Console.WriteLine($"    after drain: room={after.CurrentRoomType} floor={after.ActFloor}");
        if (after.CurrentRoomType == RoomType.CombatRoom)
        {
            Console.WriteLine("  STALL: rewards drained but room still CombatRoom");
            DumpStall(bindings, handle, after, sts2, action: "after drain, room still CombatRoom");
            return false;
        }
        return true;
    }

    private static void DumpStall(Sts2Bindings bindings, RunHandle handle, RunSnapshot snap, Assembly sts2, string action)
    {
        Console.WriteLine($"    when: {action}");
        Console.WriteLine($"    floor={snap.ActFloor} room={snap.CurrentRoomType} hp={snap.CurrentHp}/{snap.MaxHp}");
        if (snap.RewardsState is { } r)
            Console.WriteLine($"    snapshot.rewards: {r.Available.Count} pending");
        else
            Console.WriteLine("    snapshot.rewards: null");
        if (snap.CombatState is { } c)
        {
            Console.WriteLine($"    combat: inProgress={c.IsInProgress} playPhase={c.IsPlayPhase} round={c.Round} energy={c.Energy}/{c.MaxEnergy}");
            Console.WriteLine($"    hand.count={c.Hand.Count} draw={c.DrawPileCount} discard={c.DiscardPileCount} block={c.PlayerBlock}");
            for (var i = 0; i < c.Enemies.Count; i++)
            {
                var e = c.Enemies[i];
                var intent = e.Intents.Count > 0 ? e.Intents[0].Kind.ToString() : "none";
                Console.WriteLine($"    enemy[{i}]: id={e.MonsterId} hp={e.Hp}/{e.MaxHp} block={e.Block} attacks={e.IntendsAttack} intent={intent}");
            }
        }

        // Reflective dump of the engine internals the wire DTOs don't surface.
        DumpEngineInternals(bindings, sts2);

        // Inventory of Wait*/Yield*/Delay* methods on sts2 types that return Task.
        // If any unpatched ones appear in stack frames during a stall, that's
        // the lead. We emit a short list — full inventory is too noisy.
        Console.WriteLine("    candidate-unpatched async hooks (top of sts2.dll matches):");
        foreach (var hit in EnumerateAsyncHooks(sts2).Take(40))
            Console.WriteLine($"      {hit}");
    }

    // Cheaply read CombatManager.Instance + RunManager fields by reflection so
    // we can sample state the wire doesn't surface. Uses BindingFlags rather
    // than the Sts2Bindings fields directly because those are private — keep
    // this command self-contained so its dump format can evolve without
    // exposing internal fields.
    private static void DumpEngineInternals(Sts2Bindings bindings, Assembly sts2)
    {
        try
        {
            var cmType = sts2.GetType("MegaCrit.Sts2.Core.Combat.CombatManager")
                       ?? sts2.GetType("MegaCrit.Sts2.Core.CombatManager");
            var cm = cmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (cm is null)
            {
                Console.WriteLine("    CombatManager.Instance is null");
                return;
            }
            Console.WriteLine("    CombatManager raw fields:");
            DumpInstanceMembers(cm, prefix: "      ", maxDepth: 1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    DumpEngineInternals threw: {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
        }

        try
        {
            var rmType = sts2.GetType("MegaCrit.Sts2.Core.RunManager");
            var rm = rmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (rm is null) return;
            var aeProp = rmType?.GetProperty("ActionExecutor", BindingFlags.Public | BindingFlags.Instance);
            var ae = aeProp?.GetValue(rm);
            if (ae is null)
            {
                Console.WriteLine("    RunManager.ActionExecutor is null");
                return;
            }
            Console.WriteLine("    ActionExecutor raw fields:");
            DumpInstanceMembers(ae, prefix: "      ", maxDepth: 1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    DumpEngineInternals (RunManager.ActionExecutor) threw: {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
        }
    }

    private static void DumpInstanceMembers(object instance, string prefix, int maxDepth)
    {
        if (maxDepth < 0) return;
        var t = instance.GetType();
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
        {
            if (p.GetIndexParameters().Length > 0) continue;
            object? v;
            try { v = p.GetValue(instance); }
            catch (Exception ex) { v = $"<threw {Diagnostics.Unwrap(ex).GetType().Name}>"; }
            Console.WriteLine($"{prefix}{p.Name}: {Render(v)}");
        }
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name))
        {
            object? v;
            try { v = f.GetValue(instance); }
            catch (Exception ex) { v = $"<threw {Diagnostics.Unwrap(ex).GetType().Name}>"; }
            Console.WriteLine($"{prefix}{f.Name}: {Render(v)}");
        }
    }

    private static string Render(object? v)
    {
        if (v is null) return "null";
        if (v is bool or int or long or float or double or string or Enum) return v.ToString() ?? "";
        if (v is System.Collections.ICollection coll) return $"[{coll.Count} items] {v.GetType().Name}";
        if (v is System.Threading.Tasks.Task t) return $"Task(IsCompleted={t.IsCompleted}, Status={t.Status})";
        return v.GetType().Name;
    }

    private static IEnumerable<string> EnumerateAsyncHooks(Assembly sts2)
    {
        Type?[] types;
        try { types = sts2.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types; }
        foreach (var t in types.OfType<Type>())
        {
            MethodInfo[] methods;
            try { methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly); }
            catch { continue; }
            foreach (var m in methods)
            {
                Type? rt;
                try { rt = m.ReturnType; }
                catch { continue; }
                if (rt is null || !typeof(System.Threading.Tasks.Task).IsAssignableFrom(rt)) continue;
                var n = m.Name;
                if (!n.StartsWith("Wait", StringComparison.Ordinal)
                    && !n.StartsWith("Yield", StringComparison.Ordinal)
                    && !n.Contains("Delay", StringComparison.Ordinal)) continue;
                string sig;
                try { sig = string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name)); }
                catch { sig = "<load-error>"; }
                yield return $"{t.FullName}.{n}({sig})";
            }
        }
    }

    private static ulong ParseSeed(string[] args, ulong defaultSeed)
    {
        var idx = Array.IndexOf(args, "--seed");
        if (idx < 0 || idx + 1 >= args.Length) return defaultSeed;
        return ulong.TryParse(args[idx + 1], out var v) ? v : defaultSeed;
    }

    private static int ParseInt(string[] args, string flag, int defaultValue)
    {
        var idx = Array.IndexOf(args, flag);
        if (idx < 0 || idx + 1 >= args.Length) return defaultValue;
        return int.TryParse(args[idx + 1], out var v) ? v : defaultValue;
    }
}

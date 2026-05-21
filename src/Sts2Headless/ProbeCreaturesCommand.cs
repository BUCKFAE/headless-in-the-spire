using System.Reflection;
using Sts2Headless.Runtime;

namespace Sts2Headless;

// `just probe-creatures <encounter-id>` — start a run, force-enter the
// combat, then walk every reachable Creature-typed reference inside
// CombatManager.DebugOnlyGetState() and each spawned enemy. Reports
// every entity's runtime type, HP, alive-ness, and parentage.
//
// Built for the Doormaker hypothesis (b): the wire's enemy projection
// reads CombatState.Enemies and filters by IsAlive — if Doormaker holds
// a dormant Door child Creature (or the engine carries a separate
// _combatants list that includes monsters the wire skips), it'd be
// invisible to every existing probe. This walks the full graph so
// hidden entities surface.
internal static class ProbeCreaturesCommand
{
    public static int Run(string vendorDir, string[] args)
    {
        var idx = Array.IndexOf(args, "--probe-creatures");
        var encounterId = idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : "";
        if (string.IsNullOrWhiteSpace(encounterId))
        {
            Console.Error.WriteLine("usage: --probe-creatures <encounter-id>");
            return 1;
        }

        var preamble = RuntimeBootstrap.Run(vendorDir);
        if (preamble.SetupError is not null)
        {
            Console.Error.WriteLine($"  bootstrap setup failed: {preamble.SetupError}");
            return 1;
        }
        foreach (var s in BootstrapSequence.Apply(preamble.Sts2!))
            if (!s.Ok) Console.Error.WriteLine($"  WARN: bootstrap '{s.Label}': {s.Detail}");

        Sts2Bindings bindings;
        try { bindings = Sts2Bindings.Bind(preamble.Sts2!, preamble.SyncContext); }
        catch (Exception ex) { Console.Error.WriteLine($"  bind failed: {ex.Message}"); return 1; }

        // Optional `--turns N` to end N player turns before dumping state.
        // Some bosses spawn pets / phase entities during their first move
        // sequence rather than at combat start; this lets us watch the
        // graph evolve. (Doormaker's first move is DramaticOpenMove —
        // worth probing after it fires to see if a Door pet spawns.)
        var turnsIdx = Array.IndexOf(args, "--turns");
        var turns = turnsIdx >= 0 && turnsIdx + 1 < args.Length
            && int.TryParse(args[turnsIdx + 1], out var t) ? t : 0;

        var handle = bindings.StartIroncladRun(seed: 42, withNeow: false);
        bindings.SetPlayerHp(handle, 999, 999);
        var (inProgress, enemyCount) = bindings.StartCombat(handle, encounterId);
        Console.WriteLine($"start_combat: encounter={encounterId} inProgress={inProgress} wireEnemyCount={enemyCount}");

        for (var i = 0; i < turns; i++)
        {
            try { bindings.EndTurn(handle); }
            catch (Exception ex) { Console.WriteLine($"  end_turn {i + 1} threw: {ex.GetType().Name}: {ex.Message}"); break; }
            var snap = bindings.ReadSnapshot(handle);
            var c = snap.CombatState;
            if (c is null || !c.IsInProgress) { Console.WriteLine($"  combat ended after {i + 1} turn(s)"); break; }
            Console.WriteLine($"  after end_turn {i + 1}: round={c.Round} enemies=[{string.Join(",", c.Enemies.Select(e => $"{e.MonsterId}:hp={e.Hp}/{e.MaxHp}"))}]");
        }

        var sts2 = preamble.Sts2!;
        var cmType = sts2.GetType("MegaCrit.Sts2.Core.Combat.CombatManager")
                   ?? sts2.GetType("MegaCrit.Sts2.Core.CombatManager");
        var cm = cmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (cm is null) { Console.WriteLine("CombatManager.Instance is null"); return 0; }

        var getState = cmType!.GetMethod("DebugOnlyGetState", BindingFlags.Public | BindingFlags.Instance);
        var state = getState?.Invoke(cm, null);
        Console.WriteLine($"CombatManager.DebugOnlyGetState() => {state?.GetType().FullName ?? "null"}");
        if (state is null) return 0;

        // Doormaker-specific diagnostic: name the boss's current move
        // and any powers attached. Tells us whether the move state
        // machine is advancing or stuck on DramaticOpenMove forever.
        if (state.GetType().GetProperty("Enemies") is { } enemiesProp
            && enemiesProp.GetValue(state) is System.Collections.IEnumerable enemyList)
        {
            foreach (var enemy in enemyList)
            {
                if (enemy is null) continue;
                var monsterProp = enemy.GetType().GetProperty("Monster", BindingFlags.Public | BindingFlags.Instance);
                var monster = monsterProp?.GetValue(enemy);
                if (monster is null) continue;
                var nextMove = monster.GetType().GetProperty("NextMove", BindingFlags.Public | BindingFlags.Instance)?.GetValue(monster);
                // Powers live on the Creature, not the Monster (the wire's
                // ReadEnemies reads _enemyPowers off the enemy wrapper).
                var powers = enemy.GetType().GetProperty("Powers", BindingFlags.Public | BindingFlags.Instance)?.GetValue(enemy);
                var powerSummary = "<unknown>";
                if (powers is System.Collections.IEnumerable powerList)
                {
                    var names = new List<string>();
                    foreach (var p in powerList)
                    {
                        if (p is null) continue;
                        var id = p.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(p)?.ToString();
                        var amount = p.GetType().GetProperty("Amount", BindingFlags.Public | BindingFlags.Instance)?.GetValue(p)?.ToString();
                        names.Add(id is null ? p.GetType().Name : (amount is null ? id : $"{id}:{amount}"));
                    }
                    powerSummary = names.Count == 0 ? "<none>" : string.Join(",", names);
                }
                // MoveState's "name" lives in a Title / DisplayName / Id-shaped
                // field. Dump every string-typed member so we can see what's
                // there without guessing.
                var moveDesc = "<null>";
                if (nextMove is not null)
                {
                    var bits = new List<string>();
                    foreach (var p in nextMove.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (p.GetIndexParameters().Length > 0) continue;
                        if (p.PropertyType != typeof(string)) continue;
                        try { bits.Add($"{p.Name}={p.GetValue(nextMove)}"); } catch { }
                    }
                    foreach (var f in nextMove.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (f.FieldType != typeof(string)) continue;
                        try { bits.Add($"{f.Name}={f.GetValue(nextMove)}"); } catch { }
                    }
                    moveDesc = bits.Count == 0 ? nextMove.GetType().Name : string.Join(" ", bits);
                }
                Console.WriteLine($"  diag enemy: {enemy.GetType().Name} monster={monster.GetType().Name} nextMove=[{moveDesc}] powers={powerSummary}");
            }
        }

        // Pass 1: every field on the state object, with kind annotation.
        // Surfaces any collection we don't currently read (a `_combatants`
        // list distinct from `Enemies`, a `_props` list, etc).
        Console.WriteLine("=== CombatState fields (all) ===");
        foreach (var f in state.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            object? v;
            try { v = f.GetValue(state); }
            catch (Exception ex) { Console.WriteLine($"  {f.Name}: <{ex.GetType().Name}>"); continue; }
            var kind = ClassifyValue(v);
            Console.WriteLine($"  {f.Name} [{f.FieldType.Name}] = {kind}");
        }
        Console.WriteLine("=== CombatState properties (all) ===");
        foreach (var p in state.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length > 0) continue;
            object? v;
            try { v = p.GetValue(state); }
            catch (Exception ex) { Console.WriteLine($"  {p.Name}: <{ex.GetType().Name}>"); continue; }
            var kind = ClassifyValue(v);
            Console.WriteLine($"  {p.Name} [{p.PropertyType.Name}] = {kind}");
        }

        // Pass 2: walk every Creature-typed reference reachable from
        // `state`, regardless of IsAlive — find the enemies list, then
        // for each enemy walk ITS fields for nested Creature children.
        Console.WriteLine("=== Reachable creatures (alive or not) ===");
        var creatureType = sts2.GetType("MegaCrit.Sts2.Core.Entities.Creatures.Creature");
        Console.WriteLine($"  Creature type resolved: {creatureType?.FullName ?? "<unresolved>"}");

        var visited = new HashSet<object>(ReferenceComparer.Instance);
        var queue = new Queue<(object Obj, string Path)>();
        queue.Enqueue((state, "state"));
        var depthCap = 7; // deeper than the original 4 so Doormaker.Pets[N] and similar nested-collection children land in the dump
        var currentDepth = 0;
        var nextDepthBoundary = queue.Count;
        var found = 0;

        while (queue.Count > 0 && currentDepth <= depthCap)
        {
            var (obj, path) = queue.Dequeue();
            foreach (var (childPath, child) in EnumerateChildren(obj, path))
            {
                if (child is null) continue;
                var ct = child.GetType();
                // Match by namespace path so e.g. `Creature` vs `CreatureModel`
                // vs an internal `CreatureState` all surface. The wire reads
                // `.Monster` on each list item — strongly implying the item
                // is a Creature/Combatant wrapper around a MonsterModel.
                var isCreatureLike = ct.FullName?.Contains("Creature", StringComparison.Ordinal) == true
                    || ct.FullName?.Contains("Combatant", StringComparison.Ordinal) == true
                    || ct.FullName?.Contains("Monster", StringComparison.Ordinal) == true;
                if (isCreatureLike && ct.Namespace?.StartsWith("MegaCrit") == true)
                {
                    Console.WriteLine($"  + {childPath}: {ct.FullName} {DescribeCreature(child)}");
                    found++;
                }
                // Enqueue if we want to recurse: MegaCrit objects to walk
                // their state, OR any non-string IEnumerable so we look
                // inside collections (List<Creature>.GetType() lives in
                // System.Collections.Generic, not MegaCrit — without this
                // we'd never see the items the wire reads).
                var isCollection = child is System.Collections.IEnumerable && child is not string;
                var isMegaCrit = ct.Namespace?.StartsWith("MegaCrit") == true;
                if ((isMegaCrit || isCollection)
                    && !visited.Contains(child)
                    && !ct.IsValueType)
                {
                    if (isCollection && childPath.EndsWith("Pets", StringComparison.Ordinal))
                    {
                        var n = 0;
                        foreach (var _ in (System.Collections.IEnumerable)child) n++;
                        Console.WriteLine($"    [diag] {childPath} array length = {n}");
                    }
                    visited.Add(child);
                    queue.Enqueue((child, childPath));
                }
            }
            if (--nextDepthBoundary == 0)
            {
                currentDepth++;
                nextDepthBoundary = queue.Count;
            }
        }
        Console.WriteLine($"  -- {found} creature reference(s) found (depth ≤ {depthCap}) --");
        return 0;
    }

    private static IEnumerable<(string Path, object? Child)> EnumerateChildren(object obj, string path)
    {
        if (obj is System.Collections.IEnumerable enumerable && obj is not string)
        {
            var i = 0;
            foreach (var item in enumerable)
            {
                yield return ($"{path}[{i++}]", item);
            }
            yield break;
        }
        var t = obj.GetType();
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            object? v;
            try { v = f.GetValue(obj); } catch { continue; }
            yield return ($"{path}.{f.Name}", v);
        }
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length > 0) continue;
            object? v;
            try { v = p.GetValue(obj); } catch { continue; }
            yield return ($"{path}.{p.Name}", v);
        }
    }

    private static string ClassifyValue(object? v)
    {
        if (v is null) return "null";
        if (v is string s) return $"\"{s}\"";
        if (v is System.Collections.ICollection c) return $"<{v.GetType().Name} count={c.Count}>";
        if (v is System.Collections.IEnumerable e)
        {
            var n = 0; foreach (var _ in e) n++;
            return $"<{v.GetType().Name} enum-count={n}>";
        }
        if (v.GetType().IsValueType) return v.ToString() ?? "<value>";
        return $"<{v.GetType().Name}>";
    }

    private static string DescribeCreature(object creature)
    {
        var t = creature.GetType();
        string get(string name)
        {
            try
            {
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (p is not null) return p.GetValue(creature)?.ToString() ?? "?";
                var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f is not null) return f.GetValue(creature)?.ToString() ?? "?";
            }
            catch { }
            return "?";
        }
        return $"hp={get("CurrentHp")}/{get("MaxHp")} alive={get("IsAlive")} dead={get("IsDead")}";
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new();
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}

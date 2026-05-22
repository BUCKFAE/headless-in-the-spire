using System.Reflection;
using Sts2Headless.Runtime;

namespace Sts2Headless.Commands;

// `just probe-encounter <encounter-id>` — start a run, force-enter the
// given combat via the same engine path debug/start_combat uses, dump
// the spawned monsters' real runtime types (their FQN, not just the
// MonsterId enum), then play the first hand card with target=enemy[0].
// Any thrown exception is fully unwrapped to stderr — the diagnostic
// value is the stacktrace into sts2.dll that the sweep test only
// sees wrapped as "internal error: NullReferenceException".
//
// Use this to find which on-play / on-damage hook is the offender on
// the play_card-NRE encounters (KAISER_CRAB_BOSS, LAGAVULIN_MATRIARCH_BOSS).
internal static class ProbeEncounterCommand
{
    public static int Run(string vendorDir, string[] args)
    {
        var idx = Array.IndexOf(args, "--probe-encounter");
        var encounterId = idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : "";
        if (string.IsNullOrWhiteSpace(encounterId))
        {
            Console.Error.WriteLine("usage: --probe-encounter <encounter-id> [--deck CARD_ID[:upgrade],…]");
            return 1;
        }

        // Optional `--deck CARD:upgrade,CARD,…` to override the default
        // Hellraiser+Pommel probe deck. Used when the default deck dodges
        // the bug we're hunting (e.g. QUEEN_BOSS NREs on BLUDGEON-class
        // decks but the auto-play chain routes around it).
        var deckIdx = Array.IndexOf(args, "--deck");
        var overrideDeck = deckIdx >= 0 && deckIdx + 1 < args.Length
            ? args[deckIdx + 1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s =>
                {
                    var parts = s.Split(':');
                    return (Id: parts[0], Up: parts.Length > 1 && int.TryParse(parts[1], out var u) ? u : 0);
                })
                .ToArray()
            : null;

        // Optional `--target <int>` to override the target index for
        // AnyEnemy cards. Defaults to 0 (first enemy). The agent in the
        // sweep targets by threat priority, which may pick a different
        // index than the probe's hardcoded 0 — and the engine NRE may
        // only fire on a specific target.
        var targetIdx = Array.IndexOf(args, "--target");
        int? targetOverride = targetIdx >= 0 && targetIdx + 1 < args.Length
            && int.TryParse(args[targetIdx + 1], out var tv) ? tv : null;

        // Optional `--relics RELIC_ID,RELIC_ID,…` to grant relics before
        // start_combat. The encounter sweep grants TOUGH_BANDAGES; passing
        // it here matches the sweep's exact setup so probe results
        // mirror the bug-surface state.
        var relicsIdx = Array.IndexOf(args, "--relics");
        var relics = relicsIdx >= 0 && relicsIdx + 1 < args.Length
            ? args[relicsIdx + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Array.Empty<string>();

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
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  bind failed: {Diagnostics.Describe(Diagnostics.Unwrap(ex))}");
            return 1;
        }

        var handle = bindings.StartIroncladRun(seed: 42, withNeow: false);
        Console.WriteLine($"probe-encounter: started Ironclad run, encounter={encounterId}");

        // Replace deck with the sweep's Hellraiser + Pommel×2 set and pump HP,
        // unless --deck overrides it.
        var deck = overrideDeck ?? new[]
        {
            (Id: "HELLRAISER", Up: 0), (Id: "POMMEL_STRIKE", Up: 0), (Id: "POMMEL_STRIKE", Up: 0),
        };
        Console.WriteLine($"  deck: {string.Join(", ", deck.Select(c => c.Up > 0 ? $"{c.Id}+{c.Up}" : c.Id))}");
        bindings.ReplaceDeck(handle, deck.Select(c => (c.Id, c.Up)).ToArray());
        bindings.SetPlayerHp(handle, 999, 999);
        foreach (var relicId in relics)
        {
            Console.WriteLine($"  relic: {relicId}");
            bindings.GiveRelic(handle, relicId);
        }

        var (inProgress, enemyCount) = bindings.StartCombat(handle, encounterId);
        Console.WriteLine($"  start_combat: inProgress={inProgress} enemyCount={enemyCount}");

        // Dump real monster types via reflection — what GenerateMonsters returned.
        var sts2 = preamble.Sts2!;
        var cmType = sts2.GetType("MegaCrit.Sts2.Core.Combat.CombatManager")
                   ?? sts2.GetType("MegaCrit.Sts2.Core.CombatManager");
        var cm = cmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (cm is null)
        {
            Console.WriteLine("  CombatManager.Instance is null");
            return 0;
        }
        System.Collections.IEnumerable? enemies = null;
        var getState = cmType!.GetMethod("DebugOnlyGetState", BindingFlags.Public | BindingFlags.Instance);
        var state = getState?.Invoke(cm, null);
        Console.WriteLine($"  CombatManager.DebugOnlyGetState() => {state?.GetType().FullName ?? "null"}");
        if (state is not null)
        {
            foreach (var f in state.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.Name.Contains("Enem", StringComparison.OrdinalIgnoreCase)
                    || f.Name.Contains("Monst", StringComparison.OrdinalIgnoreCase)
                    || f.Name.Contains("Creat", StringComparison.OrdinalIgnoreCase))
                {
                    var v = f.GetValue(state);
                    Console.WriteLine($"    field {f.Name}: {v?.GetType().FullName ?? "null"}");
                    if (v is System.Collections.IEnumerable e && enemies is null) enemies = e;
                }
            }
        }
        if (enemies is not null)
        {
            Console.WriteLine("  spawned monster types:");
            int i = 0;
            foreach (var e in enemies)
            {
                Console.WriteLine($"    [{i++}] {e?.GetType().FullName ?? "<null>"}");
                if (e is null) continue;
                // Dump the monster's declared methods so any swallowed-NRE
                // suspects (lifecycle / damage hooks) are visible at a glance.
                var t = e.GetType();
                while (t is not null && t.Namespace?.StartsWith("MegaCrit") == true)
                {
                    Console.WriteLine($"      type: {t.FullName}");
                    MethodInfo[] methods;
                    try { methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly); }
                    catch { methods = []; }
                    foreach (var m in methods.Where(m =>
                        !m.IsSpecialName
                        && typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType)))
                    {
                        Console.WriteLine($"        {m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}) -> Task");
                    }
                    t = t.BaseType;
                }
            }
        }

        if (!inProgress)
        {
            Console.WriteLine("  combat did not enter progress; skipping play_card probe");
            return 0;
        }

        // Drive the agent's play pattern: play every available card on the
        // first enemy, then end turn, repeat for a few rounds. Any thrown
        // exception surfaces unwrapped — the NRE we're hunting is somewhere
        // in this sequence on the KAISER_CRAB / LAGAVULIN_MATRIARCH paths.
        // 20 rounds is enough to kill weak enemies and surface post-kill
        // SUMMON / on-death engine hooks — most boss-side bugs that
        // depend on combat duration show up well inside that window.
        for (var round = 0; round < 50; round++)
        {
            var snap = bindings.ReadSnapshot(handle);
            var c = snap.CombatState;
            if (c is null || !c.IsInProgress)
            {
                Console.WriteLine($"  combat ended (round={round})");
                break;
            }
            Console.WriteLine($"  round={c.Round} energy={c.Energy}/{c.MaxEnergy} hand={c.Hand.Count} enemies=[{string.Join(",", c.Enemies.Select(e => $"{e.MonsterId}:hp={e.Hp}/{e.MaxHp}"))}]");
            var playedThisRound = 0;
            for (var i = 0; i < c.Hand.Count && playedThisRound < 6; i++)
            {
                var card = c.Hand[i];
                if (!card.CanPlay || card.Cost < 0 || card.Cost > c.Energy) continue;
                var target = card.TargetType == Sts2Headless.Protocol.Methods.TargetType.AnyEnemy ? (targetOverride ?? 0) : (int?)null;
                try
                {
                    Console.WriteLine($"    play[{i}] {card.Id} cost={card.Cost} target={target}");
                    bindings.PlayCard(handle, i, target);
                    playedThisRound++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  play_card THREW:");
                    var cur = Diagnostics.Unwrap(ex);
                    while (cur is not null)
                    {
                        Console.WriteLine($"    {cur.GetType().FullName}: {cur.Message}");
                        Console.WriteLine(cur.StackTrace);
                        cur = cur.InnerException;
                    }
                    return 0;
                }
                snap = bindings.ReadSnapshot(handle);
                c = snap.CombatState;
                if (c is null || !c.IsInProgress) { Console.WriteLine("  combat ended mid-round"); return 0; }
                // hand indexes shift after play; restart from 0.
                i = -1;
            }
            try
            {
                bindings.EndTurn(handle);
                Console.WriteLine($"    end_turn");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  end_turn THREW:");
                var cur = Diagnostics.Unwrap(ex);
                while (cur is not null)
                {
                    Console.WriteLine($"    {cur.GetType().FullName}: {cur.Message}");
                    Console.WriteLine(cur.StackTrace);
                    cur = cur.InnerException;
                }
                return 0;
            }
        }
        return 0;
    }
}

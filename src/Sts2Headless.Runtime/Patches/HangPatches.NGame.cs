using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Sts2Headless.Runtime.Patches;

// Vantom.DismemberMove has one unguarded `NGame.Instance.DoHitStop(2, 1)`
// call (state-machine IL 198-201). Every other NGame/NCombatRoom singleton
// access in this method body is null-gated with the standard
// `dup; brtrue; pop; ldnull; br` pattern — this one isn't. It's a real
// game-code bug that doesn't surface in production because the Godot scene
// tree always sets NGame.Instance non-null; headless has no tree, so
// `callvirt NGame.DoHitStop` NREs on a null receiver. The async state
// machine swallows that NRE into a Task.SetException, the enemy turn
// handler logs nothing and never restores `IsPlayPhase = true`, and the
// caller observes Round=3, IsPlayPhase=False forever.
//
// Why a transpiler instead of a Harmony prefix on DoHitStop: callvirt
// checks the receiver for null *before* dispatching the call. A prefix on
// DoHitStop's body can only run if the JIT actually gets to the dispatch;
// with a null receiver, the CLR throws NRE at the callvirt itself. The
// only ways to fix this at the call site are (a) install a non-null
// NGame.Instance stub and patch every method on the type (too invasive
// — NGame is the engine root singleton, and property getters touch
// uninitialized backing fields), or (b) excise the bad call from
// DismemberMove's IL via transpiler. We do (b).
//
// Transpiler shape: walk the method's IL, find the
// `call NGame.get_Instance` immediately followed by two `ldc.i4` (the
// HitStop args) and a `callvirt NGame.DoHitStop`, and replace those four
// instructions with no-ops. The surrounding await chain (Cmd.Wait,
// AttackCommand.Execute, AddToCombatAndPreview<Wound>) is left
// untouched, so the gameplay still runs in full — boss attacks the
// player, three Wound cards land in the discard pile.
public static partial class HangPatches
{
    private static PatchOutcome PatchVantomDismemberMoveDoHitStop(Harmony harmony, Assembly sts2)
    {
        const string label = "MegaCrit.Sts2.Core.Models.Monsters.Vantom.DismemberMove (IL transpile: skip unguarded NGame.DoHitStop)";

        var vantom = sts2.GetType("MegaCrit.Sts2.Core.Models.Monsters.Vantom");
        if (vantom is null)
            return new PatchOutcome(label, Patched: false, Detail: "Vantom type not found");

        var move = vantom.GetMethod("DismemberMove",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (move is null)
            return new PatchOutcome(label, Patched: false, Detail: "DismemberMove method not found");

        // DismemberMove is `async Task`; the gameplay IL lives on the
        // compiler-generated state machine's MoveNext.
        var asyncAttr = move.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.Name == "AsyncStateMachineAttribute");
        if (asyncAttr is null || asyncAttr.ConstructorArguments.Count != 1
            || asyncAttr.ConstructorArguments[0].Value is not Type smType)
            return new PatchOutcome(label, Patched: false, Detail: "no AsyncStateMachine attribute on DismemberMove");

        var moveNext = smType.GetMethod("MoveNext",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (moveNext is null)
            return new PatchOutcome(label, Patched: false, Detail: $"MoveNext not found on {smType.FullName}");

        var transpiler = typeof(HangPatches).GetMethod(
            nameof(SkipNGameDoHitStopTranspiler),
            BindingFlags.Static | BindingFlags.NonPublic)!;
        harmony.Patch(moveNext, transpiler: new HarmonyMethod(transpiler));
        return new PatchOutcome(label, Patched: true, Detail: $"transpiler installed on {smType.FullName}.MoveNext");
    }

    // The transpiler: rewrite the call sequence
    //
    //     call    NGame.get_Instance
    //     ldc.i4  <int>          (HitStopType arg)
    //     ldc.i4  <int>          (some bool/enum arg)
    //     callvirt NGame.DoHitStop
    //
    // into four no-ops. The pre-call stack is empty and DoHitStop returns
    // void, so removing the entire 4-instruction sequence is balanced.
    // Using `Nop` (rather than removing the instructions outright) keeps
    // branch offsets identical so we don't have to rebuild jump targets.
    private static IEnumerable<CodeInstruction> SkipNGameDoHitStopTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var list = instructions.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            // Anchor on the unique `callvirt NGame.DoHitStop` — there's
            // exactly one in DismemberMove. Walk backwards to identify
            // the matching `call NGame.get_Instance` (3 instructions
            // earlier on the canonical shape) and the two ldc.i4 in
            // between. Whoever owns the IL might tweak HitStopType /
            // bool arg constants on a future game version; we don't
            // assert their values, only that the shape is intact.
            var ins = list[i];
            if (ins.opcode != OpCodes.Callvirt) continue;
            if (ins.operand is not MethodBase mb) continue;
            if (mb.Name != "DoHitStop") continue;
            if (mb.DeclaringType?.FullName != "MegaCrit.Sts2.Core.Nodes.NGame") continue;
            if (i < 3) continue;

            var prior = list[i - 3];
            if (prior.opcode != OpCodes.Call) continue;
            if (prior.operand is not MethodBase getInst) continue;
            if (getInst.Name != "get_Instance") continue;
            if (getInst.DeclaringType?.FullName != "MegaCrit.Sts2.Core.Nodes.NGame") continue;

            // Match. Replace the 4-instruction window with Nops.
            for (int k = i - 3; k <= i; k++)
            {
                list[k] = new CodeInstruction(OpCodes.Nop);
            }
        }
        return list;
    }
}

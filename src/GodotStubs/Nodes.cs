// Empty type shells matching what sts2.dll's metadata references. Bodies are
// intentionally minimal — these types never run; they exist so the metadata
// loader resolves every TypeRef. Members are added on demand only when a
// caller forces it (runtime MissingMethodException / TypeLoadException).
//
// The MethodName / PropertyName / SignalName pattern mirrors real
// GodotSharp's source-generated layout: every node-like class gets its own
// nested helpers that inherit from the parent's. sts2's own SG-emitted
// user types extend these, so they must exist nested in each Godot class
// that sts2 directly subclasses.
//
// This file holds the GodotObject → Node → CanvasItem → Node2D hierarchy.
// Resource-rooted types live in Resources.cs; the Control branch lives in
// Controls.cs.

namespace Godot;

public class GodotObject
{
    public class MethodName { }
    public class PropertyName { }
    public class SignalName { }

    // from: CombatManager.AfterCombatRoomLoaded — `await ToSignal(target, …)`
    //   used pervasively to wait for animation/UI events. In headless we
    //   return an awaiter that's already complete so the chain doesn't park.
    public SignalAwaiter ToSignal(GodotObject source, StringName signal)
        => new(source, signal, this);

    // from: AtlasManager.GetSprite (and AutoSlay/WaitHelper paths) call
    //   GodotObject.IsInstanceValid to check whether a cached node was freed
    //   by Godot. We hold references so nothing is "freed"; returning true
    //   keeps the cached value path. Surfaced once Engine.GetMainLoop became
    //   non-null and unblocked the surrounding code.
    public static bool IsInstanceValid(GodotObject? _) => true;

    // from: GodotTreeExtensions.AddChildSafely's off-main-thread branch:
    //   `((GodotObject)parent).CallDeferred(MethodName.AddChild,
    //     (Variant[])(object)new Variant[1] { Variant.op_Implicit(child) })`.
    //   Required by Harmony's IL copier (HangPatches.Cards
    //   .PatchAddChildSafelyNullParent) even though the live path takes
    //   the main-thread branch above it. Real Godot dispatches the call
    //   on the engine's deferred queue; headless owns no queue, so the
    //   no-op matches the rest of the AddChild side-effect surface.
    public Variant CallDeferred(StringName method, params Variant[] args) => default;
}

public class Node : GodotObject
{
    public new class MethodName : GodotObject.MethodName
    {
        public static readonly StringName AddChild = new("add_child");
        public static readonly StringName QueueFree = new("queue_free");
        public static readonly StringName RemoveChild = new("remove_child");
    }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName
    {
        public static readonly StringName ChildEnteredTree = new("child_entered_tree");
        public static readonly StringName ChildExitingTree = new("child_exiting_tree");
        public static readonly StringName Ready = new("ready");
        public static readonly StringName TreeExited = new("tree_exited");
        public static readonly StringName TreeExiting = new("tree_exiting");
    }

    // from: EventOption.Chosen → CardModel.GetTaughtUpgradedTag chain →
    //   Node.set_Name(StringName). MissingMethodException at first option
    //   pick. Auto-property is enough; nothing reads it back in headless.
    public StringName Name { get; set; } = new(string.Empty);

    // from: CombatManager.AfterCombatRoomLoaded — instantiates animation
    //   tweens via Node.CreateTween(). The Tween is enqueued via Callable
    //   wrappers; no animations run in headless so a fresh stub instance
    //   is enough for the chain to type-check and complete.
    public Tween CreateTween() => new();
    public SceneTree? GetTree() => null;

    // from: NDamageNumVfx.Create reads
    //   ((Node)SceneTree.Root).GetViewport().GetVisibleRect() during enemy
    //   attacks. Real Godot returns the ancestor Viewport; in headless we
    //   return a singleton stub so the chain falls through to GetVisibleRect
    //   (which yields Rect2.Zero → Vector2.Zero spawn pos → TestMode short-
    //   circuit in Create(Vector2, int)). Other callers (NScrollableContainer,
    //   NPotionHolder, …) are GUI-only and never execute under our drive.
    private static readonly Viewport _stubViewport = new();
    public Viewport? GetViewport() => _stubViewport;
    public bool IsInsideTree() => false;
    public void QueueFree() { }

    // from: CardPileCmd.Shuffle gates Cmd.Wait on
    //   waitTimeAccumulator >= ((Node)SceneTree.Root).GetProcessDeltaTime().
    //   Cmd.Wait is Harmony-patched to no-op in headless, so the gate value
    //   doesn't matter; 0.0 keeps the comparison `acc >= 0` always true and
    //   matches the "no frame elapsed" reality.
    public double GetProcessDeltaTime() => 0.0;

    // from: MegaCrit.Sts2.Core.Models.Monsters.Flyconid.VulnerableSporesMove
    //   (and other monster moves) calls `GetNode<T>(NodePath)` to fetch a
    //   VFX node for the move's animation. Without this stub the missing-
    //   method exception is thrown inside the enemy turn's async chain, is
    //   swallowed by TaskHelper.LogTaskExceptions, and leaves CombatManager
    //   half-transitioned (EndingPlayerTurnPhaseTwo=True, IsEnemyTurnStarted=
    //   True, IsInProgress=True) — the "combat stall" reported in
    //   agent-survival-gaps.md. Returning null lets the call complete; if
    //   the caller NREs, that exception still gets swallowed the same way,
    //   but at least the move can short-circuit past the VFX in moves that
    //   null-check the result. Generic param is constrained to class in real
    //   Godot's signature; we don't enforce here so any T resolves.
    public T? GetNode<T>(NodePath _) where T : class => null;
    public Node? GetNode(NodePath _) => null;

    // from: SPINY_TOAD's Buff intent (Act 2 seed 42 floor 8) calls
    //   Node.GetChildren(includeInternal). Real Godot returns the live
    //   child list; in headless the node has no children, so an empty
    //   collection lets the iteration short-circuit without NREs.
    public Collections.Array<Node> GetChildren(bool _ = false) => new();
    // No scene tree in headless → no nodes are ancestors of anything,
    // every node is its own root, no parent to fetch.
    public bool IsAncestorOf(Node _) => false;
    public Node? GetParent() => null;

    // from: GodotTreeExtensions.AddChildSafely → parent.AddChild(child,
    //   false, (InternalMode)0). Required by Harmony's IL copier when
    //   patching AddChildSafely's null-parent path (HangPatches.Cards
    //   .PatchAddChildSafelyNullParent) — Harmony walks the IL of the
    //   method-to-patch and resolves every callee through reflection,
    //   so the signature has to be present even when no live caller
    //   hits it. Real Godot bumps the engine's scene-tree state; in
    //   headless we own nothing to add to, so this is a no-op.
    // The InternalMode enum lives nested inside Node in real Godot 4
    // (Godot.Node.InternalMode); the sts2.dll IL bakes that nesting
    // into the method-ref, so the stub must match the nesting exactly
    // or Harmony's ResolveMethodHandle fails with MissingMethodException
    // ("Method not found: 'Void Godot.Node.AddChild(Godot.Node, Boolean,
    // InternalMode)'").
    public enum InternalMode
    {
        Disabled = 0,
        Front = 1,
        Back = 2,
    }

    public void AddChild(Node node, bool forceReadableName = false, InternalMode @internal = InternalMode.Disabled) { }

    // from: same chain — RemoveChildSafely's body. Pair with AddChild so
    //   Harmony's copier resolves both when patching either method.
    public void RemoveChild(Node node) { }

    public int GetChildCount(bool _ = false) => 0;
    public void MoveChild(Node _, int __) { }
}

public class CanvasItem : Node
{
    public new class MethodName : Node.MethodName { }
    public new class PropertyName : Node.PropertyName { }
    public new class SignalName : Node.SignalName
    {
        public static readonly StringName ItemRectChanged = new("item_rect_changed");
        public static readonly StringName VisibilityChanged = new("visibility_changed");
    }

    // from: VFX nodes call CanvasItem.GetViewportRect() to size their effect to
    //   the visible area. Mirrors Node.GetViewport().GetVisibleRect() shape;
    //   Rect2.default is the zero rect which the engine VFX code generally
    //   treats as "off-screen / no-op."
    public Rect2 GetViewportRect() => default;

    // from: a card-play VFX path on the Pommel/Hellraiser combo route
    //   (surfaced on seed 42 Act 2 floor 12 with the deck-replace cheat).
    //   No-op in headless: there's no visual to modulate.
    public void SetSelfModulate(Color _) { }

    // from: CardSweep — 9 cards (AFTERLIFE, BODYGUARD, CLEANSE, DIRGE,
    //   LEGION_OF_BONE, NECRO_MASTERY, PULL_AGGRO, REANIMATE, SPUR) all
    //   crashed on a missing `set_Modulate(Godot.Color)`. CanvasItem.Modulate
    //   is a Color property in real Godot; the engine sets it to tint a
    //   sprite. Auto-property with white-default; nothing reads it back
    //   in headless paths.
    public Color Modulate { get; set; } = new Color(1f, 1f, 1f, 1f);
}

// ── Node2D and descendants ──────────────────────────────────────────────

public class Node2D : CanvasItem
{
    public new class MethodName : CanvasItem.MethodName { }
    public new class PropertyName : CanvasItem.PropertyName { }
    public new class SignalName : CanvasItem.SignalName { }

    // from: Neow path during EnterAct (PROBE_NEOW=1) — caught by GD.PushError
    //   "Method not found: 'Void Godot.Node2D.set_Position(Godot.Vector2)'."
    //   NEventRoom places child nodes at a Position. Auto-property is
    //   enough; nothing reads it back in headless paths.
    public Vector2 Position { get; set; }

    // from: enemy moves (multiple monsters) place their attack VFX at a
    //   global-space target. Without this stub the missing-method exception
    //   thrown inside the move's async chain is swallowed by
    //   TaskHelper.LogTaskExceptions and the enemy turn never completes —
    //   the second-most-common combat stall surfaced by probe-combat-stall.
    //   Auto-property is enough; nothing reads it back in headless.
    public Vector2 GlobalPosition { get; set; }

    // from: act-1 boss-tier monster moves (KIN_FOLLOWER, KIN_PRIEST) read
    //   Scale to retune their sprite when entering an attack pose. Surfaces
    //   as `Method not found: 'Godot.Vector2 Godot.Node2D.get_Scale()'.` in
    //   the enemy-turn fire-and-forget chain — same swallow path as
    //   GlobalPosition above. Auto-property; nothing reads it back.
    public Vector2 Scale { get; set; }
}

public class Sprite2D : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }

    // from: Doormaker.UpdateVisual swaps the boss sprite when transitioning
    //   phase. Auto-property is enough — no headless reader looks at it back.
    public Texture2D? Texture { get; set; }
}

public class GpuParticles2D : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName
    {
        public static readonly StringName Finished = new("finished");
    }

    // from: monster move VFX (e.g. VulnerableSporesMove, AttackSplash) reads
    //   ProcessMaterial to retune a particle's shader/colour at spawn. Without
    //   this stub the MissingMethodException surfaces *synchronously* on the
    //   first VFX kick after combat starts — auto-property is enough since
    //   nothing reads the value back in headless paths.
    public Material? ProcessMaterial { get; set; }
    // from: same VFX call sites as ProcessMaterial — set the particle count
    //   on spawn. Auto-property; no consumer reads back.
    public int Amount { get; set; }
}

public class CpuParticles2D : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName
    {
        public static readonly StringName Finished = new("finished");
    }
}

public class Line2D : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
}

public class PathFollow2D : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
}

public class BackBufferCopy : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
}

// from: MegaCrit.Sts2.Core.Nodes.NGame..cctor
//   TypeLoadException during EnterAct: "Could not load type 'Godot.Window'".
//   Static field on the node class is typed as Window; never accessed via
//   real Godot APIs at runtime in headless. Empty placeholder is enough.
public class Window : Node
{
    public new class MethodName : Node.MethodName { }
    public new class PropertyName : Node.PropertyName { }
    public new class SignalName : Node.SignalName { }
}

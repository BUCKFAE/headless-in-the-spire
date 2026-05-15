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
}

public class Sprite2D : Node2D
{
    public new class MethodName : Node2D.MethodName { }
    public new class PropertyName : Node2D.PropertyName { }
    public new class SignalName : Node2D.SignalName { }
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

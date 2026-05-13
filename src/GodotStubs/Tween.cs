// Tween / awaiter shells. sts2.dll references Godot.Tween for animation
// playback and Godot.IAwaiter`1 / Godot.SignalAwaiter for await-on-signal
// patterns. In headless mode nothing actually plays or signals; the stubs
// are no-ops that let the metadata load and methods short-circuit cleanly.
//
// from: MegaCrit.Sts2.Core.Combat.CombatManager.AfterCombatRoomLoaded.
//   Without these, EnterMapCoord → CombatRoom.StartCombat → AfterCombatRoomLoaded
//   throws TypeLoadException on Godot.IAwaiter`1 / Godot.Tween and the combat
//   ends up half-initialised: IsInProgress=true but no hand drawn and
//   IsPlayPhase stays false.

namespace Godot;

// Standard awaitable contract: the C# compiler's await pattern needs
// IsCompleted, GetResult, and OnCompleted from INotifyCompletion. Marking
// the interface as INotifyCompletion lets sts2's code compile against the
// expected shape — even though nothing actually awaits these in headless.
public interface IAwaiter<out T> : System.Runtime.CompilerServices.INotifyCompletion
{
    bool IsCompleted { get; }
    T GetResult();
}

// Returned by GodotObject.ToSignal(...); sts2 references it as a type but
// never reaches a real call site in our headless path.
public class SignalAwaiter : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName { }

    public SignalAwaiter(GodotObject _, StringName __, GodotObject ___) { }
    public IAwaiter<Variant[]> GetAwaiter() => new InstantAwaiter();

    private sealed class InstantAwaiter : IAwaiter<Variant[]>
    {
        public bool IsCompleted => true;
        public Variant[] GetResult() => System.Array.Empty<Variant>();
        public void OnCompleted(System.Action continuation) => continuation();
    }
}

// Animation player. sts2 calls TweenProperty / TweenMethod / Kill / etc.;
// each returns a chainable handle or completes immediately. The nested
// enums and tweener classes are reference-only — sts2 reads them off
// method return types but never inspects values in headless paths.
public class Tween : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName
    {
        public static readonly StringName Finished = new("finished");
    }

    public enum EaseType { In = 0, Out = 1, InOut = 2, OutIn = 3 }
    public enum TransitionType { Linear = 0, Sine = 1, Quint = 2, Quart = 3, Quad = 4, Expo = 5, Elastic = 6, Cubic = 7, Circ = 8, Bounce = 9, Back = 10, Spring = 11 }

    public event System.Action? Finished;
    private void RaiseFinished() => Finished?.Invoke();

    public PropertyTweener TweenProperty(GodotObject _, NodePath __, Variant ___, double ____) => new();
    public MethodTweener TweenMethod(Callable _, Variant __, Variant ___, double ____) => new();
    public IntervalTweener TweenInterval(double _) => new();
    public CallbackTweener TweenCallback(Callable _) => new();

    public Tween Chain() => this;
    public Tween Parallel() => this;
    public Tween SetEase(EaseType _) => this;
    public Tween SetTrans(TransitionType _) => this;
    public Tween SetParallel(bool _) => this;
    public Tween SetLoops(int _) => this;
    public bool IsRunning() => false;
    public bool IsValid() => false;
    public bool CustomStep(double _) => true;
    public void Kill() { }
    public void Pause() { }
    public void Play() { }
}

public class Tweener : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName { }
}

public class PropertyTweener : Tweener
{
    public new class MethodName : Tweener.MethodName { }
    public new class PropertyName : Tweener.PropertyName { }
    public new class SignalName : Tweener.SignalName { }
}
public class MethodTweener : Tweener
{
    public new class MethodName : Tweener.MethodName { }
    public new class PropertyName : Tweener.PropertyName { }
    public new class SignalName : Tweener.SignalName { }
}
public class IntervalTweener : Tweener
{
    public new class MethodName : Tweener.MethodName { }
    public new class PropertyName : Tweener.PropertyName { }
    public new class SignalName : Tweener.SignalName { }
}
public class CallbackTweener : Tweener
{
    public new class MethodName : Tweener.MethodName { }
    public new class PropertyName : Tweener.PropertyName { }
    public new class SignalName : Tweener.SignalName { }
}

// from: combat animation paths reach Engine.GetSingleton / TimeScale / FPS.
//   AfterCombatRoomLoaded queues animation work through here; the headless
//   stubs return innocuous defaults so the call chain doesn't throw.
public static class Engine
{
    public static double TimeScale { get; set; } = 1.0;
    public static int MaxFps { get; set; } = 60;
    public static double GetFramesPerSecond() => 60.0;
    public static GodotObject? GetSingleton(StringName _) => null;
    public static MainLoop? GetMainLoop() => null;
    public static string GetArchitectureName() => "x86_64";
}

public class MainLoop : GodotObject
{
    public new class MethodName : GodotObject.MethodName { }
    public new class PropertyName : GodotObject.PropertyName { }
    public new class SignalName : GodotObject.SignalName { }
}

// Godot.NodePath and Godot.Callable referenced by Tween signatures. Both
// are value types in real Godot; structs with default state are enough
// here since nothing reads from them.
// Real Godot.NodePath is a sealed class, not a struct — the implicit
// string→NodePath conversion sts2 emits is a `call class NodePath
// op_Implicit(string)`, which fails to bind against a struct stub.
public sealed class NodePath
{
    public NodePath(string _) { }
    public static implicit operator NodePath(string s) => new(s);
}


// Engine-side platform plumbing: OS env queries, GD logging, the (untyped)
// Godot.Dictionary, and small enums the platform helpers reference. None of
// these have meaningful state in headless — defaults stand in for the
// real engine's behaviour.

namespace Godot;

// from: every Godot.OS reference in sts2.dll (27 members), enumerated via
//   `just list-members Godot.OS`. Defaults are the smallest safe values
//   (empty string/array, false, 0, Error.Ok, empty Dictionary). If any
//   headless code path needs richer behaviour, override that specific
//   member; do not add members that --list-members didn't surface.
public static partial class OS
{
    // ── bool returns: default false ──
    public static bool HasFeature(string _) => false;
    public static bool IsDebugBuild() => false;
    public static bool IsInLowProcessorUsageMode() => false;
    public static bool IsSandboxed() => false;
    public static bool IsStdOutVerbose() => false;
    public static bool IsUserfsPersistent() => false;

    // ── int / uint returns ──
    public static int GetProcessorCount() => 0;
    public static ulong GetStaticMemoryPeakUsage() => 0;
    public static ulong GetStaticMemoryUsage() => 0;

    // ── string returns: empty string ──
    public static string GetDataDir() => string.Empty;
    public static string GetDistributionName() => string.Empty;
    public static string GetEnvironment(string _) => string.Empty;
    public static string GetExecutablePath() => string.Empty;
    public static string GetLocale() => string.Empty;
    public static string GetLocaleLanguage() => string.Empty;
    public static string GetModelName() => string.Empty;
    public static string GetName() => string.Empty;
    public static string GetProcessorName() => string.Empty;
    public static string GetUserDataDir() => string.Empty;
    public static string GetVersion() => string.Empty;

    // ── string[] returns ──
    public static string[] GetCmdlineArgs() => Array.Empty<string>();
    public static string[] GetCmdlineUserArgs() => Array.Empty<string>();
    public static string[] GetGrantedPermissions() => Array.Empty<string>();

    // ── other ──
    public static Dictionary GetMemoryInfo() => new();
    public static Error ShellOpen(string _) => Error.Ok;
    public static Error ShellShowInFileManager(string _, bool __) => Error.Ok;
    public static void Crash(string _) { }
}

// from: Godot.OS.GetMemoryInfo() return type (and likely future references).
// Real Godot.Dictionary is a Variant-keyed dict exposed to GDScript; for
// our purposes an empty class is enough — the result is never read in
// headless paths we've reached so far.
public partial class Dictionary { }

// from: every Godot.GD reference in sts2.dll (8 members), enumerated via
//   `just list-members Godot.GD`. Load<T> returns default (null for ref
//   types); rand helpers return 0. Print* go to stdout, PushError/PrintErr/
//   PushWarning go to stderr with a [godot] prefix — real Godot surfaces
//   these in its log/editor, and silently dropping them masks engine-side
//   complaints that we'd otherwise have to reverse-engineer from symptoms.
public static partial class GD
{
    public static T Load<T>(string _) => default!;
    public static double RandRange(double _, double __) => 0;
    public static float Randf() => 0;
    // Everything routes to stderr: stdout is the AD-2 NDJSON wire and must
    // never carry log noise. Real Godot displays Print* in the editor/console,
    // which is logically stderr for a headless host. Push*/PrintErr also dump
    // the MegaCrit frames of the current stack so silently-swallowed engine
    // exceptions are recoverable without re-running under a debugger.
    public static void Print(string s) => Console.Error.WriteLine($"[godot] {s}");
    public static void PrintRich(string s) => Console.Error.WriteLine($"[godot] {s}");
    public static void PrintErr(string s) { Console.Error.WriteLine($"[godot:err] {s}"); WriteMegaCritFrames("[godot:err]"); }
    public static void PushError(string s) { Console.Error.WriteLine($"[godot:push-error] {s}"); WriteMegaCritFrames("[godot:push-error]"); }
    public static void PushWarning(string s) => Console.Error.WriteLine($"[godot:push-warning] {s}");

    private static void WriteMegaCritFrames(string prefix)
    {
        foreach (var line in Environment.StackTrace.Split('\n'))
        {
            if (line.Contains("MegaCrit.")) Console.Error.WriteLine($"{prefix}   {line.TrimStart()}");
        }
    }
}

// Real Godot enums have many values; we declare a placeholder until a
// caller actually compares against one.
public enum Error { Ok = 0 }
public enum Key { None = 0 }

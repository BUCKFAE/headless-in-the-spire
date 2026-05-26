using Sts2Headless.Agents.Contracts;

namespace Sts2Headless.Eval;

// Convenience base for in-repo C# agents. The author subclasses this and
// implements `CreateAgent()` (hand-wired — no `new()` constraint, no DI
// container, no reflection on constructor parameters). The harness
// spawns the shared `Sts2Headless.AgentRunner` exe, which receives the
// manifest's FQN on its command line, instantiates it via reflection,
// and calls CreateAgent() to materialise the IAgent.
//
// Two seals on the base contract:
//
//   * Language → "csharp-bundled". One stable label across every
//     BundledAgent subclass so a leaderboard can pivot on it.
//   * Command  → the AgentRunner invocation, with --manifest set to
//     `GetType().FullName!`. Subclasses can't override this; the
//     contract with AgentRunner is non-negotiable.
//
// Why hand-write CreateAgent: agents with non-trivial constructors
// (IroncladAgent's five policies, future RL-backed weights) need
// explicit composition. It also lets two manifests wrap the same
// IAgent class with different dependencies — each variant is its own
// ranked row on the leaderboard (e.g. IroncladManifest vs
// IroncladConservativeManifest). No per-variant agent class needed.
public abstract class BundledAgent : AgentManifest
{
    // The hook the AgentRunner exe calls after resolving this manifest
    // type by FQN. The author hand-wires the agent and any of its
    // policies or dependencies here.
    public abstract IAgent CreateAgent();

    public sealed override string Language => "csharp-bundled";

    public sealed override IReadOnlyList<string> Command
    {
        // Prefer the built dll next to the harness exe when it exists —
        // `dotnet <path-to-dll>` is faster and avoids the source-tree
        // dependency that `dotnet run --project src/…` carries. The
        // built-dll path is what HostProcess.Start uses for the host
        // (AppContext.BaseDirectory), so this matches its posture. Fall
        // back to `dotnet run --project src/Sts2Headless.AgentRunner
        // --no-build --` when invoked from a context that doesn't have
        // the dll locally (e.g. a custom build output directory).
        get
        {
            var dll = Path.Combine(AppContext.BaseDirectory, "Sts2Headless.AgentRunner.dll");
            var fqn = GetType().FullName
                ?? throw new InvalidOperationException("BundledAgent subclasses must have a non-null full type name.");

            if (File.Exists(dll))
                return ["dotnet", dll, "--manifest", fqn];

            return
            [
                "dotnet", "run",
                "--project", "src/Sts2Headless.AgentRunner",
                "--no-build", "--",
                "--manifest", fqn,
            ];
        }
    }
}

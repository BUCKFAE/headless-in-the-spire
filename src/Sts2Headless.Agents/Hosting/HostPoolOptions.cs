namespace Sts2Headless.Agents.Hosting;

// Per-process configuration for a single HostProcess. Replay root is the
// only required field — that's the env var the host reads to know where
// to land .mcr / .run / manifest artefacts (AD-8). Two HostProcess
// instances pointed at the same ReplayRoot are safe (per-run subdirs
// are pid-suffixed so they can't collide); the index file at the root
// is rebuilt on each finalize.
//
// AgentName is the label the host stamps into the manifest's Header so
// the viewer can group runs by author ("GreedyAgent (seed=42)"). Default
// is "unknown".
public sealed record HostProcessOptions(
    string ReplayRoot,
    string? HostDllPath = null,
    TimeSpan? RequestTimeout = null,
    Action<string>? OnStderr = null,
    string? AgentName = null);

// Pool-level configuration. Each of the N workers gets its own replay
// subdirectory rooted at ReplayRootBase, named worker-0 .. worker-(N-1).
// OnWorkerStderr (if provided) is called per stderr line with the
// originating worker's index — useful for prefixed logging when
// debugging cross-process behavior.
public sealed record HostPoolOptions(
    int WorkerCount,
    string ReplayRootBase,
    string? HostDllPath = null,
    TimeSpan? RequestTimeout = null,
    Action<int, string>? OnWorkerStderr = null);

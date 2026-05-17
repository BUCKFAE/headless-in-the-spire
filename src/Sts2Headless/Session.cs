using Sts2Headless.Protocol.Methods;
using Sts2Headless.Replay;
using Sts2Headless.Runtime;

namespace Sts2Headless;

// Single-slot run holder. The host owns at most one active run at a time;
// `run/new` writes here, every stateful method reads from here.
//
// Single-slot is deliberate for now (no runId routing). When a future pass
// adds multi-session support, this class grows to a dictionary keyed by id
// and the wire methods learn to accept one — but the lifetime semantics
// (host owns the lifetime, clients never hold raw handles) stay the same.
//
// Recorder (AD-8) is an optional per-run companion. Set when
// STS2_REPLAY_OUT is in scope at run/new time; null otherwise. Lives on
// Session because its lifetime is exactly the run's lifetime — bound at
// run/new, finalised on the next run/new (or host shutdown).
public sealed class Session
{
    public RunHandle? Run { get; private set; }
    public Character? Character { get; private set; }
    public ulong Seed { get; private set; }
    public ReplayRecorder? Recorder { get; private set; }

    public bool IsActive => Run is not null;

    public void Set(RunHandle run, Character character, ulong seed, ReplayRecorder? recorder = null)
    {
        Run = run;
        Character = character;
        Seed = seed;
        Recorder = recorder;
    }

    public void Clear()
    {
        Run = null;
        Character = null;
        Seed = 0;
        Recorder = null;
    }
}

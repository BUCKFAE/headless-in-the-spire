using Sts2Headless.Protocol.Methods;
using Sts2Headless.Runtime;

namespace Sts2Headless;

// Single-slot run holder. The host owns at most one active run at a time;
// `run/new` writes here, every stateful method reads from here.
//
// Single-slot is deliberate for now (no runId routing). When a future pass
// adds multi-session support, this class grows to a dictionary keyed by id
// and the wire methods learn to accept one — but the lifetime semantics
// (host owns the lifetime, clients never hold raw handles) stay the same.
public sealed class Session
{
    public RunHandle? Run { get; private set; }
    public Character? Character { get; private set; }
    public ulong Seed { get; private set; }

    public bool IsActive => Run is not null;

    public void Set(RunHandle run, Character character, ulong seed)
    {
        Run = run;
        Character = character;
        Seed = seed;
    }

    public void Clear()
    {
        Run = null;
        Character = null;
        Seed = 0;
    }
}

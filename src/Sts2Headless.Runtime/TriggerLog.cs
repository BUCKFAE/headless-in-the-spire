using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Runtime;

// Process-global ring buffer for "trigger" events emitted from Harmony
// postfixes (RelicHookPatches, future CardHookPatches, etc.). The host
// drains this on each run/state response and surfaces the drained
// entries as the wire's `triggeredSincePrev` field.
//
// Why static / global: Harmony postfixes can't easily capture a per-
// session sink — they run as plain CLR methods bound at patch time, and
// the patched code reaches them via a static dispatch. Carrying a
// Session reference through hundreds of patched methods would mean
// either an AsyncLocal<Session> (fragile across the sts2 sync-context
// flip) or a thread-static map keyed by something we'd have to invent.
// The host is single-session anyway — it always drives one run/new at
// a time — so a process-global queue is the right shape.
//
// Why a bounded log: a long combat fires hundreds of hook invocations
// between two run/state reads (every card play, every block change,
// every relic that listens). Unbounded growth would balloon if a
// caller stopped reading state; the cap lets us drop oldest entries
// loudly rather than OOM. Cap chosen at 8192: ~one normal combat is
// well under, a pathological no-drain leak surfaces fast.
//
// Thread-safety: Harmony postfixes can fire from any thread sts2 chose
// to run async work on. The lock is uncontested in the headless single-
// thread sync context, but cheap insurance.
public static class TriggerLog
{
    public const int Capacity = 8192;

    private static readonly object _gate = new();
    private static readonly Queue<TriggerEvent> _buf = new(Capacity);
    private static long _droppedSinceLastDrain;

    public static void Record(TriggerKind kind, string sourceId, string hook)
    {
        if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(hook)) return;
        lock (_gate)
        {
            if (_buf.Count >= Capacity)
            {
                _buf.Dequeue();
                _droppedSinceLastDrain++;
            }
            _buf.Enqueue(new TriggerEvent(kind, sourceId, hook));
        }
    }
    // TriggerKind / TriggerEvent are defined in Sts2Headless.Protocol.Methods —
    // the wire types are the single source of truth. Runtime references
    // Protocol so we can use them directly without duplicating the shape.

    // Atomically pull every event since the last drain. Returns the
    // count of buffered events that were dropped due to capacity so
    // the host can surface "we lost N entries" rather than silently
    // truncating the wire.
    public static (IReadOnlyList<TriggerEvent> Events, long Dropped) Drain()
    {
        lock (_gate)
        {
            if (_buf.Count == 0 && _droppedSinceLastDrain == 0)
                return (Array.Empty<TriggerEvent>(), 0);
            var snapshot = _buf.ToArray();
            var dropped = _droppedSinceLastDrain;
            _buf.Clear();
            _droppedSinceLastDrain = 0;
            return (snapshot, dropped);
        }
    }

    // Reset state between runs (the host calls this on run/new so a
    // stale combat's tail doesn't bleed into the next run's first
    // run/state). Cleaner than relying on a Drain() that callers might
    // skip on the run-new path.
    public static void Reset()
    {
        lock (_gate)
        {
            _buf.Clear();
            _droppedSinceLastDrain = 0;
        }
    }
}


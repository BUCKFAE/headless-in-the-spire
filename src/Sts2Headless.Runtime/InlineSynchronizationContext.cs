namespace Sts2Headless.Runtime;

// Installed in place of the default SynchronizationContext before any sts2
// code runs. Task.Yield() posts to SynchronizationContext.Current, so by
// executing posted callbacks inline we collapse the game's async chains
// into synchronous execution — no Godot frame loop, no awaiter parking.
//
// A naive Post that just calls d(state) recursively can blow the stack
// when callbacks queue more callbacks. The recursion guard + queue keeps
// the outermost Post on the stack and drains nested ones iteratively.
//
// Ported from external-tools/sts2-cli/src/Sts2Headless/RunSimulator.cs:39-87.
public sealed class InlineSynchronizationContext : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();
    private bool _executing;

    public override void Post(SendOrPostCallback d, object? state)
    {
        if (_executing)
        {
            _queue.Enqueue((d, state));
            return;
        }

        _executing = true;
        try
        {
            d(state);
            while (_queue.Count > 0)
            {
                var (cb, st) = _queue.Dequeue();
                cb(st);
            }
        }
        finally
        {
            _executing = false;
        }
    }

    public override void Send(SendOrPostCallback d, object? state) => d(state);

    public void Pump()
    {
        while (_queue.Count > 0)
        {
            var (cb, st) = _queue.Dequeue();
            _executing = true;
            try { cb(st); }
            finally { _executing = false; }
        }
    }
}

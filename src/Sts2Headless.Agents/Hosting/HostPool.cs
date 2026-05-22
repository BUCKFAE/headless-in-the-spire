using System.Threading.Channels;
using Sts2Headless.Agents.Contracts;

namespace Sts2Headless.Agents.Hosting;

// Bounded-concurrency supervisor over N persistent HostProcess workers.
// Goal #3 (parallel execution: N independent processes, isolated state,
// addressable IPC) is satisfied here — stdio is per-process by AD-2 and
// each worker writes to its own replay subdirectory so the only shared
// resource across workers is the filesystem root.
//
// Workers are eagerly started in the constructor so that the
// (substantial) sts2.dll bootstrap cost is paid once per worker
// up-front, not on every work item. Within a single worker, runs are
// serial: that's the same posture every existing test takes (HostReuse
// pins idempotent reuse across run/new), and it matches the host's
// single-slot Session model.
//
// HostPool itself is thread-safe across RunAsync callers — work items
// queue on an internal channel and are dispatched to whichever worker
// becomes idle next.
public sealed class HostPool : IAsyncDisposable
{
    private readonly HostProcess[] _workers;
    private readonly Channel<HostProcess> _idle;
    private bool _disposed;

    public int WorkerCount => _workers.Length;
    public IReadOnlyList<string> ReplayRoots { get; }

    public HostPool(HostPoolOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.WorkerCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(options),
                "WorkerCount must be > 0.");
        if (string.IsNullOrEmpty(options.ReplayRootBase))
            throw new ArgumentException("ReplayRootBase is required.", nameof(options));

        Directory.CreateDirectory(options.ReplayRootBase);

        _workers = new HostProcess[options.WorkerCount];
        var roots = new string[options.WorkerCount];
        _idle = Channel.CreateUnbounded<HostProcess>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
        });

        // Eagerly start every worker. If any one fails, tear down the
        // partially-built pool before surfacing the exception so we
        // don't leak processes on a half-constructed HostPool.
        try
        {
            for (var i = 0; i < options.WorkerCount; i++)
            {
                var workerIndex = i;
                var replayRoot = Path.Combine(options.ReplayRootBase, $"worker-{workerIndex}");
                var onStderr = options.OnWorkerStderr is null
                    ? (Action<string>?)null
                    : line => options.OnWorkerStderr(workerIndex, line);

                var worker = HostProcess.Start(new HostProcessOptions(
                    ReplayRoot: replayRoot,
                    HostDllPath: options.HostDllPath,
                    RequestTimeout: options.RequestTimeout,
                    OnStderr: onStderr));

                _workers[i] = worker;
                roots[i] = replayRoot;
                _idle.Writer.TryWrite(worker);
            }
        }
        catch
        {
            DisposeAsyncCore().AsTask().GetAwaiter().GetResult();
            throw;
        }

        ReplayRoots = roots;
    }

    // Acquire an idle worker, run the caller's lambda against it, return
    // the worker to the pool. The lambda owns the worker for its entire
    // duration — start a run, drive it through the agent loop, finalise
    // — and is responsible for leaving the host in a state where the
    // next work item's `run/new` can succeed (which, by Session
    // single-slot semantics, is automatic: every `run/new` resets).
    public async Task<T> RunAsync<T>(
        Func<ITransport, CancellationToken, Task<T>> workItem,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var worker = await _idle.Reader.ReadAsync(ct);
        try
        {
            return await workItem(worker, ct);
        }
        finally
        {
            // Return the worker to the idle pool even on exception. The
            // worker may be in any session state; the next work item
            // calling run/new resets it. If the work item killed the
            // process (rare), the next caller's first SendAsync will
            // surface that as a "host closed stdout" error rather than
            // hanging — see HostProcess.SendAsync.
            if (!_disposed) _idle.Writer.TryWrite(worker);
        }
    }

    public ValueTask DisposeAsync() => DisposeAsyncCore();

    private async ValueTask DisposeAsyncCore()
    {
        if (_disposed) return;
        _disposed = true;
        _idle.Writer.TryComplete();

        // Best-effort parallel teardown — one slow worker shouldn't
        // serialise dispose of the rest.
        var disposes = _workers
            .Where(w => w is not null)
            .Select(w => w.DisposeAsync().AsTask())
            .ToArray();
        try { await Task.WhenAll(disposes); }
        catch { /* swallow per-worker dispose errors; we're tearing down */ }
    }
}

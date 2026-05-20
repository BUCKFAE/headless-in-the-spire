using System.Diagnostics;
using System.Text.Json;
using Sts2Headless.Protocol;

namespace Sts2Headless.Agents;

// Production-grade subprocess wrapper for a single headless host. One
// process, one stdio pair, sequential request/response. Implements
// ITransport so anything an agent can do over a wire connection works
// here, and so HostPool can hand workers to caller code without leaking
// the concrete process type.
//
// Intentionally distinct from the test-only HostSubprocess fixture in
// tests/Sts2Headless.IntegrationTests/: this class never sets
// --enable-debug (AD-7 production-host invariant) and parameterises every
// per-process knob (replay root, dll path, timeout) instead of inlining
// test-shaped defaults. Tests that want the debug surface keep using
// HostSubprocess; production callers (HostPool, future drivers) use this.
public sealed class HostProcess : ITransport, IAsyncDisposable
{
    private readonly Process _proc;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeSpan _timeout;
    private long _nextId;
    private bool _stdinClosed;
    private bool _disposed;

    public string ReplayRoot { get; }

    private HostProcess(Process proc, string replayRoot, TimeSpan timeout)
    {
        _proc = proc;
        ReplayRoot = replayRoot;
        _timeout = timeout;
    }

    public static HostProcess Start(HostProcessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrEmpty(options.ReplayRoot))
            throw new ArgumentException("ReplayRoot is required.", nameof(options));

        var hostDll = options.HostDllPath
            ?? Path.Combine(AppContext.BaseDirectory, "Sts2Headless.dll");
        if (!File.Exists(hostDll))
            throw new FileNotFoundException(
                $"Sts2Headless.dll not found at {hostDll}. Pass HostProcessOptions.HostDllPath " +
                "or run from a build output directory that produced it as a transitive dep.",
                hostDll);

        Directory.CreateDirectory(options.ReplayRoot);

        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(hostDll);
        psi.ArgumentList.Add("--stdio");
        // AD-7: production hosts must NEVER pass --enable-debug. The pool
        // and HostProcess deliberately do not expose a knob for it — debug
        // is a test-fixture-only concession.
        psi.Environment["STS2_REPLAY_OUT"] = options.ReplayRoot;
        if (!string.IsNullOrWhiteSpace(options.AgentName))
        {
            psi.Environment["STS2_REPLAY_AGENT"] = options.AgentName;
        }

        var proc = Process.Start(psi)
            ?? throw new InvalidOperationException(
                $"failed to start headless host subprocess: {hostDll}");

        var onStderr = options.OnStderr;
        _ = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await proc.StandardError.ReadLineAsync()) is not null)
                {
                    onStderr?.Invoke(line);
                }
            }
            catch { /* process exit races stderr drain; nothing to do */ }
        });

        return new HostProcess(proc, options.ReplayRoot, options.RequestTimeout ?? TimeSpan.FromSeconds(30));
    }

    public async Task<TResult> SendAsync<TResult>(string method, object? @params = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_stdinClosed)
            throw new InvalidOperationException("HostProcess stdin is closed; cannot send further requests.");

        // Serialise per-process: stdio is a single in-order channel, the
        // host serves requests sequentially, and ReadLineAsync of an
        // interleaved response stream would lose track of which line
        // belongs to whom. Concurrent SendAsync from multiple awaiters on
        // the same HostProcess is therefore queued, not parallelised.
        await _gate.WaitAsync();
        try
        {
            var id = Interlocked.Increment(ref _nextId);
            var paramsNode = @params is null
                ? null
                : JsonSerializer.SerializeToNode(@params, @params.GetType(), EnvelopeIo.JsonOptions);
            var request = new Request(id, method, paramsNode);
            var line = JsonSerializer.Serialize(request, EnvelopeIo.JsonOptions);

            await _proc.StandardInput.WriteLineAsync(line);
            await _proc.StandardInput.FlushAsync();

            var responseLine = await _proc.StandardOutput.ReadLineAsync().WaitAsync(_timeout);
            if (string.IsNullOrEmpty(responseLine))
                throw new InvalidOperationException(
                    $"host closed stdout before responding to {method} (id={id}).");

            var response = JsonSerializer.Deserialize<Response>(responseLine, EnvelopeIo.JsonOptions)
                ?? throw new InvalidOperationException($"{method} (id={id}): response deserialised to null");
            if (response.Id != id)
                throw new InvalidOperationException(
                    $"{method}: response id {response.Id} does not match request id {id}");
            if (response.Error is not null)
                throw new HostMethodErrorException(method, response.Error);
            if (response.Result is null)
                throw new InvalidOperationException($"{method}: response has neither result nor error");

            return response.Result.Deserialize<TResult>(EnvelopeIo.JsonOptions)
                ?? throw new InvalidOperationException($"{method}: result deserialised to null as {typeof(TResult).Name}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (!_stdinClosed)
            {
                _proc.StandardInput.Close();
                _stdinClosed = true;
            }
            await _proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            if (!_proc.HasExited) _proc.Kill(entireProcessTree: true);
        }
        finally
        {
            if (!_proc.HasExited) _proc.Kill(entireProcessTree: true);
            _proc.Dispose();
            _gate.Dispose();
        }
    }
}

// Thrown when the host returns a non-null error envelope. Preserves the
// wire error code + message so callers can match on `Error.Code` without
// having to round-trip back through `SendRawAsync`.
public sealed class HostMethodErrorException(string method, Error error)
    : Exception($"{method}: host returned error code={error.Code} message=\"{error.Message}\"")
{
    public string Method { get; } = method;
    public Error Error { get; } = error;
}

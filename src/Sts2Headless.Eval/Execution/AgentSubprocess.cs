using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sts2Headless.Eval.Json;
using Sts2Headless.Protocol;

namespace Sts2Headless.Eval.Execution;

// Thin client over an agent subprocess speaking the agent/* dialect.
// Mirrors HostProcess: NDJSON over stdin/stdout, sequential request /
// response, errors surfaced as typed exceptions.
//
// What's different from HostProcess:
//
//   * The command is data — comes from the manifest, not hard-coded.
//     Cwd, environment, and argv all flow through. This is the layer
//     that makes "any language with stdio" a valid agent.
//
//   * No "single repeated host" semantics. We start one of these per
//     cell, drive it through agent/init → agent/decide* →
//     agent/teardown, then dispose. The agent is allowed to be
//     stateful across decisions inside the cell (FR-2 explicit) but
//     never across cells.
//
//   * Reads are cancellable via the soft per-decision budget the
//     caller passes in. Exceeding it surfaces as
//     `AgentTimeoutException`, not a transport error — the caller
//     classifies the outcome.
internal sealed class AgentSubprocess : IAsyncDisposable
{
    private readonly Process _proc;
    private readonly StringBuilder _stderrBuf;
    private readonly Task _stderrPump;
    private long _nextId;
    private bool _stdinClosed;
    private bool _disposed;

    public string Command { get; }
    public int ProcessId  { get; }

    private AgentSubprocess(Process proc, string command, StringBuilder stderrBuf, Task stderrPump)
    {
        _proc       = proc;
        _stderrBuf  = stderrBuf;
        _stderrPump = stderrPump;
        Command     = command;
        ProcessId   = proc.Id;
    }

    public static AgentSubprocess Start(AgentSubprocessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Command.Count == 0)
            throw new ArgumentException("AgentManifest.Command must have at least one element (the program).", nameof(options));

        var psi = new ProcessStartInfo(options.Command[0])
        {
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            // Force UTF-8 IO. Agents communicate JSON; encoding ambiguity
            // would surface as silent corruption on Windows.
            StandardInputEncoding  = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardErrorEncoding  = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        for (var i = 1; i < options.Command.Count; i++)
            psi.ArgumentList.Add(options.Command[i]);

        if (!string.IsNullOrEmpty(options.WorkingDirectory))
            psi.WorkingDirectory = options.WorkingDirectory;

        if (options.Environment is { } env)
        {
            foreach (var (k, v) in env)
                psi.Environment[k] = v;
        }

        var proc = Process.Start(psi)
            ?? throw new InvalidOperationException(
                $"AgentSubprocess: failed to start agent process — {Render(options.Command)}");

        var stderrBuf = new StringBuilder();
        var stderrPump = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await proc.StandardError.ReadLineAsync()) is not null)
                {
                    lock (stderrBuf) stderrBuf.AppendLine(line);
                    options.OnStderr?.Invoke(line);
                }
            }
            catch { /* process exit races stderr drain; nothing to do */ }
        });

        return new AgentSubprocess(proc, Render(options.Command), stderrBuf, stderrPump);
    }

    // Send a typed request, await typed response. cancellationToken is the
    // per-decision budget the harness applies on each call; expiry maps
    // to AgentTimeoutException.
    public async Task<TResult> SendAsync<TResult>(string method, object? @params, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_stdinClosed)
            throw new InvalidOperationException("AgentSubprocess: stdin is closed; cannot send further requests.");

        var id = Interlocked.Increment(ref _nextId);
        var paramsNode = @params is null
            ? null
            : JsonSerializer.SerializeToNode(@params, @params.GetType(), EvalJson.Wire);

        var request = new Request(id, method, paramsNode);
        var requestLine = JsonSerializer.Serialize(request, EvalJson.Wire);

        await _proc.StandardInput.WriteLineAsync(requestLine.AsMemory(), ct);
        await _proc.StandardInput.FlushAsync(ct);

        string? responseLine;
        try
        {
            responseLine = await _proc.StandardOutput.ReadLineAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw new AgentTimeoutException(method, id, GetStderrSnapshot());
        }

        if (responseLine is null)
            throw new AgentEofException(method, id, _proc.ExitCode, GetStderrSnapshot());

        var response = JsonSerializer.Deserialize<Response>(responseLine, EvalJson.Wire)
            ?? throw new InvalidOperationException($"AgentSubprocess: {method} (id={id}) response deserialised to null");

        if (response.Id != id)
            throw new InvalidOperationException(
                $"AgentSubprocess: {method} response id {response.Id} does not match request id {id}");

        if (response.Error is not null)
            throw new AgentMethodErrorException(method, response.Error, GetStderrSnapshot());

        if (response.Result is null && typeof(TResult) != typeof(object) && typeof(TResult) != typeof(JsonNode))
            throw new InvalidOperationException($"AgentSubprocess: {method} response has neither result nor error");

        if (response.Result is null) return default!;
        return response.Result.Deserialize<TResult>(EvalJson.Wire)
            ?? throw new InvalidOperationException($"AgentSubprocess: {method} result deserialised to null as {typeof(TResult).Name}");
    }

    public string GetStderrSnapshot()
    {
        lock (_stderrBuf) return _stderrBuf.ToString();
    }

    public bool HasExited => _proc.HasExited;
    public int  ExitCode  => _proc.HasExited ? _proc.ExitCode : int.MinValue;

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
            await _proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch (TimeoutException)
        {
            if (!_proc.HasExited) _proc.Kill(entireProcessTree: true);
        }
        finally
        {
            if (!_proc.HasExited) _proc.Kill(entireProcessTree: true);
            try { await _stderrPump.WaitAsync(TimeSpan.FromSeconds(1)); }
            catch { /* ignore */ }
            _proc.Dispose();
        }
    }

    private static string Render(IReadOnlyList<string> command)
    {
        return string.Join(' ', command.Select(c => c.Contains(' ') ? $"\"{c}\"" : c));
    }
}

internal sealed record AgentSubprocessOptions(
    IReadOnlyList<string>                       Command,
    string?                                     WorkingDirectory = null,
    IReadOnlyDictionary<string, string>?        Environment      = null,
    Action<string>?                             OnStderr         = null);

// Thrown when the agent times out responding to a wire call. Carries
// the method + request id + stderr snapshot so the harness can render
// a useful row.
public sealed class AgentTimeoutException(string method, long id, string stderr)
    : Exception($"agent timeout on {method} (id={id}). stderr tail:\n{Tail(stderr)}")
{
    public string Method { get; } = method;
    public long   Id     { get; } = id;
    public string Stderr { get; } = stderr;

    private static string Tail(string s, int chars = 2048)
        => s.Length <= chars ? s : "…" + s[^chars..];
}

// Thrown when the agent emits an error envelope. Captures the wire
// error code + message for the harness to map to a terminus
// (AgentCrash / HarnessError depending on the code).
public sealed class AgentMethodErrorException(string method, Error error, string stderr)
    : Exception($"agent returned error on {method}: code={error.Code} message=\"{error.Message}\"")
{
    public string Method { get; } = method;
    public Error  Error  { get; } = error;
    public string Stderr { get; } = stderr;
}

// Thrown when the agent process closes stdout before responding to a
// pending wire call — typically a segfault, unhandled exception, or
// OOM kill. Process exit code + stderr snapshot are preserved.
public sealed class AgentEofException(string method, long id, int exitCode, string stderr)
    : Exception($"agent closed stdout before responding to {method} (id={id}); exit code {exitCode}. stderr tail:\n{Tail(stderr)}")
{
    public string Method   { get; } = method;
    public long   Id       { get; } = id;
    public int    ExitCode { get; } = exitCode;
    public string Stderr   { get; } = stderr;

    private static string Tail(string s, int chars = 2048)
        => s.Length <= chars ? s : "…" + s[^chars..];
}

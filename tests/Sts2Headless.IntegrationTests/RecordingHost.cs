using System.Diagnostics;
using System.Text.Json;
using Sts2Headless.Protocol;
using Xunit;
using Xunit.Sdk;

namespace Sts2Headless.IntegrationTests;

// Subprocess wrapper for replay-recording tests. Mirrors HostSubprocess
// in shape but parameterised on a tmp directory the host writes its
// replays under (STS2_REPLAY_OUT). Each test instantiates its own
// RecordingHost — the env var bakes the output root into the process
// and so two recording tests must not share a subprocess. Matches the
// NoDebugHost pattern used in DebugDisabledTests.
internal sealed class RecordingHost : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly Process _proc;
    private long _nextId = 1;
    private bool _disposed;

    public string ReplayRoot { get; }

    private RecordingHost(Process proc, string replayRoot)
    {
        _proc = proc;
        ReplayRoot = replayRoot;
    }

    public static RecordingHost Start(string replayRoot)
    {
        var hostDll = Path.Combine(AppContext.BaseDirectory, "Sts2Headless.dll");
        Assert.True(File.Exists(hostDll), $"Sts2Headless.dll not found at {hostDll}");

        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(hostDll);
        psi.ArgumentList.Add("--stdio");
        psi.ArgumentList.Add("--enable-debug");
        psi.Environment["STS2_REPLAY_OUT"] = replayRoot;

        var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("failed to start recording host subprocess");

        _ = Task.Run(async () =>
        {
            var debug = Environment.GetEnvironmentVariable("STS2_HEADLESS_DEBUG") is not null;
            string? line;
            while ((line = await proc.StandardError.ReadLineAsync()) is not null)
            {
                if (debug) Console.Error.WriteLine($"[host] {line}");
            }
        });

        return new RecordingHost(proc, replayRoot);
    }

    public async Task<TResult> SendAsync<TResult>(string method, object? @params = null)
    {
        var response = await SendRawAsync(method, @params);
        if (response.Error is not null)
            throw new XunitException($"{method}: error code={response.Error.Code} message=\"{response.Error.Message}\"");
        if (response.Result is null)
            throw new XunitException($"{method}: response has neither result nor error");
        return response.Result.Deserialize<TResult>(EnvelopeIo.JsonOptions)
            ?? throw new XunitException($"{method}: result deserialised to null");
    }

    // Negative-path counterpart: assert the response IS an error.
    // Renamed away from `ExpectErrorAsync` so tests that wire-share both
    // host classes (e.g. RunHistoryMethodTests with the no-recorder
    // HostSubprocess and the recorder-equipped RecordingHost) can call
    // the same-shaped helper on either.
    public async Task<Error> ExpectRawErrorAsync(string method, object? @params = null)
    {
        var response = await SendRawAsync(method, @params);
        if (response.Error is null)
            throw new XunitException($"{method}: expected error, got result");
        return response.Error;
    }

    private async Task<Response> SendRawAsync(string method, object? @params)
    {
        var id = Interlocked.Increment(ref _nextId);
        var request = new Request(id, method, @params is null ? null : JsonSerializer.SerializeToNode(@params, EnvelopeIo.JsonOptions));
        var line = JsonSerializer.Serialize(request, EnvelopeIo.JsonOptions);
        await _proc.StandardInput.WriteLineAsync(line);
        await _proc.StandardInput.FlushAsync();

        var cts = new CancellationTokenSource(Timeout);
        var responseLine = await _proc.StandardOutput.ReadLineAsync(cts.Token);
        if (responseLine is null)
            throw new XunitException($"host closed stdout before responding to {method}");
        return JsonSerializer.Deserialize<Response>(responseLine, EnvelopeIo.JsonOptions)
            ?? throw new XunitException($"host returned unparseable line: {responseLine}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _proc.StandardInput.Close();
            if (!_proc.WaitForExit(5000)) _proc.Kill(entireProcessTree: true);
        }
        catch { /* best-effort */ }
        await Task.CompletedTask;
    }
}

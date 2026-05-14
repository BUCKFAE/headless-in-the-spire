using System.Diagnostics;
using System.Text.Json;
using Sts2Headless.Protocol;
using Xunit;
using Xunit.Sdk;

namespace Sts2Headless.IntegrationTests;

// Spawns the headless host with --stdio and drives it through one or more
// typed JSON-RPC exchanges. Test bodies talk to it via SendAsync<TResult>
// / ExpectErrorAsync, never building or parsing JSON by hand — the Protocol
// DTOs are the source of truth, and a wire-shape rename surfaces as a
// compile error here rather than a passing-but-wrong assertion.
//
// One subprocess per fixture instance. Multi-request tests reuse the same
// process so session state (run/new → run/state) is preserved; separate
// instances reset the session.
public sealed class HostSubprocess : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly Process _proc;
    private long _nextId = 1;
    private bool _stdinClosed;
    private bool _disposed;

    public HostSubprocess()
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
        // AD-7: debug methods (debug/give_relic, debug/set_hp, …) are
        // opt-in via --enable-debug. The integration-test fixture is a
        // test context by construction, so we always opt in; production
        // hosts must never set this flag. The
        // HostSubprocessNoDebug counterpart in DebugDisabledTests
        // deliberately omits it to pin the gate from the other side.
        psi.ArgumentList.Add("--enable-debug");

        _proc = Process.Start(psi)
            ?? throw new InvalidOperationException("failed to start headless host subprocess");

        // Drain stderr asynchronously so a chatty host doesn't block on a full
        // pipe buffer. When STS2_HEADLESS_DEBUG is set, mirror to the test's
        // stderr so debug lines surface in test output.
        _ = Task.Run(async () =>
        {
            var debug = Environment.GetEnvironmentVariable("STS2_HEADLESS_DEBUG") is not null;
            string? line;
            while ((line = await _proc.StandardError.ReadLineAsync()) is not null)
            {
                if (debug) Console.Error.WriteLine($"[host] {line}");
            }
        });
    }

    // Send a request, expect a successful response (envelope.error == null),
    // deserialise the result as TResult. The id is generated and verified
    // automatically; tests don't manage it.
    public async Task<TResult> SendAsync<TResult>(string method, object? @params = null)
    {
        var response = await SendRawAsync(method, @params);
        if (response.Error is not null)
        {
            throw new XunitException(
                $"{method}: expected result, got error code={response.Error.Code} message=\"{response.Error.Message}\"");
        }
        if (response.Result is null)
        {
            throw new XunitException($"{method}: response has neither result nor error");
        }
        var typed = response.Result.Deserialize<TResult>(EnvelopeIo.JsonOptions);
        if (typed is null)
        {
            throw new XunitException($"{method}: result deserialised to null as {typeof(TResult).Name}");
        }
        return typed;
    }

    // Send a request, assert the host returns an error envelope. Returns the
    // error so the test can match on code and message text. Throws (test
    // failure) if the host returns a successful result instead.
    public async Task<Error> ExpectErrorAsync(string method, object? @params = null)
    {
        var response = await SendRawAsync(method, @params);
        if (response.Error is null)
        {
            throw new XunitException(
                $"{method}: expected error envelope, got result \"{response.Result?.ToJsonString()}\"");
        }
        return response.Error;
    }

    // Lower-level: get the raw Response envelope back. Used by tests that
    // need to inspect both result and id, or that want to assert on the
    // envelope rather than the payload (e.g. parse-error round-trips).
    public async Task<Response> SendRawAsync(string method, object? @params)
    {
        if (_stdinClosed)
        {
            throw new InvalidOperationException("HostSubprocess stdin is closed; cannot send further requests");
        }

        var id = _nextId++;
        var paramsNode = @params is null
            ? null
            : JsonSerializer.SerializeToNode(@params, @params.GetType(), EnvelopeIo.JsonOptions);
        var request = new Request(id, method, paramsNode);
        var line = JsonSerializer.Serialize(request, EnvelopeIo.JsonOptions);

        await _proc.StandardInput.WriteLineAsync(line);
        await _proc.StandardInput.FlushAsync();

        var responseLine = await _proc.StandardOutput.ReadLineAsync().WaitAsync(Timeout);
        if (string.IsNullOrEmpty(responseLine))
        {
            var stderr = await _proc.StandardError.ReadToEndAsync();
            throw new XunitException($"no response received for {method} (id={id}). stderr:\n{stderr}");
        }

        var response = JsonSerializer.Deserialize<Response>(responseLine, EnvelopeIo.JsonOptions)
            ?? throw new XunitException($"{method} (id={id}): response deserialised to null");
        if (response.Id != id)
        {
            throw new XunitException($"{method}: response id {response.Id} does not match request id {id}");
        }
        return response;
    }

    // Some tests (parse-error round-trip) need to send raw bytes that are
    // not valid Request envelopes. Exposes the underlying stream so they
    // can write whatever they want.
    public async Task<Response> SendRawLineAsync(string rawLine)
    {
        await _proc.StandardInput.WriteLineAsync(rawLine);
        await _proc.StandardInput.FlushAsync();

        var responseLine = await _proc.StandardOutput.ReadLineAsync().WaitAsync(Timeout);
        if (string.IsNullOrEmpty(responseLine))
        {
            var stderr = await _proc.StandardError.ReadToEndAsync();
            throw new XunitException($"no response received for raw line. stderr:\n{stderr}");
        }
        return JsonSerializer.Deserialize<Response>(responseLine, EnvelopeIo.JsonOptions)
            ?? throw new XunitException("response deserialised to null");
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
            await _proc.WaitForExitAsync().WaitAsync(Timeout);
            Assert.Equal(0, _proc.ExitCode);
        }
        catch (TimeoutException)
        {
            if (!_proc.HasExited) _proc.Kill(entireProcessTree: true);
            throw;
        }
        finally
        {
            if (!_proc.HasExited) _proc.Kill(entireProcessTree: true);
            _proc.Dispose();
        }
    }
}

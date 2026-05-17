using System.Diagnostics;
using System.Text.Json;
using Sts2Headless.Protocol;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Pins the AD-7 gate from the negative side: a host spawned WITHOUT
// --enable-debug must reject every debug/* method with the typed wire
// error code WireErrorCode.DebugMethodDisabled. The test is critical: it
// is the regression net that catches an accidental dispatch-table change
// (e.g. removing the GateDebug wrapper) that would otherwise re-enable
// debug methods in production. Without this test, a one-line bug could
// silently weaken the gate.
//
// Why a bespoke subprocess rather than the shared HostSubprocess fixture:
// HostSubprocess.cs hard-codes --enable-debug because every other
// integration test relies on debug methods. xUnit fixtures don't take
// constructor arguments, so reparameterising the existing fixture isn't
// possible. The host is small enough that an ad-hoc spawn here is cheap.
public class DebugDisabledTests
{
    [Fact]
    public async Task DebugSetHp_WithoutEnableDebugFlag_ReturnsDebugDisabledError()
    {
        await using var host = NoDebugHost.Start();

        var err = await host.ExpectErrorAsync(
            "debug/set_hp", new DebugSetHpParams(Hp: 1));

        // The exact code is the contract. Clients that accidentally
        // invoke a debug method MUST get DebugMethodDisabled, not
        // MethodNotFound (the method IS catalogued) and not InternalError
        // (the host is healthy, the call is just gated off).
        Assert.Equal(WireErrorCode.DebugMethodDisabled, err.Code);
        Assert.Contains("--enable-debug", err.Message);
        Assert.Contains("debug/set_hp", err.Message);
    }

    [Fact]
    public async Task DebugGiveRelic_WithoutEnableDebugFlag_ReturnsDebugDisabledError()
    {
        await using var host = NoDebugHost.Start();

        var err = await host.ExpectErrorAsync(
            "debug/give_relic", new DebugGiveRelicParams(RelicId: "BURNING_BLOOD"));

        Assert.Equal(WireErrorCode.DebugMethodDisabled, err.Code);
        Assert.Contains("debug/give_relic", err.Message);
    }

    [Fact]
    public async Task DebugKillAllEnemies_WithoutEnableDebugFlag_ReturnsDebugDisabledError()
    {
        await using var host = NoDebugHost.Start();

        var err = await host.ExpectErrorAsync(
            "debug/kill_all_enemies", new DebugKillAllEnemiesParams());

        Assert.Equal(WireErrorCode.DebugMethodDisabled, err.Code);
        Assert.Contains("debug/kill_all_enemies", err.Message);
    }

    [Fact]
    public async Task NonDebugMethod_StillWorks_WithoutEnableDebugFlag()
    {
        await using var host = NoDebugHost.Start();

        // Sanity: production methods must keep working with debug off.
        // Otherwise we'd have a regression where --enable-debug becomes
        // load-bearing for production paths, which is exactly the wrong
        // shape for an opt-in test affordance.
        var ping = await host.SendAsync<HostPingResult>("host/ping");
        Assert.True(ping.Ok);
    }
}

// Minimal subprocess wrapper — same shape as HostSubprocess but no
// --enable-debug flag and no flow for shared use across multiple tests.
// Each test starts/disposes its own so the gate is exercised cleanly.
internal sealed class NoDebugHost : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly Process _proc;
    private long _nextId = 1;
    private bool _disposed;

    private NoDebugHost(Process proc) => _proc = proc;

    public static NoDebugHost Start()
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
        // Deliberately NO --enable-debug. This is what makes the gate
        // observable in tests.

        var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("failed to start no-debug host subprocess");
        // Drain stderr so a chatty host doesn't block.
        _ = Task.Run(async () =>
        {
            string? line;
            while ((line = await proc.StandardError.ReadLineAsync()) is not null)
            {
                // Silent unless the test wants to debug; matches HostSubprocess.
            }
        });
        return new NoDebugHost(proc);
    }

    public async Task<TResult> SendAsync<TResult>(string method, object? @params = null)
    {
        var response = await SendRawAsync(method, @params);
        Assert.Null(response.Error);
        return response.Result!.Deserialize<TResult>(EnvelopeIo.JsonOptions)!;
    }

    public async Task<Error> ExpectErrorAsync(string method, object? @params = null)
    {
        var response = await SendRawAsync(method, @params);
        Assert.NotNull(response.Error);
        return response.Error!;
    }

    private async Task<Response> SendRawAsync(string method, object? @params)
    {
        var id = _nextId++;
        var paramsNode = @params is null
            ? null
            : JsonSerializer.SerializeToNode(@params, @params.GetType(), EnvelopeIo.JsonOptions);
        var request = new Request(id, method, paramsNode);
        var line = JsonSerializer.Serialize(request, EnvelopeIo.JsonOptions);
        await _proc.StandardInput.WriteLineAsync(line);
        await _proc.StandardInput.FlushAsync();

        var responseLine = await _proc.StandardOutput.ReadLineAsync().WaitAsync(Timeout);
        Assert.False(string.IsNullOrEmpty(responseLine), $"no response for {method} (id={id})");
        return JsonSerializer.Deserialize<Response>(responseLine!, EnvelopeIo.JsonOptions)!;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _proc.StandardInput.Close();
            await _proc.WaitForExitAsync().WaitAsync(Timeout);
        }
        catch (TimeoutException)
        {
            if (!_proc.HasExited) _proc.Kill(entireProcessTree: true);
        }
        finally
        {
            if (!_proc.HasExited) _proc.Kill(entireProcessTree: true);
            _proc.Dispose();
        }
    }
}

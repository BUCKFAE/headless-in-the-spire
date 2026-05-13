using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit;

namespace Sts2Headless.Runtime.Tests;

// End-to-end subprocess fixture: spawn the host with --stdio, write one or
// more NDJSON requests, read the responses back. The host bootstraps sts2.dll
// on startup (since Pass 2 of the vertical slice), so these tests need
// vendor/sts2.dll — i.e. `just setup` must have been run.
public class StdioHostTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Host_Ping_Roundtrips()
    {
        var response = await SendOne("""{"id":1,"method":"host/ping"}""");

        Assert.Equal(1L, (long)response["id"]!);
        Assert.Null(response["error"]);
        var result = response["result"]!.AsObject();
        Assert.True((bool)result["ok"]!);
        Assert.NotNull(result["gameSha256"]);
    }

    [Fact]
    public async Task Host_Unknown_Method_Returns_MethodNotFound()
    {
        var response = await SendOne("""{"id":7,"method":"does/not/exist"}""");

        Assert.Equal(7L, (long)response["id"]!);
        var error = response["error"]!.AsObject();
        Assert.Equal(-32601, (int)error["code"]!);
        Assert.Contains("does/not/exist", (string)error["message"]!);
    }

    [Fact]
    public async Task RunNew_Ironclad_Returns_Player()
    {
        var response = await SendOne("""{"id":3,"method":"run/new","params":{"seed":42}}""");

        Assert.Equal(3L, (long)response["id"]!);
        Assert.Null(response["error"]);
        var result = response["result"]!.AsObject();
        Assert.True((bool)result["ok"]!);
        Assert.Equal("ironclad", (string)result["character"]!);
        Assert.Equal(42UL, (ulong)result["seed"]!);
        Assert.Contains("Player", (string)result["playerType"]!);
    }

    [Fact]
    public async Task RunNew_Unknown_Character_Returns_InternalError()
    {
        var response = await SendOne("""{"id":4,"method":"run/new","params":{"character":"silent"}}""");

        Assert.Equal(4L, (long)response["id"]!);
        var error = response["error"]!.AsObject();
        Assert.Equal(-32603, (int)error["code"]!);
        Assert.Contains("silent", (string)error["message"]!);
    }

    // Spawns the host, writes one request line, reads one response line,
    // closes stdin and waits for clean exit. Returns the parsed response.
    private static async Task<JsonObject> SendOne(string requestLine)
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

        using var proc = Process.Start(psi)!;
        try
        {
            await proc.StandardInput.WriteLineAsync(requestLine);
            await proc.StandardInput.FlushAsync();
            proc.StandardInput.Close();

            var line = await proc.StandardOutput.ReadLineAsync().WaitAsync(Timeout);
            if (string.IsNullOrEmpty(line))
            {
                var stderr = await proc.StandardError.ReadToEndAsync();
                throw new Xunit.Sdk.XunitException($"no response received. stderr:\n{stderr}");
            }

            await proc.WaitForExitAsync().WaitAsync(Timeout);
            Assert.Equal(0, proc.ExitCode);

            return JsonNode.Parse(line)!.AsObject();
        }
        finally
        {
            if (!proc.HasExited) proc.Kill(entireProcessTree: true);
        }
    }
}

using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit;

namespace Sts2Headless.IntegrationTests;

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

    [Fact]
    public async Task RunState_AfterRunNew_ReturnsPlayerSnapshot()
    {
        var responses = await SendMany(
            """{"id":1,"method":"run/new","params":{"seed":1}}""",
            """{"id":2,"method":"run/state"}""");

        Assert.Null(responses[0]["error"]);

        var state = responses[1];
        Assert.Equal(2L, (long)state["id"]!);
        Assert.Null(state["error"]);
        var result = state["result"]!.AsObject();
        Assert.True((bool)result["ok"]!);
        Assert.Equal("ironclad", (string)result["character"]!);
        Assert.Equal(1UL, (ulong)result["seed"]!);
        // Ironclad starts at 80/80 — we don't pin the exact number here in case
        // the game rebalances, but the values must be sensible (positive HP,
        // non-negative gold, non-empty starting deck).
        Assert.True((int)result["hp"]! > 0, $"hp should be > 0, was {result["hp"]}");
        Assert.True((int)result["maxHp"]! > 0, $"maxHp should be > 0, was {result["maxHp"]}");
        Assert.True((int)result["hp"]! <= (int)result["maxHp"]!);
        Assert.True((int)result["gold"]! >= 0, $"gold should be >= 0, was {result["gold"]}");
        Assert.True((int)result["deckSize"]! > 0, $"deckSize should be > 0, was {result["deckSize"]}");
    }

    [Fact]
    public async Task RunState_WithoutRunNew_ReturnsInternalError()
    {
        var response = await SendOne("""{"id":1,"method":"run/state"}""");

        Assert.Equal(1L, (long)response["id"]!);
        var error = response["error"]!.AsObject();
        Assert.Equal(-32603, (int)error["code"]!);
        Assert.Contains("no active run", (string)error["message"]!);
    }

    private static async Task<JsonObject> SendOne(string requestLine)
    {
        var responses = await SendMany(requestLine);
        return responses[0];
    }

    // Spawns the host, writes each request line in order, reads exactly one
    // response per request, closes stdin and waits for clean exit. Stateful
    // method pairs (run/new → run/state) need this because the session lives
    // for the process's lifetime — separate subprocesses would reset it.
    private static async Task<JsonObject[]> SendMany(params string[] requestLines)
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
            foreach (var line in requestLines)
            {
                await proc.StandardInput.WriteLineAsync(line);
            }
            await proc.StandardInput.FlushAsync();
            proc.StandardInput.Close();

            var responses = new JsonObject[requestLines.Length];
            for (var i = 0; i < requestLines.Length; i++)
            {
                var line = await proc.StandardOutput.ReadLineAsync().WaitAsync(Timeout);
                if (string.IsNullOrEmpty(line))
                {
                    var stderr = await proc.StandardError.ReadToEndAsync();
                    throw new Xunit.Sdk.XunitException($"no response received for request {i}. stderr:\n{stderr}");
                }
                responses[i] = JsonNode.Parse(line)!.AsObject();
            }

            await proc.WaitForExitAsync().WaitAsync(Timeout);
            Assert.Equal(0, proc.ExitCode);

            return responses;
        }
        finally
        {
            if (!proc.HasExited) proc.Kill(entireProcessTree: true);
        }
    }
}

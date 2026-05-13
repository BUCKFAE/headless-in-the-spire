using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit;

namespace Sts2Headless.Runtime.Tests;

// End-to-end subprocess fixture: spawn the host with --stdio, write one
// NDJSON request, read one NDJSON response. Validates the wire framing,
// dispatch, and that the exe exits cleanly on stdin close. No vendor/sts2
// involvement — that arrives in the next pass.
public class StdioHostTests
{
    [Fact]
    public async Task Host_Ping_Roundtrips()
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
            await proc.StandardInput.WriteLineAsync("""{"id":1,"method":"host/ping"}""");
            await proc.StandardInput.FlushAsync();
            proc.StandardInput.Close();

            var timeout = TimeSpan.FromSeconds(10);
            var line = await proc.StandardOutput.ReadLineAsync().WaitAsync(timeout);
            Assert.False(string.IsNullOrEmpty(line),
                $"no response received from host. stderr: {await proc.StandardError.ReadToEndAsync()}");

            var node = JsonNode.Parse(line!)!.AsObject();
            Assert.Equal(1L, (long)node["id"]!);
            Assert.Null(node["error"]);
            var result = node["result"]!.AsObject();
            Assert.True((bool)result["ok"]!);
            Assert.NotNull(result["gameSha256"]);

            await proc.WaitForExitAsync().WaitAsync(timeout);
            Assert.Equal(0, proc.ExitCode);
        }
        finally
        {
            if (!proc.HasExited) proc.Kill(entireProcessTree: true);
        }
    }

    [Fact]
    public async Task Host_Unknown_Method_Returns_MethodNotFound()
    {
        var hostDll = Path.Combine(AppContext.BaseDirectory, "Sts2Headless.dll");
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
            await proc.StandardInput.WriteLineAsync("""{"id":7,"method":"does/not/exist"}""");
            await proc.StandardInput.FlushAsync();
            proc.StandardInput.Close();

            var line = await proc.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(string.IsNullOrEmpty(line));

            var node = JsonNode.Parse(line!)!.AsObject();
            Assert.Equal(7L, (long)node["id"]!);
            var error = node["error"]!.AsObject();
            Assert.Equal(-32601, (int)error["code"]!);
            Assert.Contains("does/not/exist", (string)error["message"]!);

            await proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            if (!proc.HasExited) proc.Kill(entireProcessTree: true);
        }
    }
}

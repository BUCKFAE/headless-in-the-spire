using System.Text.Json.Nodes;
using Xunit;

namespace Sts2Headless.UnitTests;

// In-process dispatch tests. StdioHost.Run is given fake stdin/stdout text
// readers and a fake handler dictionary — no subprocess, no game, no I/O
// indirection. The subprocess fixture in Sts2Headless.IntegrationTests
// proves the wiring end-to-end; these tests pin the dispatch logic by
// itself.
public class StdioHostDispatchTests
{
    [Fact]
    public void Run_ReturnsZero_OnEmptyInput()
    {
        var stdin = new StringReader("");
        var stdout = new StringWriter();

        var exit = StdioHost.Run(stdin, stdout, new Dictionary<string, StdioHost.Handler>());

        Assert.Equal(0, exit);
        Assert.Equal(string.Empty, stdout.ToString());
    }

    [Fact]
    public void Run_DispatchesKnownMethod_AndReturnsResult()
    {
        var stdin = new StringReader("{\"id\":1,\"method\":\"echo\",\"params\":{\"value\":\"hi\"}}\n");
        var stdout = new StringWriter();
        var methods = new Dictionary<string, StdioHost.Handler>
        {
            ["echo"] = p => p,
        };

        var exit = StdioHost.Run(stdin, stdout, methods);

        Assert.Equal(0, exit);
        var response = ParseSingle(stdout.ToString());
        Assert.Equal(1, (int)response["id"]!);
        Assert.Equal("hi", (string)response["result"]!.AsObject()["value"]!);
        Assert.Null(response["error"]);
    }

    [Fact]
    public void Run_ReturnsMethodNotFound_ForUnknownMethod()
    {
        var stdin = new StringReader("{\"id\":7,\"method\":\"nope\"}\n");
        var stdout = new StringWriter();

        StdioHost.Run(stdin, stdout, new Dictionary<string, StdioHost.Handler>());

        var response = ParseSingle(stdout.ToString());
        Assert.Equal(7, (int)response["id"]!);
        Assert.Null(response["result"]);
        var error = response["error"]!.AsObject();
        Assert.Equal(-32601, (int)error["code"]!);
        Assert.Contains("nope", (string)error["message"]!);
    }

    [Fact]
    public void Run_ReturnsInternalError_OnHandlerException()
    {
        var stdin = new StringReader("{\"id\":2,\"method\":\"boom\"}\n");
        var stdout = new StringWriter();
        var methods = new Dictionary<string, StdioHost.Handler>
        {
            ["boom"] = _ => throw new InvalidOperationException("kaboom"),
        };

        StdioHost.Run(stdin, stdout, methods);

        var response = ParseSingle(stdout.ToString());
        Assert.Equal(2, (int)response["id"]!);
        Assert.Null(response["result"]);
        var error = response["error"]!.AsObject();
        Assert.Equal(-32603, (int)error["code"]!);
        Assert.Contains("kaboom", (string)error["message"]!);
        Assert.Contains("InvalidOperationException", (string)error["message"]!);
    }

    [Fact]
    public void Run_UnwrapsReflectionExceptions_BeforeReporting()
    {
        // Handlers that go through reflection (the real Sts2Bindings path)
        // wrap their real failures in TargetInvocationException. The dispatch
        // layer's job is to surface the inner message, not the wrapper.
        var stdin = new StringReader("{\"id\":3,\"method\":\"wrapped\"}\n");
        var stdout = new StringWriter();
        var methods = new Dictionary<string, StdioHost.Handler>
        {
            ["wrapped"] = _ => throw new System.Reflection.TargetInvocationException(
                new ArgumentException("real problem")),
        };

        StdioHost.Run(stdin, stdout, methods);

        var error = ParseSingle(stdout.ToString())["error"]!.AsObject();
        Assert.Contains("ArgumentException", (string)error["message"]!);
        Assert.Contains("real problem", (string)error["message"]!);
        Assert.DoesNotContain("TargetInvocationException", (string)error["message"]!);
    }

    [Fact]
    public void Run_ReturnsParseError_OnMalformedLine_AndContinues()
    {
        var stdin = new StringReader("garbage\n{\"id\":5,\"method\":\"ok\"}\n");
        var stdout = new StringWriter();
        var methods = new Dictionary<string, StdioHost.Handler>
        {
            ["ok"] = _ => JsonNode.Parse("{\"ok\":true}"),
        };

        var exit = StdioHost.Run(stdin, stdout, methods);

        Assert.Equal(0, exit);
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);

        var parseErr = JsonNode.Parse(lines[0])!.AsObject();
        Assert.Equal(-1, (int)parseErr["id"]!);
        Assert.Equal(-32700, (int)parseErr["error"]!.AsObject()["code"]!);

        var second = JsonNode.Parse(lines[1])!.AsObject();
        Assert.Equal(5, (int)second["id"]!);
        Assert.True((bool)second["result"]!.AsObject()["ok"]!);
    }

    [Fact]
    public void Run_PassesNullParams_WhenOmitted()
    {
        var stdin = new StringReader("{\"id\":9,\"method\":\"sniff\"}\n");
        var stdout = new StringWriter();
        JsonNode? captured = JsonNode.Parse("\"sentinel\"");
        var methods = new Dictionary<string, StdioHost.Handler>
        {
            ["sniff"] = p => { captured = p; return JsonNode.Parse("{\"ok\":true}"); },
        };

        StdioHost.Run(stdin, stdout, methods);

        Assert.Null(captured);
    }

    private static JsonObject ParseSingle(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        return JsonNode.Parse(lines[0])!.AsObject();
    }
}

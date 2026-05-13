using System.Text.Json.Nodes;
using Sts2Headless.Protocol;
using Xunit;

namespace Sts2Headless.UnitTests;

// NDJSON framing for the wire protocol. The host-side equivalent lives in
// Sts2Headless.Protocol.EnvelopeIo. These tests pin the framing contract:
// blank-line skipping, EOF returns null, malformed JSON throws, null fields
// are omitted from output, LF terminator (never CRLF).
public class EnvelopeIoTests
{
    [Fact]
    public void ReadRequest_ReturnsNull_OnEmptyInput()
    {
        var reader = new StringReader("");
        Assert.Null(EnvelopeIo.ReadRequest(reader));
    }

    [Fact]
    public void ReadRequest_SkipsBlankLines()
    {
        var reader = new StringReader("\n\n\n{\"id\":7,\"method\":\"ping\"}\n");
        var request = EnvelopeIo.ReadRequest(reader);
        Assert.NotNull(request);
        Assert.Equal(7, request!.Id);
        Assert.Equal("ping", request.Method);
    }

    [Fact]
    public void ReadRequest_ReadsOneRequestAtATime()
    {
        // The host loop calls ReadRequest in a loop; consecutive lines should
        // each round-trip as their own request.
        var reader = new StringReader(
            "{\"id\":1,\"method\":\"a\"}\n{\"id\":2,\"method\":\"b\"}\n");

        var first = EnvelopeIo.ReadRequest(reader);
        var second = EnvelopeIo.ReadRequest(reader);
        var third = EnvelopeIo.ReadRequest(reader);

        Assert.Equal("a", first?.Method);
        Assert.Equal(1, first?.Id);
        Assert.Equal("b", second?.Method);
        Assert.Equal(2, second?.Id);
        Assert.Null(third);
    }

    [Fact]
    public void ReadRequest_ParsesParams_AsJsonNode()
    {
        var reader = new StringReader(
            "{\"id\":3,\"method\":\"run/new\",\"params\":{\"seed\":42,\"character\":\"ironclad\"}}\n");

        var request = EnvelopeIo.ReadRequest(reader);

        Assert.NotNull(request);
        var p = request!.Params!.AsObject();
        Assert.Equal(42, (int)p["seed"]!);
        Assert.Equal("ironclad", (string)p["character"]!);
    }

    [Fact]
    public void ReadRequest_OmittedParams_IsNull()
    {
        var reader = new StringReader("{\"id\":4,\"method\":\"host/ping\"}\n");
        var request = EnvelopeIo.ReadRequest(reader);
        Assert.NotNull(request);
        Assert.Null(request!.Params);
    }

    [Fact]
    public void ReadRequest_Throws_OnMalformedJson()
    {
        var reader = new StringReader("not json at all\n");
        Assert.ThrowsAny<Exception>(() => EnvelopeIo.ReadRequest(reader));
    }

    [Fact]
    public void ReadRequest_Throws_OnJsonNullLiteral()
    {
        // The serializer returns null for the literal "null" token; our code
        // converts that into an InvalidDataException so callers can't be
        // surprised by a null Request.
        var reader = new StringReader("null\n");
        Assert.Throws<InvalidDataException>(() => EnvelopeIo.ReadRequest(reader));
    }

    [Fact]
    public void WriteResponse_OmitsErrorWhenSuccessful()
    {
        var writer = new StringWriter();
        EnvelopeIo.WriteResponse(writer,
            new Response(1, JsonNode.Parse("{\"ok\":true}"), null));

        var line = writer.ToString();
        Assert.DoesNotContain("\"error\"", line);
        Assert.Contains("\"result\"", line);
    }

    [Fact]
    public void WriteResponse_OmitsResultWhenErrored()
    {
        var writer = new StringWriter();
        EnvelopeIo.WriteResponse(writer,
            new Response(1, null, new Error(-32601, "method not found: x", null)));

        var line = writer.ToString();
        Assert.DoesNotContain("\"result\"", line);
        Assert.Contains("\"error\"", line);
        Assert.Contains("-32601", line);
    }

    [Fact]
    public void WriteResponse_EndsWithSingleLf()
    {
        var writer = new StringWriter();
        EnvelopeIo.WriteResponse(writer, new Response(1, null, null));

        var output = writer.ToString();
        // Explicit LF — never CRLF, regardless of host platform's newline
        // convention. This is the framing the AD-2 protocol assumes.
        Assert.EndsWith("\n", output);
        Assert.DoesNotContain("\r\n", output);
    }

    [Fact]
    public void RoundTrip_ResponseIsParseableAsRequestPipeline()
    {
        // Sanity check that what we write on one side comes back parseable as
        // a JsonNode on the other. Detects accidental encoding drift.
        var writer = new StringWriter();
        EnvelopeIo.WriteResponse(writer,
            new Response(99, JsonNode.Parse("{\"a\":1}"), null));

        var parsed = JsonNode.Parse(writer.ToString())!.AsObject();
        Assert.Equal(99, (int)parsed["id"]!);
        Assert.Equal(1, (int)parsed["result"]!.AsObject()["a"]!);
    }
}

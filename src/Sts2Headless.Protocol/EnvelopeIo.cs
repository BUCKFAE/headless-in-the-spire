using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Headless.Protocol;

// NDJSON framing for the wire protocol (AD-2). One JSON object per line,
// terminated by LF. We write the newline explicitly rather than relying on
// TextWriter.WriteLine so the framing is platform-independent (Windows
// callers would otherwise get CRLF and need to strip it).
public static class EnvelopeIo
{
    // Shared by every layer that touches the wire: envelope framing here,
    // typed method DTOs in Methods.cs, request/response (de)serialisation
    // in tests. One JsonOptions instance = one source of truth for naming,
    // null-handling, etc. — schema drift can't sneak in via a stray new().
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Returns null at EOF. Throws on malformed JSON — callers decide whether
    // to surface a parse error and continue, or shut down.
    public static Request? ReadRequest(TextReader reader)
    {
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            return JsonSerializer.Deserialize<Request>(line, JsonOptions)
                ?? throw new InvalidDataException("request deserialised to null");
        }
        return null;
    }

    public static void WriteResponse(TextWriter writer, Response response)
    {
        var json = JsonSerializer.Serialize(response, JsonOptions);
        writer.Write(json);
        writer.Write('\n');
        writer.Flush();
    }
}

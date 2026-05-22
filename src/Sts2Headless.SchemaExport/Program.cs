using System.Text.Encodings.Web;
using System.Text.Json;
using Sts2Headless.Utils;

namespace Sts2Headless.SchemaExport;

// Emits protocol/openrpc.json from the typed records in Sts2Headless.Protocol
// (AD-5). Invoked via `just export-schema`. Reads no sts2 bytes — runs
// without vendor/ — so contributors and CI can regenerate the artefact on
// any clone.
//
// CLI:
//   Sts2Headless.SchemaExport [--output <path>]
//
// `--output` resolves relative to the current working directory; defaults
// to `protocol/openrpc.json`. The tool also reads `GAME_VERSION` relative
// to the cwd for the `info.version` field, falling back to a placeholder
// when the file is absent (so the tool stays runnable from arbitrary cwds
// during development).

internal static class Program
{
    private const string DefaultOutput = "protocol/openrpc.json";

    public static int Main(string[] args)
    {
        try
        {
            var output = ParseOutputArg(args);
            var cwd = Environment.CurrentDirectory;
            var gameVersion = ReadGameVersion(cwd) ?? "0.0.0+unknown";

            var doc = OpenRpcEmitter.Emit(gameVersion);

            var outputPath = Path.IsPathRooted(output) ? output : Path.Combine(cwd, output);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            // UnsafeRelaxedJsonEscaping keeps `<`, `>`, `'`, em-dashes etc.
            // as their literal characters — important for an artefact that's
            // read by humans and diffed in code review. The escape pass would
            // also turn the GAME_VERSION placeholder's angle brackets into
            // `<`, which is correct but illegible.
            var json = doc.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
            File.WriteAllText(outputPath, json + Environment.NewLine);

            Console.Out.WriteLine($"wrote {Path.GetRelativePath(cwd, outputPath)}  ({json.Length:n0} bytes)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Sts2Headless.SchemaExport: {ex.Message}");
            return 1;
        }
    }

    private static string ParseOutputArg(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--output" && i + 1 < args.Length) return args[i + 1];
        }
        return DefaultOutput;
    }

    private static string? ReadGameVersion(string cwd) => GameVersionPin.Read(cwd)?.Version;
}

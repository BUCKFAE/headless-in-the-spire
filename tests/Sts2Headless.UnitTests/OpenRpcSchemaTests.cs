using System.Reflection;
using System.Text.Json.Nodes;
using Json.Schema;
using Sts2Headless.Runtime;
using Xunit;

namespace Sts2Headless.UnitTests;

// Validates the checked-in protocol/openrpc.json against the OpenRPC
// meta-schema (AD-5). A malformed schema landing on main would otherwise
// only surface when a downstream codegen ran — much further from the
// commit that broke it.
//
// The meta-schema is vendored under Resources/ (snapshot from
// https://github.com/open-rpc/meta-schema) so the test is hermetic.
public class OpenRpcSchemaTests
{
    [Fact]
    public void Protocol_OpenRpc_Json_Validates_Against_Meta_Schema()
    {
        var repoRoot = Paths.LocateRepoRoot();
        var openrpcPath = Path.Combine(repoRoot, "protocol", "openrpc.json");
        Assert.True(File.Exists(openrpcPath),
            $"protocol/openrpc.json not found at {openrpcPath} — run `just export-schema`.");

        var openrpcNode = JsonNode.Parse(File.ReadAllText(openrpcPath))
            ?? throw new InvalidDataException("protocol/openrpc.json parsed to null");

        var metaSchema = LoadMetaSchema();
        var result = metaSchema.Evaluate(openrpcNode, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
        });

        if (!result.IsValid)
        {
            var errors = string.Join("\n  ", FlattenErrors(result));
            Assert.Fail($"protocol/openrpc.json failed meta-schema validation:\n  {errors}");
        }
    }

    [Fact]
    public void Protocol_OpenRpc_Json_Every_Ref_Resolves_To_Components_Schemas()
    {
        // Beyond the meta-schema's structural check: every $ref in the
        // document must point to a schema we've actually emitted. A typo
        // ("#/components/schemas/RunNewParms") would otherwise lurk until
        // a generator tried to follow it.
        var repoRoot = Paths.LocateRepoRoot();
        var openrpcNode = JsonNode.Parse(File.ReadAllText(Path.Combine(repoRoot, "protocol", "openrpc.json")))
            ?? throw new InvalidDataException("protocol/openrpc.json parsed to null");

        var schemas = openrpcNode["components"]?["schemas"] as JsonObject
            ?? throw new InvalidDataException("missing components/schemas");
        var schemaNames = schemas.Select(kv => kv.Key).ToHashSet();

        var missing = new List<string>();
        CollectMissingRefs(openrpcNode, schemaNames, missing);

        Assert.True(missing.Count == 0,
            $"unresolved $refs: [{string.Join(", ", missing.Distinct())}]");
    }

    private static JsonSchema LoadMetaSchema()
    {
        // The OpenRPC meta-schema $refs into https://meta.json-schema.tools/
        // (the dialect it's authored in) for the JSON Schema parts of an
        // OpenRPC document. JsonSchema.Net does no network fetching by
        // default, so we vendor both schemas and pre-register the
        // json-schema-tools one under its canonical URI before parsing the
        // OpenRPC meta-schema.
        //
        // Both files have their `$schema` dialect declarator stripped because
        // it points at the same json-schema-tools URI (self-referential,
        // would re-trigger the offline-fetch failure). Every keyword they
        // use is valid Draft 2020-12, JsonSchema.Net's default — so the
        // validation semantics are unchanged.
        var jsonSchemaTools = LoadEmbeddedJson("Sts2Headless.UnitTests.Resources.json-schema-tools-meta.json");
        var openRpcMeta = LoadEmbeddedJson("Sts2Headless.UnitTests.Resources.openrpc-meta-schema.json");

        SchemaRegistry.Global.Register(
            new Uri("https://meta.json-schema.tools/"),
            JsonSchema.FromText(jsonSchemaTools.ToJsonString()));

        return JsonSchema.FromText(openRpcMeta.ToJsonString());
    }

    private static JsonObject LoadEmbeddedJson(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"embedded resource {resourceName} not found. Available: "
                + string.Join(", ", asm.GetManifestResourceNames()));
        using var reader = new StreamReader(stream);

        var obj = JsonNode.Parse(reader.ReadToEnd()) as JsonObject
            ?? throw new InvalidDataException($"{resourceName} did not parse to a JsonObject");
        obj.Remove("$schema");
        return obj;
    }

    private static void CollectMissingRefs(JsonNode? node, HashSet<string> schemaNames, List<string> missing)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kv in obj)
                {
                    if (kv.Key == "$ref" && kv.Value is JsonValue v && v.TryGetValue(out string? target))
                    {
                        const string prefix = "#/components/schemas/";
                        if (target.StartsWith(prefix, StringComparison.Ordinal))
                        {
                            var name = target[prefix.Length..];
                            if (!schemaNames.Contains(name)) missing.Add(target);
                        }
                    }
                    else
                    {
                        CollectMissingRefs(kv.Value, schemaNames, missing);
                    }
                }
                break;

            case JsonArray arr:
                foreach (var item in arr) CollectMissingRefs(item, schemaNames, missing);
                break;
        }
    }

    private static IEnumerable<string> FlattenErrors(EvaluationResults result)
    {
        if (result.HasErrors)
        {
            foreach (var (kw, msg) in result.Errors!)
            {
                yield return $"{result.InstanceLocation}: {kw}: {msg}";
            }
        }
        foreach (var detail in result.Details)
        {
            foreach (var s in FlattenErrors(detail)) yield return s;
        }
    }
}

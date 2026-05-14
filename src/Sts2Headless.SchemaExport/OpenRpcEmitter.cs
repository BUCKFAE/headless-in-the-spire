using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Sts2Headless.Protocol;

namespace Sts2Headless.SchemaExport;

// Builds the OpenRPC document (https://open-rpc.org, spec 1.4.x) for the
// wire protocol. The pipeline is:
//
//   1. Collect the hoist set — every public type declared in the
//      `Sts2Headless.Protocol.Methods` namespace (records and enums).
//   2. For each hoisted type, ask System.Text.Json.Schema's exporter for
//      its JSON Schema. A TransformSchemaNode callback replaces every
//      *nested* reference to another hoisted type with a `$ref` into
//      `#/components/schemas/...`. The root schema stays inline so it
//      becomes the canonical definition.
//   3. Walk MethodCatalog (the single source of truth shared with the
//      host's dispatch table, AD-5) and emit one OpenRPC Method Object
//      per entry, pointing `params` / `result` at `$ref`s.
//   4. Wrap the whole thing in the OpenRPC envelope.
//
// The Unknown-sentinel discipline (AD-5: every wire enum carries an
// Unknown variant so clients tolerate game-patch additions) is not
// expressible in JSON Schema, so we don't try. Generated clients must
// not exhaustive-match these enums; the AD documents the contract.

internal static class OpenRpcEmitter
{
    // The OpenRPC spec is at 1.4.x, but the published @open-rpc/meta-schema
    // package (the artefact every tool actually validates against) only
    // enumerates up through 1.3.2. Pin to 1.3.2 so the meta-schema unit test
    // passes today; bump alongside the meta-schema snapshot when its enum
    // catches up.
    private const string OpenRpcVersion = "1.3.2";
    private const string SchemasRefPrefix = "#/components/schemas/";

    public static JsonObject Emit(string gameVersion)
    {
        var hoistSet = CollectHoistSet();
        var schemas = BuildComponentSchemas(hoistSet);
        var methods = BuildMethods();

        return new JsonObject
        {
            ["openrpc"] = OpenRpcVersion,
            ["info"] = new JsonObject
            {
                ["title"] = "headless-in-the-spire",
                ["version"] = gameVersion,
                ["description"] = "Wire protocol for the headless-in-the-spire runner. "
                    + "NDJSON over stdio with a JSON-RPC envelope (AD-2). "
                    + "Schema generated from C# records in Sts2Headless.Protocol (AD-5).",
            },
            ["methods"] = methods,
            ["components"] = new JsonObject
            {
                ["schemas"] = schemas,
            },
        };
    }

    // Every public record and enum in Sts2Headless.Protocol.Methods is a
    // candidate for hoisting. We don't try to be selective — anything in
    // that namespace is by convention part of the wire surface, and
    // hoisting unused types is harmless (callers can ignore them).
    private static HashSet<Type> CollectHoistSet()
    {
        var asm = typeof(MethodCatalog).Assembly;
        return asm.GetTypes()
            .Where(t => t.IsPublic
                && t.Namespace == "Sts2Headless.Protocol.Methods"
                && (t.IsEnum || t.IsClass))
            .ToHashSet();
    }

    private static JsonObject BuildComponentSchemas(HashSet<Type> hoistSet)
    {
        // EnvelopeIo.JsonOptions is locked (the wire layer mutates it on first
        // use). Clone it for the exporter, sharing every other setting
        // (converters, naming, null handling) so the schema reflects the
        // serializer that actually writes the wire. JsonSchemaExporter needs
        // an explicit TypeInfoResolver — JsonSerializer.Default would supply
        // one lazily during serialisation, but the exporter doesn't go
        // through that path.
        var options = new JsonSerializerOptions(EnvelopeIo.JsonOptions)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (ctx, node) =>
            {
                // Path is the JSON-Pointer-style trail from the root of the
                // current schema. Empty path = we're at the root — emit the
                // full inline definition, which is what the components/schemas
                // slot is supposed to hold. BaseTypeInfo / PropertyInfo are
                // not reliable signals here: BaseTypeInfo is set only for
                // polymorphic bases, and PropertyInfo is null whenever we
                // descend into array items.
                if (ctx.Path.Length == 0) return StripSchemaDialect(node);

                // Nested reference to another hoisted type → $ref it. Without
                // this, JsonSchemaExporter inlines Card / Power / Intent at
                // first occurrence and emits internal $refs to its own
                // generation path for subsequent ones — which break the
                // moment we hoist the schema into components/schemas, since
                // the JSON Pointer no longer resolves.
                if (hoistSet.Contains(ctx.TypeInfo.Type))
                {
                    return new JsonObject
                    {
                        ["$ref"] = SchemasRefPrefix + ctx.TypeInfo.Type.Name,
                    };
                }

                // Nullable hoisted enum (e.g. `Character?`) — the exporter
                // walks `Nullable<Character>` and inlines `{ enum: [...vals,
                // null] }` rather than recursing through the underlying type.
                // Without this branch, generated clients duplicate the enum
                // (`Character` and `Character1` in pydantic) because the
                // property carries the values inline. Emit a clean nullable
                // $ref instead.
                var underlying = Nullable.GetUnderlyingType(ctx.TypeInfo.Type);
                if (underlying is not null && hoistSet.Contains(underlying))
                {
                    return new JsonObject
                    {
                        ["anyOf"] = new JsonArray
                        {
                            new JsonObject { ["$ref"] = SchemasRefPrefix + underlying.Name },
                            new JsonObject { ["type"] = "null" },
                        },
                    };
                }

                return node;
            },
        };

        var schemas = new JsonObject();
        foreach (var t in hoistSet.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            var schema = options.GetJsonSchemaAsNode(t, exporterOptions);
            schemas[t.Name] = schema;
        }
        return schemas;
    }

    private static JsonArray BuildMethods()
    {
        var methods = new JsonArray();
        foreach (var entry in MethodCatalog.All)
        {
            var paramsArray = new JsonArray();
            if (entry.ParamsType is not null)
            {
                paramsArray.Add(new JsonObject
                {
                    ["name"] = "params",
                    ["required"] = true,
                    ["schema"] = new JsonObject
                    {
                        ["$ref"] = SchemasRefPrefix + entry.ParamsType.Name,
                    },
                });
            }

            methods.Add(new JsonObject
            {
                ["name"] = entry.Name,
                ["summary"] = entry.Summary,
                ["paramStructure"] = "by-name",
                ["params"] = paramsArray,
                ["result"] = new JsonObject
                {
                    ["name"] = "result",
                    ["schema"] = new JsonObject
                    {
                        ["$ref"] = SchemasRefPrefix + entry.ResultType.Name,
                    },
                },
            });
        }
        return methods;
    }

    // Root schemas in components/schemas must not carry $schema/$dialect —
    // that's a property of the top-level document, not of inline subschemas.
    // JsonSchemaExporter emits one by default; strip it so the resulting
    // OpenRPC doc validates against the meta-schema cleanly.
    private static JsonNode StripSchemaDialect(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            obj.Remove("$schema");
        }
        return node;
    }
}

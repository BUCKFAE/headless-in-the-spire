using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Sts2Headless.Cheats;
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
                    + "Schema generated from C# records in Sts2Headless.Protocol (AD-5). "
                    + "Transport is stdio-only — there is no HTTP server, so the "
                    + "OpenRPC Playground's \"Try it now\" button has nothing to call. "
                    + "Drive methods via `just runner::stdio`, the C# agents in Sts2Headless.Agents, "
                    + "the generated Python client under clients/python/, or the MCP server.",
            },
            ["methods"] = methods,
            ["components"] = new JsonObject
            {
                ["schemas"] = schemas,
            },
        };
    }

    // Every public record and enum in Sts2Headless.Protocol.Methods *and*
    // Sts2Headless.Cheats is a candidate for hoisting. We don't try to be
    // selective — anything in those namespaces is by convention part of the
    // wire surface, and hoisting unused types is harmless (callers ignore
    // them). The cheat namespace is split out into its own assembly so
    // Sts2Headless.Agents can't see it (AD-7); the schema however describes
    // the full surface the host serves.
    private static HashSet<Type> CollectHoistSet()
    {
        var protocolAsm = typeof(MethodCatalog).Assembly;
        var cheatsAsm = typeof(CheatMethodCatalog).Assembly;
        return protocolAsm.GetTypes().Concat(cheatsAsm.GetTypes())
            .Where(t => t.IsPublic
                && (t.Namespace == "Sts2Headless.Protocol.Methods"
                    || t.Namespace == "Sts2Headless.Cheats")
                && (t.IsEnum || t.IsClass)
                // Static classes (compiler emits IsAbstract+IsSealed): no
                // instance members, no wire shape — they're helpers like
                // CardIdNames that don't belong in components/schemas.
                && !(t.IsAbstract && t.IsSealed)
                // [OpaqueWireString] enums (CardId today): the enum values
                // are proprietary content sourced from vendor/sts2.dll and
                // must not appear in committed wire artefacts. Skipping
                // them from the hoist set keeps them out of
                // components/schemas; TransformSchemaNode below replaces
                // property-level references with `{"type": "string"}`.
                && t.GetCustomAttribute<OpaqueWireStringAttribute>() is null)
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

                // OpaqueWireString-marked enum (CardId today) — render as
                // bare string so the proprietary enum membership never lands
                // in the committed schema. Same JsonStringEnumConverter on
                // the C# side still maps wire strings to the typed enum at
                // deserialise time; the schema just doesn't enumerate the
                // values. See OpaqueWireStringAttribute for the rationale.
                if (ctx.TypeInfo.Type.IsEnum
                    && ctx.TypeInfo.Type.GetCustomAttribute<OpaqueWireStringAttribute>() is not null)
                {
                    return new JsonObject { ["type"] = "string" };
                }

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

        // Reused for types marked [SchemaSnakeCase] — `RunHistoryDocument`
        // and its nested records (AD-8). The host serialises these via
        // `RunHistoryDocument.JsonOptions` (`SnakeCaseLower`), so the
        // emitted schema must match or downstream clients silently lose
        // every field whose Python identifier differs from the wire key.
        var snakeOptions = new JsonSerializerOptions(options)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };

        var schemas = new JsonObject();
        foreach (var t in hoistSet.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            var typeOptions = IsSnakeCaseType(t) ? snakeOptions : options;
            var schema = typeOptions.GetJsonSchemaAsNode(t, exporterOptions);
            schemas[t.Name] = schema;
        }

        // The hoist-set short-circuit in TransformSchemaNode replaces nested
        // hoisted-type schemas with bare `$ref`s before JsonSchemaExporter
        // gets a chance to wrap reference-type properties with their NRT
        // nullability. The exporter's `Nullable.GetUnderlyingType` path
        // handles value-type `Character?` (covered by the second branch in
        // TransformSchemaNode) but reference-type `CombatState?` carries
        // nullability as an attribute, not a wrapper, so the short-circuit
        // wins and the resulting schema marks the property required +
        // non-nullable. The host omits the field when null
        // (DefaultIgnoreCondition.WhenWritingNull), which then fails
        // deserialisation on every client. Fix here so the schema
        // matches the wire.
        foreach (var t in hoistSet)
        {
            if (!t.IsClass) continue;
            if (schemas[t.Name] is not JsonObject schema) continue;
            ReconcileNullableProperties(t, schema, hoistSet);
        }

        return schemas;
    }

    // Reconcile every nullable property with the wire's omit-when-null
    // contract. EnvelopeIo.JsonOptions sets
    // DefaultIgnoreCondition=WhenWritingNull, so any property whose value
    // is null at serialise time is dropped from the payload entirely.
    // JsonSchemaExporter doesn't model that policy: nullable properties
    // come back marked `required` because their constructor parameter has
    // no default. Strip them from `required` here, and for bare-$ref
    // hoisted-ref properties also wrap the schema in `anyOf [$ref, null]`
    // (the hoist-set short-circuit in TransformSchemaNode replaces the
    // exporter's nullable wrapping with a bare $ref before this point).
    private static void ReconcileNullableProperties(Type t, JsonObject schema, HashSet<Type> hoistSet)
    {
        if (schema["properties"] is not JsonObject properties) return;
        var required = schema["required"] as JsonArray;
        var nullabilityContext = new NullabilityInfoContext();

        foreach (var propInfo in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!IsNullableProperty(propInfo, nullabilityContext)) continue;

            var namingPolicy = IsSnakeCaseType(t)
                ? JsonNamingPolicy.SnakeCaseLower
                : JsonNamingPolicy.CamelCase;
            var jsonName = propInfo.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? namingPolicy.ConvertName(propInfo.Name);
            if (properties[jsonName] is not JsonObject propSchema) continue;

            // Bare-$ref to a hoisted type — wrap with anyOf null. Other
            // shapes (`type: [...,null]`, existing anyOf) already permit
            // null, so the schema is fine; only the required list needs
            // fixing for them.
            if (propSchema["$ref"] is JsonValue refValue
                && refValue.TryGetValue(out string? refTarget)
                && refTarget is not null
                && refTarget.StartsWith(SchemasRefPrefix, StringComparison.Ordinal)
                && hoistSet.Any(h => h.Name == refTarget[SchemasRefPrefix.Length..]))
            {
                properties[jsonName] = new JsonObject
                {
                    ["anyOf"] = new JsonArray
                    {
                        new JsonObject { ["$ref"] = refTarget },
                        new JsonObject { ["type"] = "null" },
                    },
                };
            }

            if (required is null) continue;
            for (var i = required.Count - 1; i >= 0; i--)
            {
                if (required[i] is JsonValue v
                    && v.TryGetValue(out string? s)
                    && s == jsonName)
                {
                    required.RemoveAt(i);
                }
            }
        }
    }

    private static bool IsNullableProperty(PropertyInfo prop, NullabilityInfoContext ctx)
    {
        if (Nullable.GetUnderlyingType(prop.PropertyType) is not null) return true;
        return ctx.Create(prop).ReadState == NullabilityState.Nullable;
    }

    // `[SchemaSnakeCase]` opts a record out of the default PascalCase /
    // camelCase wire convention (`EnvelopeIo.JsonOptions` has no naming
    // policy, so System.Text.Json passes property names through verbatim;
    // we layer camelCase on the client-codegen side via the schema). The
    // attribute lives on `RunHistoryDocument` + nested History records,
    // whose host-side `JsonOptions` set `SnakeCaseLower`. Without this
    // mirror, generated clients silently desync from the on-wire shape.
    private static bool IsSnakeCaseType(Type t) =>
        t.GetCustomAttribute<SchemaSnakeCaseAttribute>() is not null;

    private static JsonArray BuildMethods()
    {
        var methods = new JsonArray();
        foreach (var entry in MethodCatalog.Core.Concat(CheatMethodCatalog.All))
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

            var methodObject = new JsonObject
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
            };
            // AD-7: debug-only methods are tagged via the OpenRPC `x-`
            // extension namespace. Documentation + a hint for generated
            // clients to segregate them visually; actual enforcement is
            // host-side (the --enable-debug gate).
            if (entry.IsDebugOnly)
            {
                methodObject["x-debugOnly"] = true;
            }
            methods.Add(methodObject);
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

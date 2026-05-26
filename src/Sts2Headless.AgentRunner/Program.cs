using System.Text.Json;
using Sts2Headless.Agents.Contracts;
using Sts2Headless.Eval;
using Sts2Headless.Eval.Json;
using Sts2Headless.Eval.Protocol;
using Sts2Headless.Protocol;

// Generic stdio agent host. Takes `--manifest <FQN>` on argv, reflects
// the parameterless manifest constructor, calls CreateAgent() to get
// the IAgent, and runs the agent/* loop against Console.In / Out.
//
// Stderr is reserved for diagnostics — the harness pipes it into the
// eval log when TeeProcessStderr is on. Stdout is owned by the wire
// (one JSON response per line); any stray println there would corrupt
// the framing.
//
// Force a reference to Sts2Headless.Eval.Manifests' assembly so it's
// loaded by the time we walk AppDomain.GetAssemblies() to resolve the
// caller-supplied FQN. Without this, .NET's lazy assembly loader would
// only pull it in on first use, which would be *after* the FQN lookup.
_ = typeof(Sts2Headless.Eval.Manifests.BuiltinAgents).Assembly;

var manifestFqn = ExtractArg(args, "--manifest");
if (string.IsNullOrEmpty(manifestFqn))
{
    Console.Error.WriteLine("Sts2Headless.AgentRunner: missing required argument --manifest <FullyQualifiedTypeName>.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Example:");
    Console.Error.WriteLine("  dotnet run --project src/Sts2Headless.AgentRunner -- --manifest Sts2Headless.Eval.Manifests.GreedyManifest");
    return 2;
}

var manifestType = ResolveManifestType(manifestFqn);
if (manifestType is null)
{
    Console.Error.WriteLine($"Sts2Headless.AgentRunner: could not resolve manifest type '{manifestFqn}'. Loaded assemblies searched:");
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.FullName))
    {
        Console.Error.WriteLine($"  - {asm.GetName().Name}");
    }
    return 3;
}

if (!typeof(BundledAgent).IsAssignableFrom(manifestType))
{
    Console.Error.WriteLine($"Sts2Headless.AgentRunner: type '{manifestFqn}' is not a BundledAgent subclass.");
    return 4;
}

BundledAgent manifest;
try
{
    manifest = (BundledAgent)Activator.CreateInstance(manifestType)!;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Sts2Headless.AgentRunner: failed to instantiate '{manifestFqn}': {ex.Message}");
    return 5;
}

IAgent agent;
try
{
    agent = manifest.CreateAgent();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Sts2Headless.AgentRunner: manifest.CreateAgent() threw on '{manifestFqn}': {ex.Message}");
    return 6;
}

// ── Stdio loop ──────────────────────────────────────────────────────────
// Mirrors Sts2Headless.StdioHost but with the agent-side three-method
// dispatch table. JSON-RPC reserved error codes plus the agent-side
// -32200..-32299 range live in AgentErrorCode.

var dispatch = new Dictionary<string, Func<JsonElement?, object?>>(StringComparer.Ordinal)
{
    ["agent/init"]     = paramsEl => DispatchInit(manifest, paramsEl),
    ["agent/decide"]   = paramsEl => DispatchDecide(agent, paramsEl),
    ["agent/teardown"] = _        => new AgentTeardownResult(Ok: true),
};

// Force unbuffered output. Without this, the harness can ReadLine for
// our response while it's still sitting in our stdout buffer.
Console.Out.Flush();

try
{
    string? line;
    while ((line = Console.In.ReadLine()) is not null)
    {
        if (line.Length == 0) continue;

        Request? request;
        try
        {
            request = JsonSerializer.Deserialize<Request>(line, EnvelopeIo.JsonOptions);
        }
        catch (Exception ex)
        {
            WriteError(0, WireErrorCode.ParseError, $"parse error: {ex.Message}");
            continue;
        }
        if (request is null)
        {
            WriteError(0, WireErrorCode.InvalidRequest, "request deserialised to null");
            continue;
        }

        if (!dispatch.TryGetValue(request.Method, out var handler))
        {
            WriteError(request.Id, WireErrorCode.MethodNotFound, $"agent: method '{request.Method}' not found");
            continue;
        }

        object? result;
        try
        {
            JsonElement? paramsEl = request.Params is null
                ? null
                : JsonSerializer.SerializeToElement(request.Params, EvalJson.Wire);
            result = handler(paramsEl);
        }
        catch (Exception ex)
        {
            WriteError(request.Id, WireErrorCode.InternalError, $"agent: {ex.GetType().Name}: {ex.Message}");
            continue;
        }

        var responseLine = JsonSerializer.Serialize(new
        {
            id     = request.Id,
            result = JsonSerializer.SerializeToNode(result, result?.GetType() ?? typeof(object), EvalJson.Wire),
        }, EvalJson.Wire);
        Console.Out.WriteLine(responseLine);
        Console.Out.Flush();

        if (request.Method == "agent/teardown") break;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Sts2Headless.AgentRunner: fatal — {ex}");
    return 7;
}

return 0;

// ── helpers ────────────────────────────────────────────────────────────

static string? ExtractArg(string[] args, string flag)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i] == flag)
            return args[i + 1];
    return null;
}

static Type? ResolveManifestType(string fqn)
{
    var direct = Type.GetType(fqn);
    if (direct is not null) return direct;
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    {
        var t = asm.GetType(fqn, throwOnError: false, ignoreCase: false);
        if (t is not null) return t;
    }
    return null;
}

static AgentInitResult DispatchInit(BundledAgent manifest, JsonElement? paramsEl)
{
    // We don't currently *need* the init params on the runner side
    // (they're echoed for diagnostics and for the agent's own caches);
    // a more sophisticated agent would deserialise into AgentInitParams
    // here. Today we just self-report identity.
    _ = paramsEl;
    return new AgentInitResult(Name: manifest.Name, Version: manifest.Version);
}

static AgentDecideResult DispatchDecide(IAgent agent, JsonElement? paramsEl)
{
    if (paramsEl is not { } el)
        throw new ArgumentException("agent/decide: params is null", nameof(paramsEl));
    var p = el.Deserialize<AgentDecideParams>(EvalJson.Wire)
        ?? throw new ArgumentException("agent/decide: params deserialised to null", nameof(paramsEl));
    if (p.Snapshot is null)
        throw new ArgumentException("agent/decide: snapshot is null", nameof(paramsEl));

    var action = agent.Decide(p.Snapshot);
    return new AgentDecideResult(Action: action);
}

static void WriteError(long id, int code, string message)
{
    var response = JsonSerializer.Serialize(new
    {
        id    = id,
        error = new { code, message },
    }, EvalJson.Wire);
    Console.Out.WriteLine(response);
    Console.Out.Flush();
}

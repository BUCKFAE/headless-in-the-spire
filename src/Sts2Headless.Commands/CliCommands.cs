using Sts2Headless.Replay;

namespace Sts2Headless.Commands;

// The single source of truth for every diagnostic / generator command the exe
// serves. Program.cs no longer carries a flag ladder; it builds a CliContext,
// asks this table to Match the args, and dispatches. Adding a command means
// adding one entry here — and it shows up in `--help` for free.
//
// The production stdio host (`--stdio`) is intentionally NOT in this table: it
// owns the bootstrap/binding lifecycle and is the product, not a diagnostic.
// Program.cs keeps it on its own path.
public static class CliCommands
{
    // Order matters only where two verbs could co-occur on one command line;
    // the first match wins, matching the historical if-ladder precedence
    // (e.g. --probe-natural-chain was checked before its rewards variant).
    public static IReadOnlyList<CliCommand> All { get; } =
    [
        new(["--inspect-sts2"], "Inspect sts2.dll metadata without loading it.",
            ctx => InspectCommand.Run(ctx.VendorDir)),
        new(["--probe-init"], "Probe: load sts2.dll and report basic init.",
            ctx => ProbeInitCommand.Run(ctx.VendorDir)),
        new(["--probe-bootstrap"], "Probe: run the bootstrap sequence and report each step.",
            ctx => ProbeBootstrapCommand.Run(ctx.VendorDir)),
        new(["--probe-run-state"], "Probe: start a run and dump the snapshot.",
            ctx => ProbeRunStateCommand.Run(ctx.VendorDir)),
        new(["--probe-natural-chain"], "Probe: drive a natural action chain through a run.",
            ctx => ProbeNaturalChainCommand.Run(ctx.VendorDir, ctx.RepoRoot)),
        new(["--probe-rewards-natural-chain"], "Probe: natural chain focused on reward screens.",
            ctx => ProbeRewardsNaturalChainCommand.Run(ctx.VendorDir, ctx.RepoRoot)),
        new(["--probe-merchant"], "Probe: enter a merchant room and dump shop state.",
            ctx => ProbeMerchantCommand.Run(ctx.VendorDir)),
        new(["--probe-combat-stall"], "Probe: detect combat stalls (accepts extra args).",
            ctx => ProbeCombatStallCommand.Run(ctx.VendorDir, ctx.Args)),
        new(["--probe-types"], "Probe: inspect sts2 types (accepts a type filter arg).",
            ctx => ProbeTypesCommand.Run(ctx.VendorDir, ctx.Args)),
        new(["--probe-callers"], "Probe: find callers of a member (accepts target args).",
            ctx => ProbeCallersCommand.Run(ctx.VendorDir, ctx.Args)),
        new(["--probe-creatures"], "Probe: enumerate creatures (accepts filter args).",
            ctx => ProbeCreaturesCommand.Run(ctx.VendorDir, ctx.Args)),
        new(["--probe-method-body"], "Probe: dump a method body's IL (accepts target args).",
            ctx => ProbeMethodBodyCommand.Run(ctx.VendorDir, ctx.Args)),
        new(["--probe-encounter"], "Probe: inspect an encounter (accepts encounter args).",
            ctx => ProbeEncounterCommand.Run(ctx.VendorDir, ctx.Args)),
        // `--generate-card-ids` retained as an alias so older shell history and
        // justfile checkouts keep working through the rename. The command always
        // emits every kind's manifest, not just CardId.
        new(["--generate-content-ids", "--generate-card-ids"],
            "Generate the content-id manifests (cards, monsters, relics, …).",
            ctx => GenerateContentIdsCommand.Run(ctx.VendorDir, ctx.RepoRoot)),
        new(["--probe-modeldb"], "Probe: reflect over the engine ModelDb.",
            ctx => ProbeModelDbCommand.Run(ctx.VendorDir, ctx.RepoRoot)),
        new(["--probe-listener-dispatch"], "Probe: trace listener/trigger dispatch.",
            ctx => ProbeListenerDispatchCommand.Run(ctx.VendorDir, ctx.RepoRoot)),
        new(["--rebuild-replay-index"],
            "Rebuild <root>/runs.json from manifests (default root: vendor/replays). Doesn't load sts2.dll.",
            RebuildReplayIndex),
        // `--list-members <FQN>`: dump every member of <FQN> that sts2.dll
        // references. Used to grow GodotStubs accurately without speculation.
        new(["--list-members"], "Dump every member of a type sts2.dll references (needs a FQN, e.g. Godot.OS).",
            ListMembers),
        // `--generate-godot-stubs`: walk sts2.dll's MemberReference table,
        // diff against the current GodotStubs build output, and emit C#
        // `partial` fillers into src/GodotStubs/Generated/ for every missing
        // Godot member.  Closes the latent MissingMethodException surface
        // without speculatively mirroring all of GodotSharp.
        new(["--generate-godot-stubs"],
            "Generate filler partials for every Godot member sts2.dll references but GodotStubs lacks.",
            ctx =>
            {
                var stubPath = Path.Combine(ctx.RepoRoot, "src", "GodotStubs", "bin", "Debug", "net10.0", "GodotSharp.dll");
                GenerateGodotStubsCommand.SetCachedStubAssemblyPath(stubPath);
                return GenerateGodotStubsCommand.Run(ctx.VendorDir, ctx.RepoRoot, ctx.Args);
            }),
    ];

    // First command whose verbs appear anywhere in `args`, or null if none —
    // leaving Program.cs free to fall through to --stdio or the status dump.
    public static CliCommand? Match(string[] args) =>
        All.FirstOrDefault(cmd => cmd.Verbs.Any(args.Contains));

    public static void WriteHelp(TextWriter writer)
    {
        writer.WriteLine("sts2-headless — headless runner for Slay the Spire 2");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  sts2-headless --stdio [--enable-debug]   Run the JSON-RPC host (the product).");
        writer.WriteLine("  sts2-headless <command>                  Run a diagnostic / generator command.");
        writer.WriteLine("  sts2-headless [--help|-h]                Show this help.");
        writer.WriteLine("  sts2-headless                            Show repo / vendor / pin status.");
        writer.WriteLine();
        writer.WriteLine("Commands:");
        var width = All.SelectMany(c => c.Verbs).Max(v => v.Length);
        foreach (var cmd in All)
        {
            writer.WriteLine($"  {string.Join(", ", cmd.Verbs).PadRight(width)}  {cmd.Help}");
        }
    }

    private static int RebuildReplayIndex(CliContext ctx)
    {
        var idx = Array.IndexOf(ctx.Args, "--rebuild-replay-index");
        var rootArg = idx + 1 < ctx.Args.Length && !ctx.Args[idx + 1].StartsWith('-')
            ? ctx.Args[idx + 1]
            : Path.Combine(ctx.RepoRoot, ReplayLayout.DefaultRootRelative);
        var n = ReplayIndex.Rebuild(rootArg);
        Console.WriteLine($"rebuilt {ReplayLayout.RunsIndexPath(rootArg)} with {n} run(s)");
        return 0;
    }

    private static int ListMembers(CliContext ctx)
    {
        var idx = Array.IndexOf(ctx.Args, "--list-members");
        if (idx + 1 >= ctx.Args.Length)
        {
            Console.Error.WriteLine("--list-members needs a fully-qualified type name (e.g. Godot.OS).");
            return 1;
        }
        return ListMembersCommand.Run(ctx.VendorDir, ctx.Args[idx + 1]);
    }
}

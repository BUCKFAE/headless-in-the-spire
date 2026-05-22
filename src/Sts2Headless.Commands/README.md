# Sts2Headless.Commands

The **diagnostic / generator CLI surface**: the `CliCommands` dispatch table
plus every command behind it (`--probe-*`, `--inspect-sts2`,
`--generate-content-ids`, `--list-members`, `--rebuild-replay-index`).

## Where the entry point is

The `Sts2Headless` exe owns `Program.cs`. After bootstrap it routes:

```
--stdio        → the production JSON-RPC host (handled in the exe, not here)
--help / -h    → CliCommands.WriteHelp
<a command>    → CliCommands.Match(args) → command.Invoke(ctx)
(no verb)      → repo / vendor / pin status dump
```

So this package is reached **only** through the public `CliCommands` registry.
The individual command classes are `internal` — the exe never names them. The
one exception is `GenerateContentIdsCommand` (public): the content-manifest
integration tests reuse its ModelDb walk.

## Adding a command

1. Add a class with a `static int Run(...)` (see the existing ones).
2. Add one `CliCommand` entry to `CliCommands.All` — it becomes discoverable in
   `--help` for free.

`CliContext(RepoRoot, VendorDir, Args)` unifies the three historical `Run(...)`
shapes so the table can invoke any command uniformly. Verb matching is
position-agnostic (`Args.Contains`), preserving the old `args.Contains("--x")`
behaviour. `CliCommandsTests` (in `Sts2Headless.UnitTests`) pins resolution,
aliases, and that `--stdio` is never a table verb.

## Layout

```
CliCommand.cs / CliCommands.cs   the dispatch table (public entry point)
*Command.cs                      top-level commands (inspect, generate, list)
probe/                           --probe-* diagnostics (load sts2.dll, report)
```

Depends on Runtime + Protocol + Replay; loaded by the exe.

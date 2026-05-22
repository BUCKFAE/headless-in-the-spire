namespace Sts2Headless.Commands;

// Everything a diagnostic / generator command needs to run. Unifies the three
// divergent `Run(...)` shapes the command classes grew independently
// (`Run(vendorDir)`, `Run(vendorDir, repoRoot)`, `Run(vendorDir, args)`) behind
// one context so the dispatch table can invoke any of them the same way.
public sealed record CliContext(string RepoRoot, string VendorDir, string[] Args);

// One entry in the CLI dispatch table. `Verbs` are the flag spellings that
// select this command (more than one == alias); matching is position-agnostic
// (`Args.Contains`), preserving the historical `args.Contains("--probe-x")`
// behaviour. `Help` is the one-line description shown by `--help`.
//
// This is deliberately a data record, not an interface the command classes
// implement: the existing `internal static XCommand.Run(...)` methods stay
// untouched and are wrapped by a lambda here. That keeps the diagnostic
// commands' internals out of this refactor's blast radius while still giving
// us a single, testable place that knows every command.
public sealed record CliCommand(string[] Verbs, string Help, Func<CliContext, int> Invoke);

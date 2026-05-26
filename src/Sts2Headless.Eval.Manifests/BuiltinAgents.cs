namespace Sts2Headless.Eval.Manifests;

// "An enum where you add stuff." One `public static readonly` field per
// shipped bundled agent. IDE autocomplete on `BuiltinAgents.` lists
// every shipped agent; adding a new one is one line here plus the
// manifest class itself.
//
// Adding a bundled agent:
//   1. Implement the agent class somewhere under
//      src/Sts2Headless.Agents/Examples/, src/Sts2Headless.BattleAgent/,
//      or a sibling library.
//   2. Add a `<Name>Manifest : BundledAgent` next to GreedyManifest in
//      this project. Hand-wire its constructor in CreateAgent().
//   3. Add a line here.
//
// All three steps are typed; a typo breaks the build.
public static class BuiltinAgents
{
    public static readonly AgentManifest Greedy   = new GreedyManifest();
    public static readonly AgentManifest Ironclad = new IroncladManifest();

    // Convenience accessor — every shipped manifest in one list. Useful
    // for "run everyone" reference evals.
    public static IReadOnlyList<AgentManifest> All { get; } =
    [
        Greedy,
        Ironclad,
    ];
}

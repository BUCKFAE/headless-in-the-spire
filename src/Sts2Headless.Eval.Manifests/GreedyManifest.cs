using Sts2Headless.Agents.Contracts;
using Sts2Headless.Agents.Examples;

namespace Sts2Headless.Eval.Manifests;

// Wraps the in-repo `GreedyAgent` (src/Sts2Headless.Agents/Examples/).
// "Play whatever's in front of you" — first playable card, dumbest-but-
// always-legal map node, skips card rewards. Useful as the dumbest
// reference: any agent that doesn't beat Greedy on win rate or mean
// floor is genuinely worse, not just less aggressive.
public sealed class GreedyManifest : BundledAgent
{
    public override string Name        => "greedy";
    public override string Version     => "0.1.0";
    public override string Description => "Plays the first reasonable option at every decision point. Reference baseline.";
    public override IAgent CreateAgent() => new GreedyAgent();
}

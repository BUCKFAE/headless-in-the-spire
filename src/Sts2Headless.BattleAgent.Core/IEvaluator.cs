namespace Sts2Headless.BattleAgent.Core;

// Scores a SimState — higher is better for the player. Used by planners
// to compare candidate end-of-turn states.
//
// Pluggable so we can:
//   - Swap experimental evaluators behind the same planner ("test what
//     happens if we double the lethal bonus")
//   - Run an MCTS-style probabilistic evaluator alongside the heuristic
//     baseline once we have multiple planners
//   - Plug in a learned (RL-style) evaluator that consumes the same
//     SimState
public interface IEvaluator
{
    double Score(SimState state);
}

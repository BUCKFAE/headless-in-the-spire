using System.Text.Json.Serialization;

namespace Sts2Headless.Eval.Scoring;

// One row in the leaderboard. Rank is assigned post-sort by the
// scoring function (1-based, dense — ties resolved deterministically
// by the function's own tiebreaker so two leaderboards on the same
// data are byte-identical).
public sealed record AgentRanking(
    [property: JsonPropertyName("rank")]       int             Rank,
    [property: JsonPropertyName("agent")]      AgentIdentity   Agent,
    [property: JsonPropertyName("score")]      double          Score,
    [property: JsonPropertyName("aggregates")] AgentAggregates Aggregates);

using Sts2Headless.Agents.Contracts;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// SMITH when HP is high (>= 75% MaxHP, heal would be largely wasted),
// HEAL otherwise. Matches the HeuristicAgent default's spirit but with
// an explicit threshold so the policy is testable in isolation.
public sealed class IroncladRestPolicy : IRestPolicy
{
    // 50-seed sweep: lowering the smith threshold to 0.40 dropped
    // Act-1-boss clears from 10/50 → 1/50 because the agent stops
    // healing during mid-Act-1 lulls and dies the next fight. The
    // ratio matters more than the absolute HP — keep the heal-bias.
    private const double HealThreshold = 0.75;

    public AgentAction Choose(RunStateResult state)
    {
        var options = state.AvailableRestSiteOptions;
        if (options.Count == 0)
            throw new InvalidOperationException("IroncladRestPolicy: no options available");

        var heal = options.FirstOrDefault(o => o.IsEnabled
            && string.Equals(o.OptionId, "HEAL", StringComparison.OrdinalIgnoreCase));
        var smith = options.FirstOrDefault(o => o.IsEnabled
            && string.Equals(o.OptionId, "SMITH", StringComparison.OrdinalIgnoreCase));

        var hpRatio = state.MaxHp <= 0 ? 0.0 : (double)state.Hp / state.MaxHp;
        // Smith path — high HP or no heal available.
        if (smith is not null && hpRatio >= HealThreshold)
            return new SelectRestSiteOption(smith.Index, new[] { new[] { 0 } });
        if (heal is not null)
            return new SelectRestSiteOption(heal.Index);
        if (smith is not null)
            return new SelectRestSiteOption(smith.Index, new[] { new[] { 0 } });

        var any = options.First(o => o.IsEnabled);
        return new SelectRestSiteOption(any.Index);
    }
}

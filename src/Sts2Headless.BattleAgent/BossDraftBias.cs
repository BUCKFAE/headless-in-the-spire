using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// Per-Act-1-boss scoring deltas for the draft policy. The wire now
// surfaces RunState.Act.BossEncounter.Id.Entry at run/state time
// (RunStateResult.BossEncounterId), so the draft policy can bias
// picks toward boss-specific counters before the player reaches
// the boss fight.
//
// Design history (on a 50-seed corpus that holds 11/50 with no
// boss-bias):
//   - Initial map (positive boosts +30 / negative -40):       10/50
//   - Halved magnitudes (positive +15 / negative -20):        10/50
//   - Quarter magnitudes (positive +5 / negative -8):         10/50
//   - **Trap-only (negative biases kept; positive removed):** see below
//
// Positive bias measurably hurts: the existing flat tier list is
// already well-tuned across all three bosses, and redirecting same-
// tier ties toward boss-specific picks ends up swapping wins in
// seeds where the previous "blind" pick happened to be the better
// fit for that seed's deck-build. We retain the trap penalties
// because they encode hard counters that the tier list can't see:
// Hemokinesis vs Vantom (1 Slippery stack burned), Hellraiser vs
// Kin (random targeting on a 3-enemy fight), Inferno vs Vantom
// (single-target boss, HP cost wasted).
public static class BossDraftBias
{
    public sealed record Bias(int Delta, string Reason);

    public static Bias? Get(string? bossEncounterId, CardId card)
    {
        if (bossEncounterId is null) return null;
        return Map.TryGetValue(bossEncounterId, out var inner)
            && inner.TryGetValue(card, out var bias)
            ? bias
            : null;
    }

    public static int DeltaFor(string? bossEncounterId, CardId card)
        => Get(bossEncounterId, card)?.Delta ?? 0;

    // CEREMONIAL_BEAST_BOSS: 252 HP single target, ramping Strength,
    // Phase-2 Ringing (1 card / turn). No clear trap cards — Block-
    // only with no Body Slam is a strategic trap but no single card
    // is a "do not draft" against this boss.
    //
    // VANTOM_BOSS: 173 HP, 9 Slippery stacks at open. Big single hits
    // burn one stack each — major value loss. Inferno does nothing
    // (single target, HP cost real).
    //
    // THE_KIN_BOSS: Priest 190 + 2 Followers ~58. Hellraiser's
    // random-target auto-Strike spreads damage across 3 targets when
    // you need focus on Priest.
    private static readonly Dictionary<string, Dictionary<CardId, Bias>> Map = new()
    {
        ["CEREMONIAL_BEAST_BOSS"] = new()
        {
            // No trap cards. Block-only is a *strategic* trap but no
            // individual card is a hard counter-pick here.
        },
        ["VANTOM_BOSS"] = new()
        {
            // Positive — Whirlwind is *the* single Slippery counter
            // in the kit. Per-boss winrate before the bias: 2/15.
            // The bias only fires when Whirlwind is offered alongside
            // another same-tier card; the tier list already values
            // it at A, so we're not redirecting away from S-tier picks.
            [CardId.Whirlwind]   = new(+12, "the cleanest Slippery counter"),
            // Traps — burn Slippery stacks on big single hits.
            [CardId.Hemokinesis] = new(-12, "15 dmg burns 1 Slippery stack — value collapse"),
            [CardId.Inferno]     = new(-12, "single-target boss; AoE wasted, HP cost real"),
            [CardId.Spite]       = new(-12, "single hits per cast on Slippery"),
            [CardId.Rupture]     = new(-6,  "Str only matters post-Slippery"),
        },
        ["THE_KIN_BOSS"] = new()
        {
            [CardId.Hellraiser]  = new(-12, "random targeting spreads damage across 3 targets"),
            [CardId.Spite]       = new(-8,  "burns single-target burst while Followers scale"),
            [CardId.BodySlam]    = new(-8,  "single-target only without AoE answer"),
        },
    };
}

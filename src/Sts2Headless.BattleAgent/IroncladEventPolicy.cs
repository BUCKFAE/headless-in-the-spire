using Sts2Headless.Agents.Contracts;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent;

// Text-key heuristic policy. The wire surfaces each EventOption's
// localisation key (e.g. "AROMA_OF_CHAOS.pages.INITIAL.options.LET_GO");
// the option-name segment carries enough signal to classify the option
// as safe-exit, accept-cost, decline, etc. without a full per-event
// table.
//
// Background: the previous "pick last unlocked" heuristic killed seed 7
// on SLIPPERY_BRIDGE — the last option was HOLD_ON_0 (lose HP) and the
// safe escape was option 0 (OVERCOME). The new heuristic ranks each
// option by a per-keyword score so we pick OVERCOME-style exits and
// accept low-cost gains.
public sealed class IroncladEventPolicy : IEventPolicy
{
    public AgentAction Choose(RunStateResult state)
    {
        var options = state.AvailableEventOptions;
        if (options.Count == 0)
            throw new InvalidOperationException("IroncladEventPolicy: no event options");

        EventOption? bestOpt = null;
        var bestScore = int.MinValue;
        for (var i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            if (opt.IsLocked) continue;
            var score = ScoreOption(opt.TextKey, i, options.Count);
            if (score > bestScore)
            {
                bestScore = score;
                bestOpt = opt;
            }
        }
        if (bestOpt is null)
            throw new InvalidOperationException(
                "IroncladEventPolicy: event phase with no unlocked options");
        return new SelectEventOption(bestOpt.Index);
    }

    // Per-keyword scoring against the option name segment. Higher = more
    // desirable. The keywords cover the patterns the engine uses across
    // the STS2 event catalogue: "LEAVE/DECLINE/IGNORE/OVERCOME/ESCAPE"
    // are safe exits, "TAKE/GRAB/ACCEPT/STEAL/GAIN" are accept-payout,
    // "HOLD/CONTINUE/MAINTAIN/PRESS/STAY" are danger-stay, "FIGHT/RISK"
    // are conditional gambles. Falls back to "earlier index is safer"
    // when no keyword matches.
    private static int ScoreOption(string? textKey, int idxInList, int total)
    {
        if (textKey is null) return -idxInList; // unknown — earlier is better
        var key = ExtractOptionName(textKey).ToUpperInvariant();

        // Hard "do not pick" — these are the staying-in-danger paths.
        if (key.Contains("HOLD_ON")
            || key.Contains("MAINTAIN_CONTROL")
            || key.Contains("PRESS_ON")
            || key.Contains("CONTINUE")
            || key.Contains("BLEED")
            || key.Contains("SUFFER")
            || key.Contains("DRINK")        // some "drink" events damage
            || key.Contains("DROWN"))
            return -100;

        // Safe-exit options. Prefer.
        if (key.Contains("LEAVE")
            || key.Contains("DECLINE")
            || key.Contains("IGNORE")
            || key.Contains("ESCAPE")
            || key.Contains("OVERCOME")
            || key.Contains("RETREAT")
            || key.Contains("LET_GO")
            || key.Contains("WALK_AWAY")
            || key.Contains("SKIP"))
            return 80;

        // Take-the-payout options. Risky but usually net-positive.
        if (key.Contains("TAKE")
            || key.Contains("GRAB")
            || key.Contains("ACCEPT")
            || key.Contains("STEAL")
            || key.Contains("GAIN")
            || key.Contains("HEAL")
            || key.Contains("PRAY"))
            return 60;

        // Combat-trigger / curse-acquisition lines are risky; only take
        // when there's no safer option.
        if (key.Contains("FIGHT")
            || key.Contains("ATTACK")
            || key.Contains("PROVOKE")
            || key.Contains("CURSE")
            || key.Contains("DAMN"))
            return -50;

        // Default — moderate preference for earlier options (safer
        // ordering convention in sts2).
        return 0 - idxInList;
    }

    // Extract the "options.<NAME>" segment of a TextKey. Falls back to
    // the whole string if the pattern isn't found.
    private static string ExtractOptionName(string textKey)
    {
        var marker = ".options.";
        var idx = textKey.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return textKey;
        var rest = textKey[(idx + marker.Length)..];
        // Strip any trailing ".something" segments.
        var dot = rest.IndexOf('.');
        return dot < 0 ? rest : rest[..dot];
    }
}

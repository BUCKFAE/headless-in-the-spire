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
//
// Neow handling lives in this same policy. Neow option text-keys take
// the form `NEOW.pages.INITIAL.options.<RELIC_ID>` — the option name is
// the granted relic. The keyword-based scoring below doesn't fit that
// shape (relic names don't match "LEAVE/TAKE/HOLD" patterns), so the
// scorer dispatches Neow specifically to a relic-tier table. Two goals:
// (1) prefer relics that strengthen Ironclad strategy (PHIAL_HOLSTER's
// potion economy, Strength sources, draw cards), (2) avoid relics whose
// Chosen() call triggers a card-selection screen that's unavailable in
// headless (LEAD_PAPERWEIGHT, PRECARIOUS_SHEARS): the host's AutoAdvance
// recovers the room transition but the player walks away with no relic
// at all. Picking a known-good relic over a known-broken one is the
// biggest win-rate lever Neow has.
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

        // Neow: dispatch to relic-tier scoring (see class docstring).
        if (textKey.StartsWith("NEOW.", StringComparison.Ordinal))
            return ScoreNeowOption(ExtractOptionName(textKey).ToUpperInvariant(), idxInList);

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

    // Score a Neow relic option by its strategic value for Ironclad. The
    // option name is the relic id (PHIAL_HOLSTER, PRECARIOUS_SHEARS, …).
    //
    // Tier rationale per research-sts2-ironclad.md (§8 Quick reference):
    //   * PHIAL_HOLSTER (free potion economy boost) is the
    //     repeatedly-cited "take this" pick for Ironclad A0.
    //   * Strength-archetype relics (INFLAMER, BRUTE_FORCE, etc.) feed
    //     the highest-consistency A0 plan.
    //   * "Neow's Bones" / multi-relic grants score high when offered.
    //
    // Headless-broken-list (negative): the Neow relics whose Chosen()
    // route through NSimpleCardSelectScreen.Create — that factory loads
    // .tscn assets which aren't present in headless and returns null, so
    // the event-model body NREs and AutoAdvance recovers to MapRoom with
    // NO relic granted. Picking a broken relic is strictly worse than
    // picking any working relic. List grows as new card-select-trigger
    // Neow relics surface in failing seeds; see Sts2Bindings.Events.cs
    // ChooseEventOption recovery path for the error shape that catches
    // them.
    private static int ScoreNeowOption(string relicName, int idxInList)
    {
        // Known-broken: card-select-trigger relics that crash on Chosen()
        // in headless. Picking any of these grants no relic.
        if (NeowCardSelectBroken.Contains(relicName))
            return -200;

        // High-tier Ironclad picks. Order matters for tie-break (earlier
        // checks win when a relic appears in multiple categories).
        if (relicName == "PHIAL_HOLSTER")  return 110; // potion economy — top pick
        if (relicName == "WINGED_BOOTS")   return 100; // map-routing flexibility
        if (relicName == "STRAWBERRY")     return  95; // +7 max HP — Ironclad scales HP
        if (relicName == "PEAR")           return  95; // +10 max HP
        if (relicName == "MANGO")          return  95; // +14 max HP
        if (relicName == "BLOOD_VIAL")     return  90; // +20% heal on Neow == 16 HP
        if (NeowStrengthRelics.Contains(relicName))    return 85;
        if (NeowEconomyRelics.Contains(relicName))     return 75;
        if (NeowGenericPositive.Contains(relicName))   return 60;

        // Unknown relic — neutral score with mild earlier-is-safer bias.
        // Beats card-select-broken (-200) but loses to known-good (60+).
        return 10 - idxInList;
    }

    // Card-select-trigger Neow relics. Picking any of these in headless
    // crashes Chosen() and grants no relic. Sourced from CLAUDE.md +
    // failing-test surfacing; add to this list when new broken relics
    // appear in a seed's Neow pool.
    private static readonly HashSet<string> NeowCardSelectBroken = new(StringComparer.Ordinal)
    {
        "LEAD_PAPERWEIGHT",      // CardSelectCmd.FromDeckForCurse
        "PRECARIOUS_SHEARS",     // CardSelectCmd.FromDeckForRemove
    };

    // Strength-archetype enablers / payoffs — the highest-consistency
    // Ironclad A0 plan per research-sts2-ironclad.md §1.1.
    private static readonly HashSet<string> NeowStrengthRelics = new(StringComparer.Ordinal)
    {
        "BLOOD_RIBBON",          // strength on damage-taken
        "ORICHALCUM",            // end-of-turn block (Strength complement)
        "INFLAMER",              // strength stacking
        "VAJRA",                 // +1 starting strength
    };

    // Economy / draw / energy relics that smooth the run without locking
    // gameplay choices.
    private static readonly HashSet<string> NeowEconomyRelics = new(StringComparer.Ordinal)
    {
        "ARCANE_SCROLL",         // +draw per turn
        "SCROLL_BOXES",          // extra hand size
        "POTION_BELT",            // +potion slot
        "CONCH_SHELL",           // map-reveal economy
    };

    // Catch-all positive bucket — relics we don't have a specific tier
    // for but know aren't broken. Better than the unknown fallback.
    private static readonly HashSet<string> NeowGenericPositive = new(StringComparer.Ordinal)
    {
        "ANCHOR",
        "BAG_OF_PREP",
        "BRONZE_SCALES",
        "DEAD_BRANCH",
        "GAMBLING_CHIP",
        "HORN_CLEAT",
        "ICE_CREAM",
        "LANTERN",
        "MAW_BANK",
        "MEAT_ON_THE_BONE",
        "MERCURY_HOURGLASS",
        "ODD_MUSHROOM",
        "OMAMORI",
        "POCKETWATCH",
        "PRESERVED_INSECT",
        "QUESTION_CARD",
        "RED_SKULL",
        "RUNIC_DOME",
        "SHURIKEN",
        "SLAVERS_COLLAR",
        "SLING_OF_COURAGE",
        "SMILING_MASK",
        "SOZU",
        "STONE_CALENDAR",
        "SUNDIAL",
        "TINY_CHEST",
        "TOXIC_EGG",
        "TUNGSTEN_ROD",
        "WHITE_BEAST_STATUE",
    };
}

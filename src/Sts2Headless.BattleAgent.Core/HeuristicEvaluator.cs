namespace Sts2Headless.BattleAgent.Core;

// Hand-tuned linear evaluator. Starting weights come from
// scumthespire's ValueFunctions shape (HP heavy, block discounted,
// enemy HP heavy, lethal as hard short-circuit, statuses as future-
// damage proxies). Numbers are deliberately round so that tests can
// pin them; the planner doesn't depend on specific values, only on
// monotonicity ("more HP = better, more enemy HP = worse").
//
// All weights live on a record so a test or experiment can construct an
// alternate evaluator without subclassing.
public sealed record HeuristicWeights(
    double LethalBonus = 100_000,
    double DeathPenalty = 100_000,
    // Tuned 2026-05-24 via 50-seed sweep (sweep-v2-balanced.md): the
    // shift to PlayerHp 4 / EnemyHp -3 lifted Act 1 boss clears from
    // 7/50 → 8/50 against the same corpus. Pairs with IncomingDamage
    // -3.0 (default below) to slightly reduce defensive over-block.
    double PlayerHp = 4.0,
    double PlayerBlock = 0.3,          // block discounted vs HP (block expires)
    double EnemyHp = -3.0,
    double EnemyBlock = -0.3,
    // Penalty per stack of enemy Strength. Wire intent damage already
    // bakes in current Strength but the evaluator can't see future
    // intents — so a buffing/ramping enemy slipping the killing line
    // is worth this penalty per stack to make the planner prefer kill-
    // before-buff.
    double EnemyStrength = -3.0,
    double PlayerStrength = 6.0,
    double PlayerDexterity = 3.0,
    // Weak bumped 2026-05-18 (was 2.0) — Weak suppresses incoming
    // attack damage by 25%, which matters most against buffing
    // enemies that ramp Strength (FUZZY_WURM_CRAWLER, KIN_PRIEST etc).
    // Vuln stays at 3.0 because amplifying inflicted damage cuts
    // both ways: it makes Bash+Strike kill faster but doesn't help
    // the agent survive ramping enemies.
    double EnemyVulnerable = 3.0,
    double EnemyWeak = 4.0,
    double PlayerVulnerable = -3.0,
    double PlayerWeak = -2.0,
    double PlayerFrail = -2.0,
    // Power-card weights bumped 2026-05-18. Old values
    // systematically undervalued powers because the one-turn planner
    // sees a single trigger's worth of effect. Real combat lasts
    // 6-12 turns; a power that triggers every turn delivers 8x its
    // single-turn value over a boss fight. The new weights approximate
    // that compounding.
    double DemonForm = 40.0,           // +2 STR each turn — run-defining
    double Combust = 12.0,             // AOE every turn (with 1 HP cost)
    double Metallicize = 12.0,         // block at end of every turn
    double FeelNoPain = 8.0,           // block per exhaust
    double DarkEmbrace = 10.0,         // draw per exhaust
    double FireBreathing = 8.0,        // (previously zero — unweighted!)
    double Rupture = 5.0,              // STR per self-damage card
    double Juggernaut = 8.0,           // damage per block gain
    double Barricade = 25.0,           // block persists across turns
    double Rage = 6.0,                 // (previously zero — unweighted!)
    double Brutality = 4.0,            // (previously zero — unweighted!)
    double Evolve = 4.0,               // (previously zero — unweighted!)
    double Berserk = 20.0,             // (previously zero — unweighted!) +1 max energy
    double PlatedArmor = 5.0,          // (previously zero — unweighted!)
    double Hellraiser = 30.0,          // auto-Strike-on-draw — scales with deck strike density
    double CardsDrawn = 1.0,           // small bonus per card drawn this turn
    // Unblocked damage weight. -3.5 (was -2.5) because a one-turn
    // lookahead systematically underestimates HP cost — damage taken now
    // compounds (you fight the next combat at lower HP). Tuned
    // 2026-05-18 from seed-1 trace: at -2.5 the agent prefers
    // Bash+Strike (14 dmg, 0 block) over Strike+Defend+Defend
    // (6 dmg, 10 block) when staring down 24 incoming; at -5 it becomes
    // too defensive and draws out fights, surrendering seed 9 (which
    // previously won the Act 1 boss). -3.5 splits the difference.
    double IncomingDamage = -2.0,
    // Tempo penalty per "turns-to-kill all enemies" estimate. Disabled
    // by default (=0) — the 50-seed sweep at -6 cost ~4 wins because the
    // term double-counts against EnemyHp and pushes the planner into
    // reckless attacks. Kept as a tuning knob for future experiments.
    double TempoPenalty = 0.0)
{
    public static HeuristicWeights Default { get; } = new();
}

public sealed class HeuristicEvaluator(HeuristicWeights? weights = null) : IEvaluator
{
    private readonly HeuristicWeights _w = weights ?? HeuristicWeights.Default;

    public double Score(SimState s)
    {
        // Terminal short-circuits as additive offsets so death/lethal
        // states still rank by damage dealt / damage taken. Old
        // behaviour returned a flat ±penalty which made every "I die
        // this turn" line score identically and let the planner pick
        // "do nothing" when low HP. Now a line that deals more damage
        // before dying scores higher than a line that ends turn with
        // unspent energy.
        var alive = false;
        foreach (var e in s.Enemies)
        {
            if (!e.IsDead) { alive = true; break; }
        }

        var score = 0.0;
        if (!alive) score += _w.LethalBonus;
        if (s.Hp <= 0) score -= _w.DeathPenalty;

        score += _w.PlayerHp * s.Hp;
        score += _w.PlayerBlock * s.Block;

        foreach (var e in s.Enemies)
        {
            if (e.IsDead) continue;
            score += _w.EnemyHp * e.Hp;
            score += _w.EnemyBlock * e.Block;
            score += _w.EnemyVulnerable * e.Vulnerable;
            score += _w.EnemyWeak * e.Weak;
            score += _w.EnemyStrength * e.Strength;
        }

        var st = s.Status;
        score += _w.PlayerStrength * st.Strength;
        score += _w.PlayerDexterity * st.Dexterity;
        score += _w.PlayerVulnerable * st.Vulnerable;
        score += _w.PlayerWeak * st.Weak;
        score += _w.PlayerFrail * st.Frail;
        var totalEnemyHp = 0;
        foreach (var e in s.Enemies)
            if (!e.IsDead) totalEnemyHp += e.Hp;

        // Power-card value scales with remaining combat duration.
        // A DemonForm against a 250-HP boss is worth multiple full
        // turns of triggers; a DemonForm against a 30-HP-left rat is
        // wasted. We approximate "turns left" as sum(enemy_hp) / 10
        // (~10 damage/turn assumed Ironclad output), clamped to [0.5x, 3x]
        // so the multiplier stays sane.
        var turnsLeft = totalEnemyHp / 10.0;
        var durationMultiplier = Math.Clamp(0.5 + turnsLeft / 10.0, 0.5, 3.0);

        score += _w.DemonForm * st.DemonForm * durationMultiplier;
        score += _w.Combust * st.Combust * durationMultiplier;
        score += _w.Metallicize * st.Metallicize * durationMultiplier;
        score += _w.FeelNoPain * st.FeelNoPain * durationMultiplier;
        score += _w.DarkEmbrace * st.DarkEmbrace * durationMultiplier;
        score += _w.FireBreathing * st.FireBreathing * durationMultiplier;
        score += _w.Rupture * st.Rupture * durationMultiplier;
        score += _w.Juggernaut * st.Juggernaut * durationMultiplier;
        score += _w.Barricade * st.Barricade * durationMultiplier;
        score += _w.Rage * st.Rage * durationMultiplier;
        score += _w.Brutality * st.Brutality * durationMultiplier;
        score += _w.Evolve * st.Evolve * durationMultiplier;
        score += _w.Berserk * st.Berserk * durationMultiplier;
        score += _w.PlatedArmor * st.PlatedArmor * durationMultiplier;
        score += _w.Hellraiser * st.Hellraiser * durationMultiplier;

        score += _w.CardsDrawn * s.CardsDrawnThisTurn;

        // Project incoming damage net of current block to penalise
        // states that look fine right now but will get the player
        // killed next enemy turn.
        var incoming = 0;
        foreach (var e in s.Enemies)
        {
            if (e.IsDead || e.Intent is not { } intent) continue;
            if (intent.Damage > 0) incoming += intent.Damage * Math.Max(1, intent.Hits);
        }
        var unblocked = Math.Max(0, incoming - s.Block);
        score += _w.IncomingDamage * unblocked;

        // Tempo: prefer lines that kill enemies sooner. DPS estimate
        // from Strength + Vulnerable approximates "what a typical
        // 3-energy Ironclad turn deals." Clamp to a sane floor so the
        // term doesn't blow up when Strength=0.
        if (alive)
        {
            var dpsEstimate = Math.Max(6.0, 8.0 + 1.5 * st.Strength);
            var turnsToKill = totalEnemyHp / dpsEstimate;
            score += _w.TempoPenalty * turnsToKill;
        }

        return score;
    }
}

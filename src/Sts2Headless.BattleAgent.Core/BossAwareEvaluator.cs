namespace Sts2Headless.BattleAgent.Core;

// Switches between two HeuristicWeights based on whether the current
// SimState looks like a boss/elite fight (any enemy with MaxHp >=
// BossThreshold). Boss-tier opponents reward damage over block — the
// agent's regular weights, tuned for short fights, are too defensive
// in 250-HP-boss territory.
public sealed class BossAwareEvaluator : IEvaluator
{
    public int BossThreshold { get; }
    public IEvaluator Regular { get; }
    public IEvaluator Boss { get; }

    public BossAwareEvaluator(
        int bossThreshold = 100,
        HeuristicWeights? regularWeights = null,
        HeuristicWeights? bossWeights = null)
    {
        BossThreshold = bossThreshold;
        Regular = new HeuristicEvaluator(regularWeights ?? HeuristicWeights.Default);
        Boss = new HeuristicEvaluator(bossWeights ?? new HeuristicWeights(
            // More aggressive: weight enemy HP harder (kill faster), reduce
            // incoming damage penalty (accept hits to deal damage), boost
            // Strength and DemonForm (long fights compound).
            PlayerHp: 3.0,
            PlayerBlock: 0.2,
            EnemyHp: -4.5,
            EnemyBlock: -0.3,
            EnemyStrength: -5.0,
            PlayerStrength: 12.0,
            PlayerDexterity: 4.0,
            EnemyVulnerable: 6.0,
            EnemyWeak: 6.0,
            PlayerVulnerable: -3.0,
            PlayerWeak: -2.0,
            PlayerFrail: -2.0,
            DemonForm: 80.0,
            Combust: 18.0,
            Metallicize: 14.0,
            FeelNoPain: 12.0,
            DarkEmbrace: 14.0,
            FireBreathing: 10.0,
            Rupture: 6.0,
            Juggernaut: 12.0,
            Barricade: 45.0,
            Rage: 8.0,
            Brutality: 5.0,
            Evolve: 5.0,
            Berserk: 30.0,
            PlatedArmor: 7.0,
            CardsDrawn: 1.0,
            IncomingDamage: -1.5));
    }

    public double Score(SimState s)
    {
        return IsBossFight(s) ? Boss.Score(s) : Regular.Score(s);
    }

    private bool IsBossFight(SimState state)
    {
        foreach (var e in state.Enemies)
        {
            if (e.IsDead) continue;
            if (e.MaxHp >= BossThreshold) return true;
        }
        return false;
    }
}

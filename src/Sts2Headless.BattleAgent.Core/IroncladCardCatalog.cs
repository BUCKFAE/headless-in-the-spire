using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent.Core;

// Ironclad card effects, both base and upgraded variants. The numbers
// here trace back to:
//   - existing CardMechanics.cs (the seed-42 path baseline, already
//     verified to load and play through the engine)
//   - STS1 Ironclad card stats where the card name appears identical
//   - "TODO verify against engine" for STS2-original cards
//     (Bully, Tremble, BloodWall, AshenStrike, …)
//
// Parity tests (Sts2Headless.IntegrationTests/CombatParityTests.cs) will
// drive each modelled card through the engine and assert post-play
// state matches the simulator. Stat drift surfaces there, not in
// production runs, so the catalog can be updated as a one-line change
// when a parity test goes red.
//
// IsHeadlessUnsafe cards must never be returned by LegalActions; the
// planner respects that flag and the runtime never invokes their
// custom handler. They're catalogued anyway so DraftPolicy can rate
// them down at reward time.
public sealed class IroncladCardCatalog : ICardEffectCatalog
{
    public static IroncladCardCatalog Instance { get; } = new();

    private IroncladCardCatalog() { }

    public CardEffect? GetEffect(CardId cardId, bool upgraded) =>
        upgraded
            ? GetUpgraded(cardId) ?? GetBase(cardId)
            : GetBase(cardId);

    public IReadOnlyCollection<CardId> ModelledIds => s_modelled;

    // ── Base (unupgraded) effects ──────────────────────────────────────
    private static CardEffect? GetBase(CardId id) => id switch
    {
        // Starter
        CardId.StrikeIronclad => new(IsAttack: true, Damage: 6),
        CardId.DefendIronclad => new(IsSkill: true, Block: 5),
        CardId.Bash           => new(IsAttack: true, Damage: 8, VulnerableApply: 2),

        // Commons
        CardId.Anger          => new(IsAttack: true, Damage: 6),
        CardId.Clash          => new(IsAttack: true, Damage: 14), // attacks-only constraint enforced by planner heuristic
        CardId.Headbutt       => new(IsAttack: true, Damage: 9, IsHeadlessUnsafe: true),
        CardId.IronWave       => new(IsAttack: true, Damage: 5, Block: 5),
        CardId.PerfectedStrike=> new(IsAttack: true, Damage: 6),  // +2/Strike scaling deferred
        CardId.PommelStrike   => new(IsAttack: true, Damage: 9, DrawCards: 1),
        CardId.ShrugItOff     => new(IsSkill: true, Block: 8, DrawCards: 1),
        CardId.SwordBoomerang => new(IsAttack: true, Damage: 3, Hits: 3, TargetsAllEnemies: false),
        CardId.Thunderclap    => new(IsAttack: true, Damage: 4, TargetsAllEnemies: true, VulnerableApply: 1),
        CardId.TrueGrit       => new(IsSkill: true, Block: 7, Exhausts: true, ExhaustRandomFromHand: 1),
        CardId.TwinStrike     => new(IsAttack: true, Damage: 5, Hits: 2),
        CardId.Havoc          => new(IsSkill: true, Exhausts: true), // play top of draw — sub-flow, treat as no-op safe
        CardId.Inflame        => new(IsPower: true, StrengthGain: 2),

        // STS2-original commons (values from CardMechanics seed-42 baseline)
        CardId.BodySlam       => new(IsAttack: true, BlockToDamage: true),
        CardId.Bully          => new(IsAttack: true, Damage: 8), // TODO verify STS2 stats
        CardId.Tremble        => new(IsSkill: true), // TODO verify; treated as no-op for now

        // Uncommons
        CardId.Bludgeon       => new(IsAttack: true, Damage: 32),
        CardId.Uppercut       => new(IsAttack: true, Damage: 13, WeakApply: 1, VulnerableApply: 1),
        CardId.Armaments      => new(IsSkill: true, Block: 5, IsHeadlessUnsafe: true),
        CardId.BloodWall      => new(IsAttack: true, Damage: 4, Block: 6), // STS2 — values from CardMechanics
        CardId.Bloodletting   => new(IsSkill: true, SelfDamage: 3, EnergyGain: 2, DrawCards: 1),
        CardId.BurningPact    => new(IsSkill: true, IsHeadlessUnsafe: true),
        CardId.BattleTrance   => new(IsSkill: true, DrawCards: 3),
        CardId.DualWield      => new(IsSkill: true, IsHeadlessUnsafe: true), // copies a card — needs card-select
        CardId.Entrench       => new(IsSkill: true, Custom: EntrenchHandler),
        CardId.FlameBarrier   => new(IsSkill: true, Block: 12),  // retaliate not modelled
        CardId.Hemokinesis    => new(IsAttack: true, Damage: 15, SelfDamage: 2),
        CardId.Impervious     => new(IsSkill: true, Block: 30, Exhausts: true),
        CardId.InfernalBlade  => new(IsSkill: true, IsHeadlessUnsafe: true), // random attack to hand
        CardId.SecondWind     => new(IsSkill: true, Exhausts: true, DiscardForBlock: 5),
        CardId.Shockwave      => new(IsSkill: true, TargetsAllEnemies: true, WeakApply: 3, VulnerableApply: 3, Exhausts: true),
        CardId.Rage           => new(IsPower: true, RageGain: 3),
        CardId.Rampage        => new(IsAttack: true, Damage: 8), // scaling deferred
        CardId.Rupture        => new(IsPower: true, RuptureGain: 1),

        // Uncommon powers
        CardId.DarkEmbrace    => new(IsPower: true, DarkEmbraceGain: 1),
        CardId.FeelNoPain     => new(IsPower: true, FeelNoPainGain: 3),
        CardId.Juggernaut     => new(IsPower: true, JuggernautGain: 5),

        // STS2-original uncommons (best-guess; parity tests will surface drift)
        CardId.AshenStrike    => new(IsAttack: true, Damage: 6), // TODO verify
        CardId.Cascade        => new(IsSkill: true), // TODO verify — likely scaling/synergy
        CardId.Dismantle      => new(IsSkill: true), // TODO verify
        CardId.Taunt          => new(IsSkill: true), // TODO verify
        CardId.StoneArmor     => new(IsPower: true, PlatedArmorGain: 1),
        CardId.ExpectAFight   => new(IsPower: true), // TODO verify
        CardId.CrimsonMantle  => new(IsPower: true), // TODO verify
        CardId.Brand          => new(IsAttack: true, Damage: 6, SelfDamage: 2), // best guess; STS2 "self-damage archetype" card

        // Rares
        CardId.FiendFire      => new(IsAttack: true, Damage: 7, Exhausts: true, Custom: FiendFireHandler),
        CardId.Feed           => new(IsAttack: true, Damage: 10, Exhausts: true, Custom: FeedHandler),
        CardId.Offering       => new(IsSkill: true, SelfDamage: 6, EnergyGain: 2, DrawCards: 3, Exhausts: true),
        // Whirlwind NREs in the headless engine when played
        // (CrashTracingTransport caught it on seeds 3/5/7/8 of the
        // 10-seed sweep on 2026-05-18). The X-cost path routes through
        // a sub-flow the host doesn't fully wire. Marked unsafe until
        // the engine-side fix or the host gains the missing screen.
        CardId.Whirlwind      => new(IsAttack: true, IsHeadlessUnsafe: true),
        CardId.Barricade      => new(IsPower: true, BarricadeGain: 1),
        CardId.Corruption     => new(IsPower: true),  // exhaust-skill behaviour deferred
        CardId.DemonForm      => new(IsPower: true, DemonFormGain: 2),
        CardId.PactsEnd       => new(IsAttack: true, Damage: 12), // STS2 — scales with exhaust pile size; deferred

        // Status / curse
        CardId.Infection      => new(IsStatus: true, Ethereal: true), // unplayable in engine; treated as no-op here

        _ => null,
    };

    // ── Upgraded effects (only differences from base are returned) ─────
    private static CardEffect? GetUpgraded(CardId id) => id switch
    {
        CardId.StrikeIronclad => new(IsAttack: true, Damage: 9),
        CardId.DefendIronclad => new(IsSkill: true, Block: 8),
        CardId.Bash           => new(IsAttack: true, Damage: 10, VulnerableApply: 3),

        CardId.Anger          => new(IsAttack: true, Damage: 8),
        CardId.Clash          => new(IsAttack: true, Damage: 18),
        CardId.Headbutt       => new(IsAttack: true, Damage: 12, IsHeadlessUnsafe: true),
        CardId.IronWave       => new(IsAttack: true, Damage: 7, Block: 7),
        CardId.PerfectedStrike=> new(IsAttack: true, Damage: 6),  // +3/Strike upgrade; scaling deferred
        CardId.PommelStrike   => new(IsAttack: true, Damage: 10, DrawCards: 2),
        CardId.ShrugItOff     => new(IsSkill: true, Block: 11, DrawCards: 1),
        CardId.SwordBoomerang => new(IsAttack: true, Damage: 3, Hits: 4),
        CardId.Thunderclap    => new(IsAttack: true, Damage: 7, TargetsAllEnemies: true, VulnerableApply: 1),
        CardId.TrueGrit       => new(IsSkill: true, Block: 9, Exhausts: true, ExhaustRandomFromHand: 1),
        CardId.TwinStrike     => new(IsAttack: true, Damage: 7, Hits: 2),
        CardId.Inflame        => new(IsPower: true, StrengthGain: 3),

        CardId.BodySlam       => new(IsAttack: true, BlockToDamage: true), // cost 0 upgrade — cost change applied via SimCard.Cost

        CardId.Bludgeon       => new(IsAttack: true, Damage: 42),
        CardId.Uppercut       => new(IsAttack: true, Damage: 13, WeakApply: 2, VulnerableApply: 2),
        CardId.Armaments      => new(IsSkill: true, Block: 5, IsHeadlessUnsafe: true), // upgrade-all-in-hand variant
        CardId.Bloodletting   => new(IsSkill: true, SelfDamage: 3, EnergyGain: 3, DrawCards: 1),
        CardId.BattleTrance   => new(IsSkill: true, DrawCards: 4),
        CardId.FlameBarrier   => new(IsSkill: true, Block: 16),
        CardId.Hemokinesis    => new(IsAttack: true, Damage: 20, SelfDamage: 2),
        CardId.Impervious     => new(IsSkill: true, Block: 40, Exhausts: true),
        CardId.SecondWind     => new(IsSkill: true, Exhausts: true, DiscardForBlock: 7),
        CardId.Rage           => new(IsPower: true, RageGain: 5),
        CardId.Rupture        => new(IsPower: true, RuptureGain: 2),

        CardId.DarkEmbrace    => new(IsPower: true, DarkEmbraceGain: 1), // upgrade: cost 1
        CardId.FeelNoPain     => new(IsPower: true, FeelNoPainGain: 4),
        CardId.Juggernaut     => new(IsPower: true, JuggernautGain: 7),

        CardId.FiendFire      => new(IsAttack: true, Damage: 10, Exhausts: true, Custom: FiendFireHandler),
        CardId.Feed           => new(IsAttack: true, Damage: 12, Exhausts: true, Custom: FeedHandler),
        CardId.Offering       => new(IsSkill: true, SelfDamage: 6, EnergyGain: 2, DrawCards: 5, Exhausts: true),
        CardId.Whirlwind      => new(IsAttack: true, IsHeadlessUnsafe: true),
        CardId.DemonForm      => new(IsPower: true, DemonFormGain: 3),

        _ => null,
    };

    // ── Custom handlers for cards that don't fit the declarative shape ─

    // Entrench: double current block.
    private static SimState EntrenchHandler(CardEffectContext ctx)
        => ctx.State with { Block = ctx.State.Block * 2 };

    // Fiend Fire: exhaust hand; deal 7 (or 10 upgraded) per card exhausted
    // to single target.
    private static SimState FiendFireHandler(CardEffectContext ctx)
    {
        var perCard = ctx.Card.Upgraded ? 10 : 7;
        var exhausted = ctx.State.Hand.Count;  // about-to-exhaust count
        var dmg = perCard * exhausted;
        var (state, _) = CombatModel.DealSingleTargetDamage(
            ctx.State, ctx.TargetIndex ?? 0, dmg, hits: 1);
        // Move entire hand to exhaust pile.
        return state with
        {
            Hand = Array.Empty<SimCard>(),
            ExhaustPileCount = state.ExhaustPileCount + exhausted,
        };
    }

    // Feed: deal damage; if it kills, gain +3 max HP (+4 upgraded).
    private static SimState FeedHandler(CardEffectContext ctx)
    {
        var dmg = ctx.Card.Upgraded ? 12 : 10;
        var gain = ctx.Card.Upgraded ? 4 : 3;
        var target = ctx.TargetIndex ?? 0;
        var preTargetHp = ctx.State.Enemies.ElementAtOrDefault(target)?.Hp ?? 0;
        var (state, _) = CombatModel.DealSingleTargetDamage(ctx.State, target, dmg, hits: 1);
        var postTargetHp = state.Enemies.ElementAtOrDefault(target)?.Hp ?? 0;
        if (preTargetHp > 0 && postTargetHp <= 0)
        {
            return state with
            {
                MaxHp = state.MaxHp + gain,
                Hp = state.Hp + gain,
            };
        }
        return state;
    }

    // The full set of CardIds the catalog has explicit modelling for.
    // Computed once at type init from the GetBase switch above.
    private static readonly IReadOnlyCollection<CardId> s_modelled = ComputeModelled();

    private static IReadOnlyCollection<CardId> ComputeModelled()
    {
        var ids = new List<CardId>();
        foreach (CardId id in Enum.GetValues(typeof(CardId)))
        {
            if (GetBase(id) is not null) ids.Add(id);
        }
        return ids;
    }
}

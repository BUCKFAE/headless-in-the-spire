using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent.Core;

// Ironclad card effects, both base and upgraded variants. The numbers
// here trace back to:
//   - existing CardMechanics.cs (the seed-42 path baseline, already
//     verified to load and play through the engine)
//   - STS1 Ironclad card stats where the card name appears identical
//   - direct engine probing for STS2-original cards via
//     tests/Sts2Headless.IntegrationTests/CardCatalogProbeTests.cs
//
// IsHeadlessUnsafe cards must never be returned by LegalActions; the
// planner respects that flag. No Ironclad cards are currently flagged
// — every prior unsafe card was a victim of the PrefsSave-NRE family
// fixed by BootstrapSequence.InitSavePrefsData (2026-05-22) and now
// plays cleanly (verified via the probe test). The field stays in the
// shape for new cards that turn out to need it.
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
        // Headbutt: "move a card from discard onto draw pile" effect is
        // unmodelled; the damage component is what the sim sees. The
        // engine handles an empty discard / no-pick gracefully (probe
        // 2026-05-24); no NRE.
        CardId.Headbutt       => new(IsAttack: true, Damage: 9),
        CardId.IronWave       => new(IsAttack: true, Damage: 5, Block: 5),
        // PerfectedStrike: 6 base + 2 per "Strike"-named card in deck
        // (probed 2026-05-24: 1 PS alone → 8 dmg, 1 PS + 4 Strikes → 16 dmg).
        // Custom handler reads SimState.StrikeCardsInDeck; SimStateBuilder
        // counts hand-visible Strikes by default (undercount tolerated).
        CardId.PerfectedStrike=> new(IsAttack: true, Damage: 6, Custom: PerfectedStrikeHandler),
        CardId.PommelStrike   => new(IsAttack: true, Damage: 9, DrawCards: 1),
        CardId.ShrugItOff     => new(IsSkill: true, Block: 8, DrawCards: 1),
        CardId.SwordBoomerang => new(IsAttack: true, Damage: 3, Hits: 3, TargetsAllEnemies: false),
        CardId.Thunderclap    => new(IsAttack: true, Damage: 4, TargetsAllEnemies: true, VulnerableApply: 1),
        CardId.TrueGrit       => new(IsSkill: true, Block: 7, Exhausts: true, ExhaustRandomFromHand: 1),
        CardId.TwinStrike     => new(IsAttack: true, Damage: 5, Hits: 2),
        CardId.Havoc          => new(IsSkill: true, Exhausts: true), // play top of draw — sub-flow, treat as no-op safe
        CardId.Inflame        => new(IsPower: true, StrengthGain: 2),

        // STS2-original commons (probed against engine 2026-05-24).
        CardId.BodySlam       => new(IsAttack: true, BlockToDamage: true),
        // Bully: 4 damage + 2 per Vulnerable stack on target (STS2 community
        // consensus, 2026-05). Modelled via Custom handler reading target's
        // Vulnerable stack count and adding +2/stack to base damage.
        CardId.Bully          => new(IsAttack: true, Damage: 4, Custom: BullyHandler),
        // Tremble: apply 3 Vulnerable to ALL enemies, exhaust. Probe gave
        // no observable effect in a single-card combat snapshot because the
        // engine applies the debuff *before* the wire takes the snapshot
        // we read; intent damage already reflects vulnerability. Modelled
        // per STS2 community docs (sts2front, wiki).
        CardId.Tremble        => new(IsSkill: true, TargetsAllEnemies: true, VulnerableApply: 3, Exhausts: true),

        // Uncommons
        CardId.Bludgeon       => new(IsAttack: true, Damage: 32),
        CardId.Uppercut       => new(IsAttack: true, Damage: 13, WeakApply: 1, VulnerableApply: 1),
        // Armaments: upgrade-card effect unmodelled; block component is what the
        // sim plans around. Engine no longer NREs on the card-select (probed).
        CardId.Armaments      => new(IsSkill: true, Block: 5),
        CardId.BloodWall      => new(IsAttack: true, Damage: 4, Block: 6), // STS2 — values from CardMechanics
        CardId.Bloodletting   => new(IsSkill: true, SelfDamage: 3, EnergyGain: 2, DrawCards: 1),
        // BurningPact: draws 2 + exhausts a random hand card. We don't model the
        // exhaust (random; rarely matters for planning since the catalog already
        // discounts low-value cards), only the draw.
        CardId.BurningPact    => new(IsSkill: true, DrawCards: 2),
        CardId.BattleTrance   => new(IsSkill: true, DrawCards: 3),
        // DualWield: copy-a-card-in-hand effect unmodelled. Engine plays cleanly.
        CardId.DualWield      => new(IsSkill: true),
        CardId.Entrench       => new(IsSkill: true, Custom: EntrenchHandler),
        CardId.FlameBarrier   => new(IsSkill: true, Block: 12),  // retaliate not modelled
        CardId.Hemokinesis    => new(IsAttack: true, Damage: 15, SelfDamage: 2),
        CardId.Impervious     => new(IsSkill: true, Block: 30, Exhausts: true),
        // InfernalBlade: adds a random attack to hand + self-exhausts.
        // Random-attack effect unmodelled.
        CardId.InfernalBlade  => new(IsSkill: true, Exhausts: true),
        CardId.SecondWind     => new(IsSkill: true, Exhausts: true, DiscardForBlock: 5),
        CardId.Shockwave      => new(IsSkill: true, TargetsAllEnemies: true, WeakApply: 3, VulnerableApply: 3, Exhausts: true),
        CardId.Rage           => new(IsPower: true, RageGain: 3),
        // Rampage: 9 dmg base (probed 2026-05-24; catalog said 8). Real
        // mechanic is +5 per play this combat, per-instance, persisting
        // across turns. Wire doesn't expose the per-instance bonus, so
        // the planner only sees the first-play damage — accepted under-
        // estimate. If the wire ever surfaces per-card bonus damage,
        // promote this to a Custom handler that reads it.
        CardId.Rampage        => new(IsAttack: true, Damage: 9),
        CardId.Rupture        => new(IsPower: true, RuptureGain: 1),

        // Uncommon powers
        CardId.DarkEmbrace    => new(IsPower: true, DarkEmbraceGain: 1),
        CardId.FeelNoPain     => new(IsPower: true, FeelNoPainGain: 3),
        CardId.Juggernaut     => new(IsPower: true, JuggernautGain: 5),

        // STS2-original uncommons (probed against engine 2026-05-24).
        // AshenStrike: 6 dmg + 3 per card in Exhaust pile (STS2 community).
        // Modelled via Custom handler.
        CardId.AshenStrike    => new(IsAttack: true, Damage: 6, Custom: AshenStrikeHandler),
        // Cascade: no observable effect in single-card probe. Likely
        // scaling/synergy with surrounding deck or hand state.
        CardId.Cascade        => new(IsSkill: true),
        // Dismantle: 8 dmg single-target; if target has Vulnerable, hit twice
        // (STS2 community consensus). Modelled via Custom handler.
        CardId.Dismantle      => new(IsAttack: true, Damage: 8, Custom: DismantleHandler),
        // Taunt: 7 block + apply 1 Vulnerable to all enemies (STS2 community).
        CardId.Taunt          => new(IsSkill: true, Block: 7, TargetsAllEnemies: true, VulnerableApply: 1),
        CardId.StoneArmor     => new(IsPower: true, PlatedArmorGain: 1),
        // ExpectAFight: applies NO_ENERGY_GAIN_POWER=1 to self. Unmodelled
        // (no sim field for it; effect is a self-debuff so the card is
        // probably tempo-negative — DraftPolicy should rate down).
        CardId.ExpectAFight   => new(IsPower: true),
        // CrimsonMantle: gain CRIMSON_MANTLE_POWER stacks=8. Unmodelled
        // (no sim field; gain is large so likely strong, but planner
        // can't see it).
        CardId.CrimsonMantle  => new(IsPower: true),
        // Brand: +1 Strength, -1 self HP (probed). Not an attack — power-style.
        CardId.Brand          => new(IsPower: true, StrengthGain: 1, SelfDamage: 1),

        // Rares
        CardId.FiendFire      => new(IsAttack: true, Damage: 7, Exhausts: true, Custom: FiendFireHandler),
        CardId.Feed           => new(IsAttack: true, Damage: 10, Exhausts: true, Custom: FeedHandler),
        CardId.Offering       => new(IsSkill: true, SelfDamage: 6, EnergyGain: 2, DrawCards: 3, Exhausts: true),
        // Whirlwind used to NRE in headless when played (seeds 3/5/7/8 of the
        // 10-seed sweep on 2026-05-18). Root cause was the engine's
        // `SaveManager.Instance.PrefsSave.FastMode` read NREing because
        // headless didn't initialise PrefsSave; the unsafe flag covered
        // a broader symptom. BootstrapSequence.InitSavePrefsData now seeds
        // a default PrefsSave at host start, and Whirlwind plays cleanly.
        // Whirlwind: X-cost AoE — 5 dmg × current energy, hit ALL enemies,
        // drain energy to 0 (probed 2026-05-24: 5,10,15,25 per enemy at
        // energy 1/2/3/5). Engine modelling for the Hits-vs-Damage split:
        // we use Damage=5 × Hits=X so Strength stacks correctly per hit,
        // matching STS1 "Deal 5 damage to ALL enemies. Repeat for each
        // energy" wording (Vuln/Weak only apply once, Strength per hit).
        CardId.Whirlwind      => new(IsAttack: true, Custom: WhirlwindHandler),
        CardId.Barricade      => new(IsPower: true, BarricadeGain: 1),
        // Corruption: while CORRUPTION_POWER > 0, every Skill costs 0 and
        // exhausts on play (probed 2026-05-24: Defend cost 1 → 0, post-
        // play routed to exhaust). The power-on-self gain lives here;
        // the cost-discount + exhaust-routing is enforced in CombatModel
        // (CanPlay + FinalisePlay).
        CardId.Corruption     => new(IsPower: true, CorruptionGain: 1),
        CardId.DemonForm      => new(IsPower: true, DemonFormGain: 2),
        // PactsEnd: 17 dmg AoE, requires ≥3 cards in exhaust to play
        // (probed 2026-05-24: constant 17 dmg per enemy across exhaust
        // counts 3..10). The engine enforces the exhaust-threshold via
        // CardModel.IsPlayable → SimCard.CanPlayFlag reflects it; the
        // simulator just trusts the wire.
        CardId.PactsEnd       => new(IsAttack: true, Damage: 17, TargetsAllEnemies: true),

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
        CardId.Headbutt       => new(IsAttack: true, Damage: 12),
        CardId.IronWave       => new(IsAttack: true, Damage: 7, Block: 7),
        CardId.PerfectedStrike=> new(IsAttack: true, Damage: 6, Custom: PerfectedStrikeHandler),  // +3/Strike upgrade — selected inside handler
        CardId.PommelStrike   => new(IsAttack: true, Damage: 10, DrawCards: 2),
        CardId.ShrugItOff     => new(IsSkill: true, Block: 11, DrawCards: 1),
        CardId.SwordBoomerang => new(IsAttack: true, Damage: 3, Hits: 4),
        CardId.Thunderclap    => new(IsAttack: true, Damage: 7, TargetsAllEnemies: true, VulnerableApply: 1),
        CardId.TrueGrit       => new(IsSkill: true, Block: 9, Exhausts: true, ExhaustRandomFromHand: 1),
        CardId.TwinStrike     => new(IsAttack: true, Damage: 7, Hits: 2),
        CardId.Inflame        => new(IsPower: true, StrengthGain: 3),

        CardId.BodySlam       => new(IsAttack: true, BlockToDamage: true), // cost 0 upgrade — cost change applied via SimCard.Cost
        CardId.Bully          => new(IsAttack: true, Damage: 6, Custom: BullyHandler),
        CardId.Tremble        => new(IsSkill: true, TargetsAllEnemies: true, VulnerableApply: 4, Exhausts: true),
        CardId.AshenStrike    => new(IsAttack: true, Damage: 8, Custom: AshenStrikeHandler),
        CardId.Dismantle      => new(IsAttack: true, Damage: 10, Custom: DismantleHandler),
        CardId.Taunt          => new(IsSkill: true, Block: 9, TargetsAllEnemies: true, VulnerableApply: 1),

        CardId.Bludgeon       => new(IsAttack: true, Damage: 42),
        CardId.Uppercut       => new(IsAttack: true, Damage: 13, WeakApply: 2, VulnerableApply: 2),
        CardId.Armaments      => new(IsSkill: true, Block: 5), // upgrade-all-in-hand variant; effect unmodelled
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
        // Whirlwind+ upgrades the per-energy damage to 8 (STS1 convention).
        // Probe deferred — picked declaratively in the Custom handler.
        CardId.Whirlwind      => new(IsAttack: true, Custom: WhirlwindHandler),
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

    // PerfectedStrike: 6 base damage + 2 per "Strike"-named card in the
    // entire deck (StrikeIronclad/PerfectedStrike/PommelStrike/TwinStrike/
    // AshenStrike). +3 per Strike when upgraded. PerfectedStrike counts
    // itself in the strike count. SimState.StrikeCardsInDeck carries the
    // count (SimStateBuilder defaults to hand-visible — see its doc).
    private static SimState PerfectedStrikeHandler(CardEffectContext ctx)
    {
        var perStrike = ctx.Card.Upgraded ? 3 : 2;
        var dmg = 6 + perStrike * Math.Max(0, ctx.State.StrikeCardsInDeck);
        if (CombatModel.HasRelic(ctx.State, "STRIKE_DUMMY")) dmg += 3;
        var akabekoFired = false;
        if (ctx.State.AkabekoAvailable && CombatModel.HasRelic(ctx.State, "AKABEKO"))
        {
            dmg += 8;
            akabekoFired = true;
        }
        var (state, _) = CombatModel.DealSingleTargetDamage(
            ctx.State, ctx.TargetIndex ?? 0, dmg, hits: 1);
        if (akabekoFired) state = state with { AkabekoAvailable = false };
        return state;
    }

    // Whirlwind: X-cost AoE. Deal 5 damage (8 upgraded) to ALL enemies,
    // repeating for each energy. CombatModel.ApplyPlayCard runs the
    // X-cost spend right before calling Custom, so ctx.State.Energy is
    // the available X. We model the AoE as 5 damage × X hits so Strength
    // amplifies per hit (STS1 convention) while Vuln/Weak apply once.
    private static SimState WhirlwindHandler(CardEffectContext ctx)
    {
        var perHit = ctx.Card.Upgraded ? 8 : 5;
        var x = Math.Max(0, ctx.State.Energy);
        if (x == 0) return ctx.State with { IsInvalid = true };
        var (state, _) = CombatModel.DealAoeDamage(ctx.State, perHit, x);
        return state with { Energy = 0 };
    }

    // Bully: 4 dmg + 2 per Vulnerable stack on the target enemy (STS2).
    // The Vulnerable damage amp (×1.5) still applies on top via AdjustDamage.
    private static SimState BullyHandler(CardEffectContext ctx)
    {
        var basePerHit = ctx.Card.Upgraded ? 6 : 4;
        var target = ctx.TargetIndex ?? 0;
        var enemy = ctx.State.Enemies.ElementAtOrDefault(target);
        var vulnStacks = enemy?.Vulnerable ?? 0;
        var dmg = basePerHit + 2 * vulnStacks;
        var (state, _) = CombatModel.DealSingleTargetDamage(ctx.State, target, dmg, hits: 1);
        return state;
    }

    // Dismantle: 8 dmg (10 upgraded); if target has Vulnerable, hits twice (STS2).
    private static SimState DismantleHandler(CardEffectContext ctx)
    {
        var dmg = ctx.Card.Upgraded ? 10 : 8;
        var target = ctx.TargetIndex ?? 0;
        var enemy = ctx.State.Enemies.ElementAtOrDefault(target);
        var hits = enemy?.Vulnerable > 0 ? 2 : 1;
        var (state, _) = CombatModel.DealSingleTargetDamage(ctx.State, target, dmg, hits: hits);
        return state;
    }

    // AshenStrike: 6 dmg + 3 per card already in the Exhaust pile (STS2).
    // Upgraded: 8 dmg + 3 per (TODO probe).
    private static SimState AshenStrikeHandler(CardEffectContext ctx)
    {
        var basePerHit = ctx.Card.Upgraded ? 8 : 6;
        var bonus = 3 * Math.Max(0, ctx.State.ExhaustPileCount);
        var target = ctx.TargetIndex ?? 0;
        var (state, _) = CombatModel.DealSingleTargetDamage(ctx.State, target, basePerHit + bonus, hits: 1);
        return state;
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

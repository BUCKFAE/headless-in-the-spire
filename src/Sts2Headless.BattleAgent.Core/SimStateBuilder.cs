using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent.Core;

// Converts the wire's CombatState into a SimState the planner can
// operate on. One-way translation — once the agent commits a SimAction,
// SimAgent translates back to the wire-shaped AgentAction.
//
// Notes / loss of information (acceptable for v1):
//   - ExhaustPileCount is not on the wire; we initialise to 0. The
//     simulator tracks exhausts during the turn for FeelNoPain /
//     DarkEmbrace / Pact's-End-scaling-by-exhaust-size, but the
//     starting count is unknown.
//   - PlayerPowers come as a list of {id, amount}; we map known ids
//     to typed fields on PlayerStatus and stash the rest in Other.
public static class SimStateBuilder
{
    // strikeCardsInDeck — if the caller has a full-run deck tracker it
    // can pass the engine-truth count of "Strike"-named cards (Strike,
    // PerfectedStrike, PommelStrike, TwinStrike, AshenStrike, …) in the
    // deck. When null, we fall back to a hand-visible count (every
    // Strike-named card visible in the current hand). That undercounts
    // — there could be Strikes still in draw/discard/exhaust we can't
    // see — but a wrong-low PerfectedStrike damage is safer than a
    // wrong-high one: the planner picks it only when the visible
    // contribution alone already makes the play attractive.
    public static SimState FromWire(
        CombatState combat,
        int currentHp,
        int maxHp,
        int? strikeCardsInDeck = null,
        IReadOnlyCollection<string>? relics = null,
        IReadOnlyList<CardId>? deckCardIds = null)
    {
        var status = ReadStatus(combat.PlayerPowers);
        var hand = combat.Hand
            .Select(c => new SimCard(
                Id: c.Id,
                Cost: c.Cost,
                Upgraded: c.Upgraded,
                TargetType: c.TargetType,
                CanPlayFlag: c.CanPlay,
                OriginalHandIndex: c.Index))
            .ToArray();
        var enemies = combat.Enemies
            .Select(MapEnemy)
            .ToArray();
        var strikes = strikeCardsInDeck ?? hand.Count(c => IsStrikeNamedCard(c.Id));

        return new SimState(
            Hp: currentHp,
            MaxHp: maxHp,
            Energy: combat.Energy,
            MaxEnergyPerTurn: combat.MaxEnergy,
            Block: combat.PlayerBlock,
            Turn: combat.Round,
            Status: status,
            Hand: hand,
            DrawPileCount: combat.DrawPileCount,
            DiscardPileCount: combat.DiscardPileCount,
            ExhaustPileCount: 0,
            Enemies: enemies,
            CardsDrawnThisTurn: 0,
            IsInvalid: false,
            StrikeCardsInDeck: strikes,
            Relics: relics,
            DeckCardIds: deckCardIds);
    }

    // CardIds whose wire name contains "Strike". The CardId enum's
    // JsonStringEnumMemberName is the canonical wire id (SCREAMING_SNAKE);
    // we don't have direct access to that mapping at runtime, so the
    // list is enumerated explicitly. Update when sts2 adds new Strike-
    // named cards (engine probe: drop the new card into a deck and watch
    // PerfectedStrike's damage move).
    public static bool IsStrikeNamedCard(CardId id) => id switch
    {
        CardId.StrikeIronclad
            or CardId.PerfectedStrike
            or CardId.PommelStrike
            or CardId.TwinStrike
            or CardId.AshenStrike => true,
        _ => false,
    };

    private static SimEnemy MapEnemy(Enemy e)
    {
        var strength = ReadPower(e.Powers, PowerId.StrengthPower);
        var vuln = ReadPower(e.Powers, PowerId.VulnerablePower);
        var weak = ReadPower(e.Powers, PowerId.WeakPower);
        var slippery = ReadPower(e.Powers, PowerId.SlipperyPower);
        var plowThreshold = ReadPower(e.Powers, PowerId.PlowPower);
        var artifact = ReadPower(e.Powers, PowerId.ArtifactPower);
        var firstIntent = e.Intents.Count > 0 ? e.Intents[0] : null;
        EnemyIntent? intent = firstIntent is null
            ? null
            : new EnemyIntent(
                Kind: firstIntent.Kind,
                Damage: firstIntent.Damage ?? 0,
                Hits: firstIntent.Hits ?? 1,
                Block: firstIntent.Block ?? 0);

        var other = e.Powers
            .Where(p => !IsKnownEnemyPower(p.Id))
            .Select(p => new OpaquePower(p.Id.ToString(), p.Amount))
            .ToArray();

        return new SimEnemy(
            Index: e.Index,
            MonsterId: e.MonsterId.ToString(),
            Hp: e.Hp,
            MaxHp: e.MaxHp,
            Block: e.Block,
            Strength: strength,
            Vulnerable: vuln,
            Weak: weak,
            Intent: intent,
            OtherPowers: other.Length == 0 ? null : other,
            Slippery: slippery,
            PlowThreshold: plowThreshold,
            Artifact: artifact);
    }

    private static PlayerStatus ReadStatus(IReadOnlyList<Power> powers)
    {
        // Map every known power id to a typed field on PlayerStatus.
        return new PlayerStatus(
            Strength:      ReadPower(powers, PowerId.StrengthPower),
            Dexterity:     ReadPower(powers, PowerId.DexterityPower),
            Vulnerable:    ReadPower(powers, PowerId.VulnerablePower),
            Weak:          ReadPower(powers, PowerId.WeakPower),
            Frail:         ReadPower(powers, PowerId.FrailPower),
            // Several STS1 powers (Combust, Metallicize, PlatedArmor,
            // FireBreathing, Brutality, Evolve) are not in the STS2
            // PowerId wire enum at the current pin — surface 0 until
            // they appear (or the enum is regenerated to cover them).
            Combust:       0,
            Metallicize:   0,
            PlatedArmor:   0,
            FeelNoPain:    ReadPower(powers, PowerId.FeelNoPainPower),
            DarkEmbrace:   ReadPower(powers, PowerId.DarkEmbracePower),
            FireBreathing: 0,
            Rupture:       ReadPower(powers, PowerId.RupturePower),
            DemonForm:     ReadPower(powers, PowerId.DemonFormPower),
            Rage:          ReadPower(powers, PowerId.RagePower),
            Juggernaut:    ReadPower(powers, PowerId.JuggernautPower),
            Brutality:     0,
            Evolve:        0,
            // Berserk is not in the generated PowerId enum (absent from
            // sts2.dll's content table at the current pin) — leave at 0
            // until the engine surfaces it through the wire id list.
            Berserk:       0,
            Barricade:     ReadPower(powers, PowerId.BarricadePower),
            Corruption:    ReadPower(powers, PowerId.CorruptionPower),
            Ringing:       ReadPower(powers, PowerId.RingingPower),
            Thorns:        ReadPower(powers, PowerId.ThornsPower));
    }

    private static int ReadPower(IReadOnlyList<Power> powers, PowerId id)
    {
        foreach (var p in powers)
            if (p.Id == id) return p.Amount;
        return 0;
    }

    private static bool IsKnownEnemyPower(PowerId id) => id switch
    {
        PowerId.StrengthPower or PowerId.VulnerablePower or PowerId.WeakPower
            or PowerId.SlipperyPower or PowerId.PlowPower or PowerId.ArtifactPower => true,
        _ => false,
    };
}

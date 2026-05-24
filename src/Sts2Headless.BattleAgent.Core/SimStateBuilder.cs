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
        var strength = ReadPower(e.Powers, "STRENGTH_POWER");
        var vuln = ReadPower(e.Powers, "VULNERABLE_POWER");
        var weak = ReadPower(e.Powers, "WEAK_POWER");
        var slippery = ReadPower(e.Powers, "SLIPPERY_POWER");
        var plowThreshold = ReadPower(e.Powers, "PLOW_POWER");
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
            .Select(p => new OpaquePower(p.Id, p.Amount))
            .ToArray();

        return new SimEnemy(
            Index: e.Index,
            MonsterId: e.MonsterId,
            Hp: e.Hp,
            MaxHp: e.MaxHp,
            Block: e.Block,
            Strength: strength,
            Vulnerable: vuln,
            Weak: weak,
            Intent: intent,
            OtherPowers: other.Length == 0 ? null : other,
            Slippery: slippery,
            PlowThreshold: plowThreshold);
    }

    private static PlayerStatus ReadStatus(IReadOnlyList<Power> powers)
    {
        // Map every known power id to a typed field on PlayerStatus.
        // The id strings here are the engine's stable wire ids — same
        // convention CardMechanics.EstimateDamage uses ("STRENGTH_POWER"
        // etc.).
        return new PlayerStatus(
            Strength:      ReadPower(powers, "STRENGTH_POWER"),
            Dexterity:     ReadPower(powers, "DEXTERITY_POWER"),
            Vulnerable:    ReadPower(powers, "VULNERABLE_POWER"),
            Weak:          ReadPower(powers, "WEAK_POWER"),
            Frail:         ReadPower(powers, "FRAIL_POWER"),
            Combust:       ReadPower(powers, "COMBUST_POWER"),
            Metallicize:   ReadPower(powers, "METALLICIZE_POWER"),
            PlatedArmor:   ReadPower(powers, "PLATED_ARMOR_POWER"),
            FeelNoPain:    ReadPower(powers, "FEEL_NO_PAIN_POWER"),
            DarkEmbrace:   ReadPower(powers, "DARK_EMBRACE_POWER"),
            FireBreathing: ReadPower(powers, "FIRE_BREATHING_POWER"),
            Rupture:       ReadPower(powers, "RUPTURE_POWER"),
            DemonForm:     ReadPower(powers, "DEMON_FORM_POWER"),
            Rage:          ReadPower(powers, "RAGE_POWER"),
            Juggernaut:    ReadPower(powers, "JUGGERNAUT_POWER"),
            Brutality:     ReadPower(powers, "BRUTALITY_POWER"),
            Evolve:        ReadPower(powers, "EVOLVE_POWER"),
            Berserk:       ReadPower(powers, "BERSERK_POWER"),
            Barricade:     ReadPower(powers, "BARRICADE_POWER"),
            Corruption:    ReadPower(powers, "CORRUPTION_POWER"),
            Ringing:       ReadPower(powers, "RINGING_POWER"));
    }

    private static int ReadPower(IReadOnlyList<Power> powers, string id)
    {
        foreach (var p in powers)
            if (p.Id == id) return p.Amount;
        return 0;
    }

    private static bool IsKnownEnemyPower(string id) => id switch
    {
        "STRENGTH_POWER" or "VULNERABLE_POWER" or "WEAK_POWER"
            or "SLIPPERY_POWER" or "PLOW_POWER" => true,
        _ => false,
    };
}

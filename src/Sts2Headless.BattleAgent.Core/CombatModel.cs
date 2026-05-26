using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.BattleAgent.Core;

// Reference ICombatModel. Pure functions over SimState — no allocation
// beyond the new records.
//
// Damage math (mirrors STS1 / sts2 engine's DamageCalc shape):
//   per_hit = card.Damage + Player.Strength
//   if Player.Weak  : per_hit = floor(per_hit * 0.75)
//   if Target.Vuln  : per_hit = floor(per_hit * 1.5)
//   per_hit_to_hp = max(0, per_hit - Target.Block)
//   Target.Block = max(0, Target.Block - per_hit)
//   repeat Hits times
//
// Block math:
//   gained = card.Block + Player.Dexterity (if any)
//   if Player.Frail: gained = floor(gained * 0.75)
//   Block += gained
//   Juggernaut triggers if gained > 0 — deal JuggernautGain to a random enemy.
public sealed class CombatModel(ICardEffectCatalog catalog) : ICombatModel
{
    private readonly ICardEffectCatalog _catalog = catalog;

    // Static helper so handlers in IroncladCardCatalog (Custom delegates)
    // can apply the same relic boost. Returns true when the relic id is
    // present in the player's relic set.
    public static bool HasRelic(SimState state, string relicId)
        => state.Relics is not null && state.Relics.Contains(relicId);

    public bool AllEnemiesDead(SimState state) => state.Enemies.All(e => e.IsDead);
    public bool IsPlayerDead(SimState state) => state.Hp <= 0;
    public bool IsCombatOver(SimState state) => AllEnemiesDead(state) || IsPlayerDead(state);

    // ── LegalActions ──────────────────────────────────────────────────
    public IReadOnlyList<SimAction> LegalActions(SimState state)
    {
        var actions = new List<SimAction> { new SimEndTurn() };
        if (IsCombatOver(state)) return new[] { (SimAction)new SimEndTurn() };

        // Ringing enforcement deliberately *off* — when we capped the
        // legal actions at `Ringing` plays this turn (logic was right,
        // matched the engine) the planner started picking a different
        // single best card than the first card of its optimal full-
        // sequence, and net -1 vs not modelling it at all on the
        // 200-seed corpus (33 → 32). Suspected cause: with cap-off
        // the engine rejects extra plays so only the first one fires,
        // and the "first of optimal full sequence" happens to be a
        // better Ringing-turn pick than the planner's "optimal
        // single card under cap". Data is still read from the wire
        // (PlayerStatus.Ringing) so a future evaluator-side fix can
        // re-engage without re-plumbing.

        for (var handIdx = 0; handIdx < state.Hand.Count; handIdx++)
        {
            var card = state.Hand[handIdx];
            if (!CanPlay(state, card)) continue;
            var effect = _catalog.GetEffect(card.Id, card.Upgraded);
            // Conservative: don't play cards the catalog has no model
            // for. The wire `canPlay` flag means the *engine* will
            // accept the play, but we don't know whether the card has
            // a sub-flow that NREs in headless. End-turn-and-skip is
            // always safer than rolling the dice on an unknown card.
            // If a card we want the agent to play turns up here as
            // "unknown", extend IroncladCardCatalog rather than
            // weakening this guard.
            if (effect is null) continue;

            // Card must be targeted at an enemy if its TargetType demands.
            switch (card.TargetType)
            {
                case TargetType.AnyEnemy:
                    for (var e = 0; e < state.Enemies.Count; e++)
                    {
                        if (!state.Enemies[e].IsDead)
                            actions.Add(new SimPlayCard(handIdx, e));
                    }
                    break;
                default:
                    actions.Add(new SimPlayCard(handIdx, null));
                    break;
            }
        }
        return actions;
    }

    // CanPlay: respect the engine's CanPlayFlag (it bakes in IsPlayable
    // overrides like PactsEnd's exhaust-pile threshold) and verify the
    // card's energy cost is satisfied. Three cost regimes:
    //   - cost ≥ 0      → standard, needs cost ≤ state.Energy
    //   - cost == -1    → X-cost, playable when energy > 0 (drains all)
    //   - Skill while
    //     Corruption    → cost is 0 regardless of declared cost
    private bool CanPlay(SimState state, SimCard card)
    {
        if (!card.CanPlayFlag) return false;
        if (IsCorruptedSkill(state, card)) return true;
        if (card.Cost == -1) return state.Energy > 0;
        return card.Cost >= 0 && card.Cost <= state.Energy;
    }

    // True when Corruption is active on the player AND the card being
    // played is a Skill (per catalog). Lets CanPlay / energy-spend /
    // pile-routing branch on the discount uniformly.
    private bool IsCorruptedSkill(SimState state, SimCard card)
    {
        if (state.Status.Corruption <= 0) return false;
        var effect = _catalog.GetEffect(card.Id, card.Upgraded);
        return effect?.IsSkill == true;
    }

    // ── Apply ─────────────────────────────────────────────────────────
    public SimState Apply(SimState state, SimAction action)
    {
        if (state.IsInvalid) return state;
        switch (action)
        {
            case SimEndTurn:
                return state; // planner calls EndPlayerTurn separately
            case SimPlayCard play:
                return ApplyPlayCard(state, play);
            case SimUsePotion:
                return state; // v1: potions decided outside the planner
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private SimState ApplyPlayCard(SimState state, SimPlayCard play)
    {
        if (play.HandIndex < 0 || play.HandIndex >= state.Hand.Count)
            return state with { IsInvalid = true };

        var card = state.Hand[play.HandIndex];
        if (!CanPlay(state, card)) return state with { IsInvalid = true };

        var effect = _catalog.GetEffect(card.Id, card.Upgraded);
        if (effect is null)
        {
            // Unknown card: spend energy, exhaust effect-less to discard,
            // and continue. The catalog covers what the agent actually
            // drafts; unknowns appear when the engine deals a card
            // CardMechanics doesn't know about (e.g. mid-run additions).
            return state with
            {
                Energy = state.Energy - Math.Max(0, card.Cost),
                Hand = RemoveAt(state.Hand, play.HandIndex),
                DiscardPileCount = state.DiscardPileCount + 1,
            };
        }
        // Spend energy first so Custom handlers (Whirlwind etc.) see
        // post-cost energy. Cost regimes:
        //   - X-cost (Cost == -1): drain to 0; the handler reads pre-drain
        //     energy via ctx.State.Energy (which is state.Energy here).
        //   - Skill under Corruption: cost is 0.
        //   - Otherwise: deduct the declared cost.
        int energyAfterSpend;
        if (effect.Custom is not null && card.Cost == -1)
        {
            // X-cost: leave Energy as the X value for the handler to read.
            // The handler is responsible for zeroing energy after use.
            energyAfterSpend = state.Energy;
        }
        else if (IsCorruptedSkill(state, card))
        {
            energyAfterSpend = state.Energy;  // Skill cost is 0 while Corrupted.
        }
        else
        {
            energyAfterSpend = state.Energy - Math.Max(0, card.Cost);
        }
        var s = state with { Energy = energyAfterSpend };

        // Custom handler completely replaces the declarative effect path.
        if (effect.Custom is not null)
        {
            s = effect.Custom(new CardEffectContext(s, card, play.TargetEnemyIndex, _catalog));
            return FinalisePlay(s, card, effect, play.HandIndex);
        }

        // Self-damage first (Bloodletting/Offering pay HP up front).
        if (effect.SelfDamage > 0)
        {
            s = s with { Hp = Math.Max(0, s.Hp - effect.SelfDamage) };
            // Rupture: gain N STR when player loses HP from a card.
            if (s.Status.Rupture > 0)
                s = s with { Status = s.Status with { Strength = s.Status.Strength + s.Status.Rupture } };
        }

        if (effect.EnergyGain > 0) s = s with { Energy = s.Energy + effect.EnergyGain };
        if (effect.HealHp > 0) s = s with { Hp = Math.Min(s.MaxHp, s.Hp + effect.HealHp) };
        if (effect.DrawCards > 0) s = s with { CardsDrawnThisTurn = s.CardsDrawnThisTurn + effect.DrawCards };

        // Block application before damage so Body-Slam-shape cards in
        // the same play set see fresh block (declarative cards can't
        // chain that, but the order is right for sequenced plays).
        if (effect.Block > 0)
        {
            s = ApplyBlock(s, effect.Block);
        }

        // Stat / power gains (Inflame, etc).
        if (effect.StrengthGain != 0)
            s = s with { Status = s.Status with { Strength = s.Status.Strength + effect.StrengthGain } };
        if (effect.DexterityGain != 0)
            s = s with { Status = s.Status with { Dexterity = s.Status.Dexterity + effect.DexterityGain } };
        if (effect.LimitBreakDoubleStrength && s.Status.Strength > 0)
            s = s with { Status = s.Status with { Strength = s.Status.Strength * 2 } };

        // Apply power-card persistent effects.
        s = ApplyPowerGains(s, effect);

        // Damage. STRIKE_DUMMY adds +3 damage per hit to Strike-named cards;
        // AKABEKO adds +8 to the first attack played in combat.
        var damagePerHit = effect.Damage;
        if (damagePerHit > 0 && HasRelic(state, "STRIKE_DUMMY")
            && SimStateBuilder.IsStrikeNamedCard(card.Id))
        {
            damagePerHit += 3;
        }
        var akabekoFired = false;
        if (effect.IsAttack && state.AkabekoAvailable && HasRelic(state, "AKABEKO"))
        {
            damagePerHit += 8;
            akabekoFired = true;
        }

        if (effect.BlockToDamage)
        {
            var dmg = state.Block; // Body Slam uses block at moment of play (pre any new block from this card)
            if (effect.TargetsAllEnemies) s = DealAoeDamage(s, dmg, effect.Hits).state;
            else s = DealSingleTargetDamage(s, play.TargetEnemyIndex ?? 0, dmg, effect.Hits).state;
        }
        else if (damagePerHit > 0)
        {
            if (effect.TargetsAllEnemies) s = DealAoeDamage(s, damagePerHit, effect.Hits).state;
            else s = DealSingleTargetDamage(s, play.TargetEnemyIndex ?? 0, damagePerHit, effect.Hits).state;
        }
        // Latch off Akabeko after first attack consumed it.
        if (akabekoFired) s = s with { AkabekoAvailable = false };

        // Debuffs to enemies (post-damage so kills aren't wasted)
        if (effect.VulnerableApply > 0 || effect.WeakApply > 0)
        {
            if (effect.TargetsAllEnemies)
            {
                for (var i = 0; i < s.Enemies.Count; i++)
                    if (!s.Enemies[i].IsDead)
                        s = ApplyEnemyDebuffs(s, i, effect.VulnerableApply, effect.WeakApply);
            }
            else
            {
                var t = play.TargetEnemyIndex ?? 0;
                if (t >= 0 && t < s.Enemies.Count && !s.Enemies[t].IsDead)
                    s = ApplyEnemyDebuffs(s, t, effect.VulnerableApply, effect.WeakApply);
            }
        }

        // SecondWind-style: exhaust all non-attacks in hand, gain N block per.
        if (effect.DiscardForBlock > 0)
        {
            var keep = new List<SimCard>();
            var exhausted = 0;
            for (var i = 0; i < s.Hand.Count; i++)
            {
                if (i == play.HandIndex) { keep.Add(s.Hand[i]); continue; } // the SecondWind itself is removed below
                var other = s.Hand[i];
                var otherEffect = _catalog.GetEffect(other.Id, other.Upgraded);
                if (otherEffect?.IsAttack == true)
                {
                    keep.Add(other);
                }
                else
                {
                    exhausted++;
                }
            }
            if (exhausted > 0)
            {
                s = ApplyBlock(s, effect.DiscardForBlock * exhausted);
                s = s with
                {
                    Hand = keep,
                    ExhaustPileCount = s.ExhaustPileCount + exhausted,
                };
                // Card index of the SecondWind in `keep` is now its
                // post-filter position; FinalisePlay will remove by
                // CardId from `keep` rather than original index.
                return FinalisePlay(s, card, effect, FindHandIndex(s.Hand, card));
            }
        }

        return FinalisePlay(s, card, effect, play.HandIndex);
    }

    // Move the just-played card to discard / exhaust pile and update
    // hand. Handles end-of-play housekeeping (Rage block-per-attack,
    // exhaust triggers).
    private SimState FinalisePlay(SimState s, SimCard card, CardEffect effect, int handIdx)
    {
        // Rage: gain block per attack played this turn.
        if (effect.IsAttack && s.Status.Rage > 0)
            s = ApplyBlock(s, s.Status.Rage);

        // Bump the per-turn play counter (Ringing enforcement reads
        // this in LegalActions).
        s = s with { CardsPlayedThisTurn = s.CardsPlayedThisTurn + 1 };

        // Remove from hand.
        if (handIdx < 0 || handIdx >= s.Hand.Count)
            return s with { IsInvalid = true };
        var newHand = RemoveAt(s.Hand, handIdx);
        s = s with { Hand = newHand };

        // Route to exhaust / discard pile. Corruption forces Skills to
        // exhaust on play in addition to their declared route.
        var exhausts = effect.Exhausts
            || (effect.IsSkill && s.Status.Corruption > 0);
        if (exhausts)
        {
            s = s with { ExhaustPileCount = s.ExhaustPileCount + 1 };
            s = TriggerOnCardExhausted(s);
        }
        else
        {
            s = s with { DiscardPileCount = s.DiscardPileCount + 1 };
        }

        // Random hand-exhaust side effects (True Grit).
        if (effect.ExhaustRandomFromHand > 0 && s.Hand.Count > 0)
        {
            // Deterministic for transposition-table purposes: exhaust
            // the highest-cost card in hand (a common heuristic on True
            // Grit auto-pick — see scumthespire's random-exhaust policy).
            var count = Math.Min(effect.ExhaustRandomFromHand, s.Hand.Count);
            for (var k = 0; k < count; k++)
            {
                var idx = HighestCostIndex(s.Hand);
                s = s with
                {
                    Hand = RemoveAt(s.Hand, idx),
                    ExhaustPileCount = s.ExhaustPileCount + 1,
                };
                s = TriggerOnCardExhausted(s);
            }
        }

        return s;
    }

    private static SimState TriggerOnCardExhausted(SimState s)
    {
        if (s.Status.FeelNoPain > 0) s = ApplyBlock(s, s.Status.FeelNoPain);
        if (s.Status.DarkEmbrace > 0) s = s with { CardsDrawnThisTurn = s.CardsDrawnThisTurn + s.Status.DarkEmbrace };
        return s;
    }

    private static int FindHandIndex(IReadOnlyList<SimCard> hand, SimCard card)
    {
        for (var i = 0; i < hand.Count; i++)
            if (ReferenceEquals(hand[i], card)) return i;
        return -1;
    }

    private static int HighestCostIndex(IReadOnlyList<SimCard> hand)
    {
        var bestIdx = 0;
        var bestCost = int.MinValue;
        for (var i = 0; i < hand.Count; i++)
        {
            if (hand[i].Cost > bestCost) { bestCost = hand[i].Cost; bestIdx = i; }
        }
        return bestIdx;
    }

    private static IReadOnlyList<SimCard> RemoveAt(IReadOnlyList<SimCard> hand, int idx)
    {
        if (idx < 0 || idx >= hand.Count) return hand;
        var arr = new SimCard[hand.Count - 1];
        for (int i = 0, j = 0; i < hand.Count; i++)
            if (i != idx) arr[j++] = hand[i];
        return arr;
    }

    private static SimState ApplyPowerGains(SimState s, CardEffect e)
    {
        if (!e.IsPower) return s;
        var st = s.Status;
        if (e.CombustGain > 0)        st = st with { Combust = st.Combust + e.CombustGain };
        if (e.MetallicizeGain > 0)    st = st with { Metallicize = st.Metallicize + e.MetallicizeGain };
        if (e.PlatedArmorGain > 0)    st = st with { PlatedArmor = st.PlatedArmor + e.PlatedArmorGain };
        if (e.FeelNoPainGain > 0)     st = st with { FeelNoPain = st.FeelNoPain + e.FeelNoPainGain };
        if (e.DarkEmbraceGain > 0)    st = st with { DarkEmbrace = st.DarkEmbrace + e.DarkEmbraceGain };
        if (e.FireBreathingGain > 0)  st = st with { FireBreathing = st.FireBreathing + e.FireBreathingGain };
        if (e.RuptureGain > 0)        st = st with { Rupture = st.Rupture + e.RuptureGain };
        if (e.DemonFormGain > 0)      st = st with { DemonForm = st.DemonForm + e.DemonFormGain };
        if (e.RageGain > 0)           st = st with { Rage = st.Rage + e.RageGain };
        if (e.JuggernautGain > 0)     st = st with { Juggernaut = st.Juggernaut + e.JuggernautGain };
        if (e.BrutalityGain > 0)      st = st with { Brutality = st.Brutality + e.BrutalityGain };
        if (e.EvolveGain > 0)         st = st with { Evolve = st.Evolve + e.EvolveGain };
        if (e.BerserkGain > 0)        st = st with { Berserk = st.Berserk + e.BerserkGain };
        if (e.BarricadeGain > 0)      st = st with { Barricade = st.Barricade + e.BarricadeGain };
        if (e.CorruptionGain > 0)     st = st with { Corruption = st.Corruption + e.CorruptionGain };
        if (e.HellraiserGain > 0)     st = st with { Hellraiser = st.Hellraiser + e.HellraiserGain };
        return s with { Status = st };
    }

    // ── Damage helpers (also exposed for Custom handlers) ─────────────

    // Deal damage to a single target, returning (post-state, total HP damage dealt).
    public static (SimState state, int dealt) DealSingleTargetDamage(
        SimState state, int targetIdx, int perHit, int hits)
    {
        if (targetIdx < 0 || targetIdx >= state.Enemies.Count) return (state, 0);
        if (perHit <= 0 || hits <= 0) return (state, 0);

        var enemies = state.Enemies.ToArray();
        var target = enemies[targetIdx];
        if (target.IsDead) return (state, 0);

        var modified = AdjustDamage(perHit, state.Status, target);
        var totalHpDamage = 0;
        var block = target.Block;
        var hp = target.Hp;
        var slippery = target.Slippery;
        for (var i = 0; i < hits && hp > 0; i++)
        {
            int hpDamage;
            if (slippery > 0)
            {
                // Slippery (Vantom): cap the hit at 1 damage; decrement
                // the stack. Block doesn't help against Slippery hits
                // in our model (the stack absorbs the hit pre-block) —
                // wiki.gg describes the effect as "absorb the hit into
                // 1 HP", and STS-style implementations cap the hit
                // after block too. Conservative: apply the 1-HP cap
                // after block calc (so block first, then min(1)).
                var rawAfterBlock = Math.Max(0, modified - block);
                block = Math.Max(0, block - modified);
                hpDamage = Math.Min(1, rawAfterBlock);
                slippery--;
            }
            else
            {
                hpDamage = Math.Max(0, modified - block);
                block = Math.Max(0, block - modified);
            }
            if (hpDamage > 0)
            {
                hp = Math.Max(0, hp - hpDamage);
                totalHpDamage += hpDamage;
            }
        }
        // Plow threshold (Beast Phase-1 → stun + Strength reset when
        // HP crosses to <= threshold). Modelling the *trigger* (zero
        // intent, reset Str, both) regressed 33/200 → 32/200 — the
        // planner over-credited the free turn and skipped block prep
        // for Phase 2. Kept the field readable (SimEnemy.PlowThreshold)
        // for future evaluator-side experiments (e.g. bonus score for
        // "this attack crosses the threshold") but the damage helper
        // does not mutate state on the trigger.
        enemies[targetIdx] = target with
        {
            Block = block,
            Hp = hp,
            Slippery = slippery,
        };
        return (state with { Enemies = enemies }, totalHpDamage);
    }

    // Deal damage to ALL living enemies; returns total damage dealt.
    public static (SimState state, int dealt) DealAoeDamage(SimState state, int perHit, int hits)
    {
        var s = state;
        var total = 0;
        for (var i = 0; i < s.Enemies.Count; i++)
        {
            if (s.Enemies[i].IsDead) continue;
            var (ns, dealt) = DealSingleTargetDamage(s, i, perHit, hits);
            s = ns;
            total += dealt;
        }
        return (s, total);
    }

    private static int AdjustDamage(int perHit, PlayerStatus status, SimEnemy target)
    {
        var d = perHit + status.Strength;
        if (status.Weak > 0) d = (int)Math.Floor(d * 0.75);
        if (target.Vulnerable > 0) d = (int)Math.Floor(d * 1.5);
        return Math.Max(0, d);
    }

    private static SimState ApplyBlock(SimState s, int amount)
    {
        if (amount <= 0) return s;
        var gained = amount + s.Status.Dexterity;
        if (s.Status.Frail > 0) gained = (int)Math.Floor(gained * 0.75);
        if (gained <= 0) return s;
        var next = s with { Block = s.Block + gained };
        // Juggernaut: deal N damage to a random enemy when block is gained.
        // For determinism we pick the first living enemy.
        if (next.Status.Juggernaut > 0)
        {
            for (var i = 0; i < next.Enemies.Count; i++)
            {
                if (!next.Enemies[i].IsDead)
                {
                    next = DealSingleTargetDamage(next, i, next.Status.Juggernaut, 1).state;
                    break;
                }
            }
        }
        return next;
    }

    private static SimState ApplyEnemyDebuffs(SimState s, int idx, int vulnApply, int weakApply)
    {
        if (idx < 0 || idx >= s.Enemies.Count) return s;
        var enemies = s.Enemies.ToArray();
        var e = enemies[idx];
        if (e.IsDead) return s;
        // Artifact wire-read into SimEnemy.Artifact but NOT enforced
        // here. Tested 33/200 → 32/200: with Artifact modelled, the
        // planner sees Bash-on-Cubex applying 0 Vulnerable (correctly,
        // matching the engine) and devalues the play. But the optimal
        // Cubex line is *burn* the Artifact with one Bash and reap
        // Vuln on the next turn — a payoff the 1-turn planner can't
        // see. Leaving the field readable for a future evaluator-
        // side bonus ("the next debuff land is X turns away").
        enemies[idx] = e with
        {
            Vulnerable = e.Vulnerable + vulnApply,
            Weak = e.Weak + weakApply,
        };
        return s with { Enemies = enemies };
    }

    // ── End of turn ───────────────────────────────────────────────────
    public SimState EndPlayerTurn(SimState state)
    {
        if (IsCombatOver(state)) return state;
        var s = state;

        // End-of-player-turn effects (Combust damages all, Metallicize block).
        if (s.Status.Combust > 0)
        {
            // Combust costs 1 HP and deals N damage to ALL.
            s = s with { Hp = Math.Max(0, s.Hp - 1) };
            s = DealAoeDamage(s, s.Status.Combust, 1).state;
            if (AllEnemiesDead(s)) return s;
        }
        if (s.Status.Metallicize > 0) s = ApplyBlock(s, s.Status.Metallicize);

        // Hellraiser end-of-turn payoff (see SimState comment for the
        // model). We don't have the per-card draw stream, so this
        // applies the expected-value damage burst at end-of-player-
        // turn against the highest-HP living enemy.
        if (s.Status.Hellraiser > 0 && s.CardsDrawnThisTurn > 0
            && s.StrikeCardsInDeck > 0)
        {
            // Approximate deck size from pile counts plus current hand.
            // Underestimate is safe (over-values Hellraiser); over-
            // estimate is the recoverable direction.
            var deckSize = Math.Max(1,
                s.DrawPileCount + s.DiscardPileCount + s.ExhaustPileCount + s.Hand.Count);
            var density = s.StrikeCardsInDeck / (double)deckSize;
            // Cap density at 0.6 — beyond that we're modelling a Strike
            // singleton deck the agent can't realistically hold.
            density = Math.Min(0.6, density);
            var expectedStrikes = (int)Math.Floor(s.CardsDrawnThisTurn * density);
            if (expectedStrikes > 0)
            {
                var perStrike = 6 + Math.Max(0, s.Status.Strength);
                // Target the highest-HP living enemy.
                var targetIdx = -1;
                var bestHp = -1;
                for (var i = 0; i < s.Enemies.Count; i++)
                {
                    if (s.Enemies[i].IsDead) continue;
                    if (s.Enemies[i].Hp > bestHp) { bestHp = s.Enemies[i].Hp; targetIdx = i; }
                }
                if (targetIdx >= 0)
                {
                    s = DealSingleTargetDamage(s, targetIdx, perStrike, expectedStrikes).state;
                    if (AllEnemiesDead(s)) return s;
                }
            }
        }

        // Enemy intents resolve. The wire's intent.Damage already bakes
        // in enemy Strength + player Vulnerable at snapshot time. Our
        // simulator's enemy debuffs (Vulnerable applied during the
        // player turn) DO affect the incoming damage too — STS1 applies
        // debuffs in real time. We mirror that by multiplying the wire's
        // pre-baked damage by (1 + 0.5*if-vulnerable-newly-applied).
        // Conservative: don't re-multiply; the wire's number is what
        // the engine will actually inflict if the snapshot is taken
        // post-player-turn. Player.Vulnerable + player.Weak from
        // incoming intents are applied below.
        foreach (var enemy in s.Enemies)
        {
            if (enemy.IsDead) continue;
            if (enemy.Intent is { } intent)
            {
                if (intent.Damage > 0 && intent.Hits > 0)
                {
                    var perHit = intent.Damage;
                    // Vulnerable on player applies — the wire's intent
                    // damage doesn't bake in *player* vulnerable, since
                    // we apply it during our own turn.
                    if (s.Status.Vulnerable > 0) perHit = (int)Math.Floor(perHit * 1.5);
                    // TUNGSTEN_ROD: -1 to each incoming attack hit.
                    if (HasRelic(s, "TUNGSTEN_ROD")) perHit = Math.Max(0, perHit - 1);
                    // Thorns wire-read into PlayerStatus.Thorns but
                    // NOT applied in this loop. Tested 33/200 → 31/200
                    // on the 200-seed corpus — the planner under-blocks
                    // because it credits the reflection too richly
                    // (multi-hit incoming yields N thorn hits, scoring
                    // as +N×3 enemy HP, which doesn't actually save
                    // the player from the unblocked damage). Keep the
                    // field readable so a future fix can re-apply
                    // with a player-HP-aware guard.
                    for (var h = 0; h < intent.Hits && !IsPlayerDead(s); h++)
                    {
                        var afterBlock = Math.Max(0, perHit - s.Block);
                        s = s with
                        {
                            Block = Math.Max(0, s.Block - perHit),
                            Hp = Math.Max(0, s.Hp - afterBlock),
                        };
                    }
                    if (IsPlayerDead(s)) return s;
                }
                if (intent.Block > 0)
                {
                    var enemies = s.Enemies.ToArray();
                    var idx = Array.FindIndex(enemies, e => e.Index == enemy.Index);
                    if (idx >= 0)
                    {
                        enemies[idx] = enemies[idx] with { Block = enemies[idx].Block + intent.Block };
                        s = s with { Enemies = enemies };
                    }
                }
            }
        }

        // Decay player debuffs and turn-bounded buffs.
        s = s with { Status = DecayStatus(s.Status) };

        // Start of next player turn.
        s = StartPlayerTurn(s);
        return s;
    }

    private static PlayerStatus DecayStatus(PlayerStatus st) => st with
    {
        Vulnerable = Math.Max(0, st.Vulnerable - 1),
        Weak       = Math.Max(0, st.Weak - 1),
        Frail      = Math.Max(0, st.Frail - 1),
        Rage       = 0,  // Rage resets at end of turn
        Ringing    = Math.Max(0, st.Ringing - 1),
    };

    private static SimState StartPlayerTurn(SimState s)
    {
        // Block clears unless Barricade.
        var newBlock = s.Status.Barricade > 0 ? s.Block : 0;
        // PlatedArmor block.
        if (s.Status.PlatedArmor > 0) newBlock += s.Status.PlatedArmor;

        // DemonForm: gain N strength.
        var newStr = s.Status.Strength + s.Status.DemonForm;
        var status = s.Status with { Strength = newStr };

        // Brutality: lose 1 HP and draw 1.
        var hp = s.Hp;
        var drawnExtra = 0;
        if (status.Brutality > 0)
        {
            hp = Math.Max(0, hp - status.Brutality);
            drawnExtra += status.Brutality;
        }

        // Refill energy + new turn.
        return s with
        {
            Hp = hp,
            Energy = s.MaxEnergyPerTurn,
            Block = newBlock,
            Turn = s.Turn + 1,
            Status = status,
            // Hand is cleared; we don't know the next hand without
            // deck/discard knowledge. Planners that look multiple turns
            // ahead must treat post-EOT Hand as empty.
            Hand = Array.Empty<SimCard>(),
            CardsDrawnThisTurn = 0 + drawnExtra,
            CardsPlayedThisTurn = 0,
        };
    }
}

using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Agents;

// Small pure helpers over CombatState that more than one agent will
// reach for. Kept separate from CardMechanics — these don't touch the
// card catalogue.
public static class CombatHelpers
{
    // Sum of incoming damage from every enemy that intends to attack
    // this turn. Trusts the wire-surfaced intent.Damage to already
    // include engine-side modifiers (sts2's DamageCalc is the source of
    // this number, and the engine computes it with Strength/Vulnerable
    // already baked in — adding them here would double-count). Block
    // gained this turn is the caller's responsibility to subtract.
    public static int IncomingDamage(CombatState combat)
    {
        var sum = 0;
        foreach (var e in combat.Enemies)
        {
            foreach (var intent in e.Intents)
            {
                if (intent.Damage is not int d) continue;
                var hits = intent.Hits ?? 1;
                sum += d * hits;
            }
        }
        return sum;
    }
}

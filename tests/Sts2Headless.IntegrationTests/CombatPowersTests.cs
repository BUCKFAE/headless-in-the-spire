using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the powers/buffs slice of the CombatState wire surface:
// CombatState.PlayerPowers and Enemy.Powers, both populated via Sts2Bindings'
// ReadPowers walk over sts2's Creature.Powers collection. Lives in its own
// class for xUnit parallelism (mirrors the split CombatTests / CombatFightTests
// / CombatRewardShapeTests layout).
//
// The "won't break in the future" discipline here:
//   1. We don't assert exact starting-hand contents — the starter deck can
//      grow or be re-balanced. We discover Bash by id substring across the
//      draws of a few rounds.
//   2. We don't pin which enemy variant lands on seed 42 — the test reads
//      whatever enemy 0 is and asserts only the wire-shape invariants
//      (powers list grows, applied power carries a stable id + amount > 0).
//   3. The failure path of the second test surfaces the full set of card ids
//      the loop saw, so a future engineer diagnosing a starter-deck change
//      gets the actionable info inline rather than having to reach for a
//      separate probe.
//
// Shares one HostSubprocess via IClassFixture; every test starts with run/new.
public class CombatPowersTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public CombatPowersTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task CombatStart_PlayerPowers_IsEmptyList()
    {
        // Ironclad enters combat with no Creature.Powers entries — Burning
        // Blood is an end-of-combat relic hook, not a Power. The wire must
        // surface an empty list (not omit the field, not return null), so
        // clients can iterate without a null guard.
        var start = await _host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        var monster = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var inCombat = await _host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: monster.Col, Row: monster.Row));

        Assert.NotNull(inCombat.CombatState);
        Assert.Empty(inCombat.CombatState!.PlayerPowers);
    }

    [Fact]
    public async Task PlayBash_AppliesVulnerableToEnemy_SurfacesOnEnemyPowers()
    {
        // Powers wire-path verification end-to-end: play sts2's stock
        // power-applying starter card (Bash → Vulnerable on a single enemy)
        // and confirm the new debuff lands in the targeted Enemy.Powers
        // list with a non-empty id and a positive amount.
        //
        // Loop discipline keeps the enemy alive while we wait for Bash to
        // draw into hand: every non-Bash turn ends without playing any
        // AnyEnemy card (Strike) — only end_turn fires, which keeps enemy 0
        // alive until our discard reshuffles Bash back. Ironclad's starter
        // deck is 10 cards / hand of 5, so 8 turns is comfortably above the
        // expected upper bound. Tracked card ids surface in the failure
        // diagnostic so a future rebalance (Bash removed, replaced, or
        // renamed) is debuggable from the test output alone.
        var start = await _host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));
        var monster = start.AvailableMapNodes.First(n => n.Type == MapNodeType.Monster && n.Row > 0);
        var inCombat = await _host.SendAsync<RunSelectMapNodeResult>(
            "run/select_map_node", new RunSelectMapNodeParams(Col: monster.Col, Row: monster.Row));
        Assert.NotNull(inCombat.CombatState);
        var combat = inCombat.CombatState!;
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (var turn = 0; turn < 8 && combat.IsInProgress && combat.IsPlayPhase; turn++)
        {
            foreach (var c in combat.Hand) seenIds.Add(c.Id);

            var bash = combat.Hand.FirstOrDefault(c =>
                c.CanPlay
                && c.Cost <= combat.Energy
                && c.TargetType == TargetType.AnyEnemy
                && c.Id.Contains("BASH", StringComparison.OrdinalIgnoreCase));

            if (bash is not null)
            {
                Assert.NotEmpty(combat.Enemies);
                var powersBefore = combat.Enemies[0].Powers;

                var after = await _host.SendAsync<RunPlayCardResult>(
                    "run/play_card", new RunPlayCardParams(CardIndex: bash.Index, TargetIndex: 0));
                Assert.True(after.Ok);
                Assert.NotNull(after.CombatState);
                Assert.NotEmpty(after.CombatState!.Enemies);
                var powersAfter = after.CombatState.Enemies[0].Powers;

                // A "new or amplified" power is one whose (id, amount) pair
                // wasn't on the enemy before. Set-diff rather than index-
                // checks because sts2's Powers list ordering isn't part of
                // the wire contract — and innate enemy powers (Ritual on a
                // Cultist, etc.) would otherwise inflate powersBefore.
                var newlyApplied = powersAfter
                    .Where(p => !powersBefore.Any(b => b.Id == p.Id && b.Amount == p.Amount))
                    .ToList();
                Assert.True(newlyApplied.Count > 0,
                    $"Bash should have added a new Enemy.Powers entry; " +
                    $"before=[{string.Join(",", powersBefore.Select(p => $"{p.Id}:{p.Amount}"))}], " +
                    $"after=[{string.Join(",", powersAfter.Select(p => $"{p.Id}:{p.Amount}"))}]");
                var applied = newlyApplied[0];
                Assert.False(string.IsNullOrEmpty(applied.Id),
                    "applied power should carry a non-empty id");
                Assert.True(applied.Amount > 0,
                    $"applied power {applied.Id} should have positive amount, was {applied.Amount}");
                return;
            }

            var ended = await _host.SendAsync<RunEndTurnResult>("run/end_turn");
            if (!ended.Ok || ended.CombatState is null) break;
            combat = ended.CombatState;
        }

        Assert.Fail(
            $"Bash never drew into hand in 8 turns starting from seed 42. " +
            $"Card ids seen: [{string.Join(", ", seenIds.OrderBy(x => x))}]. " +
            $"If the Ironclad starter deck no longer contains a 'BASH' card, " +
            $"update the substring match in this test to target the replacement " +
            $"power-applying starter card.");
    }
}

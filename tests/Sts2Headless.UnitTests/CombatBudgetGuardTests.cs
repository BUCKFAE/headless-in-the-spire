using Sts2Headless.Agents.Driving;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.UnitTests;

// Unit-level tests for CombatBudgetGuard — pure C# state, no host. The
// guard is pumped a sequence of synthetic RunStateResult snapshots and
// we assert it either trips or doesn't, depending on the shape.
//
// Tests use a per-test set of small budgets (4-5) so we don't have to
// hand-author 80 snapshots to hit the default ceiling.
public class CombatBudgetGuardTests
{
    [Fact]
    public void OutOfCombatSnapshots_NeverTrip()
    {
        var guard = new CombatBudgetGuard(maxCombatRounds: 3, maxNoProgressRounds: 2);
        for (var i = 0; i < 100; i++)
        {
            guard.Observe(MapRoom(hp: 60));
        }
        // No throw — out of combat means the budget is dormant.
    }

    [Fact]
    public void CombatThatLegitimatelyProgresses_DoesNotTrip()
    {
        var guard = new CombatBudgetGuard(maxCombatRounds: 10, maxNoProgressRounds: 4);
        for (var round = 1; round <= 9; round++)
        {
            // Each round: enemy loses HP, player takes a hit. Real progress.
            var playerHp = 60 - round;
            var enemyHp = 50 - round * 4;
            guard.Observe(CombatSnapshot(round: round, hp: playerHp, ("LOUSE", enemyHp, 0, [])));
        }
        // Combat ends inside the budget — must not throw.
    }

    [Fact]
    public void RoundCounterExceedsMaxCombatRounds_ThrowsMaxRounds()
    {
        var guard = new CombatBudgetGuard(maxCombatRounds: 5, maxNoProgressRounds: 99);
        // Rounds 1..5 are legitimate; the moment round=6 lands the
        // guard must trip on the MaxRounds branch.
        for (var round = 1; round <= 5; round++)
        {
            guard.Observe(CombatSnapshot(round: round, hp: 60 - round, ("LOUSE", 50 - round, 0, [])));
        }
        var ex = Assert.Throws<CombatBudgetExceededException>(() =>
            guard.Observe(CombatSnapshot(round: 6, hp: 54, ("LOUSE", 44, 0, []))));
        Assert.Equal(BudgetKind.MaxRounds, ex.Kind);
        Assert.Equal(5, ex.Budget);
        Assert.Equal(6, ex.Observed);
        Assert.Contains("LOUSE", ex.Encounter);
    }

    [Fact]
    public void NoVitalsChangeAcrossRounds_ThrowsMaxNoProgressRounds()
    {
        var guard = new CombatBudgetGuard(maxCombatRounds: 99, maxNoProgressRounds: 3);
        // Round 1 establishes baseline; rounds 2,3,4 carry identical
        // vitals → noProgressRounds reaches 3 → throw on round 4.
        guard.Observe(CombatSnapshot(round: 1, hp: 60, ("BRUTE", 40, 0, [])));
        guard.Observe(CombatSnapshot(round: 2, hp: 60, ("BRUTE", 40, 0, [])));
        guard.Observe(CombatSnapshot(round: 3, hp: 60, ("BRUTE", 40, 0, [])));
        var ex = Assert.Throws<CombatBudgetExceededException>(() =>
            guard.Observe(CombatSnapshot(round: 4, hp: 60, ("BRUTE", 40, 0, []))));
        Assert.Equal(BudgetKind.MaxNoProgressRounds, ex.Kind);
        Assert.Equal(3, ex.Budget);
        Assert.Equal(3, ex.Observed);
    }

    [Fact]
    public void MultipleSnapshotsWithinSameRound_DoNotIncrementNoProgress()
    {
        var guard = new CombatBudgetGuard(maxCombatRounds: 99, maxNoProgressRounds: 3);
        // 10 snapshots at round=1 with identical vitals — within-round
        // re-reads, not cross-round stalemate. Must not trip.
        for (var i = 0; i < 10; i++)
        {
            guard.Observe(CombatSnapshot(round: 1, hp: 60, ("BRUTE", 40, 0, [])));
        }
        // Now actually advance the round with a delta — should clear the
        // counter and not throw.
        guard.Observe(CombatSnapshot(round: 2, hp: 58, ("BRUTE", 36, 0, [])));
    }

    [Fact]
    public void VitalsDeltaResets_NoProgressCounter()
    {
        var guard = new CombatBudgetGuard(maxCombatRounds: 99, maxNoProgressRounds: 3);
        guard.Observe(CombatSnapshot(round: 1, hp: 60, ("BRUTE", 40, 0, [])));
        guard.Observe(CombatSnapshot(round: 2, hp: 60, ("BRUTE", 40, 0, []))); // no-progress 1
        guard.Observe(CombatSnapshot(round: 3, hp: 58, ("BRUTE", 40, 0, []))); // delta → reset
        // After the reset, we need 3 more no-progress rounds to trip.
        guard.Observe(CombatSnapshot(round: 4, hp: 58, ("BRUTE", 40, 0, [])));
        guard.Observe(CombatSnapshot(round: 5, hp: 58, ("BRUTE", 40, 0, [])));
        var ex = Assert.Throws<CombatBudgetExceededException>(() =>
            guard.Observe(CombatSnapshot(round: 6, hp: 58, ("BRUTE", 40, 0, []))));
        Assert.Equal(BudgetKind.MaxNoProgressRounds, ex.Kind);
    }

    [Fact]
    public void NewEncounter_ResetsBudgetEvenWithoutOutOfCombatGap()
    {
        var guard = new CombatBudgetGuard(maxCombatRounds: 99, maxNoProgressRounds: 3);
        // Combat A: stuck for 2 rounds (one short of tripping).
        guard.Observe(CombatSnapshot(round: 1, hp: 60, ("BRUTE", 40, 0, [])));
        guard.Observe(CombatSnapshot(round: 2, hp: 60, ("BRUTE", 40, 0, [])));
        guard.Observe(CombatSnapshot(round: 3, hp: 60, ("BRUTE", 40, 0, [])));
        // Switch to combat B — different monster, same room flag etc.
        // Must reset the counter (otherwise back-to-back encounters
        // would inherit each other's no-progress count).
        guard.Observe(CombatSnapshot(round: 1, hp: 60, ("LOUSE", 20, 0, [])));
        guard.Observe(CombatSnapshot(round: 2, hp: 60, ("LOUSE", 20, 0, [])));
        // Two no-progress rounds in combat B (would have tripped if
        // we'd carried over A's count of 2).
    }

    [Fact]
    public void SentinelHpEnemy_AppendsAdvisoryToExceptionMessage()
    {
        // Doormaker ships MaxHp ≈ 999999999. When the budget guard trips
        // on a fight against an enemy at sentinel HP, the exception must
        // call it out — otherwise "combat exceeded 80 rounds" reads as
        // a deck/agent problem rather than the design-placeholder /
        // unrecovered-phase-transition that it actually is.
        var guard = new CombatBudgetGuard(maxCombatRounds: 3, maxNoProgressRounds: 99);
        guard.Observe(CombatSnapshot(round: 1, hp: 60, ("DOORMAKER", 999_999_999, 0, [])));
        guard.Observe(CombatSnapshot(round: 2, hp: 60, ("DOORMAKER", 999_999_998, 0, [])));
        guard.Observe(CombatSnapshot(round: 3, hp: 60, ("DOORMAKER", 999_999_997, 0, [])));
        var ex = Assert.Throws<CombatBudgetExceededException>(() =>
            guard.Observe(CombatSnapshot(round: 4, hp: 60, ("DOORMAKER", 999_999_996, 0, []))));
        Assert.NotNull(ex.Advisory);
        Assert.Contains("sentinel-HP", ex.Advisory!);
        Assert.Contains("DOORMAKER", ex.Advisory!);
        // The advisory must also land in the exception's Message (the
        // common consumption path — sweep reports use ex.Message verbatim).
        Assert.Contains("sentinel-HP", ex.Message);
    }

    [Fact]
    public void NormalHpEnemy_LeavesAdvisoryNull()
    {
        // Ordinary boss / elite HPs (50–500) never trigger the sentinel
        // call-out — otherwise the warning becomes noise and stops being
        // a signal.
        var guard = new CombatBudgetGuard(maxCombatRounds: 3, maxNoProgressRounds: 99);
        guard.Observe(CombatSnapshot(round: 1, hp: 60, ("BRUTE", 400, 0, [])));
        guard.Observe(CombatSnapshot(round: 2, hp: 60, ("BRUTE", 399, 0, [])));
        guard.Observe(CombatSnapshot(round: 3, hp: 60, ("BRUTE", 398, 0, [])));
        var ex = Assert.Throws<CombatBudgetExceededException>(() =>
            guard.Observe(CombatSnapshot(round: 4, hp: 60, ("BRUTE", 397, 0, []))));
        Assert.Null(ex.Advisory);
        Assert.DoesNotContain("sentinel-HP", ex.Message);
    }

    [Fact]
    public void PowerChange_CountsAsProgress()
    {
        var guard = new CombatBudgetGuard(maxCombatRounds: 99, maxNoProgressRounds: 2);
        guard.Observe(CombatSnapshot(round: 1, hp: 60, ("BRUTE", 40, 0, [])));
        // Round 2: HP unchanged but enemy gained a power stack — counts
        // as progress, no-progress stays at 0.
        guard.Observe(CombatSnapshot(round: 2, hp: 60, ("BRUTE", 40, 0, [("STRENGTH", 2)])));
        guard.Observe(CombatSnapshot(round: 3, hp: 60, ("BRUTE", 40, 0, [("STRENGTH", 2)]))); // no-progress 1
        // Round 4 with no change again → tripping at 2 consecutive.
        var ex = Assert.Throws<CombatBudgetExceededException>(() =>
            guard.Observe(CombatSnapshot(round: 4, hp: 60, ("BRUTE", 40, 0, [("STRENGTH", 2)]))));
        Assert.Equal(BudgetKind.MaxNoProgressRounds, ex.Kind);
    }

    // ── Synthetic snapshot helpers ──────────────────────────────────────

    private static RunStateResult MapRoom(int hp) => Snapshot(
        hp: hp, combat: null);

    private static RunStateResult CombatSnapshot(int round, int hp, params (string MonsterId, int Hp, int Block, (string Id, int Amount)[] Powers)[] enemiesIn)
    {
        var enemies = enemiesIn.Select((e, ix) => new Enemy(
            Index: ix,
            MonsterId: e.MonsterId,
            Hp: e.Hp,
            MaxHp: Math.Max(e.Hp, 100),
            Block: e.Block,
            IntendsAttack: false,
            Intents: Array.Empty<Intent>(),
            Powers: e.Powers.Select(p => new Power(p.Id, p.Amount)).ToList())).ToList();
        var combat = new CombatState(
            Round: round,
            Energy: 3, MaxEnergy: 3,
            PlayerBlock: 0,
            IsPlayPhase: true,
            IsInProgress: true,
            DrawPileCount: 5, DiscardPileCount: 0,
            Hand: Array.Empty<Card>(),
            Enemies: enemies,
            PlayerPowers: Array.Empty<Power>());
        return Snapshot(hp: hp, combat: combat);
    }

    private static RunStateResult Snapshot(int hp, CombatState? combat) => new(
        Ok: true,
        Character: Character.Ironclad,
        Seed: 1uL,
        Hp: hp, MaxHp: 80,
        Gold: 99,
        DeckSize: 12,
        CurrentRoomType: combat is null ? RoomType.MapRoom : RoomType.CombatRoom,
        ActFloor: 5,
        CurrentActIndex: 0,
        IsGameOver: false,
        IsVictory: false,
        IsDead: false,
        AvailableMapNodes: Array.Empty<MapNode>(),
        AvailableEventOptions: Array.Empty<EventOption>(),
        AvailableRestSiteOptions: Array.Empty<RestSiteOption>(),
        AvailableMerchantItems: Array.Empty<MerchantItem>(),
        AvailableTreasureRelics: Array.Empty<TreasureRelic>(),
        CombatState: combat,
        RewardsState: null,
        Relics: Array.Empty<Relic>(),
        OwnedPotions: Array.Empty<OwnedPotion>(),
        TriggeredSincePrev: Array.Empty<TriggerEvent>(),
        TriggeredDropped: 0);
}

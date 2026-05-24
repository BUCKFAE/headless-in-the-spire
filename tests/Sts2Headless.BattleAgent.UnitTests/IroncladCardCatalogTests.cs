using Sts2Headless.BattleAgent.Core;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.BattleAgent.UnitTests;

// Pin the card catalog's contents — keeps the v1 modelled-card set
// honest and surfaces accidental removals during refactors.
public sealed class IroncladCardCatalogTests
{
    private static readonly ICardEffectCatalog Catalog = IroncladCardCatalog.Instance;

    [Fact]
    public void StrikeBaseDealsSix() =>
        Assert.Equal(6, Catalog.GetEffect(CardId.StrikeIronclad, upgraded: false)!.Damage);

    [Fact]
    public void StrikeUpgradedDealsNine() =>
        Assert.Equal(9, Catalog.GetEffect(CardId.StrikeIronclad, upgraded: true)!.Damage);

    [Fact]
    public void DefendBaseBlocksFive() =>
        Assert.Equal(5, Catalog.GetEffect(CardId.DefendIronclad, upgraded: false)!.Block);

    [Fact]
    public void BashAppliesVulnerable() =>
        Assert.Equal(2, Catalog.GetEffect(CardId.Bash, upgraded: false)!.VulnerableApply);

    [Fact]
    public void InflameIsAPower() =>
        Assert.True(Catalog.GetEffect(CardId.Inflame, upgraded: false)!.IsPower);

    [Fact]
    public void InflameGivesTwoStrength() =>
        Assert.Equal(2, Catalog.GetEffect(CardId.Inflame, upgraded: false)!.StrengthGain);

    [Fact]
    public void BodySlamIsBlockToDamage() =>
        Assert.True(Catalog.GetEffect(CardId.BodySlam, upgraded: false)!.BlockToDamage);

    // The previous batch of "<Card>IsHeadlessUnsafe" tests was deleted on
    // 2026-05-24: every Ironclad card the catalog had flagged unsafe
    // (Headbutt, Armaments, BurningPact, DualWield, InfernalBlade,
    // Whirlwind) now plays cleanly through the engine — verified by
    // tests/Sts2Headless.IntegrationTests/CardCatalogProbeTests.cs. The
    // PrefsSave-NRE bootstrap fix from 2026-05-22 closed the underlying
    // issue. Re-add a test here only when a new card surfaces a real
    // headless gap.
    [Fact]
    public void NoIroncladCardsCurrentlyHeadlessUnsafe()
    {
        foreach (var id in Catalog.ModelledIds)
        {
            var effect = Catalog.GetEffect(id, upgraded: false);
            Assert.False(effect!.IsHeadlessUnsafe,
                $"card {id} is flagged IsHeadlessUnsafe — if this is intentional, " +
                "update this test to allow it explicitly.");
            var upgraded = Catalog.GetEffect(id, upgraded: true);
            if (upgraded is not null)
            {
                Assert.False(upgraded.IsHeadlessUnsafe,
                    $"upgraded card {id} is flagged IsHeadlessUnsafe — same rule.");
            }
        }
    }

    [Fact]
    public void UnknownCardReturnsNull() =>
        Assert.Null(Catalog.GetEffect(CardId.Unknown, upgraded: false));

    [Fact]
    public void ModelledIdsContainsCoreIroncladCards()
    {
        var modelled = Catalog.ModelledIds;
        Assert.Contains(CardId.StrikeIronclad, modelled);
        Assert.Contains(CardId.DefendIronclad, modelled);
        Assert.Contains(CardId.Bash, modelled);
        Assert.Contains(CardId.Inflame, modelled);
        Assert.Contains(CardId.DemonForm, modelled);
    }

    [Fact]
    public void ModelledIdsCountIsAtLeastForty()
    {
        // v1 target: ~45 cards. Floor at 40 guards against accidental
        // wholesale deletion; ceiling-free so the catalog can grow.
        Assert.True(Catalog.ModelledIds.Count >= 40,
            $"expected at least 40 modelled cards, got {Catalog.ModelledIds.Count}");
    }

    [Fact]
    public void EveryModelledCardHasACategory()
    {
        // Every modelled card is either an attack, skill, power, status,
        // or curse. Catches accidental "new(VulnerableApply: 1)" entries
        // that forget IsAttack/IsSkill — the planner's ordering and the
        // evaluator's tagging depend on the category.
        foreach (var id in Catalog.ModelledIds)
        {
            var effect = Catalog.GetEffect(id, upgraded: false);
            Assert.NotNull(effect);
            Assert.True(
                effect!.IsAttack || effect.IsSkill || effect.IsPower
                    || effect.IsStatus || effect.IsCurse,
                $"card {id} has no category (IsAttack/IsSkill/IsPower/IsStatus/IsCurse)");
        }
    }
}

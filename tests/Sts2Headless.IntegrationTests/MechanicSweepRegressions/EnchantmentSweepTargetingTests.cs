using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests.MechanicSweepRegressions;

// Regression pin for the EnchantmentSweep fixture fix.
//
// Before: the sweep's FixedDeck was [STRIKE×2, DEFEND×2] and the sweep
// only tried hand index 0. Type-restricted enchantments refused
// STRIKE outright:
//
//   * GOOPY, NIMBLE → require Skill cards
//   * IMBUED        → requires Power cards
//   * SOULS_POWER   → requires the Exhaust keyword on the target
//
// Four enchantments came back Unplayable for a fixture-shape reason,
// not an engine bug.
//
// The fix: FixedDeck now spans the archetype space
// (STRIKE / DEFEND / INFLAME [Power] / IMPERVIOUS [Exhaust Skill] /
// DEMON_FORM [Power]) and the sweep walks every hand index until one
// enchant_card succeeds. All 22 enchantments now Play or Trigger.
//
// These tests pin one happy-path per fix axis so a regression in the
// per-hand-index walk or the deck composition surfaces here ahead of
// the slow full sweep.
public class EnchantmentSweepTargetingTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    public EnchantmentSweepTargetingTests(HostSubprocess host) => _host = host;

    private static readonly (string CardId, int UpgradeLevel)[] FixtureDeck =
    [
        ("STRIKE_IRONCLAD", 0),
        ("DEFEND_IRONCLAD", 0),
        ("INFLAME",         0),
        ("IMPERVIOUS",      0),
        ("DEMON_FORM",      0),
    ];

    // For each of the 4 previously-failing enchantments, the same deck
    // must yield at least one valid target. The test walks the hand
    // looking for the first index that accepts the enchantment — same
    // logic as the sweep.
    [Theory]
    [InlineData("GOOPY")]
    [InlineData("IMBUED")]
    [InlineData("NIMBLE")]
    [InlineData("SOULS_POWER")]
    public async Task TypeRestrictedEnchantment_FindsValidTargetInMixedDeck(string enchantmentId)
    {
        var transport = new HostSubprocessTransportAdapter(_host);
        await transport.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        await transport.SetHpAsync(999, 999);
        await transport.ReplaceDeckAsync(FixtureDeck);
        await transport.StartCombatAsync("SLIMES_NORMAL");

        var state = await transport.SendAsync<RunStateResult>("run/state");
        Assert.NotNull(state.CombatState);
        var handSize = state.CombatState!.Hand.Count;
        Assert.True(handSize > 0, "fixture should have a non-empty opening hand");

        // Same walk shape the sweep uses: try every hand index until
        // one succeeds. If all refuse, the enchantment is genuinely
        // not satisfiable by this fixture — surface that.
        string? successCard = null;
        System.Exception? lastErr = null;
        for (int i = 0; i < handSize; i++)
        {
            try
            {
                var resp = await transport.EnchantCardAsync(enchantmentId, handIndex: i, amount: 1);
                successCard = resp.CardId;
                break;
            }
            catch (System.Exception ex) { lastErr = ex; }
        }
        Assert.True(successCard is not null,
            $"{enchantmentId}: refused on every hand card. "
            + $"Last error: {lastErr?.Message ?? "<none>"}. "
            + "Extend EnchantmentSweep.FixedDeck to cover this enchantment's "
            + "card-type requirement.");
    }
}

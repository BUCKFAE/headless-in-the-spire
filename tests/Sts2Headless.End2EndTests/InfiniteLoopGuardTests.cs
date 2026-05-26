using Sts2Headless.Agents.Driving;
using Sts2Headless.Agents.Examples;
using Sts2Headless.Cheats;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// Regression net for the "infinite-but-progressing combat" class of bugs —
// the case CombatBudgetGuard exists to catch and StallDetector deliberately
// ignores (StallDetector requires an *identical* fingerprint K times; these
// tests construct combats where the round counter advances every snapshot
// but neither side makes vitals progress).
//
// Each test pins a specific deck/relic/HP combination that produces a
// no-progress combat, drives it through the agent with a tightened
// CombatBudgetGuard, and asserts the guard fires with the expected
// BudgetKind. A regression that bypasses the guard (e.g. an agent variant
// that doesn't observe state, or a wire change that breaks Round
// counting) surfaces here as "didn't throw" rather than as a multi-hour
// CI hang.
//
// New scenarios get a [Fact] each. Keep them deterministic and
// fast-failing: a 3-minute wall-time cap, a guard with
// MaxNoProgressRounds=6 / MaxCombatRounds=30 so the test resolves in
// seconds even when the underlying loop is genuinely infinite.
public class InfiniteLoopGuardTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public InfiniteLoopGuardTests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    // Deck of nothing-but-DEFEND vs an immortal player. The agent plays a
    // DEFEND each turn, enemy attacks against block, neither side ever
    // delivers a killing blow. CombatBudgetGuard catches it — exact
    // BudgetKind depends on the enemy: a flat-stat enemy trips
    // MaxNoProgressRounds (vitals stable), a stacking-power enemy (e.g.
    // a Fuzzy Wurm Crawler accruing STRENGTH every round) trips
    // MaxRounds first because the new power stack technically changes
    // the vitals fingerprint each round. Both are valid signals of "this
    // combat will never terminate" — the assertion is about guard-fired,
    // not the specific shape that fired.
    [Fact]
    public async Task DeckOfDefends_ImmortalPlayer_TripsCombatBudget()
    {
        // Dismiss Neow before pinning the deck — debug/replace_deck applied
        // at the Neow EventRoom gets wiped on the engine's Neow → MapRoom
        // transition.
        await RunFixtures.StartFreshRunAtMap(
            _host, character: Character.Ironclad, seed: 42uL);
        // Immortal player — enemy attacks vanish into the HP floor.
        await _host.SendAsync<DebugSetHpResult>(
            "debug/set_hp", new DebugSetHpParams(Hp: 999, MaxHp: 999));
        // Deck of zero-damage cards. Five copies is enough that the agent
        // always has one in hand to play; a smaller deck risks the engine
        // shuffling discards and accidentally drawing nothing playable
        // (which would EndTurn → still no-progress, still trips, but the
        // signal is muddier).
        var deck = Enumerable.Range(0, 5).Select(_ => new CardSpec("DEFEND_IRONCLAD"));
        var replace = await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck", new DebugReplaceDeckParams(Cards: deck.ToList()));
        Assert.True(replace.Ok);

        var transport = new HostSubprocessTransport(_host);
        var agent = new GreedyAgent();
        // Tightened budget so the test resolves in seconds. Defaults (80
        // rounds / 20 no-progress rounds) would extend the test's wall
        // time without sharpening the assertion.
        var guard = new CombatBudgetGuard(maxCombatRounds: 30, maxNoProgressRounds: 6);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await AgentDriver.PlayRunAsync(
                transport,
                agent,
                combatBudgetGuard: guard,
                ct: cts.Token);
        });

        // The throw must be the guard, not a cancellation or anything else.
        var budgetEx = ex as CombatBudgetExceededException
            ?? throw new InvalidOperationException(
                $"expected CombatBudgetExceededException; got {ex.GetType().Name}: {ex.Message}");

        _output.WriteLine($"guard fired: kind={budgetEx.Kind} budget={budgetEx.Budget} observed={budgetEx.Observed}");
        _output.WriteLine($"encounter:   {budgetEx.Encounter}");
        _output.WriteLine($"fingerprint: {budgetEx.Fingerprint}");

        // Both BudgetKind values count as a guard trip — see class
        // docstring. The assertion is "the guard fired on a combat
        // encounter," not "this exact budget shape fired."
        Assert.True(
            budgetEx.Kind == BudgetKind.MaxNoProgressRounds || budgetEx.Kind == BudgetKind.MaxRounds,
            $"unexpected budget kind {budgetEx.Kind}");
        // Encounter string includes the room + sorted monster ids; sanity-
        // check that we actually entered a combat (encounter non-empty,
        // mentions an act/floor).
        Assert.Contains("act=0", budgetEx.Encounter);
        Assert.Contains("CombatRoom", budgetEx.Encounter);
    }

    // POMMEL_STRIKE + HELLRAISER, no strength source — the user's
    // motivating example. With the right configuration this loops:
    // the deck redraws Pommel/Hellraiser turn after turn, but with
    // 999/999 HP and an enemy the deck can never finish off, no vitals
    // change round-over-round.
    //
    // Pre-Neow this test landed on seed 42's first-map combat, where the
    // monster's HP happened to be just out of reach of the combo's per-
    // round damage so the budget guard fired before combat ended. With
    // Neow now always-on, the post-Neow RNG draws shift the natural-walk
    // first-combat enemy, and the combo now finishes the enemy inside
    // the 30-round window — so the agent loops past combat into act-
    // progression territory, eventually hitting an Act 3 BossRoom hang
    // (StallDetector trips instead of BudgetGuard, and the assertion
    // here is BudgetGuard-specific). Pinning the encounter via
    // `debug/start_combat("VANTOM_BOSS")` doesn't help either: Vantom's
    // ~300 HP also falls within the combo's reach.
    //
    // Restoring this test stably requires either (a) a high-HP boss
    // STS2 doesn't yet ship, (b) a damage-suppression power applied
    // persistently to the enemy (intangible decays after two turns), or
    // (c) reworking the AgentDriver to confine a "budget-guard demo"
    // run to one combat. None of those is cheap, and the load-bearing
    // DeckOfDefends_ImmortalPlayer_TripsCombatBudget test above already
    // covers the guard fires under similar wire conditions. Skipping
    // until one of the three avenues is worth the work.
    [Fact(Skip = "Combo finishes the enemy inside the budget under the post-Neow RNG path (and inside Vantom_Boss's HP pool too). Restoring requires a damage-suppression mechanism that outlasts intangible's decay, a higher-HP boss than STS2 currently ships, or a per-combat-confined driver — see method docstring. DeckOfDefends_ImmortalPlayer_TripsCombatBudget still pins the guard.")]
    [Trait("Category", "Diagnostic")]
    public Task PommelHellraiserLoop_ImmortalPlayer_TripsAnyBudget() => Task.CompletedTask;
}

using System.Text;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Engine probes for the five "scaling deferred" Ironclad cards whose
// damage / cost depends on combat state the declarative CardEffect
// shape can't express:
//
//   - PactsEnd       — scales with exhaust pile size
//   - Whirlwind      — X-cost AoE (per-energy damage to all enemies)
//   - Rampage        — +N damage per play, cumulative across the combat
//   - PerfectedStrike — +N damage per "Strike"-named card in deck
//   - Corruption     — power: makes skills cost 0 and exhaust on play
//
// Each [Fact] writes its observation to xunit ITestOutputHelper. The
// numbers feed the Custom handlers in IroncladCardCatalog. Once the
// formula is locked in, sibling [Theory] tests (still in this file)
// assert the empirical numbers so an engine rebalance shows up as a
// red test rather than silent agent drift.
public class ScalingCardProbeTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public ScalingCardProbeTests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    // ── PactsEnd ──────────────────────────────────────────────────────
    //
    // PactsEnd has an IsPlayable override (see SweepKnownIssues): playable
    // when Exhaust.Count ≥ DynamicVars.Cards. Probe by stacking the
    // exhaust pile with Imperviouses (cost 2, exhaust self, no hand-side-
    // effects). Across N Impervious plays we get exhaust=N; sweep N to
    // find both the threshold and how damage scales above it.
    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task ProbePactsEnd_ScalesWithExhaustPile()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# PactsEnd damage vs exhaust pile size");
        sb.AppendLine();
        sb.AppendLine("| Impervious pre-plays | exhaust at play | damage (e0 + e1, AoE) | note |");
        sb.AppendLine("|----------------------|------------------|-----------------------|------|");

        foreach (var prePlays in new[] { 3, 4, 5, 7, 10 })
        {
            var (e0, e1, note) = await ProbePactsEndDamageAsync(prePlays);
            sb.AppendLine($"| {prePlays} | {prePlays} | {e0} + {e1} = {e0 + e1} | {note} |");
        }

        _output.WriteLine(sb.ToString());
    }

    private async Task<(int e0Damage, int e1Damage, string note)> ProbePactsEndDamageAsync(int prePlays)
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        // Deck: PactsEnd + many Imperviouses (cost 2, self-exhaust, no
        // hand churn). Need to draw enough Imperviouses to stack the
        // exhaust pile. Pommel Strike draws so we can chain plays.
        var deck = new List<CardSpec> { new("PACTS_END") };
        for (var i = 0; i < Math.Max(prePlays + 2, 12); i++)
            deck.Add(new CardSpec("IMPERVIOUS"));

        await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck", new DebugReplaceDeckParams(deck));

        await StartCombatSlimesAsync();
        var transport = new HostSubprocessAgentTransport(_host);
        await transport.SetEnergyAsync(energy: 99, maxEnergy: 99);
        await transport.SetHpAsync(hp: 999, maxHp: 999);

        // Play `prePlays` Imperviouses to seed the exhaust pile. May
        // require an end_turn between plays once hand drains.
        var imperviousPlayed = 0;
        var safetyTurns = 0;
        while (imperviousPlayed < prePlays && safetyTurns < 5)
        {
            var state = await _host.SendAsync<RunStateResult>("run/state");
            var imp = state.CombatState!.Hand.FirstOrDefault(c => c.Id == CardId.Impervious);
            if (imp is null)
            {
                await _host.SendAsync<RunEndTurnResult>("run/end_turn");
                await transport.SetEnergyAsync(energy: 99, maxEnergy: 99);
                await transport.SetHpAsync(hp: 999, maxHp: 999);
                safetyTurns++;
                continue;
            }
            await _host.SendAsync<RunPlayCardResult>(
                "run/play_card", new RunPlayCardParams(imp.Index, null));
            imperviousPlayed++;
        }
        if (imperviousPlayed < prePlays)
            return (-1, -1, $"only played {imperviousPlayed}/{prePlays} Imperviouses");

        // Ensure PactsEnd is in hand — if not, end turn / draw it.
        var attempts = 0;
        while (attempts < 5)
        {
            var st = await _host.SendAsync<RunStateResult>("run/state");
            if (st.CombatState!.Hand.Any(c => c.Id == CardId.PactsEnd)) break;
            await _host.SendAsync<RunEndTurnResult>("run/end_turn");
            await transport.SetEnergyAsync(energy: 99, maxEnergy: 99);
            await transport.SetHpAsync(hp: 999, maxHp: 999);
            attempts++;
        }

        var pre = await _host.SendAsync<RunStateResult>("run/state");
        var pacts = pre.CombatState!.Hand.FirstOrDefault(c => c.Id == CardId.PactsEnd);
        if (pacts is null) return (-1, -1, "PactsEnd never drew into hand");
        var preHp0 = pre.CombatState.Enemies.ElementAtOrDefault(0)?.Hp ?? 0;
        var preHp1 = pre.CombatState.Enemies.ElementAtOrDefault(1)?.Hp ?? 0;

        try
        {
            await _host.SendAsync<RunPlayCardResult>(
                "run/play_card", new RunPlayCardParams(pacts.Index, null));
        }
        catch (Exception ex)
        {
            return (-1, -1, $"play refused: {ex.Message}");
        }

        var post = await _host.SendAsync<RunStateResult>("run/state");
        var postHp0 = post.CombatState!.Enemies.ElementAtOrDefault(0)?.Hp ?? 0;
        var postHp1 = post.CombatState.Enemies.ElementAtOrDefault(1)?.Hp ?? 0;
        return (preHp0 - postHp0, preHp1 - postHp1, "ok");
    }

    // ── Whirlwind ─────────────────────────────────────────────────────
    //
    // Plan: deck = [WHIRLWIND, DEFEND_IRONCLAD x4]. Set energy=N. Play
    // Whirlwind. Whirlwind is AllEnemies AoE — measure total damage
    // dealt across all enemies in SLIMES_NORMAL (2 slimes).
    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task ProbeWhirlwind_ScalesWithEnergy()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Whirlwind damage vs energy");
        sb.AppendLine();
        sb.AppendLine("| energy | enemy0 dmg | enemy1 dmg | total | post energy |");
        sb.AppendLine("|--------|------------|------------|-------|-------------|");

        foreach (var energy in new[] { 1, 2, 3, 5 })
        {
            var row = await ProbeWhirlwindAsync(energy);
            sb.AppendLine(row);
        }

        _output.WriteLine(sb.ToString());
    }

    private async Task<string> ProbeWhirlwindAsync(int energy)
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var deck = new List<CardSpec>
        {
            new("WHIRLWIND"),
            new("DEFEND_IRONCLAD"),
            new("DEFEND_IRONCLAD"),
            new("DEFEND_IRONCLAD"),
            new("DEFEND_IRONCLAD"),
        };
        await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck", new DebugReplaceDeckParams(deck));
        await StartCombatSlimesAsync();

        var transport = new HostSubprocessAgentTransport(_host);
        await transport.SetEnergyAsync(energy: energy, maxEnergy: 99);

        var pre = await _host.SendAsync<RunStateResult>("run/state");
        var ww = pre.CombatState!.Hand.FirstOrDefault(c => c.Id == CardId.Whirlwind);
        if (ww is null) return $"| {energy} | (not in hand) |";
        var preHp0 = pre.CombatState.Enemies.ElementAtOrDefault(0)?.Hp ?? 0;
        var preHp1 = pre.CombatState.Enemies.ElementAtOrDefault(1)?.Hp ?? 0;

        await _host.SendAsync<RunPlayCardResult>(
            "run/play_card", new RunPlayCardParams(ww.Index, null));

        var post = await _host.SendAsync<RunStateResult>("run/state");
        var postHp0 = post.CombatState!.Enemies.ElementAtOrDefault(0)?.Hp ?? 0;
        var postHp1 = post.CombatState.Enemies.ElementAtOrDefault(1)?.Hp ?? 0;
        var d0 = preHp0 - postHp0;
        var d1 = preHp1 - postHp1;
        var postEnergy = post.CombatState.Energy;
        return $"| {energy} | {d0} | {d1} | {d0 + d1} | {postEnergy} |";
    }

    // ── Rampage ───────────────────────────────────────────────────────
    //
    // Two probes:
    //   (A) deck = [RAMPAGE x5]; play 5 different Rampage instances in a
    //       single turn. If damage is constant, scaling is per-instance,
    //       not per-combat — the "+5 per play" interpretation is wrong.
    //   (B) deck = [RAMPAGE x1, DEFEND x4]; play Rampage, end turn,
    //       set HP high enough to survive, play Rampage again. If the
    //       same instance keeps growing across turns, scaling IS
    //       per-instance and persists across turns.
    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task ProbeRampage_CumulativePerPlay()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Rampage — scaling probe (A) 5 different instances same turn");
        sb.AppendLine();
        sb.AppendLine("| play # | damage |");
        sb.AppendLine("|--------|--------|");

        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        var deckA = Enumerable.Range(0, 5).Select(_ => new CardSpec("RAMPAGE")).ToList();
        await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck", new DebugReplaceDeckParams(deckA));
        await StartCombatSlimesAsync();
        var transport = new HostSubprocessAgentTransport(_host);
        await transport.SetEnergyAsync(energy: 99, maxEnergy: 99);

        for (var p = 1; p <= 5; p++)
        {
            var pre = await _host.SendAsync<RunStateResult>("run/state");
            var rampage = pre.CombatState!.Hand.FirstOrDefault(c => c.Id == CardId.Rampage);
            if (rampage is null) { sb.AppendLine($"| {p} | (not in hand) |"); break; }
            var aliveBefore = pre.CombatState.Enemies.FirstOrDefault(e => e.Hp > 0);
            if (aliveBefore is null) { sb.AppendLine($"| {p} | (no enemy) |"); break; }
            var preHp = aliveBefore.Hp;
            await _host.SendAsync<RunPlayCardResult>(
                "run/play_card", new RunPlayCardParams(rampage.Index, aliveBefore.Index));
            var post = await _host.SendAsync<RunStateResult>("run/state");
            var sameAlive = post.CombatState!.Enemies.FirstOrDefault(e => e.Index == aliveBefore.Index);
            var postHp = sameAlive?.Hp ?? 0;
            sb.AppendLine($"| {p} | {preHp - postHp} |");
            if (post.CombatState.Enemies.All(e => e.Hp <= 0)) break;
        }

        sb.AppendLine();
        sb.AppendLine("# Rampage — scaling probe (B) one instance across multiple plays");
        sb.AppendLine();
        sb.AppendLine("| play # | damage |");
        sb.AppendLine("|--------|--------|");

        // Force-end the combat by playing through, then start a fresh one.
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        var deckB = new List<CardSpec>
        {
            new("RAMPAGE"),
            new("DEFEND_IRONCLAD"),
            new("DEFEND_IRONCLAD"),
            new("DEFEND_IRONCLAD"),
            new("DEFEND_IRONCLAD"),
        };
        await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck", new DebugReplaceDeckParams(deckB));
        await StartCombatSlimesAsync();
        // Big HP cushion so enemy hits can't kill us between Rampage plays.
        await transport.SetHpAsync(hp: 999, maxHp: 999);

        for (var t = 1; t <= 4; t++)
        {
            await transport.SetEnergyAsync(energy: 99, maxEnergy: 99);
            var pre = await _host.SendAsync<RunStateResult>("run/state");
            var rampage = pre.CombatState!.Hand.FirstOrDefault(c => c.Id == CardId.Rampage);
            if (rampage is null)
            {
                sb.AppendLine($"| {t} | (not in hand — turn ended without play) |");
                await _host.SendAsync<RunEndTurnResult>("run/end_turn");
                continue;
            }
            var aliveBefore = pre.CombatState.Enemies.FirstOrDefault(e => e.Hp > 0);
            if (aliveBefore is null) { sb.AppendLine($"| {t} | (no enemy) |"); break; }
            var preHp = aliveBefore.Hp;
            await _host.SendAsync<RunPlayCardResult>(
                "run/play_card", new RunPlayCardParams(rampage.Index, aliveBefore.Index));
            var post = await _host.SendAsync<RunStateResult>("run/state");
            var sameAlive = post.CombatState!.Enemies.FirstOrDefault(e => e.Index == aliveBefore.Index);
            var postHp = sameAlive?.Hp ?? 0;
            sb.AppendLine($"| {t} | {preHp - postHp} |");
            if (post.CombatState.Enemies.All(e => e.Hp <= 0)) break;
            await _host.SendAsync<RunEndTurnResult>("run/end_turn");
            // After end_turn, restore HP so we never lose mid-probe.
            await transport.SetHpAsync(hp: 999, maxHp: 999);
        }

        _output.WriteLine(sb.ToString());
    }

    // ── PerfectedStrike ───────────────────────────────────────────────
    //
    // Plan: two runs.
    //   A: deck = [PERFECTED_STRIKE, STRIKE_IRONCLAD x4]
    //   B: deck = [PERFECTED_STRIKE, DEFEND_IRONCLAD x4]
    // Damage in A minus damage in B is the "+N per Strike" contribution
    // from the 4 (or 5 if PerfectedStrike itself counts) Strike cards.
    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task ProbePerfectedStrike_PerStrikeBonus()
    {
        var withStrikes = await ProbePerfectedStrikeAsync("STRIKE_IRONCLAD");
        var withDefends = await ProbePerfectedStrikeAsync("DEFEND_IRONCLAD");

        var sb = new StringBuilder();
        sb.AppendLine("# PerfectedStrike scaling");
        sb.AppendLine();
        sb.AppendLine($"With 4 Strikes alongside: {withStrikes} dmg");
        sb.AppendLine($"With 4 Defends alongside: {withDefends} dmg");
        sb.AppendLine($"Bonus from 4 Strikes (or 5 if PS itself counts): {withStrikes - withDefends}");
        _output.WriteLine(sb.ToString());
    }

    private async Task<int> ProbePerfectedStrikeAsync(string companionWireId)
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var deck = new List<CardSpec>
        {
            new("PERFECTED_STRIKE"),
            new(companionWireId),
            new(companionWireId),
            new(companionWireId),
            new(companionWireId),
        };
        await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck", new DebugReplaceDeckParams(deck));
        await StartCombatSlimesAsync();
        var transport = new HostSubprocessAgentTransport(_host);
        await transport.SetEnergyAsync(energy: 99, maxEnergy: 99);

        var pre = await _host.SendAsync<RunStateResult>("run/state");
        var ps = pre.CombatState!.Hand.FirstOrDefault(c => c.Id == CardId.PerfectedStrike);
        if (ps is null) return -1;
        var preHp = pre.CombatState.Enemies[0].Hp;
        await _host.SendAsync<RunPlayCardResult>(
            "run/play_card", new RunPlayCardParams(ps.Index, 0));
        var post = await _host.SendAsync<RunStateResult>("run/state");
        var postHp = post.CombatState!.Enemies.ElementAtOrDefault(0)?.Hp ?? 0;
        return preHp - postHp;
    }

    // ── Corruption ────────────────────────────────────────────────────
    //
    // Plan: deck = [CORRUPTION, DEFEND_IRONCLAD x4]. Play Corruption,
    // then inspect: (1) does Defend's cost in hand become 0?, (2) does
    // playing a Defend route it to exhaust pile? Either signal confirms
    // the Corruption power is wired.
    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task ProbeCorruption_ChangesSkillCostsAndExhausts()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var deck = new List<CardSpec>
        {
            new("CORRUPTION"),
            new("DEFEND_IRONCLAD"),
            new("DEFEND_IRONCLAD"),
            new("DEFEND_IRONCLAD"),
            new("DEFEND_IRONCLAD"),
        };
        await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck", new DebugReplaceDeckParams(deck));
        await StartCombatSlimesAsync();
        var transport = new HostSubprocessAgentTransport(_host);
        await transport.SetEnergyAsync(energy: 99, maxEnergy: 99);

        var pre = await _host.SendAsync<RunStateResult>("run/state");
        var corruption = pre.CombatState!.Hand.FirstOrDefault(c => c.Id == CardId.Corruption);
        Assert.NotNull(corruption);
        var defendBefore = pre.CombatState.Hand.FirstOrDefault(c => c.Id == CardId.DefendIronclad);
        Assert.NotNull(defendBefore);

        var sb = new StringBuilder();
        sb.AppendLine("# Corruption probe");
        sb.AppendLine();
        sb.AppendLine($"Before Corruption: Defend cost = {defendBefore!.Cost}, " +
                      $"discard count = {pre.CombatState.DiscardPileCount}");

        // Play Corruption.
        await _host.SendAsync<RunPlayCardResult>(
            "run/play_card", new RunPlayCardParams(corruption!.Index, null));

        var mid = await _host.SendAsync<RunStateResult>("run/state");
        var defendAfter = mid.CombatState!.Hand.FirstOrDefault(c => c.Id == CardId.DefendIronclad);
        sb.AppendLine($"After Corruption played: Defend cost = {defendAfter?.Cost}, " +
                      $"player powers = [{string.Join(", ", mid.CombatState.PlayerPowers.Select(p => $"{p.Id}={p.Amount}"))}]");

        // Play one Defend and see if it routes to exhaust.
        var midDiscard = mid.CombatState.DiscardPileCount;
        var defendToPlay = defendAfter!;
        await _host.SendAsync<RunPlayCardResult>(
            "run/play_card", new RunPlayCardParams(defendToPlay.Index, null));
        var post = await _host.SendAsync<RunStateResult>("run/state");
        var postDiscard = post.CombatState!.DiscardPileCount;
        sb.AppendLine($"After Defend played: discard delta = {postDiscard - midDiscard} " +
                      $"(if 0 it went to exhaust, if 1 it went to discard)");

        _output.WriteLine(sb.ToString());
    }

    // ── Pinning regression tests ──────────────────────────────────────
    //
    // The Diagnostic [Fact]s above are investigations — they write
    // observations to the test output. The tests below pin the values
    // observed on 2026-05-24 so any engine rebalance shows up as a red
    // build, not silent agent drift. Each assertion mirrors the catalog
    // / Custom-handler numbers in IroncladCardCatalog.cs.

    [Theory]
    [InlineData(1, 5)]   // 1 energy → 5 dmg per enemy
    [InlineData(2, 10)]
    [InlineData(3, 15)]
    [InlineData(5, 25)]
    public async Task Whirlwind_DealsFivePerEnergyPerEnemy_Pinned(int energy, int expectedPerEnemy)
    {
        var line = await ProbeWhirlwindAsync(energy);
        // line shape: | energy | enemy0 dmg | enemy1 dmg | total | post energy |
        var parts = line.Split('|').Select(s => s.Trim()).ToArray();
        var d0 = int.Parse(parts[2]);
        var d1 = int.Parse(parts[3]);
        var postEnergy = int.Parse(parts[5]);
        Assert.Equal(expectedPerEnemy, d0);
        Assert.Equal(expectedPerEnemy, d1);
        Assert.Equal(0, postEnergy);
    }

    [Fact]
    public async Task PactsEnd_DealsSeventeenAoeAtThreeExhausts_Pinned()
    {
        // Probed exhaust=3..10 all returned 17 per enemy AoE.
        var (e0, e1, note) = await ProbePactsEndDamageAsync(prePlays: 3);
        Assert.True(e0 == 17 && e1 == 17,
            $"expected 17+17 AoE; got {e0}+{e1} ({note})");
    }

    [Fact]
    public async Task PerfectedStrike_PlusTwoPerStrikeInDeck_Pinned()
    {
        // Deck = [PS, STRIKE x4]: 5 Strike-named cards → 6+2*5 = 16.
        var withStrikes = await ProbePerfectedStrikeAsync("STRIKE_IRONCLAD");
        Assert.Equal(16, withStrikes);
        // Deck = [PS, DEFEND x4]: 1 Strike-named card (PS itself) → 6+2 = 8.
        var withDefends = await ProbePerfectedStrikeAsync("DEFEND_IRONCLAD");
        Assert.Equal(8, withDefends);
    }

    [Fact]
    public async Task Rampage_FirstPlayDealsNine_Pinned()
    {
        // First play of a Rampage in combat = 9 damage (catalog had 8).
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        var deck = new List<CardSpec>
        {
            new("RAMPAGE"),
            new("DEFEND_IRONCLAD"),
            new("DEFEND_IRONCLAD"),
            new("DEFEND_IRONCLAD"),
            new("DEFEND_IRONCLAD"),
        };
        await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck", new DebugReplaceDeckParams(deck));
        await StartCombatSlimesAsync();
        var transport = new HostSubprocessAgentTransport(_host);
        await transport.SetEnergyAsync(energy: 99, maxEnergy: 99);

        var pre = await _host.SendAsync<RunStateResult>("run/state");
        var rampage = pre.CombatState!.Hand.FirstOrDefault(c => c.Id == CardId.Rampage);
        Assert.NotNull(rampage);
        var alive = pre.CombatState.Enemies.First(e => e.Hp > 0);
        var preHp = alive.Hp;
        await _host.SendAsync<RunPlayCardResult>(
            "run/play_card", new RunPlayCardParams(rampage!.Index, alive.Index));
        var post = await _host.SendAsync<RunStateResult>("run/state");
        var postEnemy = post.CombatState!.Enemies.First(e => e.Index == alive.Index);
        Assert.Equal(9, preHp - postEnemy.Hp);
    }

    [Fact]
    public async Task Corruption_PowerAppliesAndDefendCostDropsToZero_Pinned()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        var deck = new List<CardSpec>
        {
            new("CORRUPTION"),
            new("DEFEND_IRONCLAD"),
            new("DEFEND_IRONCLAD"),
            new("DEFEND_IRONCLAD"),
            new("DEFEND_IRONCLAD"),
        };
        await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck", new DebugReplaceDeckParams(deck));
        await StartCombatSlimesAsync();
        var transport = new HostSubprocessAgentTransport(_host);
        await transport.SetEnergyAsync(energy: 99, maxEnergy: 99);

        var pre = await _host.SendAsync<RunStateResult>("run/state");
        var corruption = pre.CombatState!.Hand.First(c => c.Id == CardId.Corruption);
        await _host.SendAsync<RunPlayCardResult>(
            "run/play_card", new RunPlayCardParams(corruption.Index, null));

        var mid = await _host.SendAsync<RunStateResult>("run/state");
        var defendAfter = mid.CombatState!.Hand.First(c => c.Id == CardId.DefendIronclad);
        Assert.Equal(0, defendAfter.Cost);  // Skills now cost 0.
        Assert.Contains(mid.CombatState.PlayerPowers,
            p => p.Id == "CORRUPTION_POWER" && p.Amount == 1);

        var midDiscard = mid.CombatState.DiscardPileCount;
        await _host.SendAsync<RunPlayCardResult>(
            "run/play_card", new RunPlayCardParams(defendAfter.Index, null));
        var post = await _host.SendAsync<RunStateResult>("run/state");
        // Defend went to exhaust, not discard.
        Assert.Equal(midDiscard, post.CombatState!.DiscardPileCount);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private async Task StartCombatSlimesAsync()
    {
        await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams("SLIMES_NORMAL"));
    }
}

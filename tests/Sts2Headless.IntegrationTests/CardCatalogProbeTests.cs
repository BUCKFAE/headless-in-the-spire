using System.Text;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Engine probes for Ironclad cards whose catalog entries were once
// guessed ("TODO verify") or flagged as unsafe in headless. For each
// card under test the probe:
//
//   1. run/new Ironclad seed=42
//   2. debug/replace_deck = [card x1, DEFEND_IRONCLAD x4]
//   3. debug/start_combat SLIMES_NORMAL
//   4. debug/set_energy = 99 (so cost is never the reason a card refuses to play)
//   5. find the card in hand by CardId
//   6. play_card and diff CombatState before / after
//
// Acts as a regression net: every card listed here must keep playing
// cleanly headless. The diff (damage to enemy, block on player, powers
// applied, hand change, max-hp change) also pins the measured stats so
// engine rebalances surface as test breaks, not silent acceptance.
//
// The probe writes a markdown summary to /tmp/card-probe.md, captured
// in the test output too so CI artifacts pick it up.
public class CardCatalogProbeTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public CardCatalogProbeTests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    // Ironclad cards whose catalog entries were once guessed or whose
    // play paths once NRE'd in headless. Each wire id matches the
    // canonical SCREAMING_SNAKE_CASE the engine uses. Every entry must
    // keep playing cleanly — that's the regression net.
    public static IEnumerable<object[]> CardsUnderTest() =>
        new[]
        {
            new object[] { "BULLY",          false },
            new object[] { "TREMBLE",        false },
            new object[] { "ASHEN_STRIKE",   false },
            new object[] { "CASCADE",        false },
            new object[] { "DISMANTLE",      false },
            new object[] { "TAUNT",          false },
            new object[] { "EXPECT_A_FIGHT", false },
            new object[] { "CRIMSON_MANTLE", false },
            new object[] { "BRAND",          false },
            new object[] { "HEADBUTT",       false },
            new object[] { "ARMAMENTS",      false },
            new object[] { "BURNING_PACT",   false },
            new object[] { "DUAL_WIELD",     false },
            new object[] { "INFERNAL_BLADE", false },
            new object[] { "WHIRLWIND",      false },
        };

    [Theory]
    [MemberData(nameof(CardsUnderTest))]
    public async Task ProbeCard_PlaysCleanly(string cardWireId, bool upgraded)
    {
        // Regression net: each card listed must keep playing cleanly.
        // If a future engine change re-NREs one of them, this red test
        // tells us before agent runs surface a mid-act crash.
        var observation = await ProbeOne(cardWireId, upgraded);
        _output.WriteLine(observation.ToString());
        Assert.True(observation.Played,
            $"{cardWireId} no longer plays cleanly headless: {observation.Note}");
    }

    // Pin the measured effect of each card with a crisp expectation.
    // If the engine rebalances the card (or our probe fixture changes),
    // this catches the drift — the catalog needs an explicit update,
    // not silent acceptance.
    [Theory]
    [InlineData("BULLY",          4,  0, 0)]    // 4 dmg attack
    [InlineData("ASHEN_STRIKE",   6,  0, 0)]    // 6 dmg attack
    [InlineData("DISMANTLE",      8,  0, 0)]    // 8 dmg attack (was IsSkill in catalog)
    [InlineData("TAUNT",          0,  7, 0)]    // 7 block skill (was no-op)
    [InlineData("HEADBUTT",       9,  0, 0)]    // 9 dmg attack — no longer unsafe
    [InlineData("ARMAMENTS",      0,  5, 0)]    // 5 block skill — no longer unsafe
    [InlineData("BRAND",          0,  0, -1)]   // self-damage 1 (catalog used to guess 2)
    public async Task ProbeCard_StatsMatchExpected(
        string cardWireId, int expectedEnemyDamage, int expectedSelfBlock, int expectedHpDelta)
    {
        var obs = await ProbeOne(cardWireId, upgraded: false);
        Assert.True(obs.Played, $"{cardWireId} did not play: {obs.Note}");
        var pre = obs.HandStatePreplay!;
        var post = obs.HandStatePostplay!;
        var dmg = pre.Enemies.Count > 0 && post.Enemies.Count > 0
            ? pre.Enemies[0].Hp - post.Enemies[0].Hp : 0;
        Assert.Equal(expectedEnemyDamage, dmg);
        Assert.Equal(expectedSelfBlock, post.PlayerBlock);
        Assert.Equal(expectedHpDelta, obs.PostHp - obs.PreHp);
    }

    // Pin the powers each "power-shaped" card applies. CrimsonMantle and
    // ExpectAFight don't fit the damage/block shape; they grant a named
    // power to the player. Brand grants Strength as a side effect.
    [Theory]
    [InlineData("CRIMSON_MANTLE",  "CRIMSON_MANTLE_POWER", 8)]
    [InlineData("EXPECT_A_FIGHT",  "NO_ENERGY_GAIN_POWER", 1)]
    [InlineData("BRAND",           "STRENGTH_POWER",       1)]
    public async Task ProbeCard_AppliesExpectedPower(
        string cardWireId, string expectedPowerId, int expectedAmount)
    {
        var obs = await ProbeOne(cardWireId, upgraded: false);
        Assert.True(obs.Played, $"{cardWireId} did not play: {obs.Note}");
        var expectedPower = PowerIdNames.FromWire(expectedPowerId);
        var preAmount = obs.HandStatePreplay!.PlayerPowers
            .FirstOrDefault(p => p.Id == expectedPower)?.Amount ?? 0;
        var postAmount = obs.HandStatePostplay!.PlayerPowers
            .FirstOrDefault(p => p.Id == expectedPower)?.Amount ?? 0;
        Assert.Equal(expectedAmount, postAmount - preAmount);
    }

    // A single fact that runs the same probe over every card and writes
    // a unified markdown report. Lets a reader grep one file rather than
    // splice individual theory outputs together.
    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task ProbeAll_AndWriteReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Card catalog probe — Ironclad regression set");
        sb.AppendLine();
        sb.AppendLine("Each row is one card played against SLIMES_NORMAL with energy=99,");
        sb.AppendLine("from a deck of [card, defend x4]. Diff is post-play minus pre-play.");
        sb.AppendLine();
        sb.AppendLine("| card | played | dmg→enemy0 | self.block | self.hp Δ | hand Δ | draw Δ | discard Δ | exhaust Δ | powers applied | note |");
        sb.AppendLine("|------|--------|------------|------------|-----------|--------|--------|-----------|-----------|----------------|------|");

        foreach (var row in CardsUnderTest())
        {
            var id = (string)row[0];
            var upgraded = (bool)row[1];
            var obs = await ProbeOne(id, upgraded);
            sb.AppendLine(obs.AsMarkdownRow());
            _output.WriteLine(obs.AsLogLine());
        }

        var path = "/tmp/card-probe.md";
        await File.WriteAllTextAsync(path, sb.ToString());
        _output.WriteLine($"--- report written to {path} ---");
    }

    private async Task<Observation> ProbeOne(string cardWireId, bool upgraded)
    {
        // Fresh run on every probe so we don't leak state between cards.
        // The shared HostSubprocess fixture is fine: run/new resets the
        // session.
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        // Deck: target card + 4 Defends. Hand draws 5 → all 5 in hand.
        var deck = new List<(string, int)>
        {
            (cardWireId, upgraded ? 1 : 0),
            ("DEFEND_IRONCLAD", 0),
            ("DEFEND_IRONCLAD", 0),
            ("DEFEND_IRONCLAD", 0),
            ("DEFEND_IRONCLAD", 0),
        };
        await _host.SendAsync<DebugReplaceDeckResult>(
            "debug/replace_deck",
            new DebugReplaceDeckParams(deck.Select(c => new CardSpec(c.Item1, c.Item2)).ToList()));

        // Use the production cheat client extensions through the
        // HostSubprocessAgentTransport adapter (same path the
        // RestSiteSmithTests use).
        var transport = new HostSubprocessAgentTransport(_host);

        try
        {
            await _host.SendAsync<DebugStartCombatResult>(
                "debug/start_combat", new DebugStartCombatParams("SLIMES_NORMAL"));
        }
        catch (Exception ex)
        {
            return Observation.NoPlay(cardWireId, upgraded, $"start_combat failed: {ex.Message}");
        }

        await transport.SetEnergyAsync(energy: 99, maxEnergy: 99);

        var pre = await _host.SendAsync<RunStateResult>("run/state");
        if (pre.CombatState is null)
            return Observation.NoPlay(cardWireId, upgraded, "no CombatState after start_combat");

        var expectedId = CardIdNames.FromWire(cardWireId);
        if (expectedId == CardId.Unknown)
            return Observation.NoPlay(cardWireId, upgraded, $"unknown wire CardId: {cardWireId}");
        var card = pre.CombatState.Hand.FirstOrDefault(c => c.Id == expectedId);
        if (card is null)
            return Observation.NoPlay(cardWireId, upgraded,
                $"card not in starting hand of 5; hand was: {string.Join(",", pre.CombatState.Hand.Select(c => c.Id))}");

        // Target: enemy 0 if card needs a target.
        int? target = card.TargetType == TargetType.AnyEnemy ? 0 : (int?)null;

        try
        {
            await _host.SendAsync<RunPlayCardResult>(
                "run/play_card", new RunPlayCardParams(card.Index, target));
        }
        catch (Exception ex)
        {
            return Observation.NoPlay(cardWireId, upgraded, $"play_card refused: {ex.Message}");
        }

        var post = await _host.SendAsync<RunStateResult>("run/state");

        return new Observation(
            CardId: cardWireId,
            Upgraded: upgraded,
            Played: true,
            HandStatePreplay: pre.CombatState,
            HandStatePostplay: post.CombatState,
            PreHp: pre.Hp,
            PostHp: post.Hp,
            PreMaxHp: pre.MaxHp,
            PostMaxHp: post.MaxHp,
            Note: null);
    }

    private sealed record Observation(
        string CardId,
        bool Upgraded,
        bool Played,
        CombatState? HandStatePreplay,
        CombatState? HandStatePostplay,
        int PreHp,
        int PostHp,
        int PreMaxHp,
        int PostMaxHp,
        string? Note)
    {
        public static Observation NoPlay(string cardId, bool upgraded, string reason) =>
            new(cardId, upgraded, Played: false,
                HandStatePreplay: new CombatState(0, 0, 0, 0, false, false, 0, 0,
                    Array.Empty<Card>(), Array.Empty<Enemy>(), Array.Empty<Power>()),
                HandStatePostplay: null,
                PreHp: -1, PostHp: -1, PreMaxHp: -1, PostMaxHp: -1, Note: reason);

        public string AsMarkdownRow()
        {
            if (!Played || HandStatePreplay is null || HandStatePostplay is null)
                return $"| {CardId}{(Upgraded ? "+" : "")} | ✗ |  |  |  |  |  |  |  |  | {Note ?? ""} |";

            var pre = HandStatePreplay;
            var post = HandStatePostplay;
            var enemyDmg = pre.Enemies.Count > 0 && post.Enemies.Count > 0
                ? pre.Enemies[0].Hp - post.Enemies[0].Hp
                : 0;
            var preNames = new HashSet<PowerId>(pre.PlayerPowers.Select(p => p.Id));
            var newPowers = post.PlayerPowers
                .Where(p => !preNames.Contains(p.Id))
                .Select(p => $"{p.Id}={p.Amount}")
                .Concat(post.PlayerPowers
                    .Where(p => preNames.Contains(p.Id))
                    .Select(p =>
                    {
                        var preAmount = pre.PlayerPowers.First(q => q.Id == p.Id).Amount;
                        return p.Amount != preAmount ? $"{p.Id}{(p.Amount - preAmount):+0;-0}" : "";
                    })
                    .Where(s => s.Length > 0))
                .ToList();
            return $"| {CardId}{(Upgraded ? "+" : "")} | ✓ | "
                + $"{enemyDmg} | {post.PlayerBlock} | {PostHp - PreHp:+0;-0;0} | "
                + $"{post.Hand.Count - pre.Hand.Count + 1} | "
                + $"{post.DrawPileCount - pre.DrawPileCount} | "
                + $"{post.DiscardPileCount - pre.DiscardPileCount} | "
                + $"{(pre.Hand.Count - post.Hand.Count - 1)} (≈exhaust) | "
                + $"{string.Join(", ", newPowers)} | "
                + $"{Note ?? ""} |";
        }

        public string AsLogLine() => AsMarkdownRow();

        public override string ToString() => AsMarkdownRow();
    }
}

using Sts2Headless.Agents.Driving;
using Sts2Headless.Agents.Examples;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// Recon: walk Ironclad seed 42 from run/new through to the boss room with
// GreedyAgent + heal-between-rooms, recording every decision and snapshot
// into documentation/research/seed42-recon.md. The output is checked in so
// the seed's terrain (enemies, intent shapes, rewards, events) is a stable
// reference for agent-development work without re-running the test.
//
// Diagnostic, not regression: `[Trait("Category", "Diagnostic")]` keeps it
// out of the default test run; invoke explicitly with
// `dotnet test --filter "FullyQualifiedName~Seed42Recon"`.
public class Seed42ReconTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public Seed42ReconTests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task RecordSeed42Terrain()
    {
        var inner = new HostSubprocessTransport(_host);
        var recon = new ReconTransport(inner);

        // run/new through the recording transport so the recon captures it.
        await recon.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var agent = new GreedyAgent();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        RunStateResult? state = null;
        var healCount = 0;
        var bossEntered = false;
        Exception? error = null;

        // Phase 1: walk to BossRoom, healing between map rooms so the greedy
        //   agent doesn't starve. Phase 2: heal one last time, then let the
        //   agent fight the boss so the recon captures the boss enemy's
        //   round-1 intent + stats. Greedy will lose, but we don't care —
        //   we want the data, not the win.
        try
        {
            while (true)
            {
                state = (await AgentDriver.PlayRunAsync(
                    recon,
                    agent,
                    stopWhen: s => (!bossEntered && s.CurrentRoomType == RoomType.BossRoom)
                                    || (s.CurrentRoomType == RoomType.MapRoom && s.Hp < s.MaxHp),
                    ct: cts.Token)).FinalState;

                if (state.CurrentRoomType == RoomType.BossRoom && !bossEntered)
                {
                    // Heal to full and let the next PlayRunAsync pass push
                    // into the boss combat. We pre-mark bossEntered so the
                    // stop condition no longer fires on BossRoom — the agent
                    // will keep running until game-over (returned via the
                    // outcome) or the stall detector trips.
                    var heal0 = await recon.SendAsync<DebugSetHpResult>(
                        "debug/set_hp", new DebugSetHpParams(Hp: state.MaxHp));
                    Assert.True(heal0.Ok);
                    bossEntered = true;
                    continue;
                }

                var heal = await recon.SendAsync<DebugSetHpResult>(
                    "debug/set_hp", new DebugSetHpParams(Hp: state.MaxHp));
                Assert.True(heal.Ok);
                healCount++;
                if (healCount >= 50) break;
            }
        }
        catch (Exception ex)
        {
            // Game-over throw is expected — the greedy agent loses the boss.
            error = ex;
        }

        // Write to repo-root documentation/research/. The test working dir is
        // the test bin/ folder; walk up to the repo root.
        var outPath = ResolveRepoPath("documentation/research/seed42-recon.md");
        await File.WriteAllTextAsync(outPath, recon.Markdown);
        _output.WriteLine($"recon written to {outPath} ({recon.Markdown.Length} chars, {healCount} heals)");
        if (state is not null)
            _output.WriteLine($"final state: room={state.CurrentRoomType} floor={state.ActFloor} hp={state.Hp}/{state.MaxHp} gameOver={state.IsGameOver}");
        if (error is not null)
            _output.WriteLine($"walk error: {error.GetType().Name}: {error.Message}");
        else
            _output.WriteLine("walk error: none");

        // Soft assertion — we want the file even on partial walks, but flag
        // if we never made it to the boss so the recon is incomplete.
        Assert.True(bossEntered, "recon never reached the boss room");
    }

    private static string ResolveRepoPath(string relative)
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "Sts2Headless.slnx"))) return Path.Combine(dir, relative);
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        throw new InvalidOperationException("Could not locate repo root from " + AppContext.BaseDirectory);
    }
}

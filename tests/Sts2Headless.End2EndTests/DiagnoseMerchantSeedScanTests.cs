using Sts2Headless.Agents;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// One-shot diagnostic: scan seeds 1..N with the GreedyAgent (heal-between-
// rooms) and report which ones surface a MerchantRoom inside the agent's
// natural path. Used to pick a seed for MerchantRoomTests that doesn't
// rely on a side-branch merchant the agent would never visit.
//
// Each seed walk caps at a few floors so the scan finishes quickly; we're
// looking for "merchant reachable early," not "merchant exists somewhere."
// Trait("category", "diagnostic") keeps this out of the default test run.
public class DiagnoseMerchantSeedScanTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    private readonly ITestOutputHelper _output;

    public DiagnoseMerchantSeedScanTests(HostSubprocess host, ITestOutputHelper output)
    {
        _host = host;
        _output = output;
    }

    [Fact]
    [Trait("category", "diagnostic")]
    public async Task ScanSeeds_FindMerchantOnGreedyPath()
    {
        const int firstSeed = 1;
        const int lastSeed = 25;
        const int floorBudget = 15;

        var results = new List<string>();
        for (ulong seed = firstSeed; seed <= lastSeed; seed++)
        {
            var outcome = await TryReachMerchant(seed, floorBudget);
            results.Add($"seed={seed}: {outcome}");
            _output.WriteLine(results[^1]);
        }

        _output.WriteLine("=== summary ===");
        foreach (var r in results.Where(r => r.Contains("MERCHANT")))
        {
            _output.WriteLine(r);
        }
    }

    private async Task<string> TryReachMerchant(ulong seed, int floorBudget)
    {
        try
        {
            await _host.SendAsync<RunNewResult>(
                "run/new", new RunNewParams(Character: Character.Ironclad, Seed: seed));
        }
        catch (Exception ex)
        {
            return $"run/new threw: {ex.Message}";
        }

        var transport = new HostSubprocessTransport(_host);
        var agent = new GreedyAgent();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        var state = await _host.SendAsync<RunStateResult>("run/state");
        var startFloor = state.ActFloor;
        var heals = 0;

        try
        {
            for (var step = 0; step < 500; step++)
            {
                if (state.CurrentRoomType == RoomType.MerchantRoom)
                {
                    return $"MERCHANT at floor {state.ActFloor} after {heals} heals";
                }
                if (state.IsGameOver) return $"game over at floor {state.ActFloor}";
                if (state.ActFloor - startFloor >= floorBudget) return $"budget exhausted at floor {state.ActFloor} (no merchant)";

                if (state.CurrentRoomType == RoomType.MapRoom && state.Hp < state.MaxHp)
                {
                    var heal = await _host.SendAsync<DebugSetHpResult>(
                        "debug/set_hp", new DebugSetHpParams(Hp: state.MaxHp));
                    if (!heal.Ok) return "set_hp returned ok=false";
                    heals++;
                    state = await _host.SendAsync<RunStateResult>("run/state");
                    continue;
                }

                state = (await AgentDriver.PlayRunAsync(
                    transport,
                    agent,
                    stopWhen: s => s.CurrentRoomType == RoomType.MerchantRoom
                                    || s.IsGameOver
                                    || (s.CurrentRoomType == RoomType.MapRoom && s.Hp < s.MaxHp)
                                    || (s.CurrentRoomType == RoomType.MapRoom && s.ActFloor - startFloor >= floorBudget),
                    ct: cts.Token)).FinalState;
            }
            return $"step cap hit at floor {state.ActFloor}";
        }
        catch (OperationCanceledException)
        {
            return $"timeout at floor {state.ActFloor} (likely combat stall)";
        }
        catch (Exception ex)
        {
            return $"threw {ex.GetType().Name} at floor {state.ActFloor}: {ex.Message}";
        }
    }
}

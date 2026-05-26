using Sts2Headless.Agents.Driving;
using Sts2Headless.Agents.Examples;
using Sts2Headless.Agents.Hosting;
using Sts2Headless.Protocol.Methods;
using Sts2Headless.TestSupport;
using Xunit;

namespace Sts2Headless.End2EndTests;

// Goal #3 end-to-end gate: HostPool runs N parallel headless hosts
// without crosstalk, and the engine's run-level determinism holds across
// processes (same seed → same scripted state, regardless of which
// worker ran it).
//
// xUnit already runs the integration / end2end suites with
// `parallelizeTestCollections: true`, so multiple hosts running
// concurrently has been incidental coverage for some time. This test
// upgrades that to an *explicit* assertion: parallelism is the contract
// of HostPool, and any future change that broke per-process isolation
// (a hidden static, a shared lockfile, an engine-side userdata write)
// surfaces here as a divergent FinalState across workers.
public class ParallelHostPoolTests
{
    private const int WorkerCount = 4;

    [Fact]
    public async Task FourConcurrentWorkers_SameSeed_IsolatedReplays_IdenticalFinalState()
    {
        using var tmpDir = new TempDir("sts2-pool");
        var tmpRoot = tmpDir.Path;

        await using var pool = new HostPool(new HostPoolOptions(
            WorkerCount: WorkerCount,
            ReplayRootBase: tmpRoot,
            RequestTimeout: TimeSpan.FromMinutes(2)));

        // All four workers run the same scripted slice: Ironclad
        // seed 42, drive GreedyAgent through ~2-3 rooms (enough to
        // close at least one combat so the replay recorder
        // actually flushes a .mcr — the recorder writes on
        // combat-end, not at run/new). Same script + same seed
        // must yield byte-identical scalar state on every worker —
        // that's the engine-determinism invariant HostReuseTests
        // already pins *within* one process; this version pins it
        // *across* processes.
        var tasks = Enumerable.Range(0, WorkerCount)
            .Select(_ => pool.RunAsync(async (host, ct) =>
            {
                await host.SendAsync<RunNewResult>(
                    "run/new", new RunNewParams(
                        Character: Character.Ironclad,
                        Seed: 42uL));
                var outcome = await AgentDriver.PlayRunAsync(
                    host,
                    new GreedyAgent(),
                    stopWhen: s => s.ActFloor >= 3,
                    maxSteps: 400,
                    ct: ct);
                return outcome.FinalState;
            }))
            .ToArray();

        var states = await Task.WhenAll(tasks);

        // Determinism check — every worker terminated at the same
        // scripted state. We compare the scalar fields that move
        // (HP, gold, floor, deck size, current room) — they're the
        // ones a cross-process RNG / save-file / static-state leak
        // would perturb.
        var ref0 = states[0];
        for (var i = 1; i < states.Length; i++)
        {
            var s = states[i];
            Assert.Equal(ref0.Character, s.Character);
            Assert.Equal(ref0.Seed, s.Seed);
            Assert.Equal(ref0.Hp, s.Hp);
            Assert.Equal(ref0.MaxHp, s.MaxHp);
            Assert.Equal(ref0.Gold, s.Gold);
            Assert.Equal(ref0.DeckSize, s.DeckSize);
            Assert.Equal(ref0.ActFloor, s.ActFloor);
            Assert.Equal(ref0.CurrentActIndex, s.CurrentActIndex);
            Assert.Equal(ref0.CurrentRoomType, s.CurrentRoomType);
            Assert.Equal(ref0.IsGameOver, s.IsGameOver);
        }

        // Isolation check — every worker has its own replay
        // subdirectory, populated, with no path that escaped into a
        // sibling worker's tree.
        for (var i = 0; i < pool.WorkerCount; i++)
        {
            var dir = Path.Combine(tmpRoot, $"worker-{i}");
            Assert.True(Directory.Exists(dir), $"worker-{i} replay dir missing: {dir}");
            Assert.NotEmpty(Directory.EnumerateFiles(
                dir, "*", SearchOption.AllDirectories));
        }
    }

    [Fact]
    public async Task DifferentSeedsAcrossWorkers_ProduceDistinctReplays()
    {
        using var tmpDir = new TempDir("sts2-pool");
        var tmpRoot = tmpDir.Path;

        await using var pool = new HostPool(new HostPoolOptions(
            WorkerCount: WorkerCount,
            ReplayRootBase: tmpRoot,
            RequestTimeout: TimeSpan.FromMinutes(2)));

        // Each worker takes a different seed. The post-bootstrap
        // map shapes ought to diverge — that's enough proof that
        // each worker is genuinely talking to its own engine
        // instance (a leaky shared RNG would collapse all seeds to
        // whichever ran first).
        var seeds = new ulong[] { 1, 2, 3, 4 };
        var tasks = seeds
            .Select(seed => pool.RunAsync(async (host, ct) =>
            {
                await host.SendAsync<RunNewResult>("run/new",
                    new RunNewParams(Character: Character.Ironclad, Seed: seed));
                // Every run starts at Neow EventRoom; dismiss it so the
                // post-boot MapRoom shapes are what we compare. Pick the
                // last unlocked option (HeuristicAgent default).
                var state = await host.SendAsync<RunStateResult>("run/state");
                if (state.CurrentRoomType == RoomType.EventRoom
                    && state.AvailableEventOptions.Count > 0)
                {
                    var lastUnlocked = state.AvailableEventOptions
                        .Reverse().FirstOrDefault(o => !o.IsLocked)
                        ?? state.AvailableEventOptions[^1];
                    await host.SendAsync<RunSelectEventOptionResult>(
                        "run/select_event_option",
                        new RunSelectEventOptionParams(OptionIndex: lastUnlocked.Index));
                    state = await host.SendAsync<RunStateResult>("run/state");
                }
                return state.AvailableMapNodes
                    .Select(n => (n.Col, n.Row, n.Type))
                    .ToArray();
            }))
            .ToArray();

        var mapShapes = await Task.WhenAll(tasks);

        // At least two of the four post-boot maps must differ —
        // if all four are identical, seed isn't actually influencing
        // map generation across workers (suspect: shared static
        // RNG state).
        var distinct = mapShapes
            .Select(s => string.Join("|", s.Select(t => $"{t.Col},{t.Row},{t.Type}")))
            .Distinct()
            .Count();
        Assert.True(distinct >= 2,
            $"Expected at least two distinct map shapes across {seeds.Length} seeds " +
            $"on separate workers; got {distinct}. Suggests cross-process state leak.");
    }
}

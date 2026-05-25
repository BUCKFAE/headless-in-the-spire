using Sts2Headless.Agents.Driving;
using Sts2Headless.BattleAgent;
using Sts2Headless.IntegrationTests;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.End2EndTests;

// Per-seed diagnostic runs that dump the full wire trace to
// /tmp/ironclad-a0/seed-<N>-walk.md regardless of outcome. Used to
// figure out why specific seeds die where they do — what cards landed
// in the deck, what fights the agent struggled with, what HP it was
// at when entering bosses.
//
// Marked diagnostic so they don't run on `just validation::test`.
public class IroncladAgentSeedTraceTests
{
    [Theory]
    [Trait("Category", "Diagnostic")]
    [InlineData(1uL)]
    [InlineData(3uL)]
    [InlineData(7uL)]
    [InlineData(8uL)]
    [InlineData(10uL)]
    public async Task DumpFullTrace(ulong seed)
    {
        Directory.CreateDirectory("/tmp/ironclad-a0");
        await using var host = new HostSubprocess();
        await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: seed));

        var initialState = await host.SendAsync<RunStateResult>("run/state");
        var startAct = initialState.CurrentActIndex;

        var transport = new ReconTransport(new HostSubprocessTransport(host));
        var agent = new IroncladAgent();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        RunOutcome? outcome = null;
        Exception? error = null;
        try
        {
            outcome = await AgentDriver.PlayRunAsync(
                transport,
                agent,
                stopWhen: s => s.CurrentActIndex > startAct || s.ActFloor >= 22,
                ct: cts.Token);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        var header = $"# seed {seed}\n"
            + $"outcome={(outcome is null ? "EXCEPTION" : outcome.TerminatedBy.ToString())}\n"
            + (outcome is not null
                ? $"final: room={outcome.FinalState.CurrentRoomType} floor={outcome.FinalState.ActFloor} "
                  + $"act={outcome.FinalState.CurrentActIndex} hp={outcome.FinalState.Hp}/{outcome.FinalState.MaxHp} "
                  + $"gameOver={outcome.FinalState.IsGameOver}\n"
                : $"exception: {error?.GetType().FullName}: {error?.Message}\n")
            + "\n";
        await File.WriteAllTextAsync(
            $"/tmp/ironclad-a0/seed-{seed}-walk.md",
            header + transport.Markdown);

        // Smoke contract — assert nothing more than "didn't hit step limit".
        if (outcome is not null)
            Assert.NotEqual(TerminationReason.StepLimit, outcome.TerminatedBy);
    }
}

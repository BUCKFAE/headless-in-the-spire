using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Error-shape tests pinning that every run-scoped wire method rejects calls
// when no run is active. None of them mutate host state on success (they
// expect failure), and none of them issue a run/new — so they can all share
// a single never-bootstrapped session via IClassFixture, shaving one
// subprocess each off the suite.
//
// If a future test in this class issues run/new, the contract breaks: the
// shared host would carry the run forward into the next test and the
// "no active run" assertion would flake. Add a new fixture or move the
// test elsewhere instead.
public class NoActiveRunErrorTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public NoActiveRunErrorTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task RunState_WithoutRunNew_ReturnsInternalError()
    {
        var error = await _host.ExpectErrorAsync("run/state");

        Assert.Equal(-32603, error.Code);
        Assert.Contains("no active run", error.Message);
    }

    [Fact]
    public async Task SelectMapNode_WithoutRunNew_ReturnsInternalError()
    {
        var error = await _host.ExpectErrorAsync(
            "run/select_map_node", new RunSelectMapNodeParams(Col: 3, Row: 0));

        Assert.Equal(-32603, error.Code);
        Assert.Contains("no active run", error.Message);
    }

    [Fact]
    public async Task SelectEventOption_WithoutRunNew_ReturnsInternalError()
    {
        var error = await _host.ExpectErrorAsync(
            "run/select_event_option",
            new RunSelectEventOptionParams(OptionIndex: 0));

        Assert.Equal(-32603, error.Code);
        Assert.Contains("no active run", error.Message);
    }

    [Fact]
    public async Task EndTurn_WithoutRunNew_ReturnsInternalError()
    {
        var error = await _host.ExpectErrorAsync("run/end_turn");

        Assert.Equal(-32603, error.Code);
        Assert.Contains("no active run", error.Message);
    }

    [Fact]
    public async Task PlayCard_WithoutRunNew_ReturnsInternalError()
    {
        var error = await _host.ExpectErrorAsync(
            "run/play_card", new RunPlayCardParams(CardIndex: 0));

        Assert.Equal(-32603, error.Code);
        Assert.Contains("no active run", error.Message);
    }
}

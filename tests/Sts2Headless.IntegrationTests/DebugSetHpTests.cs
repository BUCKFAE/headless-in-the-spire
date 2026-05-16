using Sts2Headless.Protocol;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the debug/set_hp wire surface. Lives in its own class so it
// can share one HostSubprocess across the four cases without each one
// paying the boot cost again. The fixture passes --enable-debug
// (HostSubprocess.cs); the negative-case "rejected when disabled" lives
// in DebugDisabledTests which spins up its own subprocess without the
// flag, since IClassFixture instances aren't parametrically configurable.
public class DebugSetHpTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;

    public DebugSetHpTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task SetHpAndMaxHp_UpdatesPlayerSnapshot()
    {
        var start = await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        Assert.True(start.Ok);

        var beforeState = await _host.SendAsync<RunStateResult>("run/state");
        var targetHp = beforeState.MaxHp + 20;
        var targetMaxHp = beforeState.MaxHp + 20;

        var resp = await _host.SendAsync<DebugSetHpResult>(
            "debug/set_hp", new DebugSetHpParams(Hp: targetHp, MaxHp: targetMaxHp));
        Assert.True(resp.Ok);
        Assert.Equal(targetHp, resp.Hp);
        Assert.Equal(targetMaxHp, resp.MaxHp);
        Assert.False(resp.IsGameOver);

        var post = await _host.SendAsync<RunStateResult>("run/state");
        Assert.Equal(targetHp, post.Hp);
        Assert.Equal(targetMaxHp, post.MaxHp);
    }

    [Fact]
    public async Task SetHpOnly_LeavesMaxHpUnchanged()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        var before = await _host.SendAsync<RunStateResult>("run/state");

        // Set HP to 1 (anything < current). MaxHp must stay where it was —
        // omitting maxHp in the params is the canonical "heal/wound the
        // player without touching their pool" path.
        var resp = await _host.SendAsync<DebugSetHpResult>(
            "debug/set_hp", new DebugSetHpParams(Hp: 1));
        Assert.Equal(1, resp.Hp);
        Assert.Equal(before.MaxHp, resp.MaxHp);
    }

    [Fact]
    public async Task NegativeHp_ReturnsInvalidParamsError()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        var err = await _host.ExpectErrorAsync(
            "debug/set_hp", new DebugSetHpParams(Hp: -1));
        Assert.Equal(WireErrorCode.InvalidParams, err.Code);
        Assert.Contains("hp must be >= 0", err.Message);
    }

    [Fact]
    public async Task HpAboveMaxHp_ReturnsInvalidParamsError()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        var state = await _host.SendAsync<RunStateResult>("run/state");

        var err = await _host.ExpectErrorAsync(
            "debug/set_hp", new DebugSetHpParams(Hp: state.MaxHp + 1));
        Assert.Equal(WireErrorCode.InvalidParams, err.Code);
        Assert.Contains("must be <=", err.Message);
    }
}

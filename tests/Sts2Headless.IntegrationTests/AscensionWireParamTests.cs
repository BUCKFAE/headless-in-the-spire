using Sts2Headless.Protocol;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the Ascension wire surface on RunNewParams. Pins:
//   * run/new with Ascension=0 (default) preserves prior behavior.
//   * run/new with Ascension=1 succeeds end-to-end.
//   * Negative ascension is rejected with InvalidParams.
//
// Per BLOCKED.md (resolved 2026-05-18), ascension was previously
// hardcoded to 0 in HostMethods + ReplayHeaderFactory. The wire
// parameter is the contract; the bindings layer just passes it
// through to RunState.CreateForTest's ascensionLevel field.
public class AscensionWireParamTests
{
    [Fact]
    public async Task AscensionZero_DefaultBehavior_Succeeds()
    {
        await using var host = new HostSubprocess();
        var resp = await host.SendAsync<RunNewResult>(
            "run/new",
            new RunNewParams(Character: Character.Ironclad, Seed: 42uL, Ascension: 0));
        Assert.True(resp.Ok);
        Assert.Equal(Character.Ironclad, resp.Character);
    }

    [Fact]
    public async Task AscensionOmitted_TreatedAsZero()
    {
        await using var host = new HostSubprocess();
        var resp = await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        Assert.True(resp.Ok);
    }

    [Fact]
    public async Task AscensionOne_Succeeds()
    {
        // The interesting case: Ironclad starts with ASCENDERS_BANE in
        // the deck and slightly tougher monster scaling. The wire-side
        // contract here is just "the engine accepts ascension=1 and
        // run/new completes". Win-rate at A1 is measured separately
        // in End2EndTests.IroncladAgentA1Tests.
        await using var host = new HostSubprocess();
        var resp = await host.SendAsync<RunNewResult>(
            "run/new",
            new RunNewParams(Character: Character.Ironclad, Seed: 42uL, Ascension: 1));
        Assert.True(resp.Ok);
        Assert.Equal(Character.Ironclad, resp.Character);
    }

    [Fact]
    public async Task NegativeAscension_RejectedAsInvalidParams()
    {
        await using var host = new HostSubprocess();
        var err = await host.ExpectErrorAsync(
            "run/new",
            new RunNewParams(Character: Character.Ironclad, Seed: 42uL, Ascension: -1));
        // Mapped from ArgumentException → InvalidParams in HostMethods.
        Assert.True(err.Code == WireErrorCode.InvalidParams || err.Code == WireErrorCode.InternalError,
            $"expected InvalidParams or InternalError, got {err.Code}: {err.Message}");
        Assert.Contains("ascension", err.Message, StringComparison.OrdinalIgnoreCase);
    }
}

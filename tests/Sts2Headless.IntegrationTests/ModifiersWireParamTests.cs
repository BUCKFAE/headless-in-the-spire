using Sts2Headless.Protocol;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the Modifiers wire surface on RunNewParams. Pins:
//   * run/new with Modifiers=null/omitted defaults to an empty list
//     in the echoed RunNewResult.Modifiers.
//   * run/new with a known modifier round-trips: the echoed list
//     matches what was sent.
//   * run/new with ModifierId.Unknown is rejected as InvalidParams.
//
// Per BLOCKED.md (resolved 2026-05-18), modifiers were previously
// hardcoded to `Array.Empty<string>()` in the replay header. Today
// the wire validates + echoes + records in the replay header; engine
// plumb-through that would actually alter starting state (DRAFT decks,
// SEALED_DECK constraints, etc.) is a follow-up.
public class ModifiersWireParamTests
{
    [Fact]
    public async Task ModifiersOmitted_EchoIsEmptyList()
    {
        await using var host = new HostSubprocess();
        var resp = await host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        Assert.True(resp.Ok);
        Assert.NotNull(resp.Modifiers);
        Assert.Empty(resp.Modifiers);
    }

    [Fact]
    public async Task SingleKnownModifier_RoundTripsThroughEcho()
    {
        await using var host = new HostSubprocess();
        var resp = await host.SendAsync<RunNewResult>(
            "run/new",
            new RunNewParams(
                Character: Character.Ironclad,
                Seed: 42uL,
                Modifiers: new[] { ModifierId.Hoarder }));
        Assert.True(resp.Ok);
        Assert.Equal(new[] { ModifierId.Hoarder }, resp.Modifiers);
    }

    [Fact]
    public async Task MultipleKnownModifiers_RoundTripInOrder()
    {
        await using var host = new HostSubprocess();
        var requested = new[] { ModifierId.Draft, ModifierId.Hoarder, ModifierId.Insanity };
        var resp = await host.SendAsync<RunNewResult>(
            "run/new",
            new RunNewParams(
                Character: Character.Ironclad,
                Seed: 42uL,
                Modifiers: requested));
        Assert.True(resp.Ok);
        Assert.Equal(requested, resp.Modifiers);
    }

    [Fact]
    public async Task UnknownModifier_RejectedAsInvalidParams()
    {
        await using var host = new HostSubprocess();
        var err = await host.ExpectErrorAsync(
            "run/new",
            new RunNewParams(
                Character: Character.Ironclad,
                Seed: 42uL,
                Modifiers: new[] { ModifierId.Hoarder, ModifierId.Unknown }));
        Assert.Equal(WireErrorCode.InvalidParams, err.Code);
        Assert.Contains("modifiers[1]", err.Message);
        Assert.Contains("Unknown", err.Message);
    }
}

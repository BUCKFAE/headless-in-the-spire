using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Coverage for the debug/peek_* simulation surface. Both methods are
// currently in a "pool fallback" / "stubbed" posture (see CheatDtos
// comments) — full simulation requires SerializableRunState clone/restore
// that isn't wired yet. These tests pin the *current* contract so a future
// upgrade to full-simulation can land alongside an obvious test diff.
public class DebugPeekTests : IClassFixture<HostSubprocess>
{
    private readonly HostSubprocess _host;
    public DebugPeekTests(HostSubprocess host) => _host = host;

    [Fact]
    public async Task PeekCardReward_DuringCombat_ReturnsNonEmptyPool()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));
        await _host.SendAsync<DebugStartCombatResult>(
            "debug/start_combat", new DebugStartCombatParams(EncounterId: "SLIMES_NORMAL"));

        var result = await _host.SendAsync<DebugPeekCardRewardResult>(
            "debug/peek_card_reward", new DebugPeekCardRewardParams());
        Assert.True(result.Cards.Count > 0,
            $"expected non-empty card pool; ok={result.Ok}, notes={result.Notes}");
        Assert.All(result.Cards, c => Assert.False(string.IsNullOrWhiteSpace(c.Id)));
    }

    [Fact]
    public async Task PeekEventOutcome_StubbedShape_EchoesEventIdWithNotes()
    {
        await _host.SendAsync<RunNewResult>(
            "run/new", new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

        // Stubbed at the current pin (see CheatDtos comment): ok=false,
        // zero deltas, empty diff lists, and a `notes` line explaining
        // the limitation. The eventId echoes back so callers can keep
        // logs tied to the input.
        const string EventId = "AROMA_OF_CHAOS";
        var result = await _host.SendAsync<DebugPeekEventOutcomeResult>(
            "debug/peek_event_outcome",
            new DebugPeekEventOutcomeParams(EventId: EventId, OptionIndex: 0));
        Assert.False(result.Ok);
        Assert.Equal(EventId, result.EventId);
        Assert.False(string.IsNullOrWhiteSpace(result.Notes));
    }
}

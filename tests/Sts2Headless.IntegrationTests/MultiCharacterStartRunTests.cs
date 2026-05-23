using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Per-character smoke for the run/new + run/state path.
//
// The bindings layer (Sts2Bindings.Bind) iterates Enum.GetValues<Character>()
// at host startup and resolves each character's class in sts2.dll —
// missing types fail the build of the dictionary, so this test reaching
// run/new for every Character is the runtime confirmation that every
// enum value has a registered factory and the engine doesn't blow up
// when CreateForNewRun<T> closes over the named character.
//
// What we deliberately don't assert per character:
//   * Specific starting HP, starting gold, or starting deck contents —
//     each character has its own balance numbers, and this isn't a
//     parity test against the live game. We assert "positive" / "non-
//     empty" so a fresh character with totally different numbers still
//     passes.
//   * Combat behaviour. A character could start a run cleanly and then
//     crash on first card play due to unguarded NRE sites; that's the
//     same Doormaker-shape audit story we already handle for monsters.
//     Covered by per-character end-to-end agent tests, not here.
//
// One subprocess per test (no IClassFixture) — run/new on a busy host
// reuses session state in ways that wouldn't matter for this smoke but
// makes failure attribution easier when a single character breaks.
public class MultiCharacterStartRunTests
{
    public static TheoryData<Character> AllCharacters
    {
        get
        {
            var data = new TheoryData<Character>();
            foreach (var character in Enum.GetValues<Character>())
                data.Add(character);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AllCharacters))]
    public async Task RunNew_For_Each_Character_LandsAtMapRoom(Character character)
    {
        await using var host = new HostSubprocess();
        var resp = await host.SendAsync<RunNewResult>(
            "run/new",
            new RunNewParams(Character: character, Seed: 42uL));

        Assert.True(resp.Ok, $"run/new failed for {character}");
        Assert.Equal(character, resp.Character);
        Assert.Equal(RoomType.MapRoom, resp.CurrentRoomType);
        // Every character begins act 0 with at least one reachable Monster
        // node above the start. Don't pin the count — Necrobinder/Regent
        // may shape the early map differently than Ironclad.
        Assert.NotEmpty(resp.AvailableMapNodes);
    }

    [Theory]
    [MemberData(nameof(AllCharacters))]
    public async Task RunState_AfterRunNew_HasLivePlayer(Character character)
    {
        await using var host = new HostSubprocess();
        var newResp = await host.SendAsync<RunNewResult>(
            "run/new",
            new RunNewParams(Character: character, Seed: 42uL));
        Assert.True(newResp.Ok);

        var state = await host.SendAsync<RunStateResult>("run/state");

        Assert.True(state.Ok, $"run/state failed for {character}");
        Assert.Equal(character, state.Character);
        Assert.Equal(42uL, state.Seed);
        // Live player: positive HP, non-empty starting deck. Specific
        // starting numbers vary per character; positivity is the
        // engine-side proof that CreateForNewRun ran to completion
        // rather than half-initialising the Player.
        Assert.True(state.Hp > 0, $"{character} starts at hp={state.Hp}");
        Assert.True(state.MaxHp > 0, $"{character} starts at maxHp={state.MaxHp}");
        Assert.True(state.DeckSize > 0, $"{character} starts with deckSize={state.DeckSize}");
        Assert.False(state.IsGameOver, $"{character} fresh run reports IsGameOver=true");
        Assert.False(state.IsDead, $"{character} fresh run reports IsDead=true");
    }
}

using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.IntegrationTests;

// Pin the new triggeredSincePrev wire surface: after picking a card
// reward while LuckyFysh is owned, the next run/state must include
// a trigger event attributing AfterCardChangedPiles to LUCKY_FYSH.
//
// The existing RelicListenerTests asserts the *side effect* (+15 gold)
// reaches the wire, which proves sts2's listener pipeline fires. This
// test pins the *attribution* — that RelicHookPatches captured the
// firing and surfaced it as a structured event keyed by the relic's
// canonical wire id, not just a gold delta we'd have to back out.
public class RelicTriggerWireTests
{
    [Fact]
    public async Task SelectCardReward_SurfacesLuckyFyshTrigger_OnTriggeredSincePrev()
    {
        await using var host = new HostSubprocess();
        await host.SendAsync<RunNewResult>("run/new", new RunNewParams(Seed: 42uL));

        // Inject LuckyFysh before the first combat — same pattern as
        // RelicListenerTests. RelicCmd.Obtain itself may fire one or more
        // listener hooks (Player.AfterRelicObtained, etc.), so we drain
        // run/state once *after* the inject to clear that residue before
        // the assertion.
        var afterInject = await host.SendAsync<DebugGiveRelicResult>(
            "debug/give_relic", new DebugGiveRelicParams(RelicId: "LUCKY_FYSH"));
        Assert.True(afterInject.Ok);
        // Drain whatever fired during the inject.
        _ = await host.SendAsync<RunStateResult>("run/state");

        var rewards = await CombatHelpers.DriveCombatToRewards(host);
        var cardReward = rewards.Available.FirstOrDefault(r => r.Kind == RewardKind.Card);
        Assert.NotNull(cardReward);
        Assert.NotEmpty(cardReward!.Cards!);

        // Drain run/state once *before* the claim so we're measuring only
        // the trigger events that fall between the claim and the
        // subsequent read. The reward menu itself can emit listener events
        // (e.g. AfterModifyingCardRewardOptions), which we don't care about
        // for this assertion.
        _ = await host.SendAsync<RunStateResult>("run/state");

        var afterClaim = await host.SendAsync<RunSelectRewardResult>(
            "run/select_reward", new RunSelectRewardParams(RewardIndex: cardReward.Index, CardIndex: 0));
        Assert.True(afterClaim.Ok);

        // The claim's RunSelectRewardResult itself doesn't carry the new
        // wire field (existing reward DTOs are out of this slice's scope
        // — only RunStateResult drains the buffer). Read run/state to get
        // the post-claim trigger window.
        var state = await host.SendAsync<RunStateResult>("run/state");

        Assert.NotNull(state.TriggeredSincePrev);
        Assert.Contains(state.TriggeredSincePrev, e =>
            e.Kind == TriggerKind.Relic
            && e.Source == "LUCKY_FYSH"
            && e.Hook == "AfterCardChangedPiles");
        Assert.Equal(0, state.TriggeredDropped);
    }
}

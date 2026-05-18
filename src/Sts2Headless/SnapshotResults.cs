using Sts2Headless.Protocol.Methods;
using Sts2Headless.Runtime;

namespace Sts2Headless;

// Projections from RunSnapshot to the wire Run*Result records. Each method
// captures the "16 standard snapshot fields" projection in one place so a
// new RunSnapshot field reaches every result type via the compiler, not via
// 13 separate handler edits.
//
// RunNewResult and RunStateResult deliberately stay inline in HostMethods —
// they project a different shape (no Hp/Ok-with-snapshot subset for run/new;
// MaxHp/Gold/DeckSize/triggered-log additions for run/state).
internal static class SnapshotResults
{
    public static RunSelectMapNodeResult ToRunSelectMapNodeResult(this RunSnapshot s, int col, int row) =>
        new(Ok: true, Col: col, Row: row,
            CurrentRoomType: s.CurrentRoomType, ActFloor: s.ActFloor, CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver, IsVictory: s.IsVictory, IsDead: s.IsDead, Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes, AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions, AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState, RewardsState: s.RewardsState,
            Relics: s.Relics, OwnedPotions: s.OwnedPotions);

    public static RunSelectEventOptionResult ToRunSelectEventOptionResult(this RunSnapshot s, int optionIndex) =>
        new(Ok: true, OptionIndex: optionIndex,
            CurrentRoomType: s.CurrentRoomType, ActFloor: s.ActFloor, CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver, IsVictory: s.IsVictory, IsDead: s.IsDead, Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes, AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions, AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState, RewardsState: s.RewardsState,
            Relics: s.Relics, OwnedPotions: s.OwnedPotions);

    public static RunSelectRestSiteOptionResult ToRunSelectRestSiteOptionResult(this RunSnapshot s, int optionIndex) =>
        new(Ok: true, OptionIndex: optionIndex,
            CurrentRoomType: s.CurrentRoomType, ActFloor: s.ActFloor, CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver, IsVictory: s.IsVictory, IsDead: s.IsDead, Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes, AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions, AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState, RewardsState: s.RewardsState,
            Relics: s.Relics, OwnedPotions: s.OwnedPotions);

    public static RunLeaveTreasureRoomResult ToRunLeaveTreasureRoomResult(this RunSnapshot s) =>
        new(Ok: true,
            CurrentRoomType: s.CurrentRoomType, ActFloor: s.ActFloor, CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver, IsVictory: s.IsVictory, IsDead: s.IsDead, Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes, AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions, AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState, RewardsState: s.RewardsState,
            Relics: s.Relics, OwnedPotions: s.OwnedPotions);

    public static RunBuyMerchantItemResult ToRunBuyMerchantItemResult(this RunSnapshot s, int itemIndex) =>
        new(Ok: true, ItemIndex: itemIndex,
            CurrentRoomType: s.CurrentRoomType, ActFloor: s.ActFloor, CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver, IsVictory: s.IsVictory, IsDead: s.IsDead, Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes, AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions, AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState, RewardsState: s.RewardsState,
            Relics: s.Relics, OwnedPotions: s.OwnedPotions);

    public static RunLeaveMerchantRoomResult ToRunLeaveMerchantRoomResult(this RunSnapshot s) =>
        new(Ok: true,
            CurrentRoomType: s.CurrentRoomType, ActFloor: s.ActFloor, CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver, IsVictory: s.IsVictory, IsDead: s.IsDead, Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes, AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions, AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState, RewardsState: s.RewardsState,
            Relics: s.Relics, OwnedPotions: s.OwnedPotions);

    public static RunEndTurnResult ToRunEndTurnResult(this RunSnapshot s) =>
        new(Ok: true,
            CurrentRoomType: s.CurrentRoomType, ActFloor: s.ActFloor, CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver, IsVictory: s.IsVictory, IsDead: s.IsDead, Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes, AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions, AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState, RewardsState: s.RewardsState,
            Relics: s.Relics, OwnedPotions: s.OwnedPotions);

    public static RunPlayCardResult ToRunPlayCardResult(this RunSnapshot s, int cardIndex, int? targetIndex) =>
        new(Ok: true, CardIndex: cardIndex, TargetIndex: targetIndex,
            CurrentRoomType: s.CurrentRoomType, ActFloor: s.ActFloor, CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver, IsVictory: s.IsVictory, IsDead: s.IsDead, Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes, AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions, AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState, RewardsState: s.RewardsState,
            Relics: s.Relics, OwnedPotions: s.OwnedPotions);

    public static RunUsePotionResult ToRunUsePotionResult(this RunSnapshot s, int potionIndex, int? targetIndex) =>
        new(Ok: true, PotionIndex: potionIndex, TargetIndex: targetIndex,
            CurrentRoomType: s.CurrentRoomType, ActFloor: s.ActFloor, CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver, IsVictory: s.IsVictory, IsDead: s.IsDead, Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes, AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions, AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState, RewardsState: s.RewardsState,
            Relics: s.Relics, OwnedPotions: s.OwnedPotions);

    public static RunSelectRewardResult ToRunSelectRewardResult(this RunSnapshot s, int rewardIndex, int? cardIndex) =>
        new(Ok: true, RewardIndex: rewardIndex, CardIndex: cardIndex,
            CurrentRoomType: s.CurrentRoomType, ActFloor: s.ActFloor, CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver, IsVictory: s.IsVictory, IsDead: s.IsDead, Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes, AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions, AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState, RewardsState: s.RewardsState,
            Relics: s.Relics, OwnedPotions: s.OwnedPotions);

    public static RunSkipRewardResult ToRunSkipRewardResult(this RunSnapshot s, int rewardIndex) =>
        new(Ok: true, RewardIndex: rewardIndex,
            CurrentRoomType: s.CurrentRoomType, ActFloor: s.ActFloor, CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver, IsVictory: s.IsVictory, IsDead: s.IsDead, Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes, AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions, AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState, RewardsState: s.RewardsState,
            Relics: s.Relics, OwnedPotions: s.OwnedPotions);

    public static RunEnterNextActResult ToRunEnterNextActResult(this RunSnapshot s) =>
        new(Ok: true,
            CurrentRoomType: s.CurrentRoomType, ActFloor: s.ActFloor, CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver, IsVictory: s.IsVictory, IsDead: s.IsDead, Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes, AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions, AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState, RewardsState: s.RewardsState,
            Relics: s.Relics, OwnedPotions: s.OwnedPotions);

    public static RunProceedEventResult ToRunProceedEventResult(this RunSnapshot s) =>
        new(Ok: true,
            CurrentRoomType: s.CurrentRoomType, ActFloor: s.ActFloor, CurrentActIndex: s.CurrentActIndex,
            IsGameOver: s.IsGameOver, IsVictory: s.IsVictory, IsDead: s.IsDead, Hp: s.CurrentHp,
            AvailableMapNodes: s.AvailableMapNodes, AvailableEventOptions: s.AvailableEventOptions,
            AvailableRestSiteOptions: s.AvailableRestSiteOptions, AvailableMerchantItems: s.AvailableMerchantItems,
            CombatState: s.CombatState, RewardsState: s.RewardsState,
            Relics: s.Relics, OwnedPotions: s.OwnedPotions);
}

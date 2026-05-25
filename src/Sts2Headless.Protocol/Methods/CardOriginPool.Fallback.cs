namespace Sts2Headless.Protocol.Methods;

// Compile-only stub. The real enum + map is emitted by
// `just build::generate-content-ids` into the gitignored CardOriginPool.g.cs.
// See CardId.Fallback.cs for the full rationale — same pattern.
public enum CardOriginPool
{
    Unknown,
    Ironclad, Silent, Defect, Regent, Necrobinder,
    Colorless, Curse, Deprecated, Event, Quest, Status, Token,
}

public static class CardOriginPools
{
    public static CardOriginPool OfCard(string cardId) => CardOriginPool.Unknown;
    public static Character? OwningCharacter(string cardId) => null;
}

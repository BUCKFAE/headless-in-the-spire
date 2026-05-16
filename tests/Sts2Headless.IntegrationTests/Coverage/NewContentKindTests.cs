using Xunit;

namespace Sts2Headless.IntegrationTests.Coverage;

// Meta-coverage tripwire: when sts2 ships a new top-level content category
// (a new namespace under MegaCrit.Sts2.Core.Models that we don't have a
// generator KindSpec for), this test fails and tells us which category and
// which classes to look at. This is the answer to "how do we make sure we
// notice when the game grows a new kind of thing — orbs, enchantments,
// some-future-mechanic — that none of our coverage tooling tracks yet?"
//
// Two kinds of namespaces appear under MegaCrit.Sts2.Core.Models:
//   * Content kinds we generate a manifest for (in GenerateContentIdsCommand.Kinds).
//     These should be in sync with this test's KnownGeneratedKinds map.
//   * Structural kinds we deliberately don't generate manifests for —
//     character classes, card/relic/potion pools, achievements, acts, the
//     Singleton catch-all. These are listed in DeliberatelyUnmanifested with
//     a one-line justification each so the omission is visible, not silent.
//
// Anything outside both sets ⇒ test fails with a pointer to the unknown
// namespace, the action it needs from us (add a KindSpec or an explicit
// opt-out), and a few example class names from inside.
[Collection(InProcessSts2Collection.Name)]
public class NewContentKindTests : IClassFixture<ContentManifestFixture>
{
    private readonly ContentManifestFixture _f;
    public NewContentKindTests(ContentManifestFixture f) { _f = f; }

    // Namespaces under MegaCrit.Sts2.Core.Models that we explicitly do NOT
    // generate per-kind content manifests for. Each entry has a reason —
    // a future contributor reading this list should understand why the
    // namespace exists outside the manifest set, and reconsider when the
    // reason no longer holds.
    private static readonly IReadOnlyDictionary<string, string> DeliberatelyUnmanifested = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Static gameplay axes — five characters, five acts. Small enough
        // that hand-modelling is cheaper than codegen; already enumerated
        // explicitly in protocol DTOs (Character enum in Methods.cs).
        ["MegaCrit.Sts2.Core.Models.Acts"]        = "Acts are a fixed small set covered by gameplay-flow enums, not a content sweep.",
        ["MegaCrit.Sts2.Core.Models.Characters"]  = "Characters are enumerated explicitly in Methods.cs and small enough to model by hand.",

        // Pools are containers of cards/relics/potions, not playable
        // content. Coverage of the contained items already happens via
        // Card/Relic/Potion manifests.
        ["MegaCrit.Sts2.Core.Models.CardPools"]    = "Card pools are containers of cards; coverage rolls up through Card manifest.",
        ["MegaCrit.Sts2.Core.Models.RelicPools"]   = "Relic pools are containers of relics; coverage rolls up through Relic manifest.",
        ["MegaCrit.Sts2.Core.Models.PotionPools"]  = "Potion pools are containers of potions; coverage rolls up through Potion manifest.",

        // Achievements aren't gameplay state — they're optional metadata
        // a coverage sweep doesn't need to drive. Reconsider if/when an
        // achievement gates content that an agent must trigger.
        ["MegaCrit.Sts2.Core.Models.Achievements"] = "Achievements are metadata, not gameplay state — out of scope for content coverage.",

        // Engine internal — a Singleton stub used by the model framework,
        // not a content category. Has exactly one entry.
        ["MegaCrit.Sts2.Core.Models.Singleton"]    = "Engine internal — framework Singleton type, not gameplay content.",
    };

    [Fact]
    public void TopLevelContentNamespaces_AreEitherManifestedOrExplicitlyOptedOut()
    {
        const string root = "MegaCrit.Sts2.Core.Models";

        // Top-level namespace = the single segment immediately under
        // MegaCrit.Sts2.Core.Models (so MegaCrit.Sts2.Core.Models.Cards
        // counts; MegaCrit.Sts2.Core.Models.Afflictions.Mocks does not —
        // its top-level is still Afflictions). Group concrete subtypes by
        // that namespace to get the kind set.
        var byTopLevel = _f.AllSubtypes
            .Where(t => !t.IsAbstract && t.Namespace is not null && t.Namespace.StartsWith(root + ".", StringComparison.Ordinal))
            .GroupBy(t => t.Namespace!.Substring(0, root.Length + 1 + (t.Namespace!.IndexOf('.', root.Length + 1) is var dot && dot >= 0 ? dot - root.Length - 1 : t.Namespace.Length - root.Length - 1)))
            .ToDictionary(g => g.Key, g => g.Select(t => t.FullName!).Take(5).ToList(), StringComparer.Ordinal);

        var manifestedNamespaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spec in GenerateContentIdsCommand.Kinds)
        {
            switch (spec.Source)
            {
                case GenerateContentIdsCommand.NamespaceFilter nf:
                    manifestedNamespaces.Add(nf.Namespace);
                    break;
                case GenerateContentIdsCommand.NativeProperty _:
                case GenerateContentIdsCommand.MergedNative _:
                    // Native-property kinds map to a known namespace under
                    // root.<Kind>s (Cards / Relics / Potions / Encounters /
                    // Events / Powers). Encode the convention here rather
                    // than threading another field through the KindSpec.
                    manifestedNamespaces.Add($"{root}.{spec.Kind}s");
                    break;
            }
        }
        // The Event kind merges three ModelDb properties (AllEvents, AllSharedEvents,
        // AllAncients). AllAncients lives in the Events namespace (sts2 puts
        // ancient/Neow encounters under Models.Events), so the single
        // "Events" entry above is correct — no extra namespaces to register.

        var unknown = new List<string>();
        foreach (var ns in byTopLevel.Keys)
        {
            if (manifestedNamespaces.Contains(ns)) continue;
            if (DeliberatelyUnmanifested.ContainsKey(ns)) continue;
            unknown.Add(ns);
        }

        if (unknown.Count == 0) return;

        var lines = new List<string>
        {
            "NewContentKindTests: sts2 exposes one or more top-level content namespaces under",
            $"  {root} that neither map to a GenerateContentIdsCommand.KindSpec nor appear in",
            "  DeliberatelyUnmanifested. Add the kind to the generator's Kinds list (with a",
            "  NamespaceFilter) and a Fallback stub under src/Sts2Headless.Protocol/, OR add",
            "  the namespace to DeliberatelyUnmanifested with a one-line justification.",
            "",
            "  Unknown namespaces:"
        };
        foreach (var ns in unknown)
        {
            var samples = byTopLevel[ns];
            lines.Add($"    - {ns}  (examples: {string.Join(", ", samples)})");
        }
        Assert.Fail(string.Join('\n', lines));
    }
}

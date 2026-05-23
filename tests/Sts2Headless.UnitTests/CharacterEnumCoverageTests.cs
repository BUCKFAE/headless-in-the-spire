using Sts2Headless.Protocol.Methods;
using Xunit;

namespace Sts2Headless.UnitTests;

// Failure mode this test exists to catch:
//
// Someone adds a new value to the Character enum (e.g. Character.Watcher
// returning, or a brand-new STS2 character drops in a patch) and forgets
// to wire it through CharacterExtensions.Sts2TypeName + Sts2Bindings.Bind.
//
// The Protocol project promotes CS8509 (switch expression doesn't handle
// all inputs) to an error, so the first line of defense is the compiler.
// This test is the second — it walks every Character enum value at
// runtime and asserts Sts2TypeName produces a non-empty string. If the
// switch expression ever grew a default arm (or someone disabled CS8509
// at the project level), this would catch the regression.
//
// Lives in the unit tests (no sts2.dll) so the check is fast and runs
// even on machines without the vendor DLL. The matching runtime check —
// "the named character type actually exists in sts2.dll" — lives in
// Sts2Bindings.Bind and is exercised by the integration tests.
public class CharacterEnumCoverageTests
{
    [Fact]
    public void Every_Character_Has_A_NonEmpty_Sts2TypeName()
    {
        foreach (var character in Enum.GetValues<Character>())
        {
            var typeName = character.Sts2TypeName();
            Assert.False(
                string.IsNullOrWhiteSpace(typeName),
                $"Character.{character} has an empty Sts2TypeName — add a case to CharacterExtensions.Sts2TypeName.");
        }
    }

    // STS2 character class names are PascalCase identifiers under
    // MegaCrit.Sts2.Core.Models.Characters. A stray space, hyphen, or
    // namespace-qualified string here would silently bypass the
    // bindings layer's Sts2.GetType lookup and produce a misleading
    // "not found" error at host startup. Cheap to assert; cheaper to
    // diagnose now than at bootstrap.
    [Fact]
    public void Sts2TypeName_Looks_Like_A_CSharp_Identifier()
    {
        foreach (var character in Enum.GetValues<Character>())
        {
            var typeName = character.Sts2TypeName();
            Assert.Matches("^[A-Z][A-Za-z0-9]*$", typeName);
        }
    }
}

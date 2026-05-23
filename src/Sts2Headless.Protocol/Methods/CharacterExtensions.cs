namespace Sts2Headless.Protocol.Methods;

// Compile-time exhaustiveness gate for the Character enum.
//
// The switch expression below has no default arm, so adding a new value
// to Character makes the compiler emit CS8509 ("does not handle all
// possible inputs"). Under our Directory.Build.props default (warnings
// as errors via AnalysisLevel + TreatWarningsAsErrors on the test
// projects), that breaks the build. Whoever adds the new character is
// forced to extend this map at the same time.
//
// The returned string is the unqualified C# type name of the character
// inside sts2.dll. The bindings layer pairs it with the
// "MegaCrit.Sts2.Core.Models.Characters." namespace prefix to resolve
// `Player.CreateForNewRun<T>` for that character. Type names are case-
// sensitive on .NET reflection; preserve the engine's spelling exactly.
public static class CharacterExtensions
{
    // CS8524 (switch doesn't handle (Character)N for unnamed integer N)
    // is suppressed deliberately: we want CS8509 (a missing NAMED enum
    // value) to be the only exhaustiveness signal, because that's the
    // one that fires when someone extends the Character enum. Out-of-
    // range casts can only originate from a broken wire payload — the
    // engine itself only ever produces in-range values — and the runtime
    // SwitchExpressionException is a clear error in that case.
#pragma warning disable CS8524
    public static string Sts2TypeName(this Character character) => character switch
    {
        Character.Ironclad => "Ironclad",
        Character.Silent => "Silent",
        Character.Defect => "Defect",
        Character.Regent => "Regent",
        Character.Necrobinder => "Necrobinder",
    };
#pragma warning restore CS8524
}

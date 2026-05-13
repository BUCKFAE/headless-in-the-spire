// Real Mathf is a static class with dozens of helpers. Members added on
// demand, each with the probe failure that forced it.
//
// from: MegaCrit.Sts2.Core.Models.ModelIdSerializationCache.Init
//   (TypeLoadException → MissingMethodException progression during
//    probe-bootstrap.)

namespace Godot;

public static class Mathf
{
    // from: ModelIdSerializationCache.Init — MissingMethodException
    //   "Method not found: 'Int32 Godot.Mathf.CeilToInt(Double)'."
    public static int CeilToInt(double s) => (int)Math.Ceiling(s);

    // from: MegaCrit.Sts2.Core.Map.StandardActMap.GenerateNextCoord
    //   MissingMethodException during EnterAct's map generation:
    //   "Method not found: 'Int32 Godot.Mathf.Max(Int32, Int32)'."
    public static int Max(int a, int b) => Math.Max(a, b);

    // from: MegaCrit.Sts2.Core.Map.StandardActMap.GenerateNextCoord
    //   MissingMethodException during EnterAct's map generation:
    //   "Method not found: 'Int32 Godot.Mathf.Min(Int32, Int32)'."
    public static int Min(int a, int b) => Math.Min(a, b);
}

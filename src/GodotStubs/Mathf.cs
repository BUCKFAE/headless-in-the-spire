// Real Mathf is a static class with dozens of helpers. Members added on
// demand, each with the probe failure that forced it.
//
// from: MegaCrit.Sts2.Core.Models.ModelIdSerializationCache.Init
//   (TypeLoadException → MissingMethodException progression during
//    probe-bootstrap.)

namespace Godot;

public static partial class Mathf
{
    // from: ModelIdSerializationCache.Init — MissingMethodException
    //   "Method not found: 'Int32 Godot.Mathf.CeilToInt(Double)'."
    public static int CeilToInt(double s) => (int)Math.Ceiling(s);

    // from: MegaCrit.Sts2.Core.Multiplayer.Serialization.PacketWriter.ResizeBufferIfNecessary
    //   (round-trip test reading + re-writing a real .mcr surfaces this:
    //    "Method not found: 'Int32 Godot.Mathf.CeilToInt(Single)'.")
    //   PacketWriter computes its byte-position from a bit-position via
    //   CeilToInt(bitPosition / 8f). Mirrors the existing CeilToInt(double)
    //   and the FloorToInt double/float pair.
    public static int CeilToInt(float s) => (int)MathF.Ceiling(s);

    // from: MegaCrit.Sts2.Core.Map.StandardActMap.GenerateNextCoord
    //   MissingMethodException during EnterAct's map generation:
    //   "Method not found: 'Int32 Godot.Mathf.Max(Int32, Int32)'."
    public static int Max(int a, int b) => Math.Max(a, b);

    // from: MegaCrit.Sts2.Core.Map.StandardActMap.GenerateNextCoord
    //   MissingMethodException during EnterAct's map generation:
    //   "Method not found: 'Int32 Godot.Mathf.Min(Int32, Int32)'."
    public static int Min(int a, int b) => Math.Min(a, b);

    // from: combat-start path through tween easing / interpolation. Each
    // member emerged from a MissingMethodException on the play_card path
    // after EnterMapCoord lands the player in a CombatRoom.
    public static float Min(float a, float b) => Math.Min(a, b);
    public static float Max(float a, float b) => Math.Max(a, b);
    public static double Min(double a, double b) => Math.Min(a, b);
    public static double Max(double a, double b) => Math.Max(a, b);
    public static float Abs(float a) => Math.Abs(a);
    public static int Abs(int a) => Math.Abs(a);
    public static float Clamp(float v, float lo, float hi) => Math.Clamp(v, lo, hi);
    public static int Clamp(int v, int lo, int hi) => Math.Clamp(v, lo, hi);
    public static double Clamp(double v, double lo, double hi) => Math.Clamp(v, lo, hi);
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static double Lerp(double a, double b, double t) => a + (b - a) * t;
    public static int FloorToInt(double s) => (int)Math.Floor(s);
    public static int FloorToInt(float s) => (int)Math.Floor(s);
    public static int RoundToInt(double s) => (int)Math.Round(s);
    // from: map-selection paths in several seeds (probe-combat-stall surfaces it
    //   as `run/select_map_node` MissingMethodException). The single-precision
    //   overload comes from sts2's tween/UI math; mirrors RoundToInt(double).
    public static int RoundToInt(float s) => (int)MathF.Round(s);
    public static float Round(float s) => MathF.Round(s);
    public static double Round(double s) => Math.Round(s);
    public static float Sqrt(float s) => MathF.Sqrt(s);
    public static double Sqrt(double s) => Math.Sqrt(s);
    public static float Pow(float a, float b) => MathF.Pow(a, b);
    public static float Sin(float s) => MathF.Sin(s);
    public static float Cos(float s) => MathF.Cos(s);
}

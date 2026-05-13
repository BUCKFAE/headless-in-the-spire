using System.Reflection;

namespace Sts2Headless.Runtime;

// Shared exception-formatting helpers. Reflection-driven invocation wraps the
// real failure in TargetInvocationException; static-initialiser failures wrap
// it again in TypeInitializationException. Without unwrapping, every cctor
// crash looks identical and you can't tell which underlying thing needs a
// stub or a patch.
public static class Diagnostics
{
    public static Exception Unwrap(Exception ex)
    {
        while (ex.InnerException is not null
               && ex is TargetInvocationException or TypeInitializationException)
        {
            ex = ex.InnerException;
        }
        return ex;
    }

    public static string Describe(Exception ex)
        => $"{ex.GetType().Name}: {ex.Message}";

    // Compact stack frames from sts2.dll (game-side) + the unwrapped exception
    // header. Skips noise from Sts2Headless.* / System.* / [MoveNext] frames so
    // the failing call site is actually visible.
    public static string DescribeWithStack(Exception ex)
    {
        var lines = new List<string> { Describe(ex) };
        if (ex.StackTrace is { } trace)
        {
            foreach (var raw in trace.Split('\n'))
            {
                var line = raw.TrimEnd();
                if (line.Length == 0) continue;
                if (!line.Contains("MegaCrit.")) continue;
                lines.Add(line.TrimStart());
            }
        }
        return string.Join(" | ", lines);
    }
}

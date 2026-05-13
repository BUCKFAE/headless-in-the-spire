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
}

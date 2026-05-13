using System.Reflection;
using Sts2Headless.Runtime;
using Xunit;

namespace Sts2Headless.UnitTests;

// Diagnostics.Unwrap and Describe are the centerpiece of every error path:
// reflection invocations wrap real failures in TargetInvocationException,
// static-cctor failures wrap them again in TypeInitializationException. If
// we forget to unwrap, every game-side bug looks the same on the wire.
public class DiagnosticsTests
{
    [Fact]
    public void Unwrap_PassesThroughOrdinaryException()
    {
        var ex = new InvalidOperationException("plain");
        Assert.Same(ex, Diagnostics.Unwrap(ex));
    }

    [Fact]
    public void Unwrap_UnwrapsTargetInvocationException()
    {
        var inner = new ArgumentException("real cause");
        var wrapped = new TargetInvocationException(inner);

        Assert.Same(inner, Diagnostics.Unwrap(wrapped));
    }

    [Fact]
    public void Unwrap_UnwrapsTypeInitializationException()
    {
        var inner = new FileNotFoundException("missing dep");
        var wrapped = new TypeInitializationException("SomeType", inner);

        Assert.Same(inner, Diagnostics.Unwrap(wrapped));
    }

    [Fact]
    public void Unwrap_UnwrapsNestedWrappers()
    {
        // Real-world shape: Activator.CreateInstance throws
        // TargetInvocationException whose inner is TypeInitializationException
        // whose inner is the actual bug. We need both layers peeled.
        var bug = new NullReferenceException("the bug");
        var cctorWrap = new TypeInitializationException("X", bug);
        var reflectionWrap = new TargetInvocationException(cctorWrap);

        Assert.Same(bug, Diagnostics.Unwrap(reflectionWrap));
    }

    [Fact]
    public void Unwrap_StopsAtTargetInvocationWithoutInner()
    {
        // Defensive: a TargetInvocationException with no inner exception
        // shouldn't loop forever or NPE — return the wrapper itself.
        var weird = new TargetInvocationException(null);
        Assert.Same(weird, Diagnostics.Unwrap(weird));
    }

    [Fact]
    public void Describe_FormatsAsTypeNameColonMessage()
    {
        var ex = new InvalidOperationException("hi there");
        Assert.Equal("InvalidOperationException: hi there", Diagnostics.Describe(ex));
    }

    [Fact]
    public void DescribeWithStack_ReturnsBaseDescribe_WhenStackIsEmpty()
    {
        // An un-thrown exception has no StackTrace. The helper should still
        // return a sensible string instead of throwing or returning "".
        var ex = new InvalidOperationException("never thrown");
        Assert.Equal("InvalidOperationException: never thrown", Diagnostics.DescribeWithStack(ex));
    }

    [Fact]
    public void DescribeWithStack_FiltersToMegaCritFrames()
    {
        // The filter looks for "MegaCrit." in each stack line. We simulate a
        // game-side frame by manually constructing a stack-shaped string and
        // verifying both inclusion of MegaCrit lines and exclusion of others.
        var faked = MakeExceptionWithFakeStack(
            new[]
            {
                "   at Sts2Headless.UnitTests.DiagnosticsTests.Test()",
                "   at System.Reflection.MethodBase.Invoke(...)",
                "   at MegaCrit.Sts2.Core.Models.ModelDb.Get[T]()",
                "   at MegaCrit.Sts2.Core.Saves.SaveManager.InitProgressData()",
            });

        var line = Diagnostics.DescribeWithStack(faked);

        Assert.Contains("MegaCrit.Sts2.Core.Models.ModelDb.Get", line);
        Assert.Contains("MegaCrit.Sts2.Core.Saves.SaveManager.InitProgressData", line);
        Assert.DoesNotContain("System.Reflection.MethodBase.Invoke", line);
        Assert.DoesNotContain("Sts2Headless.UnitTests.DiagnosticsTests.Test", line);
    }

    // System.Exception.StackTrace is computed lazily from internal state set
    // when the exception is thrown. We can't set it from outside, so we use
    // a tiny subclass that overrides the getter. The helper only reads the
    // public StackTrace string, so this is faithful enough.
    private static Exception MakeExceptionWithFakeStack(IEnumerable<string> lines) =>
        new FakeStackException("fake message", string.Join('\n', lines));

    private sealed class FakeStackException : Exception
    {
        private readonly string _stack;
        public FakeStackException(string message, string stack) : base(message) { _stack = stack; }
        public override string? StackTrace => _stack;
    }
}

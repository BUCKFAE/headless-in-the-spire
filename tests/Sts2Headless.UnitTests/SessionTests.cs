using Sts2Headless.Protocol.Methods;
using Xunit;
using Sts2Headless.Runtime.Bindings;

namespace Sts2Headless.UnitTests;

// Session is the single source of truth for "is a run active and what is it?"
// Every future stateful method consults it, so the lifecycle (empty → set →
// cleared → set again) needs to be airtight even before any sts2 type is in
// scope. The RunHandle stored carries three opaque `object` slots (player,
// runState, runManager) — we don't care what's in them here, only that the
// slot holds and releases the triple atomically.
public class SessionTests
{
    private static RunHandle DummyRun() => new(new object(), new object(), new object());

    [Fact]
    public void NewSession_IsInactive_AndExposesDefaults()
    {
        var session = new Session();

        Assert.False(session.IsActive);
        Assert.Null(session.Run);
        Assert.Null(session.Character);
        Assert.Equal(0uL, session.Seed);
    }

    [Fact]
    public void Set_StoresAllFields_AndFlipsIsActive()
    {
        var session = new Session();
        var run = DummyRun();

        session.Set(run, Character.Ironclad, 42uL);

        Assert.True(session.IsActive);
        Assert.Same(run, session.Run);
        Assert.Equal(Character.Ironclad, session.Character);
        Assert.Equal(42uL, session.Seed);
    }

    [Fact]
    public void Set_Overwrites_PreviousRun()
    {
        // Calling run/new while a run is active replaces it — there's only
        // one slot. The old RunHandle is dropped, GC reclaims it.
        var session = new Session();
        session.Set(DummyRun(), Character.Ironclad, 1uL);

        var replacement = DummyRun();
        session.Set(replacement, Character.Ironclad, 99uL);

        Assert.Same(replacement, session.Run);
        Assert.Equal(99uL, session.Seed);
    }

    [Fact]
    public void Clear_ResetsAllFields_AndFlipsIsActive()
    {
        var session = new Session();
        session.Set(DummyRun(), Character.Ironclad, 7uL);

        session.Clear();

        Assert.False(session.IsActive);
        Assert.Null(session.Run);
        Assert.Null(session.Character);
        Assert.Equal(0uL, session.Seed);
    }
}

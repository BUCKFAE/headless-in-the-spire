using Xunit;

namespace Sts2Headless.UnitTests;

// Session is the single source of truth for "is a run active and what is it?"
// Every future stateful method consults it, so the lifecycle (empty → set →
// cleared → set again) needs to be airtight even before any sts2 type is in
// scope. The Player object stored is just `object` here — we don't care what
// it is, only that the slot holds and releases it correctly.
public class SessionTests
{
    [Fact]
    public void NewSession_IsInactive_AndExposesDefaults()
    {
        var session = new Session();

        Assert.False(session.IsActive);
        Assert.Null(session.Player);
        Assert.Null(session.Character);
        Assert.Equal(0uL, session.Seed);
    }

    [Fact]
    public void Set_StoresAllFields_AndFlipsIsActive()
    {
        var session = new Session();
        var player = new object();

        session.Set(player, "ironclad", 42uL);

        Assert.True(session.IsActive);
        Assert.Same(player, session.Player);
        Assert.Equal("ironclad", session.Character);
        Assert.Equal(42uL, session.Seed);
    }

    [Fact]
    public void Set_Overwrites_PreviousRun()
    {
        // Calling run/new while a run is active replaces it — there's only
        // one slot. The old Player handle is dropped, GC reclaims it.
        var session = new Session();
        session.Set(new object(), "ironclad", 1uL);

        var replacement = new object();
        session.Set(replacement, "ironclad", 99uL);

        Assert.Same(replacement, session.Player);
        Assert.Equal(99uL, session.Seed);
    }

    [Fact]
    public void Clear_ResetsAllFields_AndFlipsIsActive()
    {
        var session = new Session();
        session.Set(new object(), "ironclad", 7uL);

        session.Clear();

        Assert.False(session.IsActive);
        Assert.Null(session.Player);
        Assert.Null(session.Character);
        Assert.Equal(0uL, session.Seed);
    }
}

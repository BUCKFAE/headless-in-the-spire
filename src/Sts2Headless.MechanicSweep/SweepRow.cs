namespace Sts2Headless.MechanicSweep;

// One observation from a per-id sweep. Outcome is the test-grade
// classification; Detail carries the human-readable "what happened" line.
//
// The kinds we exercise can differ in what "success" means — for cards
// it's "the card was played", for relics it's "the relic was given and
// no hook crashed", for powers it's "the power applied and turn ended
// cleanly". The shared SweepOutcome enum is broad enough to cover all of
// them; per-sweep classes use Detail for the kind-specific specifics.
public sealed record SweepRow(
    string Id,
    SweepOutcome Outcome,
    int Steps,
    System.TimeSpan Elapsed,
    string? Detail = null);

public enum SweepOutcome
{
    // The mechanic was successfully exercised — the wire path resolved
    // cleanly through the "active" interaction (card played, potion used,
    // event option selected, ...). The strongest informational outcome.
    Played,

    // The mechanic's presence registered through the passive surface — a
    // relic was given and lived through a combat tick without crashing,
    // a power applied and the engine accepted it. Used for kinds where
    // the mechanic doesn't have a clean "play it" action.
    Triggered,

    // The mechanic exists in the manifest but couldn't be brought into
    // the sweep's exercise fixture (e.g. card never drew into hand,
    // potion couldn't be granted in this state). Informational, not a
    // failure — these are the candidates for "extend the fixture" work.
    Unreachable,

    // The wire returned an error envelope from the mechanic's exercise
    // call (insufficient energy, wrong target type, X-cost with no
    // resource, ...). Informational — the engine *said no*, cleanly.
    Unplayable,

    // The host or runtime threw an unhandled exception while exercising
    // this mechanic. THIS IS THE FAILURE SIGNAL — what the sweep exists
    // to find. Detail names the exception type + message.
    Crashed,

    // The per-id budget elapsed without any other outcome resolving.
    // Treated as a failure: a healthy mechanic resolves in seconds, a
    // 20-second hang on one card almost always means an internal stall.
    Timeout,

    // The mechanic crashed AND it's on the SweepKnownIssues allowlist —
    // an engine path we've already classified as broken in headless
    // (off-class card, UI-screen-dependent reward path, …). Not a
    // failure signal: the sweep records it for visibility but the
    // assertion stays green. When the engine ships a fix, the fixture
    // succeeds and the row flips to Played; that's the cue to remove
    // the id from SweepKnownIssues. The Detail field carries both the
    // catalog reason and the residual exception so a reader can verify
    // the underlying engine error is still the one we knew about.
    KnownUnsafe,
}

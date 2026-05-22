# Sts2Headless.TestSupport

Shared helpers for the test suites. **Not a test project itself** — no test SDK,
no runner, no `[Fact]`s — so it never shows up as an empty test assembly. Pure
BCL, no project references. Referenced by the Unit / Integration / End2End
projects.

## What's here

| Type | Purpose |
|---|---|
| `TempDir` | A uniquely-named temp directory that self-deletes on `Dispose`. Replaces the `Path.Combine(GetTempPath(), "sts2-…-" + Guid)` + manual cleanup pattern that was copy-pasted across the suites. `using var d = new TempDir("sts2-replays");` then use `d.Path`. |

## When to add here

Add a helper when the same test-only scaffolding (a fixture, a fake, a builder)
is about to be duplicated across two test projects. Anything that asserts game
behavior stays in the test projects themselves; this is plumbing only. (Note:
the `HostSubprocess` / `RecordingHost` fixtures are still source-linked between
IntegrationTests and End2EndTests — if a third consumer appears, that's the
signal to move them here too.)

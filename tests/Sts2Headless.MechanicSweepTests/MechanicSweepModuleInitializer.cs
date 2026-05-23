using System.Runtime.CompilerServices;

namespace Sts2Headless.MechanicSweepTests;

// Hook instrumentation is opt-in (BootstrapSequence.InstrumentHooksEnvVar),
// because Harmony-patching every AbstractModel hook override costs ~440ms
// per cold start. The mechanic sweeps are the one consumer that *needs*
// the patches: their per-id rows are classified Played vs Triggered by
// draining RunStateResult.TriggeredSincePrev between calls (see
// PowerSweep.DrainTriggers, RelicSweep, etc.) and a missing patcher
// silently flips every trigger-based id (CORRUPTION_POWER, CRUELTY_POWER,
// every reactive relic) to "Played" — a green-but-wrong sweep.
//
// A module initializer runs once per assembly, before any test class is
// constructed (which means before any IClassFixture<HostSubprocess>
// instantiation). Setting the env var here propagates to the child host
// subprocess automatically — ProcessStartInfo pre-populates psi.Environment
// from the parent process when UseShellExecute=false, so HostSubprocess
// inherits without per-call changes.
//
// The opt-in is sticky: we don't restore on exit. Test runs are
// single-purpose processes — the test runner exits and the inherited
// env disappears with it. Restoring would invite the order-of-execution
// gotcha where the per-fixture cleanup races against another test class
// using the same runner.
internal static class MechanicSweepModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        Environment.SetEnvironmentVariable(
            "STS2_INSTRUMENT_HOOKS",
            "1");
    }
}

using System.Diagnostics;
using Sts2Headless.Agents.Contracts;
using Sts2Headless.Cheats;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.MechanicSweep.Sweeps;

// Per-EventId smoke sweep. For each id in EventIdNames.AllWireNames:
//
//   1. run/new(Ironclad, seed=42)
//   2. debug/set_hp(999, 999)        — survive HP-cost events
//   3. debug/start_event(E)          — forces into EventRoom; BeginEvent
//                                      hooks fire, AvailableEventOptions
//                                      surfaces on the snapshot
//   4. Loop (≤MaxOptionsToTry pages):
//        - drain triggers via run/state
//        - if not in EventRoom OR no options → break
//        - select_event_option(0)    — pick the first option each page
//                                      (multi-page events keep us in
//                                      EventRoom; single-shot events
//                                      transition out after the first
//                                      pick)
//   5. Drain triggers one final time after the loop exits.
//
// Outcomes:
//   * Triggered  — at least one TriggerKind.Event hook attributed to
//                  this event id fired across the fixture
//   * Played     — event ran cleanly, no event-attributed hook fired
//                  (most events don't override AbstractModel hooks; the
//                  trigger surface is in the engine's event-resolution
//                  path, not the model itself)
//   * Unplayable — wire-level refusal from select_event_option (event
//                  ended, no options, etc.) — informational
//   * Crashed    — host or runtime threw an unhandled exception. THIS
//                  IS THE FAILURE SIGNAL.
//   * Timeout    — per-id budget elapsed
//
// Why this sweep matters: event-option handlers historically had a
// nasty crash class around card-select screens (see
// documentation/research/agent-survival-gaps.md). The
// CardSelectCmd / ICardSelector path was rebuilt; this sweep is the
// regression net that catches any future re-introduction or any
// new event whose option handler crashes.
public sealed class EventSweep
{
    public static readonly System.TimeSpan PerEventBudget = System.TimeSpan.FromSeconds(30);

    // Hard cap on the inner loop. Some multi-page events (Tomb of
    // Lord Yusufu shapes) iterate through several options before
    // resolving; 5 covers the long-tail without letting a hypothetical
    // "always show the same page" event loop forever.
    public const int MaxOptionsToTry = 5;

    public async System.Threading.Tasks.Task<SweepReport> RunAsync(
        ITransport transport,
        System.Collections.Generic.IReadOnlyList<string>? sampleIds = null,
        string gameVersion = "unknown",
        System.Action<SweepRow>? onRow = null,
        System.Threading.CancellationToken ct = default)
    {
        var universe = EventIdNames.AllWireNames
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        var ids = sampleIds is { Count: > 0 } ? sampleIds : universe;
        var sampled = sampleIds is { Count: > 0 };

        var rows = new System.Collections.Generic.List<SweepRow>(ids.Count);
        var totalSw = Stopwatch.StartNew();
        foreach (var eventId in ids)
        {
            ct.ThrowIfCancellationRequested();
            var row = await RunOneAsync(transport, eventId, ct);
            rows.Add(row);
            onRow?.Invoke(row);
        }
        totalSw.Stop();
        return new SweepReport(
            Kind: "events",
            Rows: rows,
            TotalElapsed: totalSw.Elapsed,
            GameVersion: gameVersion,
            Sampled: sampled,
            UniverseSize: universe.Count);
    }

    private static async System.Threading.Tasks.Task<SweepRow> RunOneAsync(
        ITransport transport, string eventId, System.Threading.CancellationToken outerCt)
    {
        var sw = Stopwatch.StartNew();
        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        cts.CancelAfter(PerEventBudget);
        var ct = cts.Token;

        var firedHooks = new HashSet<string>(StringComparer.Ordinal);
        var steps = 0;

        try
        {
            // 1. Fresh run.
            await transport.SendAsync<RunNewResult>(
                "run/new",
                new RunNewParams(Character: Character.Ironclad, Seed: 42uL));

            // 2. HP cheat (some events charge HP — DeepBreath, Wellspring,
            // etc. — and we don't want a fixture death to be classified
            // as Crashed).
            await transport.SetHpAsync(999, 999);

            // 3. Force into the event. Wire result tells us whether we
            // landed in EventRoom or if the event resolved synchronously
            // back to MapRoom.
            DebugStartEventResult start;
            try
            {
                start = await transport.StartEventAsync(eventId);
            }
            catch (System.Exception wx) when (SweepInternals.IsWireError(wx))
            {
                sw.Stop();
                var outcome = SweepInternals.IsInternalError(wx) ? SweepOutcome.Crashed : SweepOutcome.Unplayable;
                return new SweepRow(
                    eventId, outcome, Steps: 0, sw.Elapsed,
                    Detail: $"start_event: {wx.GetType().Name}: {SweepInternals.Truncate(wx.Message)}");
            }
            DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), eventId, firedHooks);

            // 4. Drive each available option (picking index 0 each
            // iteration) until the event resolves or the budget caps.
            for (var page = 0; page < MaxOptionsToTry; page++)
            {
                ct.ThrowIfCancellationRequested();
                var state = await transport.SendAsync<RunStateResult>("run/state");
                DrainTriggers(state, eventId, firedHooks);

                if (state.CurrentRoomType != RoomType.EventRoom) break;
                if (state.AvailableEventOptions.Count == 0) break;

                try
                {
                    _ = await transport.SendAsync<RunSelectEventOptionResult>(
                        "run/select_event_option",
                        new RunSelectEventOptionParams(OptionIndex: 0));
                    steps++;
                }
                catch (System.Exception wx) when (SweepInternals.IsWireError(wx))
                {
                    if (SweepInternals.IsInternalError(wx))
                    {
                        sw.Stop();
                        return new SweepRow(
                            eventId, SweepOutcome.Crashed, Steps: steps, sw.Elapsed,
                            Detail: $"select_event_option: {wx.GetType().Name}: {SweepInternals.Truncate(wx.Message)}");
                    }
                    // benign refusal — engine already moved on
                    break;
                }
            }

            // 5. Final drain so post-loop hooks (AutoAdvanceFinishedEvent
            // → EnterRoom(MapRoom) → AfterRoomEntered) are captured.
            DrainTriggers(await transport.SendAsync<RunStateResult>("run/state"), eventId, firedHooks);

            sw.Stop();
            var outcome2 = firedHooks.Count > 0 ? SweepOutcome.Triggered : SweepOutcome.Played;
            var detail = firedHooks.Count > 0
                ? $"hooks: {string.Join(",", firedHooks.OrderBy(h => h, StringComparer.Ordinal))}"
                : null;
            return new SweepRow(eventId, outcome2, Steps: steps, sw.Elapsed, detail);
        }
        catch (System.OperationCanceledException) when (cts.Token.IsCancellationRequested && !outerCt.IsCancellationRequested)
        {
            sw.Stop();
            return new SweepRow(
                eventId, SweepOutcome.Timeout, Steps: steps, sw.Elapsed,
                Detail: $"per-event budget {PerEventBudget.TotalSeconds:0}s exceeded");
        }
        catch (System.Exception ex)
        {
            sw.Stop();
            return new SweepRow(
                eventId, SweepOutcome.Crashed, Steps: steps, sw.Elapsed,
                Detail: $"{ex.GetType().Name}: {SweepInternals.Truncate(ex.Message)}");
        }
    }

    private static void DrainTriggers(
        RunStateResult state,
        string eventId,
        HashSet<string> sink)
    {
        foreach (var ev in state.TriggeredSincePrev)
        {
            if (ev.Kind != TriggerKind.Event) continue;
            if (!string.Equals(ev.Source, eventId, StringComparison.Ordinal)) continue;
            if (!string.IsNullOrEmpty(ev.Hook)) sink.Add(ev.Hook);
        }
    }
}

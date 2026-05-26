using System.Diagnostics;
using Sts2Headless.Agents.Contracts;
using Sts2Headless.Agents.Driving;
using Sts2Headless.Agents.Hosting;
using Sts2Headless.Eval.Protocol;
using Sts2Headless.Protocol;
using Sts2Headless.Protocol.Methods;

namespace Sts2Headless.Eval.Execution;

// One cell's lifecycle, from process spawn to CellResult. Owns the
// terminus classification — the matrix-level orchestrator never decides
// what happened; it just collects whatever this returns.
//
// Layer responsibilities:
//
//   * `Start(...)` brings up the host (`HostProcess.Start`, AD-7 safe —
//     never passes --enable-debug) with `STS2_REPLAY_OUT` pointed at
//     the cell's replay directory (AD-8). Then brings up the agent
//     subprocess via `AgentSubprocess.Start` using the manifest's
//     declared command.
//
//   * `agent/init` handshake. Mismatched agent identity → HarnessError.
//
//   * `run/new` against the host with the cell's character / seed /
//     ascension / modifiers.
//
//   * Drive loop. We don't go through AgentDriver.PlayRunAsync because
//     the agent is *async over the wire*, not a synchronous IAgent.
//     Instead we run the same shape inline: read state, ask the agent
//     to decide, apply the action, observe stall, repeat. Reuse of
//     AgentDriver.ApplyAsync for the action → wire mapping keeps the
//     dispatch table single-sourced.
//
//   * Budget enforcement: per-decision (soft) is per-call cancellation;
//     per-cell (hard) is a shared CancellationTokenSource; MaxSteps is
//     an integer counter checked each iteration.
//
//   * `agent/teardown` best-effort, bounded by a 3s teardown budget.
//     A failed teardown is logged but doesn't reclassify the cell —
//     the run is already over.
internal static class CellExecutor
{
    public static async Task<CellResult> ExecuteAsync(
        EvaluationHarnessConfig config,
        Cell                    cell,
        string                  evalId,
        string                  evalRootAbsolute,
        Action<string>?         onLog = null,
        CancellationToken       outerCt = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(cell);

        var startedAt = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();

        var cellAbsoluteDir = Path.Combine(evalRootAbsolute, cell.RelativeReplayDir);
        Directory.CreateDirectory(cellAbsoluteDir);

        // Hard per-cell budget. Linked to the outer eval cancellation so
        // Ctrl-C still propagates through.
        using var cellCts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        cellCts.CancelAfter(cell.Budgets.PerCell);

        var manifest = cell.Manifest;
        var agentIdentity = ManifestIdentity(manifest);

        HostProcess?     host         = null;
        AgentSubprocess? agent        = null;
        string?          gameVersion  = null;
        string?          sts2Sha      = null;
        var              maxFloorSeen = 0;
        var              maxActSeen   = 0;
        var              stepsRun     = 0;

        WireErrorPayload? error    = null;
        CellTerminus      terminus = CellTerminus.HarnessError;
        RunStateResult?   lastState = null;

        try
        {
            // ── Start host ────────────────────────────────────────────────
            try
            {
                host = HostProcess.Start(new HostProcessOptions(
                    ReplayRoot:     cellAbsoluteDir,
                    HostDllPath:    config.HostDllPath,
                    RequestTimeout: cell.Budgets.PerCell,
                    AgentName:      manifest.Name,
                    OnStderr:       config.TeeProcessStderr ? (line => onLog?.Invoke($"[host:{manifest.Name}:{cell.Seed}] {line}")) : null));
            }
            catch (Exception ex)
            {
                error = new WireErrorPayload(WireErrorCode.InternalError, $"host startup failed: {ex.Message}", ex.StackTrace);
                terminus = CellTerminus.HarnessError;
                throw new HandledFault();
            }

            // ── Probe game version / sha for provenance ──────────────────
            try
            {
                var ping = await host.SendAsync<HostPingResult>("host/ping");
                gameVersion = ping.GameVersion ?? "";
                sts2Sha     = ping.GameSha256 ?? "";
            }
            catch (Exception ex)
            {
                // Couldn't ping the host — treat as HostCrash; the host is
                // alive enough to have started but not to respond.
                error = new WireErrorPayload(WireErrorCode.InternalError, $"host/ping failed: {ex.Message}", ex.StackTrace);
                terminus = CellTerminus.HostCrash;
                throw new HandledFault();
            }

            // ── Start agent subprocess ───────────────────────────────────
            try
            {
                var repoRoot = Sts2Headless.Utils.Paths.LocateRepoRoot();
                agent = AgentSubprocess.Start(new AgentSubprocessOptions(
                    Command:          manifest.Command,
                    WorkingDirectory: manifest.Cwd is null
                        ? repoRoot
                        : Path.IsPathRooted(manifest.Cwd) ? manifest.Cwd : Path.Combine(repoRoot, manifest.Cwd),
                    Environment:      manifest.Env,
                    OnStderr:         config.TeeProcessStderr ? (line => onLog?.Invoke($"[agent:{manifest.Name}:{cell.Seed}] {line}")) : null));
            }
            catch (Exception ex)
            {
                error = new WireErrorPayload(WireErrorCode.InternalError, $"agent startup failed: {ex.Message}", ex.StackTrace);
                terminus = CellTerminus.HarnessError;
                throw new HandledFault();
            }

            // ── agent/init handshake ─────────────────────────────────────
            try
            {
                using var initCts = NewDecisionCts(cellCts.Token, cell.Budgets.PerDecision);
                var initResult = await agent.SendAsync<AgentInitResult>(
                    "agent/init",
                    new AgentInitParams(
                        GameVersion:   gameVersion ?? "",
                        Sts2DllSha256: sts2Sha ?? "",
                        Character:     cell.Character,
                        Seed:          cell.Seed,
                        Ascension:     cell.Ascension,
                        Modifiers:     cell.Modifiers,
                        Budgets:       cell.Budgets,
                        EvalId:        evalId),
                    initCts.Token);
                if (!string.Equals(initResult.Name, manifest.Name, StringComparison.Ordinal))
                {
                    error = new WireErrorPayload(AgentErrorCode.AgentDeclinedToInit,
                        $"agent self-reported name '{initResult.Name}' does not match manifest '{manifest.Name}'");
                    terminus = CellTerminus.HarnessError;
                    throw new HandledFault();
                }
            }
            catch (AgentMethodErrorException ex) when (ex.Error.Code == AgentErrorCode.AgentDeclinedToInit)
            {
                error = new WireErrorPayload(ex.Error.Code, ex.Error.Message);
                terminus = CellTerminus.HarnessError;
                throw new HandledFault();
            }
            catch (AgentTimeoutException ex)
            {
                error = new WireErrorPayload(0, $"agent/init timed out: {ex.Message}");
                terminus = CellTerminus.Timeout;
                throw new HandledFault();
            }
            catch (AgentEofException ex)
            {
                error = new WireErrorPayload(ex.ExitCode, $"agent crashed during agent/init: {ex.Message}");
                terminus = CellTerminus.AgentCrash;
                throw new HandledFault();
            }
            catch (HandledFault) { throw; }
            catch (Exception ex)
            {
                error = new WireErrorPayload(WireErrorCode.InternalError, $"agent/init failed: {ex.Message}", ex.StackTrace);
                terminus = CellTerminus.HarnessError;
                throw new HandledFault();
            }

            // ── run/new on host ──────────────────────────────────────────
            try
            {
                _ = await host.SendAsync<RunNewResult>("run/new", new RunNewParams(
                    Character: cell.Character,
                    Seed:      cell.Seed,
                    Ascension: cell.Ascension,
                    Modifiers: cell.Modifiers.Count == 0 ? null : cell.Modifiers));
            }
            catch (HostMethodErrorException ex)
            {
                error = new WireErrorPayload(ex.Error.Code, ex.Error.Message);
                terminus = CellTerminus.EngineCrash;
                throw new HandledFault();
            }
            catch (Exception ex)
            {
                error = new WireErrorPayload(WireErrorCode.InternalError, $"run/new failed: {ex.Message}", ex.StackTrace);
                terminus = CellTerminus.HostCrash;
                throw new HandledFault();
            }

            // ── drive loop ────────────────────────────────────────────────
            var stall = new StallDetector();
            try
            {
                lastState = await host.SendAsync<RunStateResult>("run/state");
                UpdateMaxFloor(lastState, ref maxFloorSeen, ref maxActSeen);

                for (var step = 0; step < cell.Budgets.MaxSteps; step++)
                {
                    cellCts.Token.ThrowIfCancellationRequested();
                    stepsRun = step;

                    if (lastState.IsGameOver)
                    {
                        terminus = lastState.IsVictory ? CellTerminus.Victory : CellTerminus.Death;
                        goto DriveDone;
                    }

                    AgentDecideResult decision;
                    try
                    {
                        using var decideCts = NewDecisionCts(cellCts.Token, cell.Budgets.PerDecision);
                        decision = await agent.SendAsync<AgentDecideResult>(
                            "agent/decide",
                            new AgentDecideParams(lastState),
                            decideCts.Token);
                    }
                    catch (AgentTimeoutException ex)
                    {
                        error = new WireErrorPayload(0, $"agent/decide timed out at step {step}: {ex.Message}");
                        terminus = CellTerminus.Timeout;
                        goto DriveDone;
                    }
                    catch (AgentMethodErrorException ex)
                    {
                        error = new WireErrorPayload(ex.Error.Code, ex.Error.Message);
                        terminus = CellTerminus.AgentCrash;
                        goto DriveDone;
                    }
                    catch (AgentEofException ex)
                    {
                        error = new WireErrorPayload(ex.ExitCode, $"agent died at step {step}: {ex.Message}");
                        terminus = CellTerminus.AgentCrash;
                        goto DriveDone;
                    }

                    if (decision.Action is StopRun stop)
                    {
                        error = new WireErrorPayload(0, $"agent emitted StopRun: {stop.Reason}");
                        terminus = CellTerminus.Abandoned;
                        goto DriveDone;
                    }

                    try
                    {
                        lastState = await AgentDriver.ApplyAsync(host, decision.Action);
                    }
                    catch (HostMethodErrorException ex)
                    {
                        error = new WireErrorPayload(ex.Error.Code, ex.Error.Message);
                        terminus = CellTerminus.EngineCrash;
                        goto DriveDone;
                    }
                    catch (StallDetectedException ex)
                    {
                        error = new WireErrorPayload(0, ex.Message);
                        terminus = CellTerminus.Stalled;
                        goto DriveDone;
                    }
                    catch (Exception ex)
                    {
                        error = new WireErrorPayload(WireErrorCode.InternalError, $"host action dispatch failed at step {step}: {ex.Message}", ex.StackTrace);
                        terminus = CellTerminus.HostCrash;
                        goto DriveDone;
                    }

                    UpdateMaxFloor(lastState, ref maxFloorSeen, ref maxActSeen);

                    try { stall.Observe(lastState); }
                    catch (StallDetectedException ex)
                    {
                        error = new WireErrorPayload(0, ex.Message);
                        terminus = CellTerminus.Stalled;
                        goto DriveDone;
                    }
                }

                // Loop exited without termination: maxSteps cap reached.
                terminus = CellTerminus.MaxSteps;

            }
            catch (OperationCanceledException) when (cellCts.IsCancellationRequested)
            {
                // Hard per-cell budget expired (or outer cancellation).
                terminus = CellTerminus.Timeout;
            }
            catch (HostMethodErrorException ex)
            {
                error = new WireErrorPayload(ex.Error.Code, ex.Error.Message);
                terminus = CellTerminus.EngineCrash;
            }
            catch (Exception ex)
            {
                error = new WireErrorPayload(WireErrorCode.InternalError, $"drive loop failed: {ex.Message}", ex.StackTrace);
                terminus = CellTerminus.HarnessError;
            }

DriveDone: ;

            // ── agent/teardown (best-effort) ─────────────────────────────
            if (agent is not null && !agent.HasExited)
            {
                try
                {
                    using var teardownCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    _ = await agent.SendAsync<AgentTeardownResult>("agent/teardown", null, teardownCts.Token);
                }
                catch
                {
                    // Teardown failure does not reclassify the cell — the
                    // run's terminus is already determined.
                }
            }
        }
        catch (HandledFault)
        {
            // The path we took already set `terminus` and `error`.
        }
        finally
        {
            if (agent is not null) await agent.DisposeAsync();
            if (host is not null)  await host.DisposeAsync();
        }

        sw.Stop();

        // ── Pull metrics from the last snapshot we saw ──────────────────
        var hp        = lastState?.Hp ?? 0;
        var maxHp     = lastState?.MaxHp ?? 0;
        var gold      = lastState?.Gold ?? 0;
        var deckSize  = lastState?.DeckSize ?? 0;
        var relicCount = lastState?.Relics.Count ?? 0;

        return new CellResult(
            EvalId:        evalId,
            Agent:         agentIdentity,
            Seed:          cell.Seed,
            Character:     cell.Character,
            Ascension:     cell.Ascension,
            Modifiers:     cell.Modifiers,
            Terminus:      terminus,
            Act:           maxActSeen,
            Floor:         maxFloorSeen,
            FinalHp:       hp,
            MaxHp:         maxHp,
            Gold:          gold,
            DeckSize:      deckSize,
            RelicCount:    relicCount,
            CombatCount:   0, // best-effort metrics not wired yet; v2.
            EliteCount:    0,
            BossCount:     0,
            TurnsInCombat: 0,
            Steps:         stepsRun,
            WallClockMs:   sw.ElapsedMilliseconds,
            ReplayPath:    cell.RelativeReplayDir.Replace('\\', '/'),
            GameVersion:   gameVersion ?? "",
            Sts2DllSha256: sts2Sha ?? "",
            Scoring:       new ScoringMetrics(0.0), // populated by post-eval scoring pass
            Error:         error,
            StartedAt:     startedAt.ToString("u"),
            CompletedAt:   DateTimeOffset.UtcNow.ToString("u"));
    }

    private static AgentIdentity ManifestIdentity(AgentManifest m) =>
        new(m.Name, m.Version, m.Language, m.GetType().FullName ?? m.GetType().Name);

    private static void UpdateMaxFloor(RunStateResult s, ref int maxFloor, ref int maxAct)
    {
        if (s.CurrentActIndex > maxAct)
        {
            maxAct   = s.CurrentActIndex;
            maxFloor = s.ActFloor;
        }
        else if (s.CurrentActIndex == maxAct && s.ActFloor > maxFloor)
        {
            maxFloor = s.ActFloor;
        }
    }

    private static CancellationTokenSource NewDecisionCts(CancellationToken cellCt, TimeSpan budget)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cellCt);
        cts.CancelAfter(budget);
        return cts;
    }

    // Sentinel exception we throw to unwind to the cleanup block once a
    // setup-time fault has populated `error` and `terminus`. Catching it
    // by type instead of branching on flags keeps the happy path readable.
    private sealed class HandledFault : Exception { }
}

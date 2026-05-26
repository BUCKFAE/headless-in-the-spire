"""Markdown rendering. Same shape as the C#-emitted ``summary.md`` so a
diff-tool comparison is meaningful (NFR-1: the contract is the JSON;
the renderer is interchangeable). Two implementations of the same shape
are a useful belt-and-braces over the canonical artefact.
"""

from .model import EvaluationSummary


def format_ms(ms: int) -> str:
    """Mirror the C# emitter's FormatMs helper byte-for-byte so the two
    Markdown documents diff cleanly.
    """
    if ms < 1000:
        return f"{ms}ms"
    if ms < 60_000:
        return f"{ms / 1000:.1f}s"
    if ms < 3_600_000:
        total_minutes = ms // 60_000
        remaining_seconds = (ms - total_minutes * 60_000) // 1000
        return f"{total_minutes}m{remaining_seconds}s"
    total_hours = ms // 3_600_000
    remaining_minutes = (ms - total_hours * 3_600_000) // 60_000
    return f"{total_hours}h{remaining_minutes}m"


def render_markdown(summary: EvaluationSummary) -> str:
    """Render a Markdown leaderboard from a parsed summary.

    Field order, header lines, and table columns match the C# emitter.
    """
    lines: list[str] = []
    lines.append(f"# Evaluation — {summary.eval_id}")
    lines.append("")
    lines.append(f"Game version: `{summary.game_version}`  ")
    lines.append(f"sts2.dll SHA-256: `{summary.sts2_dll_sha256}`  ")
    lines.append(
        f"Seed bank: `{summary.seed_bank.name}` "
        f"({summary.seed_bank.count} seeds, version {summary.seed_bank.version})  ",
    )
    lines.append(_list_line("Characters", summary.characters))
    lines.append(_list_line("Ascensions", [str(a) for a in summary.ascensions]))
    lines.append(_list_line("Modifiers", summary.modifiers))
    lines.append(f"Scoring: `{summary.scoring.name}` v{summary.scoring.version}  ")
    lines.append(f"Elapsed: **{format_ms(summary.elapsed_ms)}**  ")
    lines.append(f"Cells: **{summary.cell_count}**  ")
    lines.append(f"Workers: {summary.workers}")
    lines.append("")

    lines.append(
        "| # | Agent | Version | Wins | Win% | Mean floor | p25 floor | "
        "Engine⚠ | Agent⚠ | Host⚠ | Timeout | Median wall |",
    )
    lines.append(
        "|---|-------|---------|-----:|-----:|-----------:|----------:|"
        "--------:|-------:|------:|--------:|------------:|",
    )
    for ranking in summary.ranking:
        aggs = ranking.aggregates
        lines.append(
            f"| {ranking.rank} | `{ranking.agent.name}` | {ranking.agent.version} | "
            f"{aggs.wins}/{aggs.cells} | {aggs.win_rate * 100:.1f}% | "
            f"{aggs.mean_floor:.1f} | {aggs.p25_floor} | "
            f"{aggs.engine_crashes} | {aggs.agent_crashes} | "
            f"{aggs.host_crashes} | {aggs.timeouts} | "
            f"{format_ms(aggs.median_wall_clock_ms)} |",
        )
    lines.append("")

    if summary.notable_cells:
        lines.append("## Notable cells")
        lines.append("")
        lines.append("| Agent | Seed | Terminus | Floor | Replay |")
        lines.append("|-------|------|----------|------:|--------|")
        for cell in summary.notable_cells:
            lines.append(
                f"| `{cell.agent}` | {cell.seed} | {cell.terminus} | "
                f"{cell.floor} | [{cell.replay_path}/]({cell.replay_path}/) |",
            )
        lines.append("")
    return "\n".join(lines) + "\n"


def _list_line(label: str, items: list[str]) -> str:
    if not items:
        return f"{label}: _(none)_  "
    return f"{label}: `" + "`, `".join(items) + "`  "

"""Command-line entry point. Reads ``summary.json`` from an eval
directory and prints a Markdown leaderboard.
"""

import argparse
import sys
from pathlib import Path

from .model import load_summary
from .render import render_markdown


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        prog="sts2-leaderboard",
        description="Render a Markdown leaderboard from an eval-harness summary.json.",
    )
    parser.add_argument(
        "eval_dir",
        type=Path,
        help="Path to the eval directory (replays/eval-harness/<eval-id>/) or to summary.json directly.",
    )
    args = parser.parse_args(argv)
    summary = load_summary(args.eval_dir)
    sys.stdout.write(render_markdown(summary))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

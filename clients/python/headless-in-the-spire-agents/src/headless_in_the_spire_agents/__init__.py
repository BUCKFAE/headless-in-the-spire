"""headless-in-the-spire-agents.

Algorithms (greedy, minmax, MCTS, …) and run drivers built on top of the
`headless-in-the-spire` wire client. See AD-5 for the package boundary.

This module deliberately exports nothing yet — concrete agents will land
here as they're written, and a shared `Agent` shape will be extracted
once we have two implementations to compare.
"""

__version__ = "0.0.1"

__all__ = ["__version__"]

"""headless-in-the-spire-agents — Python user tools for driving runs.

Per AD-6, canonical agents/drivers/scenarios live in C# under
`src/Sts2Headless.Agents/`. This package is *not* the regression net
and is *not* the authoritative answer to "what should the agent do" —
it exists so engineers can prototype against the wire from Python
without rebuilding dispatch.

Public surface:

- `Action` and its variants — what a Python-side agent emits.
- `GameSnapshot`, `Phase`, `current_phase` — the observation shape.
- `Agent` (Protocol) and `HeuristicAgent` (per-phase hooks) — the
  shape Python-side agents conform to.
- `play_run`, `apply_action`, `RunOutcome` — drive an agent against a
  `Client`.
- `GreedyAgent` — reference Python implementation. Illustrative; the
  canonical greedy lives in C#.
"""

from headless_in_the_spire_agents.actions import (
    Action,
    EndTurn,
    PlayCard,
    SelectEventOption,
    SelectMapNode,
    SelectReward,
    SkipReward,
)
from headless_in_the_spire_agents.agent import (
    Agent,
    HeuristicAgent,
    NoLegalActionError,
)
from headless_in_the_spire_agents.agents import GreedyAgent
from headless_in_the_spire_agents.driver import (
    RunOutcome,
    StepObserver,
    apply_action,
    play_run,
)
from headless_in_the_spire_agents.state import GameSnapshot, Phase, current_phase

__version__ = "0.0.1"

__all__ = [
    "Action",
    "Agent",
    "EndTurn",
    "GameSnapshot",
    "GreedyAgent",
    "HeuristicAgent",
    "NoLegalActionError",
    "Phase",
    "PlayCard",
    "RunOutcome",
    "SelectEventOption",
    "SelectMapNode",
    "SelectReward",
    "SkipReward",
    "StepObserver",
    "__version__",
    "apply_action",
    "current_phase",
    "play_run",
]

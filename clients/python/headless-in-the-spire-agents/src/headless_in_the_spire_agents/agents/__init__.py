"""Concrete agent implementations.

One folder per agent (the folder name doubles as the slug used in
replay-folder layout `replays/<name>/<timestamp>-<seed>/`). Each one
subclasses `HeuristicAgent` (rule-based hooks) or implements `Agent`
directly (a single `decide` method) — see `agent.py` for the contract.
Every agent MUST declare a `name` class attribute; `HeuristicAgent`
enforces this at subclass-definition time.
"""

from headless_in_the_spire_agents.agents.attack import AttackAgent
from headless_in_the_spire_agents.agents.block import BlockAgent
from headless_in_the_spire_agents.agents.greedy import GreedyAgent
from headless_in_the_spire_agents.agents.random import RandomAgent

__all__ = ["AttackAgent", "BlockAgent", "GreedyAgent", "RandomAgent"]

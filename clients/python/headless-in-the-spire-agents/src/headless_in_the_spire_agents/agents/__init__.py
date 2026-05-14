"""Concrete agent implementations.

Add a module per agent. Each one subclasses `HeuristicAgent` (rule-based
hooks) or implements `Agent` directly (a single `decide` method) — see
`agent.py` for the contract.
"""

from headless_in_the_spire_agents.agents.greedy import GreedyAgent

__all__ = ["GreedyAgent"]

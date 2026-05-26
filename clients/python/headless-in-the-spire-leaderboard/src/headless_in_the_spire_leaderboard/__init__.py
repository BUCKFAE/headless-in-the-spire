"""Downstream reader of the evaluation harness's JSON artefacts.

The C# orchestrator (``src/Sts2Headless.Eval/``) authors every fact; this
package only reads, validates, and renders. See AD-6 and AD-9.
"""

__version__ = "0.1.0"

from .model import (
    AgentAggregates,
    AgentIdentity,
    AgentRanking,
    EvaluationSummary,
    NotableCell,
    ScoringFunctionReference,
    SeedBankReference,
    load_summary,
)

__all__ = [
    "AgentAggregates",
    "AgentIdentity",
    "AgentRanking",
    "EvaluationSummary",
    "NotableCell",
    "ScoringFunctionReference",
    "SeedBankReference",
    "load_summary",
]

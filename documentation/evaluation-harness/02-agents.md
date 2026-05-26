# 02 — Writing an agent

An agent is *any* process that the harness can spawn and that speaks
the `agent/*` stdio dialect (FR-2). This file shows what the agent
author writes for the three common shapes: in-repo C#, external-repo
C#, and Python. All three hit the same wire; the differences are at
the surrounding scaffolding.

**Every agent is registered via a typed C# class — no JSON manifests,
no string-typed factories anywhere.** The class hierarchy:

```text
AgentManifest                  (abstract — the universal contract)
├── BundledAgent               (abstract — adds CreateAgent() for in-repo C# agents)
│   ├── GreedyManifest         (concrete — wraps GreedyAgent)
│   ├── IroncladManifest       (concrete — wraps IroncladAgent)
│   └── … one per in-repo agent (or variant of one)
└── (your concrete subclasses) (Python, sibling C#, Rust, …)
```

The same `AgentManifest` instance flows through everything: the
harness uses it to spawn the subprocess, the report serialises it
into `config.json`, the leaderboard reads its `Name` / `Version`.

## The `AgentManifest` contract

> Real type names that already exist: `IAgent`, `Character`,
> `ModifierId`. Proposed: `AgentManifest`, `BundledAgent`,
> `HarnessBudgets`.

```csharp
// src/Sts2Headless.Eval/Agents/AgentManifest.cs
namespace Sts2Headless.Eval;

public abstract class AgentManifest
{
    // Required.
    public abstract string Name    { get; }
    public abstract string Version { get; }
    public abstract IReadOnlyList<string> Command { get; }

    // Optional with sensible defaults.
    public virtual string?  Language    => null;       // "csharp" | "python" | …
    public virtual string?  Cwd         => null;       // null ⇒ repo root
    public virtual string?  Description => null;
    public virtual IReadOnlyDictionary<string, string>? Env => null;

    public virtual IReadOnlyList<Character>   SupportedCharacters => [Character.Ironclad];
    public virtual IReadOnlyList<int>         SupportedAscensions => [0];
    public virtual IReadOnlyList<ModifierId>? SupportedModifiers  => null;

    // Per-agent budget overrides. null ⇒ inherit EvaluationHarnessConfig.Budgets.
    public virtual HarnessBudgets? Budgets => null;
}
```

Abstract class rather than interface so the common defaults
(`SupportedCharacters = [Ironclad]`, `SupportedAscensions = [0]`,
`Budgets = null`, …) live on the base. An author who's happy with
the defaults writes three properties and stops.

The capabilities are hard-edged: if a manifest's
`SupportedCharacters` doesn't include `Character.Silent`, the harness
*skips* Silent cells for that agent and records the skip in
`summary.md`. The agent never sees an unsupported character on the
wire.

## A — in-repo C# agent

Two files: the agent itself (existing pattern, `HeuristicAgent`
subclass) and the manifest that registers it. The manifest extends
`BundledAgent`, which seals `Command` + `Language` and adds one
abstract method — `IAgent CreateAgent()` — that the manifest author
hand-writes to construct the agent. The author controls the
constructor; no `new()` constraint, no DI container, no reflection
on the agent's parameter list.

```csharp
// src/Sts2Headless.Agents/Examples/EagerEliteAgent.cs
namespace Sts2Headless.Agents.Examples;

public sealed class EagerEliteAgent : HeuristicAgent
{
    protected override AgentAction DecideMap(RunStateResult state)
    {
        var elite = state.AvailableMapNodes
            .FirstOrDefault(n => n.Type == MapNodeType.Elite);
        return elite is null
            ? base.DecideMap(state)
            : new SelectMapNode(elite.Col, elite.Row);
    }
}
```

```csharp
// src/Sts2Headless.Eval/Agents/Builtin/EagerEliteManifest.cs
namespace Sts2Headless.Eval.Agents.Builtin;

public sealed class EagerEliteManifest : BundledAgent
{
    public override string Name    => "eager-elite";
    public override string Version => "0.1.0";
    public override IAgent CreateAgent() => new EagerEliteAgent();
    // SupportedCharacters / SupportedAscensions inherit Ironclad / A0 defaults.
}
```

```csharp
// src/Sts2Headless.Eval/Agents/Builtin/BundledAgent.cs
namespace Sts2Headless.Eval;

public abstract class BundledAgent : AgentManifest
{
    /// Called by the AgentRunner subprocess after it loads this manifest
    /// by FQN. The author hand-wires the agent (and any of its policies
    /// or dependencies) here.
    public abstract IAgent CreateAgent();

    public sealed override string Language => "csharp-bundled";

    public sealed override IReadOnlyList<string> Command =>
    [
        "dotnet", "run",
        "--project", "src/Sts2Headless.AgentRunner",
        "--no-build", "--",
        "--manifest", GetType().FullName!,
    ];
}
```

The `Sts2Headless.AgentRunner` exe is a generic stdio agent host: it
takes a fully-qualified `BundledAgent` *manifest* type name on the
command line, reflects the parameterless manifest constructor,
calls `manifest.CreateAgent()` to materialise the `IAgent`, and runs
the `agent/init` → `agent/decide`* → `agent/teardown` loop. The
bundled-agent author only ever writes the manifest; the runner is
shared infrastructure.

Bundled manifests live in their own small library
(`src/Sts2Headless.Eval.Manifests/` — exact project layout pinned in
AD-9) so that both the harness and the AgentRunner exe can reference
the manifest classes without circular deps. Agent authors don't need
to know about that split.

## The `BuiltinAgents` registry

One static class, one `public static readonly` field per shipped
agent. The C# version of "an enum where you add stuff" — typed,
IDE-discoverable, no magic.

```csharp
// src/Sts2Headless.Eval/Agents/BuiltinAgents.cs
namespace Sts2Headless.Eval;

using Sts2Headless.Eval.Agents.Builtin;

public static class BuiltinAgents
{
    public static readonly AgentManifest Greedy      = new GreedyManifest();
    public static readonly AgentManifest Ironclad    = new IroncladManifest();
    public static readonly AgentManifest Random      = new RandomManifest();
    public static readonly AgentManifest Attack      = new AttackManifest();
    public static readonly AgentManifest Block       = new BlockManifest();
    public static readonly AgentManifest EagerElite  = new EagerEliteManifest();
}
```

Adding a new bundled agent is mechanical:

1. Drop the agent class under `src/Sts2Headless.Agents/Examples/`
   (or any other agent library — `Sts2Headless.BattleAgent`, etc.).
2. Add a `<Name>Manifest : BundledAgent` next to the other builtin
   manifests, implementing `CreateAgent()` with whatever constructor
   the agent needs.
3. Add one line to `BuiltinAgents`.

The contributor diff is small and entirely typed. An accidental
typo in any of those three steps is a compile error.

## Variants of the same agent class

Because `CreateAgent()` is hand-written, two manifests can wrap the
same `IAgent` class with different dependencies. Each variant is its
own ranked row on the leaderboard — no per-variant agent class
needed. This is the long-term answer to "how do we compare policy
stacks against each other?".

```csharp
public sealed class IroncladManifest : BundledAgent
{
    public override string Name    => "ironclad";
    public override string Version => "0.5.1";
    public override IAgent CreateAgent() =>
        new IroncladAgent(
            draftPolicy:    new BossAwareDraftPolicy(),
            pathPolicy:     new ElitePreferringPathPolicy(),
            restPolicy:     new HpThresholdRestPolicy(threshold: 0.5),
            eventPolicy:    new GreedyEventPolicy(),
            merchantPolicy: new BudgetMerchantPolicy());
}

public sealed class IroncladConservativeManifest : BundledAgent
{
    public override string Name    => "ironclad-conservative";
    public override string Version => "0.5.1";
    public override IAgent CreateAgent() =>
        new IroncladAgent(
            draftPolicy:    new BossAwareDraftPolicy(),
            pathPolicy:     new ElitePreferringPathPolicy(),
            restPolicy:     new HpThresholdRestPolicy(threshold: 0.7),  // rest sooner
            eventPolicy:    new GreedyEventPolicy(),
            merchantPolicy: new BudgetMerchantPolicy());
}
```

Both go on `BuiltinAgents`; the leaderboard renders them
side-by-side. Naming is the author's responsibility — keep variant
names obvious (`ironclad-conservative`, `ironclad-no-rest`,
`ironclad-mcts-1k`) so the leaderboard reads like a sentence.

## B — external-repo C# agent

You make a console app in another git repo, reference
`Sts2Headless.AgentRunner` (a NuGet or local project ref), and
delegate `Main` to it. Your agent class is a normal `IAgent` /
`HeuristicAgent` subclass.

```csharp
// external-repo/src/MyAgent.cs
using Sts2Headless.Agents.Authoring;
using Sts2Headless.Agents.Contracts;
using Sts2Headless.Protocol.Methods;

namespace MyCompany.MyAgent;

public sealed class MyAgent : HeuristicAgent
{
    protected override AgentAction DecideCombat(RunStateResult state)
    {
        // your planner
        return new EndTurn();
    }
}
```

```csharp
// external-repo/src/Program.cs
using Sts2Headless.AgentRunner;

return await AgentRunner.RunAsync<MyCompany.MyAgent.MyAgent>(args);
```

To plug your agent into someone else's eval, you ship a manifest
class. Two distribution shapes:

- **NuGet (preferred for reusable agents):** publish a
  `MyCompany.MyAgent.Manifest` NuGet that contains the manifest
  class. The eval-author adds the NuGet, does `new MyAgentManifest()`,
  done.
- **Inline (preferred for one-off runs):** the eval-author writes
  the manifest class in their own eval program, pointing at the
  external repo's command line.

Either way, the manifest class is the same shape:

```csharp
// external-repo/src/MyAgentManifest.cs   (for the NuGet route)
// — or —
// some-eval/Manifests/MyAgentManifest.cs (for the inline route)
using Sts2Headless.Eval;
using Sts2Headless.Protocol.Methods;

public sealed class MyAgentManifest : AgentManifest
{
    public override string Name        => "my-agent";
    public override string Version     => "0.2.1";
    public override string Language    => "csharp";
    public override string Description => "MCTS-based Ironclad planner.";

    public override IReadOnlyList<string> Command =>
        ["dotnet", "run", "--project", "/home/me/code/external-repo", "--no-build", "--"];

    public override HarnessBudgets? Budgets =>
        new() { PerDecision = TimeSpan.FromSeconds(45) };
}
```

The eval program registers it the same way as a built-in:

```csharp
Agents = [
    BuiltinAgents.Greedy,
    new MyAgentManifest(),
],
```

## C — Python agent

The Python client + agents packages already exist; this adds an
`agent_main` shim on the Python side and a manifest class on the C#
side. The Python file looks just like the existing `Agent` protocol
plus a `__main__` block.

```python
# my_agent/__main__.py
from headless_in_the_spire_agents import HeuristicAgent
from headless_in_the_spire_agents.action import (
    AgentAction, EndTurn, SelectMapNode,
)
from headless_in_the_spire_agents.runner import agent_main


class MyAgent(HeuristicAgent):
    name: str = "my-python-agent"
    version: str = "0.1.0"
    supported_characters = ("ironclad",)
    supported_ascensions = (0,)

    def decide_map(self, state) -> AgentAction:
        elite = next(
            (n for n in state.available_map_nodes if n.type == "Elite"),
            None,
        )
        return SelectMapNode(col=elite.col, row=elite.row) if elite else super().decide_map(state)


if __name__ == "__main__":
    agent_main(MyAgent())
```

The C#-side manifest declares how to spawn it:

```csharp
// some-eval/Manifests/MyPythonAgentManifest.cs
using Sts2Headless.Eval;

public sealed class MyPythonAgentManifest : AgentManifest
{
    public override string Name     => "my-python-agent";
    public override string Version  => "0.1.0";
    public override string Language => "python";
    public override IReadOnlyList<string> Command =>
        ["uv", "run", "python", "-m", "my_agent"];
    public override string  Cwd => "clients/python/my-agent";
    public override IReadOnlyDictionary<string, string>? Env =>
        new Dictionary<string, string> { ["PYTHONUNBUFFERED"] = "1" };
}
```

Pythonside `supported_characters` / `supported_ascensions` are
*declared* on the agent class so the Python runner can range-check
its own behaviour (and refuse to start a Silent cell with a clear
error), but the C# manifest's `SupportedCharacters` is what the
harness uses to decide which cells to schedule. The two should
agree; the manifest is the gating one.

## The `agent/*` wire dialect (sketched; exact bytes deferred to AD-9)

You only see this if you implement an adapter from scratch (no
`HeuristicAgent` base, not Python, not the AgentRunner exe). For most
authors, the framework hides it.

```text
[harness → agent]
{"id":1,"method":"agent/init","params":{
   "gameVersion":"v0.103.2","sts2DllSha256":"a1b2…",
   "character":"Ironclad","seed":42,"ascension":0,"modifiers":[],
   "budgets":{"perDecision":30000,"perCell":600000},
   "evalId":"2026-05-26T19-32-04Z"
}}

[harness ← agent]
{"id":1,"result":{"name":"my-agent","version":"0.1.0","notes":"deck-tracker enabled"}}

[harness → agent]
{"id":2,"method":"agent/decide","params":{"snapshot":{ /* full RunStateResult JSON */ }}}

[harness ← agent]
{"id":2,"result":{"action":{"kind":"PlayCard","cardIndex":3,"targetIndex":0},
                  "notes":"bash for 8 to the slime"}}

[harness → agent]
{"id":99,"method":"agent/teardown"}

[harness ← agent]
{"id":99,"result":null}
```

Properties:

- **JSON-RPC envelope** identical to the host wire (AD-2). The agent
  dialect is the *mirror* of the host dialect, not a parallel format.
- **Snapshot is the full `RunStateResult`**, byte-for-byte the same
  shape the host emits — no re-encoding, no per-eval projection.
- **`AgentAction` is the closed union** the in-repo C# agents already
  use (PlayCard, EndTurn, SelectMapNode, …) — see
  `src/Sts2Headless.Agents/Contracts/AgentAction.cs`.
- **`notes`** is free-text, optional, captured only when
  `EvaluationHarnessConfig.CaptureAgentNotes = true` (FR-12).
- **Stateful by design.** The agent process survives all `agent/decide`
  calls for the cell, so a planner can cache search trees across
  decisions (FR-2 explicit).
- **Per-decision timeout** is the soft budget — exceeding it fails
  the cell with terminus `Timeout`, not `AgentCrash`.
- **Per-cell wall-clock** is the hard budget; the harness will
  `SIGTERM` then `SIGKILL` the agent (and host) if it expires.

## Why an abstract class, not an interface

The user originally asked for an interface ("Maybe
`CommandLineAgentRunner`?"). Abstract class wins this round because:

- **Sensible defaults eliminate boilerplate.** A bundled agent
  author writes three properties (`Name`, `Version`, and the one
  override they want). An interface would force every property on
  every implementor or rely on C# default-interface-members, which
  read worse than `virtual` properties.
- **`BundledAgent : AgentManifest` slots cleanly into the hierarchy.**
  With an interface base, the bundled-agent convenience (`CreateAgent`
  + sealed `Command`) would need to be a separate class plus the
  interface, doubling the vocabulary.
- **Inheriting and `sealed override`** on the bundled subclass
  prevents an in-repo agent author from accidentally rewriting
  `Command` and breaking the AgentRunner contract. With an interface
  this guarantee is unobtainable — any implementor can re-declare
  `Command` to whatever they want.

The "interface" intent — *the user implements a typed C# thing, not
JSON* — is preserved. The shape is just abstract-class-shaped because
that's the right C# tool for the job.

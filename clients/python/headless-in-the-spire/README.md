# headless-in-the-spire (Python client)

Thin Python wrapper around the [headless-in-the-spire](../../..) C# runner for
Slay the Spire 2.

DTOs in `headless_in_the_spire._models` are generated from
[`protocol/openrpc.json`](../../../protocol/openrpc.json) (AD-5) via
`datamodel-code-generator` and vendored in. The transport
(`headless_in_the_spire.transport`) spawns the C# host as a subprocess and
talks NDJSON + JSON-RPC envelopes over stdio (AD-2). The typed `Client`
exposes one method per OpenRPC method.

```python
from headless_in_the_spire import Client
from headless_in_the_spire._models import Character, RunNewParams

with Client.spawn() as c:
    print(c.host_ping().version)
    state = c.run_new(RunNewParams(character=Character.ironclad, seed=1))
    print(state.room_type)
```

## Installation (dev)

```sh
python3 -m venv .venv
. .venv/bin/activate
pip install -e ".[dev]"
```

## Regenerating DTOs

After `protocol/openrpc.json` changes, run from the repo root:

```sh
just generate-python
```

That invokes `scripts/generate_models.py` in this package, which rewrites
`#/components/schemas/X` → `#/definitions/X` and pipes the result through
`datamodel-code-generator` (`--output-model-type pydantic_v2.BaseModel`).

The output (`src/headless_in_the_spire/_models.py`) is committed so users can
`pip install` without the dev toolchain. CI does **not** currently regenerate
on every build — the C# side enforces `Methods.cs` ↔ `MethodCatalog` parity
plus a `protocol/openrpc.json` drift check; this package follows on the
human-reviewed bump.

## Package boundary

This package contains **only** the wire client. Algorithms (minmax, MCTS,
RL drivers, …) belong in the sibling package
`headless-in-the-spire-agents` so heavy ML dependencies stay out of the thin
client. See AD-5 in [`documentation/requirements/02-architecture-decisions.md`](../../../documentation/requirements/02-architecture-decisions.md).

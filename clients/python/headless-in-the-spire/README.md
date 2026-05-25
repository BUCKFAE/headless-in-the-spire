# headless-in-the-spire (Python client)

Thin Python wrapper around the [headless-in-the-spire](../../..) C# runner for
Slay the Spire 2.

This package is a member of the repo-root [uv workspace](../../../pyproject.toml).
Toolchain prerequisites: [`uv`](https://docs.astral.sh/uv/) on PATH; uv
takes care of Python itself (3.13, pinned by `.python-version` at the repo
root) and every dev tool.

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
    print(c.host_ping().game_version)
    state = c.run_new(RunNewParams(character=Character.ironclad, seed=1))
    print(state.current_room_type)
```

## Installation (dev)

From the repo root:

```sh
just setup::setup        # if you haven't yet — also handles game-DLL bootstrap
just validation::test-python  # run this package's tests via uv
```

`just setup::setup` runs `uv sync --all-packages`, which creates `.venv` at the
repo root and installs every workspace member editable plus the shared dev
group. Don't bring your own `pip`/`virtualenv` — uv tracks all state.

## Regenerating DTOs

After `protocol/openrpc.json` changes, from the repo root:

```sh
just build::generate-python
```

That executes `scripts/generate_models.py` via `uv run`, which wraps
`components/schemas` in a minimal OpenAPI 3 doc and pipes it through
`datamodel-code-generator` (`--output-model-type pydantic_v2.BaseModel`,
`--target-python-version 3.10`, `--allow-population-by-field-name`).

The output (`src/headless_in_the_spire/_models.py`) is committed so
downstream consumers can `pip install` (or `uv pip install`) without the
dev toolchain. CI does **not** currently regenerate on every build — the
C# side enforces `Methods.cs` ↔ `MethodCatalog` parity plus a
`protocol/openrpc.json` drift check; this package follows on the
human-reviewed bump.

## Package boundary

This package contains **only** the wire client. Algorithms (minmax, MCTS,
RL drivers, …) belong in the sibling package
`headless-in-the-spire-agents` so heavy ML dependencies stay out of the thin
client. See AD-5 in [`documentation/requirements/02-architecture-decisions.md`](../../../documentation/requirements/02-architecture-decisions.md).

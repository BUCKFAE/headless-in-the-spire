#!/usr/bin/env python3
"""Generate pydantic v2 DTOs from protocol/openrpc.json.

datamodel-code-generator doesn't natively understand OpenRPC, so we wrap
`components/schemas` in a minimal OpenAPI 3 document and let the generator's
OpenAPI parser walk it. Wire `$ref`s already point at
`#/components/schemas/...`, which is the OpenAPI shape too — no rewrite.

Invoked via the just recipe:

    just generate-python              # regenerate _models.py
    just generate-python -- --check   # CI / pre-commit drift detection

Paths are anchored at the repo root, located by walking up to `GAME_VERSION`.
"""

import argparse
import json
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any


def locate_repo_root(start: Path) -> Path:
    for p in [start, *start.parents]:
        if (p / "GAME_VERSION").is_file():
            return p
    raise SystemExit("could not locate repo root (no GAME_VERSION found upwards)")


def build_lifted_schema(openrpc: dict[str, Any]) -> dict[str, Any]:
    """Lift OpenRPC's components.schemas into a minimal OpenAPI 3.0 document.

    We wrap the schemas in OpenAPI shape (not raw JSON Schema with
    `definitions/`) because datamodel-code-generator's OpenAPI parser doesn't
    synthesise a root placeholder class — JSON-Schema mode does, and we want
    a clean module.

    `$ref`s in openrpc.json already point at `#/components/schemas/...`, which
    is also the OpenAPI form, so no rewrite is needed.
    """
    components: dict[str, Any] = openrpc.get("components", {})
    schemas: dict[str, Any] = components.get("schemas", {})
    if not schemas:
        raise SystemExit("openrpc.json has no components.schemas")
    return {
        "openapi": "3.0.3",
        "info": {"title": "headless-in-the-spire", "version": "0.0.1"},
        "paths": {},
        "components": {"schemas": schemas},
    }


def run_generator(schema_path: Path, output_path: Path) -> None:
    # Input is a minimal OpenAPI 3 doc carrying the OpenRPC `components.schemas`
    # block. `pydantic_v2.BaseModel` picks the right base; `--snake-case-field`
    # matches Python conventions (pydantic preserves wire names via aliases).
    cmd = [
        sys.executable,
        "-m",
        "datamodel_code_generator",
        "--input",
        str(schema_path),
        "--input-file-type",
        "openapi",
        "--output",
        str(output_path),
        "--output-model-type",
        "pydantic_v2.BaseModel",
        "--target-python-version",
        "3.13",
        # CLAUDE.md: never emit `from __future__ import annotations`. We're
        # pinned to 3.13; native `X | Y` and `list[int]` work without it, and
        # eager annotations keep pydantic / runtime introspection honest.
        "--disable-future-imports",
        "--use-schema-description",
        "--use-field-description",
        "--snake-case-field",
        "--allow-population-by-field-name",
        "--use-double-quotes",
        "--use-annotated",
        "--disable-timestamp",
        "--use-standard-collections",
        "--use-union-operator",
        "--field-constraints",
        "--collapse-root-models",
        "--enum-field-as-literal",
        "one",
    ]
    subprocess.run(cmd, check=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="diff against committed _models.py")
    args = parser.parse_args()

    repo_root = locate_repo_root(Path(__file__).resolve())
    openrpc_path = repo_root / "protocol" / "openrpc.json"
    target_path = (
        repo_root
        / "clients"
        / "python"
        / "headless-in-the-spire"
        / "src"
        / "headless_in_the_spire"
        / "_models.py"
    )

    openrpc: dict[str, Any] = json.loads(openrpc_path.read_text())
    lifted = build_lifted_schema(openrpc)

    # Use a fixed-name tempfile under the system temp dir. datamodel-codegen
    # stamps the input filename into the generated header (`# filename: ...`)
    # — a tempfile.NamedTemporaryFile would make every run differ by that
    # line alone and defeat `--check`. The fixed name lives in the system
    # temp dir so concurrent runs in different repos don't clash with the
    # repo path; that's good enough determinism for our drift check.
    schema_tmp = Path(tempfile.gettempdir()) / "headless-in-the-spire-openrpc.json"
    schema_tmp.write_text(json.dumps(lifted))

    try:
        if args.check:
            gen_tmp = Path(tempfile.gettempdir()) / "headless-in-the-spire-_models.py"
            run_generator(schema_tmp, gen_tmp)
            generated = gen_tmp.read_text()
            committed = target_path.read_text() if target_path.exists() else ""
            if generated != committed:
                sys.stderr.write(
                    "headless_in_the_spire/_models.py is out of date — run "
                    "`just generate-python`.\n"
                )
                return 1
            return 0

        run_generator(schema_tmp, target_path)
        print(f"wrote {target_path.relative_to(repo_root)}")
        return 0
    finally:
        schema_tmp.unlink(missing_ok=True)


if __name__ == "__main__":
    raise SystemExit(main())

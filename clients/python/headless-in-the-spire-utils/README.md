# headless-in-the-spire-utils

Small grab-bag of Python helpers shared across the `headless-in-the-spire`
workspace members — directory scaffolding, path sanitization, logging
helpers, and similar plumbing that doesn't belong in either the wire
client or the agent layer.

This package is a member of the repo-root
[uv workspace](../../../pyproject.toml). The wire client
(`headless-in-the-spire`) depends on it via `{ workspace = true }`, so
agents and other downstream callers pick it up transitively.

## Layout

```
src/headless_in_the_spire_utils/
  paths.py     # clean_setup_dir, sanitize_path
tests/         # unit tests; no live host required
```

## Running tests

From the repo root:

```sh
just test-python
```

import 'scripts/common.just'

# First-run setup and per-machine bootstrap (STS2 install validation,
# DLL pull, uv .venv, replay-viewer pnpm install, git hooks).
mod setup 'scripts/setup/justfile'

# Build the C# solution and regenerate wire-protocol / GodotStubs /
# hook-surface artefacts (everything that wraps `dotnet build`).
mod build 'scripts/build/justfile'

# Run the harness: stdio host, MCP server, replays, diagnostic probes.
mod runner 'scripts/runner/justfile'

# Serve / inspect the wire-protocol schema (OpenRPC playground).
mod protocol 'scripts/protocol/justfile'

# Tests, lint, typecheck — both C# and Python suites.
mod validation 'scripts/validation/justfile'

_default:
    just --list
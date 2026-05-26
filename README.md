# headless-in-the-spire

[![CI](https://github.com/BUCKFAE/headless-in-the-spire/actions/workflows/ci.yml/badge.svg)](https://github.com/BUCKFAE/headless-in-the-spire/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](Directory.Build.props)
[![Tests](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/BUCKFAE/headless-in-the-spire/main/.github/badges/tests.json)](tests)
[![Godot stubs](https://img.shields.io/badge/Godot%20stubs-4.5.1-478CBF?logo=godotengine&logoColor=white)](src/GodotStubs)
[![C# LoC](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/BUCKFAE/headless-in-the-spire/main/.github/badges/csharp-loc.json&logo=csharp&logoColor=white)](src)

A custom headless runner for **Slay the Spire 2**. It loads the real game logic
out-of-game and drives it programmatically for deterministic testing, automation,
AI experimentation, and replay tooling.

## Features

- **Real game, no reimplementation.** The host loads the shipped `sts2.dll` and drives it as a library. STS2's own C# is the source of behavioral truth.
- **Runs headless, parallelisable.** A .NET subprocess — no Godot window, no input injection, no display server. Scenarios are reproducible on CI and trivially parallel.
- **Schema-first wire protocol.** NDJSON / JSON-RPC over stdio, with an
  OpenRPC schema exported directly from the C# method records. Ships a
  typed pydantic v2 Python client and an MCP server, so any MCP-aware
  assistant (Claude Desktop, Claude Code, …) can drive a full run
  end-to-end.
- **Native replay artefacts.** Per-combat `.mcr` and per-run `.run` files
  are written through the engine's *own* writers — the same format the
  game itself produces. A bundled TypeScript replay viewer renders them
  straight from disk.
- **Tested against the real DLL.** Three-axis test pyramid (unit /
  integration / end-to-end), plus opt-in mechanic sweeps that drive every
  card / relic / event / potion / power / encounter / affliction /
  enchantment id through a minimal fixture.

### Roadmap

- Extend existing wire protocol with more detailed info, like the remaining elite pool for current map
- Better overview of available wire / agent features
- Tooling to better compare performance of different agents across languages
- Support for `Kotlin` and `Rust`
- Improved battle simulator
- CLI runner allowing you to play sts2 when you again accidentally deleted your desktop on linux
- Improved coverage reports, ensure all code paths are covered by tests that fail loudly when the dll changes.
- Create a proper CI runner that has the proprietary sts2 dlls.

#### Replays & Controlling the actual game
The current replay viewer is very crude. I think that sooner or later someone (or Megacrit) will develop a sophisticated replay viewer.
Once a community-established solution for this (and controlling the actual game) is available, I will integrate it.

## Current State
This project is in *very active, early* development. Expect breaking changes.

## Acknowledgements and AI Notice
This repository heavily used [https://github.com/wuhao21/sts2-cli](https://github.com/wuhao21/sts2-cli) as reference point. 
While I reviewed the high-level design choices carefully, most of the implementation was AI-generated and committed without line-by-line review. Treat it accordingly.

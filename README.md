# headless-in-the-spire

[![CI](https://github.com/BUCKFAE/headless-in-the-spire/actions/workflows/ci.yml/badge.svg)](https://github.com/BUCKFAE/headless-in-the-spire/actions/workflows/ci.yml)
[![.NET](.github/badges/dotnet.svg)](Directory.Build.props)
[![Tests](.github/badges/tests.svg)](tests)
[![Godot stubs](.github/badges/godot-stubs.svg)](src/GodotStubs)
[![C# LoC](.github/badges/csharp-loc.svg)](src)

A custom headless runner for **Slay the Spire 2**. It loads the real game logic
out-of-game and drives it programmatically for deterministic testing, automation,
AI experimentation, and replay tooling.

## Current State
This project is in *very active, very early* development. Expect breaking changes hourly.

## Acknowledgements and AI Notice
This repository heavily used [https://github.com/wuhao21/sts2-cli](https://github.com/wuhao21/sts2-cli) as reference point. 
While I reviewed the high-level design choices carefully, most of the implementation was AI-generated and committed without line-by-line review. Treat it accordingly.
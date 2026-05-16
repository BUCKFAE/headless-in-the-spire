# headless-in-the-spire

[![CI](https://github.com/BUCKFAE/headless-in-the-spire/actions/workflows/ci.yml/badge.svg)](https://github.com/BUCKFAE/headless-in-the-spire/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](Directory.Build.props)
[![Tests](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/BUCKFAE/headless-in-the-spire/main/.github/badges/tests.json)](tests)
[![Godot stubs](https://img.shields.io/badge/Godot%20stubs-4.5.1-478CBF?logo=godotengine&logoColor=white)](src/GodotStubs)
[![C# LoC](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/BUCKFAE/headless-in-the-spire/main/.github/badges/csharp-loc.json&logo=csharp&logoColor=white)](src)

A custom headless runner for **Slay the Spire 2**. It loads the real game logic
out-of-game and drives it programmatically for deterministic testing, automation,
AI experimentation, and replay tooling.

## Current State
This project is in *very active, very early* development. Expect breaking changes hourly.

## Acknowledgements and AI Notice
This repository heavily used [https://github.com/wuhao21/sts2-cli](https://github.com/wuhao21/sts2-cli) as reference point. 
While I reviewed the high-level design choices carefully, most of the implementation was AI-generated and committed without line-by-line review. Treat it accordingly.
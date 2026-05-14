# headless-in-the-spire

[![CI](https://github.com/BUCKFAE/headless-in-the-spire/actions/workflows/ci.yml/badge.svg)](https://github.com/BUCKFAE/headless-in-the-spire/actions/workflows/ci.yml)
[![.NET](.github/badges/dotnet.svg)](Directory.Build.props)
[![C#](.github/badges/csharp.svg)](Directory.Build.props)
[![xUnit](.github/badges/xunit.svg)](https://github.com/xunit/xunit/releases/tag/2.9.2)
[![Tests](.github/badges/tests.svg)](tests)
[![Godot stubs](.github/badges/godot-stubs.svg)](src/GodotStubs)
[![Protocol](.github/badges/protocol.svg)](src/Sts2Headless.Protocol/Envelope.cs)

A custom headless runner for **Slay the Spire 2**. It loads the real game logic
out-of-game and drives it programmatically for deterministic testing, automation,
AI experimentation, and replay tooling.

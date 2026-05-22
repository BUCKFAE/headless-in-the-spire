# Sts2Headless.Cheats

The typed **cheat surface**: `debug/*` wire methods and the `CheatClient`
extension methods that call them. Deliberately a separate assembly from
`Sts2Headless.Protocol` so an agent that references only Protocol *cannot*
resolve cheat extensions — cheats are opt-in for tests and tooling, not part
of the behavioral surface agents play against (AD-7).

## What cheats are available

All live under the `debug/` namespace and are catalogued in
`CheatMethodCatalog.cs` (each marked `IsDebugOnly: true`):

| Method | Purpose |
|---|---|
| `debug/give_relic` | Grant a relic by id. |
| `debug/set_hp` | Set the player's current HP. |
| `debug/replace_deck` | Replace the deck with a given list of `(CardId, UpgradeLevel)`. |
| `debug/read_deck` | Read the deck back as `(CardId, UpgradeLevel)` pairs — mirrors `replace_deck`'s shape so tests can round-trip. |
| `debug/kill_all_enemies` | End the current combat by killing every enemy. |
| `debug/start_combat` | Drop straight into a named combat encounter. |

Typed client wrappers (e.g. `ReadDeckAsync`, `KillAllEnemiesAsync`) live in
`CheatClient.cs` as `ITransport` extension methods; DTOs are in
`CheatDtos.cs`, host-side handlers in `CheatHostMethods.cs`.

## How to use them

Cheats are **disabled by default**. The host only serves a `debug/*` method
when started with `--enable-debug`; without it, calls return
`WireErrorCode.DebugMethodDisabled` (-32001). The integration/e2e test
fixture (`HostSubprocess`) passes `--enable-debug` automatically — a
production host must never set it.

When adding a cheat: register it via `HostMethods.GateDebug(...)`, mark the
`CheatMethodCatalog` entry `IsDebugOnly: true`, and add both a positive case
(à la `DebugSetHpTests`) and a negative case to `DebugDisabledTests` so the
gate stays a tested regression net.

See AD-7 in `documentation/requirements/02-architecture-decisions.md`.

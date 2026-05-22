# Sts2Headless.Protocol

The **wire protocol**: the JSON-RPC-style envelope, the request/response method
records, and the `MethodCatalog` that is the single source of truth for what
methods exist. Pure DTOs and schema — no game DLL, no I/O. Almost everything
else references this project.

## Shape

- `Envelope.cs` / `EnvelopeIo.cs` — the NDJSON request/response/notification
  envelope and its `JsonSerializerOptions`. Payloads ride as `JsonNode` at the
  envelope layer and are deserialised to concrete records at dispatch (AD-2).
- `WireError.cs` — error codes (e.g. `DebugMethodDisabled` = -32001).
- `MethodCatalog.cs` — every method name + param/result shape; drives the
  OpenRPC export (AD-5) and the generated clients.
- `Methods/` — the typed param/result records (`RunNewParams`, `CombatState`,
  `RunStateResult`, …) and the content-id enums.

## Conventions

- **Enums over strings on the wire.** Any field with a fixed value set
  (`RoomType`, `MapNodeType`, `Character`, …) is a C# enum with a
  `JsonStringEnumConverter` and an `Unknown` sentinel. Grow the enum when an
  integration test surfaces a new value rather than widening the parse.
- **Generated content-id enums.** `{Kind}Id.g.cs` (CardId, RelicId, MonsterId,
  …) are emitted by `just generate-content-ids` from the proprietary
  `vendor/sts2.dll` and are **gitignored** — never committed (AD-3). Each has a
  committed `{Kind}Id.Fallback.cs` stub so the project compiles on a fresh
  clone *before* the generator has run; a conditional `<Compile Remove>` in the
  `.csproj` swaps the stub out once the generated file exists.

> If a downstream project fails to build with `'CardId' does not contain a
> definition for '…'`, the generated enums are missing — run `just setup` (or
> `just generate-content-ids`), don't edit the fallback to match.

See `documentation/requirements/02-architecture-decisions.md` (AD-2, AD-5).

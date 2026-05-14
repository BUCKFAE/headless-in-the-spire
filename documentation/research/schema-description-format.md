# Schema Description Format — Survey

Snapshot date: 2026-05-14. Background research behind
[AD-5](../requirements/02-architecture-decisions.md#ad-5--schema-description-format-openrpc).

This document records two things:

1. Whether any existing Slay the Spire project (1 or 2) publishes a formal
   protocol description we should align with or vendor.
2. The state of OpenRPC tooling for the languages we care about (C#, Python,
   Kotlin), and how well OpenRPC fits the specific shapes in
   `Sts2Headless.Protocol.Methods`.

The conclusion of (1) is "nothing exists to align with." The conclusion of
(2) is "workable, with one known DIY cost on the Kotlin side." Together they
support adopting OpenRPC; see AD-5 for the decision.

---

## 1. STS ecosystem survey

### STS2 projects

- **`wuhao21/sts2-cli`** — closest reference. NDJSON over stdin/stdout with
  bare command objects (`{"cmd":"action","action":"play_card","args":[...]}`);
  no JSON-RPC envelope, no id, no error channel beyond a `state` reply. No
  schema artefact in the repo (no `*.schema.json`, no OpenAPI / OpenRPC /
  protobuf anywhere under `src/`, `python/`, or `docs/`). Hand-written
  Python client (`python/play.py`). Stringly-typed throughout — action names,
  card IDs, room names all strings on the wire.
- **`Gennadiyev/STS2MCP`** — HTTP REST on `localhost:15526` + a Python MCP
  wrapper. No published schema (no OpenAPI even though the server is REST and
  the schema would be near-free). Protocol documented in prose under
  `docs/raw-*.md`.
- **`CharTyr/STS2-Agent`** — HTTP + SSE on `127.0.0.1:8080`, MCP on `:8765`.
  No schema; README defers to prose docs under `mcp_server/README.md`.
- **`longkerdandy/STS2-Cli-Mod`** — Named Pipe + JSON, `{"ok":true,"data":...}`
  envelope. DTOs implicit in C# `Models/` directories; no schema export.
  Stringly-typed (`STRIKE`, `DEFEND`, game modes `standard`/`daily`/`custom`).

### STS1 ecosystem (older, more mature)

- **`ForgottenArbiter/CommunicationMod`** — the seminal STS1 mod. NDJSON over
  stdin/stdout, but **asymmetric**: game→client sends JSON state, client→game
  sends plain text commands like `PLAY 1`, `END`, `CHOOSE 0`. No schema
  artefact; protocol defined entirely by prose + JSON examples in the README.
  Hand-written Python helper library `spirecomm`. Stringly-typed wire
  (`"id":"Strike_R"`, `"room":"COMBAT"`, `"class":"IRONCLAD"`). Downstream
  consumers (`cdaymand/slaythecli`, `xaved88/bottled_ai`, several forks) all
  hand-parse the JSON — there is no community-shared schema even though
  CommunicationMod is the closest thing to a standard.
- **`gamerpuppy/sts_lightspeed`** — C++ reimplementation. No wire protocol;
  in-process library with pybind11 Python bindings. Card / relic identity
  lives in C++ `enum class` + `constexpr` arrays. Other projects that reuse
  it include the headers; no schema is exposed.
- **`ptrlrd/spire-codex`** — STS2 static-data lookup service. Auto-generates
  OpenAPI via FastAPI's `/docs`, but it's a static-data API, not a runtime
  protocol — different problem. PolyForm Noncommercial license also blocks
  reuse.

### Bottom line

No STS project (1 or 2) ships OpenRPC, OpenAPI, JSON Schema, or protobuf as a
runtime protocol description. The only exception is `spire-codex`'s
auto-generated OpenAPI for *static* lookup data, which doesn't apply to our
case. CommunicationMod's prose-defined NDJSON is the closest to a de-facto
standard in the STS1 world; the STS2 generation has fragmented further across
incompatible IPC patterns (NDJSON, HTTP REST, HTTP+SSE, named pipe, MCP) with
no shared envelope, naming, or type discipline.

We are first either way. There is no compatibility pressure to align with any
existing schema, and no community templates to crib from.

---

## 2. OpenRPC tooling and fit

### Spec status

OpenRPC is at **1.4.1** (2026-02-25), with active patch cadence. Maintained
by the `open-rpc` GitHub org under Apache-2.0. Originally sponsored by ETC
Labs and adjacent to the Ethereum execution-apis ecosystem (EIP-1901). The
spec is stable, backwards-compatible per its own SemVer rule, and not at
risk of disappearing — but adoption outside Web3 is thin, and tooling
reflects that. Deep on TypeScript and Rust, shallow on Python, near-zero on
Kotlin / JVM and .NET.

References:

- [open-rpc/spec](https://github.com/open-rpc/spec) — spec source and releases.
- [open-rpc/meta-schema](https://github.com/open-rpc/meta-schema) — JSON
  Schema we'd validate `protocol/openrpc.json` against in CI.

### Python codegen

Two viable paths.

- **`openrpcclientgenerator`** (Matthew Burkard,
  [GitLab](https://gitlab.com/mburkard/openrpc-client-generator),
  [PyPI](https://pypi.org/project/openrpcclientgenerator/)). v0.51.7
  (Apr 2026). Single-maintainer. License classifier on PyPI says MIT, repo
  says AGPL-3.0 — worth resolving before any redistribution. Output uses
  pydantic v2 for models, methods sanitised to snake_case.
- **`datamodel-code-generator`**
  ([GitHub](https://github.com/koxudaxi/datamodel-code-generator/)). Heavily
  maintained, MIT, multi-target output (pydantic v2, msgspec, dataclasses,
  TypedDict). Doesn't speak OpenRPC directly — we pre-strip the schema down
  to its JSON Schema components and feed that. The method dispatch layer
  (~50 LOC of templated Python wrapping our stdio transport) is hand-rolled
  in this repo.

The second path is the more controllable one: rock-solid DTO codegen plus
a small in-repo template we own. Picks the DTO style at integration time
rather than locking us to pydantic v2.

The official **`open-rpc/generator`**
([GitHub](https://github.com/open-rpc/generator)) ships TypeScript and Rust
client components in v2.1.1 (Oct 2025). The README mentions Python and Go
but those components are stale — not a path for us today.

### Kotlin codegen

Effectively absent. Neither `open-rpc/generator` nor
`openrpcclientgenerator` ships a Kotlin or Java component, and no
third-party Kotlin OpenRPC client generator surfaced in research.
JetBrains' `kotlinx.rpc` is a different ecosystem entirely (not
OpenRPC-driven).

Realistic options:

1. Write a Kotlin component for `open-rpc/generator`
   (Mustache/Handlebars templates against its TypeScript host). Estimated
   size: a few hundred lines of templates plus a small dispatch helper.
   Once written, lives in this repo and runs on every protocol bump;
   plausible upstream contribution.
2. Pre-convert OpenRPC → OpenAPI for Kotlin only, then use
   `openapi-generator` Kotlin templates. Loses RPC semantics in the
   intermediate artefact and re-introduces the HTTP-fiction problem inside
   the Kotlin pipeline. Pragmatic but adds a translation layer we'd have
   to maintain.

AD-5 picks option 1.

### C# emission

`Tochka.JsonRpc.OpenRpc`
([NuGet](https://www.nuget.org/packages/Tochka.JsonRpc.OpenRpc), v7.4.0
Apr 2026, MIT) is the only maintained .NET OpenRPC emitter, but it's
bolted to its own server framework (`Tochka.JsonRpc.ApiExplorer`). Not a
drop-in for our setup.

The realistic path is hand-rolling on top of .NET 10's
`System.Text.Json.Schema.JsonSchemaExporter`. Sketch of the layer:

- Method index — one `MethodObject` per handler (name, params schema,
  result schema, errors, paramStructure). ~150–250 LOC for the current
  ~10-method surface, mostly mechanical mapping
  `(typeof(RunNewParams), typeof(RunNewResult)) → MethodObject`.
- `$ref` plumbing — `JsonSchemaExporter` inlines schemas by default; we
  want a shared `components/schemas` so `CombatState` isn't duplicated
  across every result. ~50 LOC to walk the exporter output and hoist
  named types.
- CI validation against the meta-schema. ~20 LOC.

Total: a single file, ~300–400 LOC, plus a unit test asserting
`rpc.discover` round-trips. The emitter is invoked by a new
`just export-schema` recipe and writes `protocol/openrpc.json` (checked
in).

### Gotchas for our specific protocol shapes

Read alongside `src/Sts2Headless.Protocol/Methods.cs` for the actual
shapes.

- **String enums with custom wire spellings** (`Character.Ironclad` ↔
  wire `"ironclad"`, `RewardKind.Card` ↔ wire `"card"`, …).
  `JsonSchemaExporter` emits `{"type":"string","enum":[...]}` directly
  from the `JsonStringEnumMemberName` attributes. No information loss.
- **`Unknown` sentinel.** Just another enum value on the wire — the
  schema models it correctly. The *semantics* (host emits `Unknown` for
  variants not yet catalogued so clients tolerate game-patch additions
  without failing) are not expressible in OpenRPC, JSON Schema, OpenAPI,
  or protobuf — none of those formats have an "open enum" construct. We
  document the contract on every such enum's `description` field, and
  generated clients must not exhaust-match.
- **Slash-namespaced method names** (`run/new`, `debug/give_relic`). The
  OpenRPC meta-schema imposes no pattern constraint, so these are
  spec-legal. Generator behaviour is the risk — every generator sanitises
  identifiers differently (typical: `/` → `_`, or nested namespace).
  Pin per-language behaviour with an integration test on the first
  generated client.
- **Notifications.** OpenRPC 1.4.x handles these by making `result`
  optional ("If undefined, the method MUST only be used as a
  notification" — [open-rpc/spec#230 / PR #368](https://github.com/open-rpc/spec/issues/230)).
  Wire side is already covered by our `Notification` record; codegen
  output may need spot-checking on older generators.
- **Per-method JSON-RPC errors.** The OpenRPC Method Object has an
  `errors: [{code, message, data?}]` array — exactly our envelope shape.
  Codes in `-32768..-32000` are reserved by JSON-RPC. Generators
  typically emit per-method exception classes from this list.
- **`ok: bool` redundancy on results.** Just a required boolean property
  on every result schema. Harmless; shows up as a field on every
  generated result class.
- **Nullable composites** (`CombatState?`, `RewardsState?`). Map to
  `{"oneOf": [{"$ref": "..."}, {"type": "null"}]}` or `"nullable": true`
  depending on JSON Schema dialect. Pydantic gets `Optional[CombatState]`;
  Kotlin gets `CombatState?`. Fine in both.

---

## Sources

- [OpenRPC Specification (1.4.x)](https://spec.open-rpc.org/)
- [open-rpc/spec releases](https://github.com/open-rpc/spec/releases)
- [open-rpc/meta-schema](https://github.com/open-rpc/meta-schema)
- [open-rpc/generator](https://github.com/open-rpc/generator)
- [openrpcclientgenerator (GitLab)](https://gitlab.com/mburkard/openrpc-client-generator)
- [datamodel-code-generator](https://github.com/koxudaxi/datamodel-code-generator/)
- [Tochka.JsonRpc.OpenRpc on NuGet](https://www.nuget.org/packages/Tochka.JsonRpc.OpenRpc)
- [EIP-1901: OpenRPC Service Discovery](https://eips.ethereum.org/EIPS/eip-1901)
- [ForgottenArbiter/CommunicationMod](https://github.com/ForgottenArbiter/CommunicationMod)
- [ForgottenArbiter/spirecomm](https://github.com/ForgottenArbiter/spirecomm)
- [gamerpuppy/sts_lightspeed](https://github.com/gamerpuppy/sts_lightspeed)
- [wuhao21/sts2-cli](https://github.com/wuhao21/sts2-cli)
- [Gennadiyev/STS2MCP](https://github.com/Gennadiyev/STS2MCP)
- [CharTyr/STS2-Agent](https://github.com/CharTyr/STS2-Agent)
- [longkerdandy/STS2-Cli-Mod](https://github.com/longkerdandy/STS2-Cli-Mod)
- [ptrlrd/spire-codex](https://github.com/ptrlrd/spire-codex)

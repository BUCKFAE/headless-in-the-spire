# GodotStubs

No-op replacements for the GodotSharp types that `sts2.dll` references, so the
game's C# can load and run **out-of-game** without a Godot engine present.

## Why this package exists

`sts2.dll` is a Godot 4.x C# assembly. Its types derive from and call into
`GodotSharp.dll` (the managed Godot binding) — `Node`, `Control`, `Resource`,
`Vector2`, `Tween`, and friends. We never start the Godot engine, so loading
`sts2.dll` against the real `GodotSharp.dll` would drag in native engine state
we don't have. Instead we ship a stand-in.

The trick is **assembly identity, not namespace**: this project builds with
`AssemblyName=GodotSharp` and `AssemblyVersion=4.5.1.0` (see the `.csproj`), so
when the loader resolves `sts2.dll`'s `GodotSharp, Version=4.5.1.0` reference it
binds to *our* DLL — which lands next to the exe and shadows the real one. The
stub types are inert: just enough surface to satisfy the references sts2 makes,
with empty or trivial bodies, so the engine layer stays dormant.

## Why the namespaces are `Godot` / `Godot.Collections`

For the identity swap to work the types must match what sts2 expects by full
name — `Godot.Node`, `Godot.Collections.Dictionary`, etc. That's why the files
declare `namespace Godot;` even though the project is called GodotStubs, and
why an IDE (Rider) may warn that the namespace doesn't match the folder/assembly
name. **That warning is expected here** — renaming the namespaces would break
the bindings.

Likewise, members that look "unused" (e.g. a `Dictionary` indexer) are present
because *sts2* references them via reflection or IL, not because our own code
calls them — every type carries a `// from:` comment recording the caller that
forced it, which is also its provenance.

Both inspections are therefore false positives unique to this package, so they
are suppressed in [`.editorconfig`](.editorconfig) **scoped to this folder
only** — the same checks keep guarding the rest of the codebase. The compiler
itself emits zero warnings here (`dotnet build` is clean); the suppressions are
purely for the IDE.

## Grows on demand

Per the project's hard rules, **do not** speculatively mirror the GodotSharp
surface. Add a stub only when a missing reference forces it, and record the
caller with a `// from: <type>.<member>` comment. `just runner::probe::list-members <FQN>`
(the `--list-members` command) dumps every member of a type that `sts2.dll`
references, so you can grow a type in one pass instead of one
`MissingMethodException` at a time.

A `GAME_VERSION` bump can change the required `GodotSharp` version; if it does,
the loader surfaces a `FileNotFoundException` naming the new version, and
`AssemblyVersion` here must be bumped to match.

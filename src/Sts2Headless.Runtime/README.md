# Sts2Headless.Runtime

Everything that talks to a **live `sts2.dll`**: resolving and loading the
proprietary game assembly out-of-game, making it safe to call into without a
Godot engine, and exposing a typed surface that the host and tests drive.

This is the only project that loads the game DLL, so all the reflection,
Harmony patching, and bootstrap sequencing lives here behind one assembly
boundary (AD-4: still no compile-time reference to sts2 — targets are
discovered reflectively).

## Layout

The files are grouped into sub-folders by concern, each with a matching
sub-namespace (`Sts2Headless.Runtime.<Folder>`):

| Folder / namespace | What's in it |
|---|---|
| `Loading/` | Vendor resolution (`VendorAssemblyResolver`), the sts2 load + reflection helpers (`Sts2Reflection`), the inline `SynchronizationContext`, repo/vendor `Paths`, and the bootstrap entry points (`RuntimeBootstrap`, `BootstrapSequence`). |
| `Patches/` | Harmony IL patches that stop the headless engine from hanging (`HangPatches.*`) plus the localization patches (`LocPatches`). |
| `Hooks/` | Model-event observability hooks (`ModelHookPatcher`, the per-kind `*HookPatches`), the monster-patch auditor, and the `TriggerLog`. |
| `Bindings/` | The driveable API surface — the `Sts2Bindings` partial class (one type across 13 files), its `RunHandle`/`RunSnapshot` handles, and the `InvocationPlan` reflection helper. |
| `CardSelection/` | The headless card-selector bridge (`CardSelector`, `HeadlessCardSelectorBridge`) that stands in for the interactive selector UI. |
| (root) | `Diagnostics`, `SampleSaves` — small cross-cutting utilities. |

## Why folders, not separate packages

The split is **organizational, not architectural**. Two facts make separate
assemblies the wrong tool here:

1. `Sts2Bindings` is a single `sealed partial class` spread over 13 files. A
   partial class is one type — it cannot cross an assembly boundary.
2. The clusters reference each other **circularly**: `Sts2Bindings` (Bindings)
   drives the card selector (CardSelection) and the patches (Patches); the
   card-selector bridge and `RunHandle` call back into `Sts2Bindings`; the
   bootstrap (Loading) installs the patches and hooks. Assembly references must
   form a DAG, so these can't be peeled into a layered package graph without a
   larger redesign (extracting interfaces to invert the cycles).

So the namespaces group files by *what they do*, not by a dependency hierarchy.
Cross-cluster references inside the assembly are just `using` directives — the
parent-namespace rule means a sub-namespace file already sees the root
`Diagnostics`/`SampleSaves` for free.

## Adding code

Drop a new file in the folder that matches its concern and declare the
matching `Sts2Headless.Runtime.<Folder>` namespace. When you reference a type
from another cluster, add the explicit `using` (the build escalates unused
usings to an error, so the set stays honest). GodotSharp surface is grown on
demand in `../GodotStubs/` — see that project's README.

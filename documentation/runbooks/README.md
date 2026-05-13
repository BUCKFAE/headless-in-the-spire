# Runbooks

Operational and debugging knowledge that accumulates over time. The intended
reader is a contributor (human or LLM agent) who has just hit a confusing
failure and wants to find out whether anyone has seen it before.

## What goes here

- **Symptoms-first debugging notes**: "if you see X, look at Y first" entries.
  Concrete, not abstract.
- **How-to recipes** for one-off operations that aren't worth a script yet
  (regenerating snapshots, bumping the game version, extracting `sts2.dll`
  from a Steam install on a new machine).
- **Known quirks** of the game / Godot / Harmony / the host that aren't
  obvious from the code.

## What does NOT go here

- Architectural decisions → `documentation/requirements/02-architecture-decisions.md`.
- API or protocol reference → generated from the schema; do not duplicate.
- Tutorials for end-users of the client libraries → separate; this directory
  is for contributors.

## Files

- [debugging.md](./debugging.md) — symptoms-first debugging guide. Currently
  a stub; grow it organically as failures are investigated.

## Convention

Each entry in `debugging.md` follows:

```
## <Symptom — what the contributor literally observes>

**First check**: <the cheapest signal that confirms or rules out the most
likely cause>

**Why this happens**: <one short paragraph>

**Fix**: <concrete steps>

**Related**: <links to issues, PRs, or other entries>
```

Keep entries terse. If something grows past a screen, it probably wants its
own dedicated doc.

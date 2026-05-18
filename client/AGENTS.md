# AGENTS.md

## Scope
This file applies to the whole `client` workspace.

## Required Project Rules
Before modifying Unity project files, read and follow these documents:

1. `docs/commonness/agent-unity-safety-rules.md`
2. `docs/commonness/project-structure-and-namespace.md`
3. `docs/commonness/scene-ownership-and-prefab-edit-rules.md`
4. `docs/commonness/bootstrap-scene-and-initialization-flow.md`
5. `docs/commonness/topdown-engine-extension-and-original-protection.md`

## Priority
- System and developer instructions have the highest priority.
- This `AGENTS.md` comes next.
- `docs/commonness/*.md` are project rules and must be followed.
- If rules conflict, ask before changing `.unity`, `.prefab`, `.asset`, or `.meta` files.

## Unity Work Rules
- Do not edit external asset originals directly.
- Prefer project-owned scripts and copied prefabs/scenes.
- Avoid direct scene YAML edits unless explicitly requested.
- Explain changed files and risk when modifying Unity serialized files.


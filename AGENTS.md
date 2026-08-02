# AGENTS.md

Be brief.

## Project

Unity 2D mobile shoot 'em up.

Main areas:

- Ship construction/crafting.
- Player team selection.
- ScriptableObject configs for ships, weapons, levels, waves and UI.
- One main battle scene configured by `LevelConfig`.
- Directed wave system with subwaves, formations, morphing and post-behaviour pipelines.
- Zenject is used for dependency injection.

## Code discovery

This project uses codebase-memory-mcp.

Always prefer MCP graph tools before grep/file search:

1. `search_graph` — find classes/functions/symbols.
2. `trace_path` — inspect dependencies/callers.
3. `get_code_snippet` — read exact source.
4. `query_graph` — complex analysis.
5. `get_architecture` — project overview.

Fallback to `rg` only when:

- MCP does not find a new file/class.
- Searching Unity YAML, prefabs, scenes, assets, config values.
- Searching logs or literal error messages.

After meaningful code changes, re-index the project with codebase-memory-mcp.

## Unity rules

Do not edit files under:

- `Library/`
- `Temp/`
- `obj/`
- `Logs/`
- package caches

Do not delete media/assets unless explicitly requested:

- sprites
- textures
- audio
- prefabs
- ScriptableObjects
- scenes

When editing Unity YAML files:

- Be careful with prefab/scene references.
- Preserve GUIDs and fileIDs.
- Prefer code/editor tooling over manual YAML edits when possible.
- If changing prefab data manually, verify the exact target prefab and field.

## Architecture preferences

Prefer:

- ScriptableObject configs for designer-facing data.
- Zenject injection over `FindObjectOfType` / `FindAnyObjectByType`.
- Explicit references/configuration over hidden global state.
- `TMP_Text` over `UnityEngine.UI.Text`.
- One battle scene + level configs instead of many duplicated battle scenes.
- Small focused components over marker/empty classes.
- Editor tooling for complex designer workflows.

Avoid:

- New singletons.
- New hard-coded level/ship/weapon IDs unless unavoidable.
- Logic depending on child index if a config/reference can be used.
- Runtime systems that depend on Editor-only APIs.
- Silent fallback behaviour that hides broken configuration.

## Coding style

Prefer readable code over clever code.

Avoid unnecessary allocations.

Avoid LINQ in `Update` or frequently executed code.

Keep methods small.

Prefer early returns.

Avoid deeply nested `if` statements.

Keep public API stable unless requested.

Do not rename serialized fields without migration.

Performance matters.

Avoid:

- allocations every frame
- reflection
- `FindObjectOfType` in gameplay
- `Resources.Load` during gameplay

## Battle and level system

Battle scene should be generic.

Level-specific data should come from `LevelConfig`:

- level id
- display name
- parallax/background
- music
- wave config
- rewards
- progression requirements

Level loading should always load the battle scene and pass/select the config.

## Wave system

`DirectedEnemySubWave` and Wave conductor are the preferred wave system.

When changing waves:

- Keep Preview Wave and runtime behaviour consistent.
- If adding a runtime feature, update the Editor preview too.
- If adding designer fields, make them visible and understandable in the custom editor.
- Avoid legacy wave paths unless explicitly requested.
- Preserve existing configured prefabs where possible.

For post-behaviour:

- Pipeline commands should be explicit and composable.
- Preview should respect delays, loops, parallel commands and infinite commands.
- Do not reset positions at loop boundaries unless the command explicitly asks for reset.

## Ship/weapon construction

Ships are selected/built through configs and saved blueprints.

Rules:

- Do not assume a fixed weapon slot count.
- Slot availability should come from UI/config/build data, not magic numbers.
- Ship ID should come from `ShipData` where possible.
- Weapon energy cost and ship energy capacity must be respected.
- Saving a ship should fail if required weapon slots are not filled or energy is exceeded.

## Abilities

Abilities should:

- Clean up spawned runtime objects on ship change if intended.
- Avoid duplicate `ParentShip` on clones/visual helpers.
- Use the real owning `ParentShip` / `WeaponController` where possible.
- Be safe when switching ships during active abilities.
- Not keep subscriptions attached to old ships after ship switch.

## UI

Use `TMP_Text` for new text fields.

UI should update through explicit methods/events, not hidden scene searches.

For editor/designer UI:

- Add clear labels.
- Avoid cramped fields.
- Add warnings when config is incomplete.
- Prefer buttons for common generation/preview actions.

## Validation

After final code changes, run at least:

- `dotnet build "Assembly-CSharp.csproj" --no-restore --verbosity:quiet --nologo`
- `dotnet build "Assembly-CSharp-Editor.csproj" --no-restore --verbosity:quiet --nologo`

If Unity project files are stale or locked by Unity, mention it clearly.

Warnings from existing injected fields/Zenject are acceptable if unrelated, but new errors are not.

## Communication

Respond in Russian.

Be direct about:

- what changed
- where changed
- how to use it
- what was verified
- any Unity setup still required in Inspector

If a request is architectural, explain tradeoffs before implementing large changes.

## Design principle

Designer-facing systems should be editable from Inspector and previewable before Play Mode whenever possible.

Runtime behaviour and Editor preview must use the same conceptual rules.

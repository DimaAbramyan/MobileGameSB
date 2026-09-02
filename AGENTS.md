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


## Unity CLI and Editor interaction

Use the Unity CLI + Unity Pipeline as the default way to inspect and modify the running Unity Editor.

The Unity Editor is normally already open. Attach to that Editor; do not start another instance unless explicitly requested.

Connection rules:

- Run Unity CLI commands from the project root.
- Use `unity status` to verify the connected Editor when needed.
- Use `unity list` / `unity command` to discover commands exposed by the connected Editor and their parameters.
- Prefer a first-class Pipeline command when one exists.
- Use `unity command eval "..."` for one-off Editor C# operations that do not have a dedicated command.
- If several Editors are open, target this project explicitly with `--project-path` instead of guessing.
- Execute Editor-mutating Unity CLI commands sequentially, never in parallel.
- Do not hard-code the Pipeline port; let the CLI discover the Editor.

Do not use licensing as an Editor health check:

- Do not run `unity license status` as a prerequisite for project work.
- Do not invoke, kill, restart, repair or replace `Unity.Licensing.Client.exe`.
- Do not run `unity auth`, `unity license`, `unity upgrade`, or `unity pipeline install/upgrade` unless the task explicitly requires it.
- In the Codex Windows environment the Licensing Client IPC may be inaccessible even while the running Editor is fully reachable through Unity Pipeline. A licensing IPC failure must not trigger recovery attempts if `unity status` / `unity command` still work.

Editor lifecycle:

- Do not launch `Unity.exe` directly.
- Do not use `-batchmode`, `-projectPath`, `unity run`, or another headless Editor as a substitute for the already running Editor unless explicitly requested.
- Do not conclude that the Editor process is gone from `unity status` alone. `unity status` reports connected Editors/Pipeline state, so it can temporarily show nothing while scripts are recompiling or the Pipeline server is reloading.
- Check Editor process existence independently with `unity editors running` and, on Windows when needed, `Get-Process Unity -ErrorAction SilentlyContinue`.
- After direct C# changes, script compilation, package reload, assembly reload, or a domain reload, treat temporary Pipeline loss as expected. Wait and reconnect instead of immediately stopping the task.
- Recovery procedure after a source edit or unexpected Pipeline disconnect:
  1. Determine whether the Unity Editor process is still running using `unity editors running` and/or the OS process list.
  2. If the Editor process is running but Pipeline is unavailable, wait and retry discovery/read-only connection for up to 90 seconds. Retry sequentially every few seconds; do not launch another Editor.
  3. Prefer a harmless read-only Editor probe such as `unity command eval "return UnityEngine.Application.unityVersion;"` once discovery begins working again.
  4. After reconnection, check whether Unity is still compiling/importing before continuing with dependent mutations.
  5. If a mutating command disconnected during execution, verify whether its effect was already applied before retrying it.
  6. If the Editor process is still running but Pipeline has not returned after the retry window, inspect the recent Unity Editor log/compile state for errors before asking the user to intervene.
  7. Only report that the Editor must be reopened when independent process checks confirm that the Editor process is actually absent, or when it has crashed/exited.
- Never treat a transient Pipeline disconnect caused by recompilation as proof that the Editor was closed.
- Never blindly retry a mutating command after a disconnect; first verify whether the requested change was already applied.

How to edit the project:

1. Use MCP graph tools to understand code and dependencies.
2. Edit C# and other plain-text source/config files directly when that is the natural representation.
3. Use Unity CLI/Pipeline for Unity-owned state: scenes, prefabs, GameObjects/components, ScriptableObjects, serialized references, import settings, AssetDatabase operations and Editor-only workflows.
4. Prefer Unity Editor APIs through Pipeline over manual `.unity`, `.prefab`, `.asset` YAML editing.
5. Use manual Unity YAML editing only as a last resort when no safe Editor/CLI path is practical.

For Editor mutations through `eval`:

- Use `Undo.RecordObject` / appropriate Undo APIs when practical for user-visible changes.
- Use `EditorUtility.SetDirty` for modified persistent objects when required.
- Use `AssetDatabase.SaveAssets()` for modified assets when required.
- Mark/save scenes through `EditorSceneManager` when the task requires persistent scene changes.
- Prefer `AssetDatabase` / `PrefabUtility` APIs for asset and prefab operations instead of filesystem-only manipulation.
- Return a small explicit result from `eval` so success can be verified.

After direct source-file edits:

- Ask the running Editor to refresh/import through Unity CLI when needed.
- A refresh or script edit can temporarily tear down the Pipeline connection during compilation/domain reload. This is not, by itself, a failure.
- Do not use a single failed `unity status`, `unity command`, or discovery call immediately after a source edit as evidence that the Editor is unavailable. Follow the Editor lifecycle recovery procedure and retry.
- Wait for Unity compilation/import to finish before running dependent Editor commands.
- Treat a CLI disconnect caused by a domain reload as expected; reconnect and inspect the resulting state.
- Do not continue with dependent mutations while `EditorApplication.isCompiling` or the AssetDatabase is still updating.

When a repeated Editor workflow needs several complex `eval` calls, prefer adding or using a focused first-class Pipeline command rather than growing fragile inline C# snippets.

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
- Prefer Unity CLI/Pipeline Editor tooling over manual YAML edits when possible.
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

After meaningful code or asset changes:

1. Let the running Unity Editor refresh/import and finish compilation through the CLI/Pipeline workflow.
2. Verify the requested state in the Editor through a read-only Pipeline command or `eval` where practical.
3. Check for new Unity compile/Console errors using an available Pipeline command when one is exposed by `unity list`.
4. Run at least:
   - `dotnet build "Assembly-CSharp.csproj" --no-restore --verbosity:quiet --nologo`
   - `dotnet build "Assembly-CSharp-Editor.csproj" --no-restore --verbosity:quiet --nologo`

If a domain reload disconnects the CLI, reconnect and verify state rather than treating the disconnect itself as a project failure.

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

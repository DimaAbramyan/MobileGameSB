## Purpose

This document defines how AI agents should interpret tasks, use project documentation, inspect the codebase and make changes to **Project: Reforge**.

Detailed coding and architecture rules live in their respective documents.

The root `AGENTS.md` remains the mandatory instruction file for coding agents.

If this document conflicts with `AGENTS.md`, follow `AGENTS.md` and report the discrepancy.

---

# Core Behaviour

The agent should:

- understand before changing;
    
- prefer existing project conventions;
    
- keep changes within the requested scope;
    
- avoid inventing missing requirements;
    
- preserve existing behaviour unless change is requested;
    
- verify meaningful changes;
    
- report uncertainty and contradictions.
    

Prefer the smallest change that correctly solves the requested problem.

Do not refactor unrelated systems without a clear reason.

---

# Task Understanding

Before meaningful implementation:

1. Understand the requested result.
    
2. Read relevant documentation.
    
3. Inspect the existing implementation.
    
4. Inspect relevant dependencies.
    
5. Identify conflicts or missing information.
    
6. Make the change only after the intended result is sufficiently clear.
    

Do not implement based only on class names or assumptions about how a system probably works.

---

# Uncertainty

Do not invent:

- gameplay rules;
    
- architecture decisions;
    
- balance values;
    
- progression behaviour;
    
- save behaviour;
    
- permanent project conventions;
    
- user intent.
    

Ask for clarification when missing or ambiguous information could materially change the result.

Examples:

- several interpretations produce different gameplay;
    
- documentation contradicts itself;
    
- code and documentation disagree and intended behaviour is unclear;
    
- required behaviour is undocumented;
    
- a change may affect save compatibility;
    
- it is unclear whether existing behaviour is intentional.
    

Do not ask unnecessary questions for minor implementation details when existing project conventions provide a safe answer.

---

# Unresolved Design

The following markers mean that the design is not final:

- `TBD`
    
- `UNDECIDED`
    
- `OPEN QUESTION`
    

Do not replace them with invented decisions.

See: [[Open-Questions]]

---

# Sources of Truth

Different sources answer different questions.

## Game Design

Describes intended player-facing behaviour.

Start from:

[[Home]]

Then read the relevant design document.

---

## Technical Documentation

Describes intended architecture and engineering conventions.

Start from:

- [[Architecture]]
    
- [[Code-Style]]
    

Then follow relevant links.

---

## Source Code and Unity Data

Describe the current implementation.

Implementation may temporarily differ from intended design.

---

## AGENTS.md

Defines mandatory operational rules for coding agents.

---

# Conflicts

If sources contradict each other:

1. Identify the contradiction.
    
2. Check whether one source is explicitly authoritative.
    
3. If intended behaviour is still unclear, ask for clarification.
    

Do not silently choose whichever version is easier to implement.

Do not silently modify documentation to match code.

Do not silently modify code to match documentation unless the task requires it.

---

# Scope

Stay within the requested task.

Do not automatically:

- refactor unrelated classes;
    
- rename unrelated APIs;
    
- replace frameworks;
    
- reorganize project folders;
    
- redesign neighboring systems;
    
- change gameplay balance;
    
- introduce new architectural patterns.
    

Supporting changes are acceptable when required for correctness.

Large architectural changes should be explained before implementation unless explicitly requested.

---

# Code Discovery

Use **Codebase Memory MCP** as the preferred way to inspect C# architecture and dependencies.

Recommended flow:

1. `search_graph`
    
2. `trace_path`
    
3. `get_code_snippet`
    
4. `query_graph`
    
5. `get_architecture`
    

Use raw search mainly for:

- Unity YAML;
    
- scenes;
    
- prefabs;
    
- assets;
    
- serialized values;
    
- logs;
    
- literal errors;
    
- newly created or unindexed files.
    

See: [[Codebase-Memory]]

---

# Architecture

Follow existing project architecture.

Key principles:

- explicit dependencies;
    
- clear ownership;
    
- composition where appropriate;
    
- designer-facing configuration;
    
- minimal global state;
    
- separation of configuration and runtime state;
    
- separation of gameplay and presentation.
    

See:

- [[Architecture]]
    
- [[System-Boundaries]]
    
- [[Runtime-State]]
    
- [[Object-Lifecycle]]
    

---

# Code Style

Follow the project's coding conventions.

See:

- [[Code-Style]]
    
- [[OOP-Principles]]
    
- [[SOLID]]
    
- [[Anti-Patterns]]
    
- [[Performance-Rules]]
    

Do not duplicate those rules here.

---

# Dependency Injection

The project uses **Zenject**.

Prefer injected dependencies over:

- `FindObjectOfType`;
    
- `FindAnyObjectByType`;
    
- service locators;
    
- new gameplay singletons;
    
- mutable global static state.
    

Do not use the Zenject container itself as a general service locator.

See: [[Dependency-Injection]]

---

# Configuration

Designer-facing gameplay data should normally use project configuration systems rather than hard-coded values.

Typical examples:

- ships;
    
- weapons;
    
- enemies;
    
- levels;
    
- waves;
    
- rewards;
    
- balance values.
    

Do not use shared ScriptableObjects as accidental mutable runtime state.

See:

- [[ScriptableObjects]]
    
- [[Data-And-Configs]]
    
- [[Runtime-State]]
    

---

# Unity Workflow

Use the running Unity Editor as the primary Editor instance.

Use Unity CLI / Unity Pipeline for Unity-owned state such as:

- scenes;
    
- prefabs;
    
- GameObjects;
    
- components;
    
- ScriptableObjects;
    
- serialized references;
    
- AssetDatabase operations;
    
- import settings.
    

Plain C# source can normally be edited directly.

A temporary Pipeline disconnect during compilation or domain reload is not automatically an Editor failure.

See: [[Unity-Workflow]]

---

# Unity Safety

Do not modify generated Unity folders such as:

- `Library/`
    
- `Temp/`
    
- `obj/`
    
- package caches.
    

Do not manually edit Unity YAML unless a safer Editor/API path is unavailable.

Preserve:

- GUIDs;
    
- fileIDs;
    
- serialized references;
    
- existing assets.
    

Do not delete project assets unless explicitly requested.

---

# Audio

The project uses **FMOD**.

Gameplay should express audio intent without spreading FMOD implementation details through unrelated systems.

Do not make gameplay correctness depend on successful audio playback.

See:

- [[Audio-System]]
    
- [[FMOD]]
    

---

# Performance

Project: Reforge is a mobile bullet hell game.

Be especially careful with high-volume systems such as:

- projectiles;
    
- enemies;
    
- targeting;
    
- collisions;
    
- effects;
    
- frequently executed gameplay loops.
    

Avoid unnecessary allocations and repeated expensive searches in hot paths.

Do not introduce complexity for hypothetical performance problems.

See: [[Performance-Rules]]

---

# Editor and Runtime Consistency

Designer-facing systems should be editable and previewable where practical.

If a system has both Editor preview and runtime behaviour, they should follow the same conceptual rules.

Do not add runtime functionality while knowingly leaving the corresponding preview inconsistent.

---

# Save Compatibility

Treat persistent data as a compatibility boundary.

Be careful when modifying:

- IDs;
    
- serialized field names;
    
- save structures;
    
- inventory representation;
    
- progression data.
    

If expected migration behaviour is unclear, ask before making a breaking change.

See: [[Save-System]]

---

# Validation

After meaningful changes:

1. Allow Unity to refresh and compile.
    
2. Wait for compilation or import to finish.
    
3. Reconnect after domain reload if necessary.
    
4. Check for new relevant Unity errors.
    
5. Verify the resulting state.
    
6. Run required project build checks from `AGENTS.md`.
    

Compilation alone does not prove that the task is complete.

Verify behaviour or configuration where practical.

---

# Documentation Updates

When an approved change affects permanent project behaviour, update the appropriate source of truth.

Examples:

- gameplay rule → relevant game-design document;
    
- architecture rule → relevant technical document;
    
- major intentional decision → [[Design-Decisions]];
    
- unresolved issue → [[Open-Questions]].
    

Do not turn incidental implementation details into permanent design rules.

---

# Communication

After completing a meaningful task, report:

- what changed;
    
- where it changed;
    
- how it now works;
    
- what was verified;
    
- any required Inspector setup;
    
- important assumptions or unresolved issues.
    

Do not claim something was tested when it was only inferred from code.

---

# Guiding Principles

When choosing between reasonable implementations, prefer:

- explicit over hidden;
    
- simple over unnecessarily abstract;
    
- composition over unnecessary inheritance;
    
- dependency injection over global access;
    
- configuration over hard-coded gameplay rules;
    
- clear ownership over shared mutable state;
    
- existing project conventions over personal preference;
    
- clarification over invented requirements.
    

---

# Related Documentation

## Behaviour

- [[Home]]
    
- [[Design-Decisions]]
    
- [[Open-Questions]]
    

## Architecture

- [[Architecture]]
    
- [[System-Boundaries]]
    
- [[Dependency-Injection]]
    
- [[Runtime-State]]
    
- [[Object-Lifecycle]]
    

## Code

- [[Code-Style]]
    
- [[OOP-Principles]]
    
- [[SOLID]]
    
- [[Anti-Patterns]]
    
- [[Performance-Rules]]
    

## Unity

- [[Unity-Architecture]]
    
- [[Data-And-Configs]]
    
- [[Unity-Workflow]]
    

## Tools

- [[Codebase-Memory]]
    

## Systems

- [[Save-System]]
    
- [[Audio-System]]
    
- [[FMOD]]
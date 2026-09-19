# Project: Reforge

> Mobile 2D top-down bullet hell shoot 'em up focused on ship construction, weapon combinations, build variety and long-term progression.

## Overview

**Project: Reforge** is a mobile top-down bullet hell shoot 'em up where the player builds and configures ships, combines different weapons, fights through enemy waves and dense projectile patterns, and develops their collection between runs.

The game combines:

- bullet hell combat;
    
- ship construction and switching;
    
- weapon customization;
    
- active and passive ship abilities;
    
- different damage types;
    
- persistent progression;
    
- crafting and collection systems.
    

Combat should remain readable and controllable on a small mobile screen even with many enemies, projectiles and effects.

---

# Project

High-level vision, terminology and project-wide decisions.

- [[Vision]]
    
- [[Core-Pillars]]
    
- [[Terminology]]
    
- [[Design-Decisions]]
    
- [[Open-Questions]]
    

---

# Core Gameplay

Fundamental gameplay rules and player experience.

- [[Core Pillars]]
    
- [[Core-Gameplay]]
    
- [[Game-Loop]]
    
- [[Combat]]
    
- [[Player-Controls]]
    
- [[Ship-Switching]]
    
- [[Damage-System]]
    
- [[Buffs]]
    
- [[Difficulty]]

`[[Combat]]` is the main document for bullet hell behaviour, projectile pressure, dodging and combat readability.

---

# Ships

Rules governing player ships, their abilities and construction.

- [[Ships]]
    
- [[Ship-Stats]]
    
- [[Ship-Abilities]]
    
- [[Ship-Construction]]
    
- [[Weapon-Mounts]]
    
- [[Ship-Rarities]]
    

Individual ships:

`02_Ships/Catalogue/`

---

# Weapons

Weapon systems, restrictions and interactions.

- [[Weapons-Definition]]
    
- [[Weapon-Rules]]
    
- [[Damage-Types]]
    
- [[Weapon-Ranks]]
    
- [[Weapon-Rarities]]
    
- [[Weapon-Energy]]
    
- [[Duplicate-Weapons]]
    

Individual weapons:

`03_Weapons/Catalogue/`

---

# Enemies

Enemy roles, behaviour and combat patterns.

- [[Enemies]]
    
- [[Enemy-Stats]]
    
- [[Enemy-Archetypes]]
    
- [[Enemy-Movement]]
    
- [[Enemy-Attacks]]
    
- [[Enemy-Shields]]
    
- [[Enemy-Drops]]
    
- [[Bosses]]
    

Individual enemies:

`04_Enemies/Catalogue/`

Bosses:

`04_Enemies/Bosses/`

---

# Levels

Level structure and encounter construction.

- [[Levels]]
    
- [[Level-Structure]]
    
- [[Waves]]
    
- [[Subwaves]]
    
- [[Formations]]
    
- [[Difficulty-Variants]]
    
- [[Level-Rewards]]
    

Individual levels:

`05_Levels/Catalogue/`

---

# Progression

Persistent player and equipment development.

- [[Progression]]
    
- [[Player-Progression]]
    
- [[Ship-Progression]]
    
- [[Weapon-Progression]]
    
- [[Unlocks]]
    
- [[Crafting]]
    
- [[Upgrade-System]]
    

---

# Economy

Currencies, rewards and resource flow.

- [[Economy]]
    
- [[Currencies]]
    
- [[Gold]]
    
- [[Metal]]
    
- [[Cores]]
    
- [[Premium-Currency]]
    
- [[Chests]]
    
- [[Shards]]
    
- [[Pity-System]]
    

Numerical economy tuning:

- [[Economy-Balance]]
    

---

# Game Modes

- [[Game-Modes]]
    
- [[Classic]]
    
- [[Endless]]
    
- [[Mutation]]
    

---

# UI / UX

Player-facing interfaces and navigation.

- [[UI-Overview]]
    
- [[Navigation]]
    
- [[Main-Menu]]
    
- [[Battle-UI]]
    
- [[Hangar]]
    
- [[Workshop]]
    
- [[Inventory]]
    
- [[Map]]
    
- [[UX-Rules]]
    

---

# Art

Visual identity and asset rules.

- [[Art-Direction]]
    
- [[Visual-Language]]
    
- [[Color-Palette]]
    
- [[Pixel-Art-Rules]]
    
- [[Ship-Art]]
    
- [[Enemy-Art]]
    
- [[Weapon-Art]]
    
- [[Effects]]
    
- [[UI-Art]]
    
- [[Art-References]]
    

---

# Audio

- [[Audio-Direction]]
    
- [[Music]]
    
- [[SFX]]
    
- [[Audio-Feedback]]
    

---

# Balance

Balance documents contain numerical tuning.

Gameplay documents describe **how a system works**.

Balance documents describe **which values it uses**.

- [[Balance]]
    
- [[Balance-Principles]]
    
- [[Damage-Balance]]
    
- [[Weapon-Balance]]
    
- [[Ship-Balance]]
    
- [[Enemy-Balance]]
    
- [[Level-Balance]]
    
- [[Economy-Balance]]
    
- [[Progression-Curves]]
    

---

# Technical Design

Technical documentation describes how the game is implemented.

- [[Technical-Overview]]
    
- [[Architecture]]
    

## Code

- [[Code-Style]]
    
- [[OOP-Principles]]
    
- [[SOLID]]
    
- [[Anti-Patterns]]
    
- [[Performance-Rules]]
    

## Architecture

- [[System-Boundaries]]
    
- [[Dependency-Injection]]
    
- [[Events-And-Messaging]]
    
- [[Runtime-State]]
    
- [[Object-Lifecycle]]
    
- [[Factories-And-Pooling]]
    

## Unity

- [[Unity-Architecture]]
    
- [[Scene-Structure]]
    
- [[MonoBehaviour-Rules]]
    
- [[ScriptableObjects]]
    
- [[Data-And-Configs]]
    
- [[Prefabs]]
    
- [[Editor-Tooling]]
    

## Technical Systems

- [[Save-System]]
    
- [[Audio-System]]
    
- [[UI-System]]
    
- [[Input-System]]
    
- [[Scene-Loading]]
    

## Integrations

- [[FMOD]]
    

## Development Tools

- [[Codebase-Memory]]
    
- [[Unity-Workflow]]
    

## AI

- [[AI-Agent-Workflow]]
    

The root `AGENTS.md` remains the mandatory instruction file for coding agents.

---

# Development

Current development state and planned releases.

- [[Current-State]]
    
- [[Roadmap]]
    
- [[Milestones]]
    
- [[Backlog]]
    
- [[Known-Issues]]
    
- [[Changelog]]
    

## Releases

Release scopes are stored in:

`15_Development/Releases/`

Current first-release target:

- [[Release-0]]
    

---

# Research

Research contains references and exploratory ideas.

Research is **not automatically approved game design**.

- [[Games]]
    
- [[Competitors]]
    
- [[Bullet-Hell-References]]
    
- [[Gameplay-References]]
    
- [[UI-References]]
    
- [[Art-References]]
    
- [[Audio-References]]
    
- [[Ideas]]
    

---

# Templates

Reusable documentation templates are stored in:

`90_Templates/`

- [[Ship-Template]]
    
- [[Weapon-Template]]
    
- [[Enemy-Template]]
    
- [[Boss-Template]]
    
- [[Level-Template]]
    
- [[Mechanic-Template]]
    
- [[Decision-Template]]
    
- [[Research-Template]]
    

---

# Archive

Deprecated or replaced documentation is stored in:

`99_Archive/`

Archived documents are not considered current sources of truth.

---

# Documentation Rules

Each rule or system should have one primary source of truth.

Avoid duplicating detailed rules across multiple documents. Link to the responsible document instead.

Use:

- `TBD` — value or behaviour has not been decided;
    
- `UNDECIDED` — an explicit design decision is still unresolved;
    
- `OPEN QUESTION` — clarification or discussion is required.
    

Do not invent missing project rules.

If documentation is missing, ambiguous or contradictory and the answer materially affects implementation or design, ask for clarification.

See:

- [[AI-Agent-Workflow]]
    
- [[Open-Questions]]
    

---

# Main Entry Points

| Topic                     | Document                 |
| ------------------------- | ------------------------ |
| What is the game?         | [[Vision]]               |
| Design principles         | [[Core-Pillars]]         |
| Core gameplay             | [[Core-Gameplay]]        |
| Bullet hell combat        | [[Combat]]               |
| Ships                     | [[Ships]]                |
| Weapons                   | [[Weapons-Definition]]              |
| Damage types              | [[Damage-Types]]         |
| Enemies                   | [[Enemies]]              |
| Levels and waves          | [[Levels]] / [[Waves]]   |
| Progression               | [[Progression]]          |
| Economy                   | [[Economy]]              |
| Game modes                | [[Game-Modes]]           |
| UI / UX                   | [[UI-Overview]]          |
| Art direction             | [[Art-Direction]]        |
| Audio                     | [[Audio-Direction]]      |
| Balance                   | [[Balance]]              |
| Architecture              | [[Architecture]]         |
| Code style                | [[Code-Style]]           |
| Zenject / DI              | [[Dependency-Injection]] |
| FMOD                      | [[FMOD]]                 |
| Codebase Memory           | [[Codebase-Memory]]      |
| Unity workflow            | [[Unity-Workflow]]       |
| AI behaviour              | [[AI-Agent-Workflow]]    |
| First release             | [[Release-0]]            |
| Current development state | [[Current-State]]        |
| Design decisions          | [[Design-Decisions]]     |
| Unresolved questions      | [[Open-Questions]]       |
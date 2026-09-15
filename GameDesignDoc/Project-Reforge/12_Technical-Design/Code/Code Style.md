## Purpose

This document defines the C# coding conventions used in **Project: Reforge**.

These rules apply to project-owned code located in:

- `Assets/Scripts`
    
- `Assets/Editor`
    

These rules do **not** apply to third-party or vendor code located in:

- `Assets/Plugins`
    

Do not rewrite or reformat third-party package code merely to match this project's style.

This document describes how project code should be written and structured.

Architectural rules are documented separately.

See:

- [[Architecture]]
    
- [[OOP-Principles]]
    
- [[SOLID]]
    
- [[Anti-Patterns]]
    
- [[Dependency-Injection]]
    
- [[Performance-Rules]]
    

---

# General Principles

Prefer:

- readable code over clever code;
    
- simple control flow;
    
- explicit intent;
    
- small focused methods;
    
- stable public APIs;
    
- consistency with existing project code.
    

Do not rewrite working code only to match a personal style preference.

---

# Naming

Use clear names based on project terminology.

Avoid vague names such as:

```text
data
value
temp
manager2
obj
thing
```

Prefer:

```text
currentShield
weaponEnergyCost
targetEnemy
shieldRegenerationRate
```

See: [[Terminology]]

---

## Classes

Use `PascalCase`.

```csharp
WeaponController
EnemyManager
ShipRuntimeState
```

Class names should describe responsibility.

Avoid overly generic names such as:

```text
Helper
Utils
Controller
Manager
```

unless their domain is clear.

Prefer:

```text
EnemyManager
DamageCalculator
SavePathUtility
```

---

## Interfaces

Use the `I` prefix.

```csharp
IDamageable
ITargetable
ISaveService
```

Interfaces should describe a meaningful contract.

Do not create an interface only because every class "should have one".

---

## Methods

Use `PascalCase`.

```csharp
TakeDamage()
SpawnEnemy()
CalculateDamage()
```

Method names should describe actions.

Boolean-returning methods should read naturally:

```csharp
CanFire()
IsAlive()
HasEnoughEnergy()
```

---

## Private Fields

Private fields must use `lowerCamelCase`.

Do **not** prefix private fields with `_`.

Preferred:

```csharp
private float currentHealth;
private WeaponConfig weaponConfig;
private EnemyManager enemyManager;
```

Avoid:

```csharp
private float _currentHealth;
private WeaponConfig _weaponConfig;
```

This rule also applies to private serialized and injected fields.

---

## Serialized Fields

Private serialized fields use `lowerCamelCase`.

Prefer:

```csharp
[SerializeField]
private float fireRate;

[SerializeField]
private WeaponConfig weaponConfig;
```

over:

```csharp
public float fireRate;
```

Do not rename serialized fields without considering migration.

When necessary, use Unity migration tools such as `FormerlySerializedAs`.

---

## Injected Fields

Private injected fields also use `lowerCamelCase`.

Example:

```csharp
[Inject]
private EnemyManager enemyManager;
```

When constructor injection is used:

```csharp
private readonly EnemyManager enemyManager;

public WeaponController(EnemyManager enemyManager)
{
    this.enemyManager = enemyManager;
}
```

See: [[Dependency-Injection]]

---

## Properties

Use `PascalCase`.

```csharp
public float CurrentHealth => currentHealth;
public bool IsAlive => currentHealth > 0f;
```

Prefer read-only public access when external mutation is not required.

Avoid exposing setters without a clear reason.

---

## Constants

Use `PascalCase`.

```csharp
private const int MaxWeaponSlots = 6;
```

Do not use constants for designer-controlled balance values.

Those values belong in configuration.

---

## Enums

Use `PascalCase` for enum names and values.

```csharp
public enum DamageType
{
    Kinetic,
    Plasma,
    Electric
}
```

Avoid embedding behaviour into numeric enum values unless intentionally designed.

---

# Class Structure

Keep related members grouped consistently.

Recommended order:

```text
Constants

Serialized fields

Injected dependencies

Private fields

Properties

Unity lifecycle methods

Public methods

Private methods

Event handlers
```

Exact ordering may follow existing local style if the file already has a clear convention.

---

# Methods

Methods should have one clear purpose.

Prefer extracting logic when a method becomes difficult to understand.

Avoid very large methods that mix:

- validation;
    
- gameplay logic;
    
- UI updates;
    
- audio;
    
- persistence.
    

Method extraction should improve readability, not simply increase method count.

---

# Early Returns

Prefer early returns when they reduce nesting.

Preferred:

```csharp
private void Fire()
{
    if (!canFire)
        return;

    if (target == null)
        return;

    SpawnProjectile();
}
```

Avoid unnecessary nesting:

```csharp
private void Fire()
{
    if (canFire)
    {
        if (target != null)
        {
            SpawnProjectile();
        }
    }
}
```

---

# Conditionals

Keep conditions readable.

Extract complex conditions into clearly named variables or methods when useful.

Prefer:

```csharp
bool canFire = cooldown <= 0f && energy >= energyCost;

if (!canFire)
    return;
```

over long unreadable expressions repeated across the method.

---

# Braces

Follow the existing project formatting convention.

Do not mix formatting styles inside the same file.

For simple guard clauses, single-line bodies are acceptable when readability remains clear.

---

# `var`

Use `var` when the type is obvious from the right-hand side.

Good:

```csharp
var weapon = new WeaponRuntime(config);
var target = FindTarget();
```

Prefer explicit types when they improve readability.

```csharp
IWeaponTarget target = GetCurrentTarget();
```

Do not use `var` when it hides important type information.

---

# Collections

Choose collections based on behaviour rather than convenience.

Avoid unnecessary conversions between:

```text
Array
List
IEnumerable
HashSet
```

in frequently executed code.

Expose the smallest useful collection interface when appropriate.

Do not expose mutable internal collections without a reason.

---

# LINQ

LINQ is acceptable for:

- Editor tooling;
    
- initialization;
    
- infrequent operations;
    
- code where readability matters more than negligible cost.
    

Avoid LINQ in hot runtime paths such as:

- `Update`;
    
- `FixedUpdate`;
    
- projectile loops;
    
- enemy loops;
    
- frequent targeting;
    
- combat calculations.
    

See: [[Performance-Rules]]

---

# Null Handling

Use null checks when absence is valid.

Do not silently ignore missing required configuration.

Bad:

```csharp
if (weaponConfig == null)
    return;
```

when `weaponConfig` is mandatory.

Required dependencies should be validated clearly.

Avoid excessive defensive null checks that hide broken setup.

---

# Exceptions

Exceptions should represent exceptional situations.

Do not use exceptions for normal gameplay flow.

Do not swallow exceptions silently.

Avoid:

```csharp
try
{
    ...
}
catch
{
}
```

If an exception is handled, preserve enough context to understand the failure.

---

# Logging

Logs should provide useful diagnostic information.

Prefer:

```csharp
Debug.LogError($"Weapon config is missing for {name}");
```

over:

```csharp
Debug.LogError("Error");
```

Avoid excessive logging in frequently executed gameplay paths.

Temporary debug logs should be removed when no longer needed.

---

# Comments

Comments should explain **why**, not restate **what** the code already says.

Avoid:

```csharp
// Damage enemy
enemy.TakeDamage(damage);
```

Useful:

```csharp
// Shield bypass is applied before resistance so Spray damage
// reaches hull regardless of remaining shield.
```

Keep comments synchronized with code.

Outdated comments are worse than no comments.

---

# XML Documentation

XML documentation is not required for every method.

Use it when documenting:

- public APIs;
    
- complex reusable systems;
    
- non-obvious contracts;
    
- Editor tooling exposed to other systems.
    

Avoid documentation noise on obvious private methods.

---

# Events

Use clear event names describing what happened.

Examples:

```csharp
OnEnemyKilled
OnDamageDealt
OnShipChanged
```

Event handlers should normally use a clear `Handle...` naming pattern.

```csharp
private void HandleEnemyKilled(...)
```

Subscriptions and unsubscriptions must have matching lifetimes.

See: [[Events-And-Messaging]]

---

# Async Code

The project uses **UniTask** where asynchronous behaviour is required.

Prefer UniTask over introducing separate asynchronous patterns without reason.

Async methods should make cancellation and object lifetime clear.

Avoid fire-and-forget tasks unless intentionally designed and safely handled.

Do not allow async work to continue using destroyed Unity objects.

---

# Unity Lifecycle

Keep Unity lifecycle methods small.

Avoid placing large systems directly inside:

```csharp
Awake()
Start()
Update()
FixedUpdate()
```

Use them primarily to delegate to focused logic.

Do not rely on accidental execution order between unrelated MonoBehaviours.

See:

- [[MonoBehaviour-Rules]]
    
- [[Object-Lifecycle]]
    

---

# Update Methods

Avoid unnecessary work every frame.

Do not perform repeated:

- scene searches;
    
- allocations;
    
- configuration lookups;
    
- component discovery;
    

inside `Update` when the result can be cached or event-driven.

---

# Serialized Data

Keep serialized field names stable.

Do not change:

- serialized field names;
    
- enum values used by persistent data;
    
- IDs;
    
- serialized structures;
    

without checking compatibility.

See:

- [[Data-And-Configs]]
    
- [[Save-System]]
    

---

# Magic Values

Avoid unexplained gameplay values inside code.

Avoid:

```csharp
damage *= 1.25f;
```

when `1.25f` is a balance value.

Prefer named configuration:

```csharp
damage *= config.ShieldDamageMultiplier;
```

Small implementation constants are acceptable when they are genuinely implementation details.

---

# Boolean Parameters

Avoid methods with many boolean parameters.

Bad:

```csharp
Fire(true, false, true, false);
```

If behaviour has several independent options, use:

- configuration;
    
- dedicated parameters;
    
- small value objects;
    
- separate behaviours.
    

---

# Regions

Avoid excessive `#region` usage.

Regions should not be used to hide classes that have become too large.

A class requiring many large regions may need responsibility separation.

---

# Access Modifiers

Use the narrowest practical visibility.

Prefer:

```text
private
```

unless another system genuinely requires access.

Use `public` as an API decision, not for convenience.

---

# Static Code

Static pure functions are acceptable.

Mutable static gameplay state is discouraged.

Good:

```csharp
DamageMath.Calculate(...)
```

Avoid:

```csharp
public static int CurrentGold;
```

See: [[Anti-Patterns]]

---

# Performance-Sensitive Code

Readable code remains the default.

For clearly hot systems, prioritize predictable runtime cost.

Typical hot systems include:

- projectiles;
    
- enemies;
    
- targeting;
    
- collision checks;
    
- combat calculations.
    

Performance-specific rules are documented in:

[[Performance-Rules]]

---

# Existing Code

When modifying project-owned code in:

- `Assets/Scripts`
    
- `Assets/Editor`
    

follow these conventions where practical.

Also:

- keep diffs focused;
    
- avoid unrelated formatting changes;
    
- avoid renaming unrelated members;
    
- preserve public APIs unless change is required.
    

Do not turn a small feature change into a whole-file style rewrite.

For third-party code under:

`Assets/Plugins`

do not apply project style rules or perform cosmetic refactoring unless modification of that package is explicitly required.

---

# Related Documentation

- [[Architecture]]
    
- [[OOP-Principles]]
    
- [[SOLID]]
    
- [[Anti-Patterns]]
    
- [[Performance-Rules]]
    
- [[Dependency-Injection]]
    
- [[Events-And-Messaging]]
    
- [[Data-And-Configs]]
    
- [[MonoBehaviour-Rules]]
    
- [[AI-Agent-Workflow]]
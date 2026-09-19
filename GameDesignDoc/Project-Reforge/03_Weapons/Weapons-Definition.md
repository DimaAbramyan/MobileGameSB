
## Purpose

This document defines the general weapon design rules and lists the weapons available in the game.

Related systems:

- [[Weapon-Types]]
    
- [[Weapon-Effects]]
    
- [[Damage-Types]]
    
- [[Combat-Glossary]]
    

---

## Core Rules

### Weapon Identity

Every weapon should have a clear gameplay identity.

Weapons should not differ only through:

- Damage
    
- Fire Rate
    
- Projectile Speed
    
- Range
    

A weapon should introduce a meaningful difference in targeting, projectile behavior, area coverage, timing, positioning, or interaction with enemies.

Also weapon shouldn't make a random attack pattern, or spread. Weapon should have repetable pattern attack, which could easily read.

---

### Damage Type

Every weapon has one Damage Type defined in [[Damage-Types]].

Current Damage Types:

- Kinetic
    
- Explosion
    
- Chemical
    
- Plasma
    
- Microwave
    
- Electric
    

---

### Weapon Type

Every weapon has a primary Weapon Type defined in [[Weapon-Types]].

Examples:

- Projectile
    
- Beam
    
- Spray
    
- Launcher
    
- Field
    
- Orbiting
    
- Drone
    

---

### Weapon Effects

Additional behavior is defined through [[Weapon-Effects]].

Examples:

- Pierce
    
- Bounce
    
- Homing
    
- Explosion
    
- Chain
    
- Burn
    
- Slow
    
- Damage over Time
    

A Weapon Effect is not a separate Damage Type.

---

## Weapon Ranks

Weapons are divided into three ranks:

| Rank | Class  |
| ---- | ------ |
| I    | Light  |
| II   | Medium |
| III  | Heavy  |

Higher-rank weapons may have:

- stronger or more complex mechanics;
    
- larger attack areas;
    
- greater damage potential.
    

Rank does not automatically mean that a weapon is strictly better in every situation.

---

## Weapon Mounts

Player ships can have multiple weapon mounts.

Different ships may support different numbers of mounts.

Weapons consume Energy depending on their Rank and properties.

The total weapon loadout must remain within the ship's available Energy capacity.

---

## Duplicate Weapons

Equipping multiple copies of the same weapon may improve its performance.

Every stacking improves weapons firerate.

Duplicate stacking should preserve the original weapon's gameplay identity.

---

## Bullet Hell Readability

Player weapons must remain visually distinguishable from enemy attacks.

Player attacks should not:

- hide dangerous enemy projectiles;
    
- cover large portions of the screen with opaque effects;
    
- create unnecessary particle clutter;
    
- make the player's hitbox difficult to track.
    

See [[Core-Gameplay#Bullet Hell]].

---


---


# Related Documents

- [[Weapon-Types]] — weapon delivery methods.
    
- [[Weapon-Effects]] — reusable weapon mechanics.
    
- [[Damage-Types]] — Shield and Armor damage modifiers.
    
- [[Combat-Glossary]] — combat terminology.
    
- [[Core-Gameplay]] — general combat and Bullet Hell rules.
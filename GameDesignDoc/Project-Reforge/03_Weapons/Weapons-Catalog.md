# Weapon typization

Every weapon entry should use the following structure:

```text
## Weapon Name

**Rank:**
**Rarity**  
**Weapon Type:**  
**Damage Type:**  
**Effects:**  

Short description of the weapon and its defining mechanic.
```

| Rarity\Damage Type | Kinetic                         | Explosion                                                         | Plasma                                                             | Resonance                 | Electric                              | Chemical                            |
| ------------------ | ------------------------------- | ----------------------------------------------------------------- | ------------------------------------------------------------------ | ------------------------- | ------------------------------------- | ----------------------------------- |
| Common             | [[#MiniGun\|MiniGun]]           | [[#Piercing Explosive Projectile\|Piercing Explosive Projectile]] | [[#PlasmaGun\|PlasmaGun]], [[#Continuous Laser\|Continuous Laser]] | [[#Microwave\|Microwave]] |                                       |                                     |
| Rare               | [[#Drill]]                      | [[#Rocket Launcher\|Rocket Launcher]]                             |                                                                    |                           | [[#Ball Lightning\|Ball Lightning]]   | [[#FlameThrower\|FlameThrower]]     |
| Epic               | [[#Circular Saw\|Circular Saw]] |                                                                   | [[#Heating Beam\|Heating Beam]]                                    | Resonance Sphere?         | [[#Chain Lightning\|Chain Lightning]] | [[#Freeze Sprayer\|Freeze Sprayer]] |
| Legendary          |                                 |                                                                   | [[#Q-Beam\|Q-Beam]]                                                |                           | [[#Arc Nodes\|Arc Nodes]]             | [[#Acid Sprayer\|Acid Sprayer]]     |

# Weapon Roster

## Kinetic

### MiniGun
**Rarity:**  Common

**Tier:** 1

**Weapon Type:** Projectile

**Damage Type:** Kinetic

Rapidly fires physical projectiles at enemies.

---

### Circular Saw
**Rarity:**  Epic

**Tier:** 2

**Weapon Type:** Projectile

**Damage Type:** Kinetic

**Effects:** Pierce, Bounce

Fires a spinning saw that can pass through enemies and bounce before disappearing.

---

### Boomerang (not done yet)
**Rarity:**  Epic

**Tier:** 2

**Weapon Type:** Projectile

**Damage Type:** Kinetic

Fires a projectile that travels forward and then returns toward the player.

---
### Drill 
**Rarity:**  Rare

**Tier:** 1

**Weapon Type:** Meele

**Damage Type:** Kinetic

On each attack decrease enemy's protection of armor. Damage incrieses multiple times by ddecreasing armor

---

## Explosion

### Rocket Launcher

**Rarity:**  Rare

**Tier:** 2

**Weapon Type:** Launcher

**Damage Type:** Explosion

**Effects:** Explosion

Fires homing missile by volley that explode on impact and damage enemies in an area.

---

### Piercing Explosive Projectile

**Rarity:**  Rare

**Tier:** 1

**Weapon Type:** Projectile

**Damage Type:** Explosion

**Effects:** Pierce, Explosion

Passes through a target and detonates behind it.

---

## Chemical

**Rarity:**  Rare

**Tier:** 1
### FlameThrower

**Weapon Type:** Spray

**Damage Type:** Chemical

Sprays fire that bypass Shield and damage Armor directly. Incrieses enemy's heat, effect accumulates, if bypases 100 degrees, enemy becomes is set on fire, and get periodical damage.

---

### Freeze Sprayer

**Rarity:**  Epic

**Tier:** 2

**Weapon Type:** Spray

**Damage Type:** Chemical

Sprays cryogen that bypass Shield and damage Armor directly. Slows enemy's movespeed, effect accumulates.

---

### Acid Sprayer

**Rarity:**  Legendary

**Tier:** 2

**Weapon Type:** Spray

**Damage Type:** Chemical

Sprays corrosive chemicals that bypass Shield and damage Armor directly. Incrieses damage taken in armor, effect accumulates.

---

## Plasma

### PlasmaGun


**Rarity:**  Common

**Tier:** 1

**Weapon Type:** Projectile

**Damage Type:** Plasma

Fires a multiple projectiles in a fan shape.

---

### Continuous Laser

**Rarity:**  Common

**Tier:** 1

**Weapon Type:** Beam

**Damage Type:** Plasma

Maintains a continuous beam against a single target.

---

### Heating Beam

**Rarity:**  Epic

**Tier:** 1

**Weapon Type:** Beam

**Damage Type:** Plasma

Continuously heats a target.

The weapon may interact with additional heat-related effects defined in [[Weapon-Effects]].

---

### Q-Beam

**Rarity:**  Legendary

**Tier:** 2

**Weapon Type:** Beam

**Damage Type:** Plasma

Accumulates a charge, if the charge damages the enemy's health, he immediately dies.

---

## Microwave

**Weapon Type:** Area

**Damage Type:** Microwave

---

## Electric

###  Arc Nodes

**Weapon Type:** Projectile

**Damage Type:** Electric

Tier:3

Fires projectiles that deal no damage on impact and remain active for a limited time.

Every few seconds, nearby projectiles connect to each other with electric arcs. Enemies intersecting these arcs take Electric damage.

The projectiles themselves are harmless — all damage comes from the electrical connections created between them.

---

### Chain Lightning

**Weapon Type:** Beam

**Damage Type:** Electric

**Effects:** Chain

Hits one enemy and then jumps between nearby targets.

---

### Ball Lightning

**Weapon Type:** Projectile

**Damage Type:** Electric

Fires a slow-moving electrical sphere that periodically attacks nearby enemies.

---
### ???

**Weapon Type:** ???

**Damage Type:** ???

Whenever enemy dies by this effect, spread all debuffs from enemy he killed all nearby enemyes

---

# Design Status

Weapon entries in this document may be in one of three states:

- **Concept** — general mechanic exists but has not been fully designed.
    
- **Designed** — gameplay behavior is defined.
    
- **Implemented** — weapon exists in the game.
    

Unfinished weapon concepts should remain in this document until their core gameplay mechanic is defined.
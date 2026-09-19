
This document defines the main combat flow and the interaction between the core gameplay systems.

High-level design principles are described in [[Core-Pillars]].

Detailed implementations of individual systems should remain in their dedicated documents:

- [[Damage-System]]
    
- [[Bodies]]
    
- [[Weapons-Definition]]
    
- [[Enemies]]
    
- [[UI-UX]]
    
- [[Game-Modes]]
    
- [[Progression]]
    
- [[Economy]]
    

---

## 1. Core Combat Fantasy

The player controls a pair of combat ships in a top-down shooter with bullet-hell elements.

Combat is built around three simultaneous activities:

- Movement and projectile avoidance
    
- Offensive target management
    
- Tactical switching between two ships
    

The player should not simply survive while holding the fire button.

Good combat should repeatedly create situations where the player must decide:

- Which enemy should be attacked first?
    
- Which ship is more effective right now?
    
- Should an ability be used immediately or saved?
    
- Is it worth moving into a dangerous position to deal more damage?
    
- Should the current ship remain active or should the player switch?
    

The main source of depth should come from interactions between these systems rather than from individually complicated mechanics.

See [[Core-Pillars#Complexity Should Come From Interactions]].

---

## 2. Core Combat Loop

The moment-to-moment combat loop is:

1. Read the current enemy situation.
    
2. Move through incoming attack patterns.
    
3. Select or naturally acquire priority targets.
    
4. Deal damage using the active ship's weapons.
    
5. Evaluate enemy Shield and Armor state.
    
6. Switch ships when another loadout becomes more effective.
    
7. Use active abilities when they provide a meaningful tactical advantage.
    
8. Destroy enemies.
    
9. Collect combat resources and drops when appropriate.
    
10. Adapt to newly spawned enemies and attack patterns.
    

This loop should repeat continuously throughout an encounter.

The player should regularly transition between:

`Dodging → Attacking → Evaluating → Switching → Exploiting an opening`

Combat should not remain in only one of these states for long periods.

---

## 3. Player Movement

The player directly controls the active ship's movement.

Movement is one of the primary defensive mechanics.

The player avoids:

- Enemy projectiles
    
- Contact threats
    
- Area attacks
    
- Environmental hazards
    
- Enemy movement patterns
    

Movement should remain responsive and predictable.

The player should feel that taking damage is generally the result of:

- Incorrect positioning
    
- Late reaction
    
- Poor route selection
    
- Excessive aggression
    
- Failure to read an attack pattern
    

Movement should not feel unreliable because of excessive inertia, unclear collision boundaries, or unreadable effects.

Exact movement parameters belong to the gameplay implementation rather than this document.

---

## 4. Two-Ship Combat System

The player enters combat with two ships.

Only one ship is actively controlled at a time.

The inactive ship remains part of the player's combat setup and can be switched in during gameplay.

The two ships should be treated as one tactical loadout rather than as two independent lives.

Each ship may differ through:

- Body
    
- Weapons
    
- Damage specialization
    
- Active ability
    
- Passive ability
    
- Survivability
    
- Utility
    
- Interaction with enemies
    

Detailed body mechanics are described in [[Bodies]].

Detailed weapon mechanics are described in [[Weapons-Definition]].

---

## 5. Ship Switching

Ship switching is one of the central gameplay mechanics.

Its purpose is not simply to provide access to another health pool.

Switching should create an immediate tactical change.

Possible reasons to switch include:

- The enemy's Shield has been removed.
    
- The second ship deals more effective Armor damage.
    
- A different weapon type is better against the current enemy group.
    
- The active ship is poorly suited to the current attack pattern.
    
- The second ship's ability is needed.
    
- The current ship needs time away from danger.
    
- A passive or temporary effect creates a better opportunity for the other ship.
    

A successful combat encounter should naturally create multiple situations where switching is useful.

If the optimal strategy is to remain on one ship until it is nearly destroyed and only then switch, the system is not fulfilling its intended role.

See [[Core-Pillars#Ship Switching Must Be a Tactical Decision]].

---

## 6. Ship Synergy

The two ships should be able to complement each other.

Synergy may be created through:

- Shield/Armor specialization
    
- Single-target and crowd-control roles
    
- Ability combinations
    
- Different effective ranges
    
- Defensive and offensive roles
    
- Weapon-type bonuses
    
- Temporary effects
    
- Enemy-state interactions
    

Example:

1. Ship A specializes in Shield damage.
    
2. The player removes the enemy Shield.
    
3. The player switches to Ship B.
    
4. Ship B uses a Armor-focused weapon and an offensive ability.
    
5. The enemy is destroyed before its Shield can recover.
    

The game should encourage this type of interaction without forcing every pair of ships into one predefined combination.

---

## 7. Shield and Armor

Enemies may use two primary defensive layers:

- Shield
    
- Armor
    

These layers exist to create tactical differences between weapons and ships.

They should not function as two visually different health bars with identical gameplay.

Different damage types and weapons may have different effectiveness against each layer.

For example:

- A weapon may be highly effective against Shields but inefficient against Armor.
    
- Another weapon may become valuable after the Shield is removed.
    
- Some mechanics may bypass or partially ignore a defensive layer.
    

The complete damage model is defined in [[Damage-System]].

The player should be able to understand the enemy's defensive state without carefully reading numerical values.

Relevant visual rules belong to [[UI-UX]] and [[Art-Direction]].

---

## 8. Weapons

Weapons define the primary offensive behavior of a ship.

Weapons should differ through mechanics rather than only through damage values.

Possible distinctions include:

- Damage type
    
- Shield/Armor effectiveness
    
- Fire rate
    
- Projectile behavior
    
- Range
    
- Piercing
    
- Area damage
    
- Target count
    
- Crowd control
    
- Positioning requirements
    
- Energy consumption
    

The player should be able to recognize the role of a weapon through gameplay.

Detailed weapon families, ranks, rarity, energy costs, and individual mechanics are defined in [[Weapons-Definition]].

---

## 9. Firing

Combat is designed around frequent weapon use.

The player should spend most combat encounters actively dealing damage rather than waiting for short attack windows.

However, attacking should not remove the importance of positioning and survival.

Some weapons may encourage:

- Continuous fire
    
- Burst damage
    
- Close-range aggression
    
- Maintaining distance
    
- Lining up multiple enemies
    
- Waiting for a specific enemy state
    
- Repositioning before firing
    

Player projectiles must remain visually subordinate to enemy threats.

See [[Core-Pillars#Player Firepower Must Not Hide Enemy Threats]].

---

## 10. Abilities

Bodies may provide active and passive abilities.

Abilities should reinforce the identity and role of a ship.

An active ability should usually create one of the following:

- A temporary offensive advantage
    
- A defensive window
    
- A positioning opportunity
    
- Crowd control
    
- A weapon interaction
    
- A ship-switching opportunity
    

Passive abilities should modify how the player approaches combat rather than serving only as generic stat bonuses.

For example, a passive may reward:

- Staying at low health
    
- Killing enemies
    
- Using a specific weapon family
    
- Switching ships
    
- Staying close to enemies
    
- Maintaining a specific combat state
    

Detailed abilities belong to [[Bodies]].

---

## 11. Bullet-Hell Gameplay

The game uses bullet-hell elements, but it is not purely a projectile-dodging game.

Enemy attack patterns exist to:

- Force movement
    
- Restrict safe areas
    
- Create positioning decisions
    
- Interrupt sustained aggression
    
- Create openings
    
- Increase pressure
    

Projectile density must leave enough mental space for the player to also think about:

- Targets
    
- Ship switching
    
- Shield/Armor state
    
- Abilities
    
- Resource collection
    

Bullet patterns should therefore support tactical gameplay rather than replace it.

Detailed readability principles are defined in [[Core-Pillars#Bullet Hell Must Remain Readable]].

Enemy attack design belongs to [[Enemies]].

---

## 12. Combat Rhythm

Combat should alternate between different pressure levels.

A typical encounter may contain:

### Low Pressure

The player has space to reposition, attack, and evaluate the situation.

### Medium Pressure

Multiple enemies or projectile patterns require active movement while the player continues attacking.

### High Pressure

The player must prioritize survival, make fast decisions, and potentially switch ships or use an ability.

### Opportunity Window

The enemy becomes vulnerable or the attack pattern creates a temporary opening for aggressive play.

Constant maximum pressure should be avoided.

Without contrast, intense moments stop feeling intense and tactical systems become harder to use.

---

## 13. Enemy Roles

Enemies should create different combat problems.

Possible roles include:

- Direct attacker
    
- Projectile-pattern generator
    
- Sniper
    
- Tank
    
- Shielded enemy
    
- Support unit
    
- Area denial enemy
    
- Fast melee/contact threat
    
- Swarm enemy
    

Enemy combinations should matter as much as individual enemy strength.

For example:

A durable Shielded enemy may not be dangerous alone, but it may protect a projectile-heavy enemy that forces the player to constantly move.

Detailed enemy archetypes are described in [[Enemies]].

---

## 14. Target Priority

The player should frequently have reasons to choose which enemy to eliminate first.

Target priority may depend on:

- Immediate danger
    
- Enemy role
    
- Defensive layer
    
- Position
    
- Current weapon setup
    
- Current active ship
    
- Available ability
    
- Potential rewards
    

Strong encounters should create meaningful target-order decisions.

Enemy health should not be the only factor determining priority.

---

## 15. Damage and Feedback

Combat actions should provide immediate feedback.

The player should understand:

- Whether an attack hit
    
- Whether the attack affected Shield or Armor
    
- Whether a weapon is effective against the target
    
- Whether an enemy is close to destruction
    
- Whether an ability activated correctly
    

Feedback may use:

- Hit effects
    
- Audio
    
- Enemy reactions
    
- Shield effects
    
- Armor damage effects
    
- UI feedback
    

Feedback must remain readable and should not generate excessive visual noise.

See [[UI-UX]] and [[Art-Direction]].

---

## 16. Pickups and Combat Resources

Enemies may drop resources during combat.

Collecting resources should introduce a small positioning decision.

The player may need to choose between:

- Remaining in a safe position
    
- Maintaining damage output
    
- Moving to collect a resource
    

Pickups should not require excessive backtracking or force the player into unavoidable damage.

The exact role of each resource is defined in [[Economy]].

Mode-specific drop rules belong to [[Game-Modes]].

---

## 17. Player Damage

The player takes damage when the active ship is hit by a valid enemy attack or hazard.

Taking damage should produce clear feedback without significantly obscuring gameplay.

The player should immediately understand:

- That damage was taken
    
- Which ship was damaged
    
- Its remaining survivability
    
- Whether switching ships is now desirable
    

Health presentation belongs to [[UI-UX]].

Body-specific survivability mechanics belong to [[Bodies]].

---

## 18. Ship Destruction

The destruction of one ship should significantly affect the player's tactical options.

Losing a ship means losing access to:

- Its weapons
    
- Its damage specialization
    
- Its active ability
    
- Its passive utility
    
- Its role within the two-ship combination
    

Because of this, preserving both ships should usually be strategically valuable.

Ship switching should therefore also function as a way to manage risk between the two ships.

The exact consequences of losing one or both ships may vary between game modes and are defined in [[Game-Modes]].

---

## 19. Encounter Structure

An encounter should introduce pressure gradually.

A basic encounter structure may follow:

`Introduction → Escalation → Combination → Peak → Recovery`

### Introduction

The player encounters a limited number of threats and can identify enemy behavior.

### Escalation

Additional enemies or mechanics are introduced.

### Combination

Different enemy roles begin interacting with each other.

### Peak

The encounter reaches its highest pressure.

### Recovery

Pressure decreases temporarily before the next encounter or wave.

This structure may be modified depending on the current game mode.

See [[Game-Modes]].

---

## 20. Difficulty

Difficulty should increase primarily through more demanding decisions rather than simple numerical inflation.

Possible difficulty increases include:

- More complex enemy combinations
    
- Faster attack patterns
    
- Less available safe space
    
- Additional enemy abilities
    
- More demanding positioning
    
- Greater importance of target priority
    
- More pressure to switch ships effectively
    

Enemy HP and damage may also scale, but numerical scaling should not be the only source of difficulty.

---

## 21. Combat Readability

The player should be able to understand the combat state quickly.

At any moment, the player should be able to identify:

- Their active ship
    
- Their approximate survivability
    
- Enemy threats
    
- Dangerous projectiles
    
- Priority targets
    
- Enemy Shield/Armor state
    
- Important pickups
    
- Ability availability
    

Combat readability takes priority over visual spectacle.

Detailed rules are defined in:

- [[Core-Pillars#Combat Must Remain Visually Readable]]
    
- [[Core-Pillars#The Interface Must Be Clear and Informative]]
    
- [[UI-UX]]
    
- [[Art-Direction]]
    

---

## 22. Core Gameplay Design Check

When adding or changing a combat mechanic, ask:

1. Does it create a meaningful decision?
    
2. Does it interact with the two-ship system?
    
3. Can it create a reason to switch ships?
    
4. Does it interact with Shield/Armor in a useful way?
    
5. Does it change positioning or target priority?
    
6. Can the player understand it during active combat?
    
7. Does it preserve bullet-hell readability?
    
8. Does it add gameplay depth rather than only numerical complexity?
    
9. Does it allow multiple viable approaches?
    
10. Does it support the principles defined in [[Core-Pillars]]?
    

If a mechanic does not meaningfully affect player decisions, its role in the combat system should be reconsidered.

This document defines the core design principles of the game.

Every new mechanic, weapon, body, ability, progression system, enemy, game mode, or interface decision should support these principles.

---

## 1. Ship Switching Must Be a Tactical Decision

The player controls two ships and can switch between them during combat.

Switching must not function only as access to a second health pool. It is one of the main tactical mechanics of the game.

Each ship should have its own combat role based on the combination of:

- Body
    
- Weapons
    
- Active ability
    
- Passive ability
    
- Stats
    
- Damage specialization
    
- Interaction with Shield and Armor
    

The `Shield / Armor system should create situations where switching ships is beneficial.

For example:

- One ship may specialize in destroying Shields.
    
- The second ship may specialize in dealing Armor damage.
    
- One ship may control groups of enemies while the other focuses on single targets.
    
- One ability may create an opportunity that the second ship can exploit.
    
- One ship may be safer in a specific enemy attack pattern.
    

A strong two-ship setup should create synergy between the ships.

The player should regularly have a reason to ask:

> Which ship is more useful in the current situation?

If switching ships does not meaningfully change how the player approaches combat, the mechanic is not being used effectively.

---

## 2. Different Tools Must Solve Different Problems

Weapons, bodies, and abilities should differ primarily through their gameplay behavior rather than only through numerical stats.

Different tools may specialize through:

- Shield damage
    
- Armor damage
    
- Single-target damage
    
- Area damage
    
- Crowd control
    
- Piercing
    
- Range
    
- Attack speed
    
- Projectile behavior
    
- Positioning requirements
    
- Defensive utility
    
- Synergy with another ship or weapon
    

A weapon with higher raw DPS should not automatically be the best choice.

Whenever possible, new content should introduce a new tactical option instead of reproducing an existing mechanic with different numbers.

---

## 3. Rarity Should Increase Variety, Not Raw Power

Higher rarity should primarily increase mechanical complexity, specialization, and build possibilities.

Rarity should not directly determine how powerful an item is.

Small statistical advantages are acceptable, but they should remain secondary to mechanical differences.

### Common

Simple and understandable mechanics.

Common items should provide a reliable foundation for builds without requiring complex conditions.

### Rare

More specialized mechanics or additional interactions.

Rare items may encourage a more specific playstyle.

### Epic

Mechanics that can significantly influence how the player builds or plays a ship.

Epic items may become important components of a build.

### Legendary

Unusual mechanics capable of defining an entire build or strategy.

Legendary items should feel unique because of what they allow the player to do, not because their numbers are dramatically higher.

The desired reaction to finding a high-rarity item is:

> I can build something different around this.

Not:

> This is simply a stronger version of my current item.

---

## 4. Builds Must Change the Way the Player Plays

A build should affect player behavior, not only increase damage or survivability.

Different builds should support different approaches to combat, such as:

- Shield-focused damage
    
- Armor-focused damage
    
- Single-target damage
    
- Crowd control
    
- Aggressive close-range play
    
- Defensive play
    
- Ability-focused builds
    
- Weapon-type specialization
    
- Synergy between two ships
    

Progression should gradually increase the number of viable strategies available to the player.

Statistical progression may exist, but it should support build development rather than replace it.

---

## 5. Bullet Hell Must Remain Readable

The game may contain large numbers of enemies, projectiles, attacks, and effects, but projectile density must never become more important than readability.

The player must always be able to quickly distinguish:

- Enemy projectiles
    
- Player projectiles
    
- The player ship
    
- Enemy attack telegraphs
    
- Important enemies
    
- Pickups and interactable objects
    

Enemy projectiles must have a higher visual priority than player projectiles.

Player attacks may be numerous, but they should remain visually less intrusive whenever possible.

Player projectiles should generally use:

- Smaller visual shapes
    
- Lower visual intensity
    
- Shorter trails
    
- Less persistent effects
    
- Reduced particle density
    

Enemy projectiles should generally use:

- Clear silhouettes
    
- Strong contrast
    
- Consistent visual language
    
- Easily recognizable movement patterns
    
- Clearly readable hitboxes
    

Player and enemy projectiles should never rely on nearly identical visual presentation.

The player should be able to understand immediately:

> This object is dangerous to me.

---

## 6. Bullet Hell Should Be Pattern-Based, Not Random Noise

Difficulty should come from understanding and navigating attack patterns rather than from visually chaotic projectile spam.

Enemy attacks should generally have recognizable structures.

Examples include:

- Radial bursts
    
- Directed spreads
    
- Sweeping attacks
    
- Rotating patterns
    
- Aimed volleys
    
- Delayed attacks
    
- Area denial
    
- Moving safe zones
    

Patterns may overlap, but overlapping attacks should still create situations that can be understood and learned.

Randomness may modify patterns, but it should not destroy their readability.

The goal is to create pressure and movement decisions rather than unpredictable visual noise.

---

## 7. Projectile Density Must Support Tactical Gameplay

The game is not purely about dodging projectiles.

The player must also make decisions about:

- Ship switching
    
- Shield and Armor damage
    
- Target priority
    
- Weapon positioning
    
- Ability timing
    
- Resource collection
    

Enemy projectile density must leave enough mental and visual space for these systems.

Extremely dense bullet patterns should therefore be used selectively rather than constantly.

High-density moments may appear during:

- Boss attacks
    
- Special enemy phases
    
- Temporary danger states
    
- High-difficulty encounters
    

They should contrast with calmer combat states.

---

## 8. Player Firepower Must Not Hide Enemy Threats

Some builds may generate large amounts of player projectiles, beams, explosions, or other effects.

Player firepower must never make enemy attacks difficult to see.

Visual effects should be reduced or simplified when necessary.

This includes limiting:

- Large hit flashes
    
- Persistent explosions
    
- Excessive glow
    
- Long projectile trails
    
- Large muzzle flashes
    
- Excessive particles
    
- Repeated damage numbers
    

Visual spectacle should support gameplay feedback, not obscure gameplay information.

---

## 9. Combat Must Remain Visually Readable

The player should always be able to understand the current combat state.

Important information includes:

- Player position
    
- Enemy positions
    
- Incoming threats
    
- Shield state
    
- Armor state
    
- Current ship
    
- Second ship condition
    
- Ability readiness
    
- Important pickups
    

Visual effects should have a clear priority hierarchy.

A recommended hierarchy is:

1. Enemy projectiles and attack telegraphs
    
2. Player ship
    
3. Important enemies
    
4. Gameplay-critical objects
    
5. Player weapon projectiles
    
6. Damage effects and particles
    
7. Background elements
    

A visually impressive effect should be simplified if it harms combat readability.

---

## 10. The Interface Must Be Clear and Informative

The interface should communicate important information quickly and with minimal effort from the player.

The UI should be:

- Clear
    
- Informative
    
- Easy to scan
    
- Consistent
    
- Suitable for small mobile screens
    

Important combat information should not require the player to read text or search through the interface.

Frequently used information should be represented visually whenever possible.

The UI should prioritize information that affects immediate gameplay decisions.

For example:

- Current and secondary ship health
    
- Shield and Armor state
    
- Active ship
    
- Ability cooldown
    
- Collected resources
    
- Important weapon or status information
    

Decorative UI elements should never reduce the readability of gameplay information.

---

## 11. Information Must Have Clear Visual Priority

Not every piece of information should demand the same amount of attention.

Critical information should be visually dominant.

Secondary information should remain available without competing with gameplay.

For example:

- Enemy attacks should attract immediate attention.
    
- Health and ability status should be quickly readable.
    
- Resource counters should remain visible but unobtrusive.
    
- Decorative elements should stay in the background.
    

The player should be able to understand the most important combat information through peripheral vision whenever possible.

---

## 12. Complexity Should Come From Interactions

Individual mechanics should remain understandable.

Depth should emerge from interactions between simple systems.

For example:

`Shield / Armor` alone is simple.

Ship switching alone is simple.

Weapon specialization alone is simple.

Together they create decisions such as:

> Break the enemy Shield with one ship, switch, activate the second ship's ability, and finish the exposed Armor with a specialized weapon.

This type of interaction is preferable to making every individual mechanic complicated.

---

## 13. Player Decisions Should Matter More Than Small Statistical Differences

The player should benefit more from making a good tactical decision than from having a slightly larger numerical stat.

Examples of meaningful decisions include:

- Switching ships at the right moment
    
- Choosing the correct target
    
- Matching weapons against Shield or Armor
    
- Using an ability at the right time
    
- Building complementary ships
    
- Positioning correctly during an enemy pattern
    

Stats still matter, but they should support decision-making rather than replace it.

---

## Design Check

When introducing a new feature, ask:

1. Does it create a new gameplay decision?
    
2. Does it interact with existing systems?
    
3. Does it improve build variety?
    
4. Does it preserve combat readability?
    
5. Does it give the player a reason to use both ships?
    
6. Does rarity change how the feature behaves rather than only increasing its power?
    
7. Can the player understand what is happening during combat?
    
8. Does it add meaningful complexity rather than visual or mechanical noise?
    

If the answer to several of these questions is no, the feature should be reconsidered.
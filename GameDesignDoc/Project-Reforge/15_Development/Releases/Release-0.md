
Status: In Development

## Goal

Release the first complete public version of Project: Reforge.

The release should demonstrate the core identity of the game:

- mobile bullet hell combat;
- ship switching;
- ship construction;
- weapon customization;
- progression between levels;
- clear visual identity.

The goal is not to include every planned system.

The goal is to deliver a small but complete version of the game.

---

# Release Principles

Release 0 should be:

- playable from start to finish;
- understandable without developer explanation;
- stable enough for public players;
- representative of the final game direction;
- small enough to realistically finish.

Features that do not meaningfully improve the first complete player experience can be postponed.

---

# Required Content

## Ships

- [ ] 10 playable ships.
- [ ] Active ability for each ship.
- [ ] Passive ability for each ship.
- [x] Ship switching during battle.
- [x] Ship construction / equipment configuration.

---

## Weapons

Target:

TBD weapons.

Required:

- [ ] Multiple weapon types.
- [x] Different damage types.
- [x] Weapon ranks.
- [x] Energy cost.
- [ ] Duplicate weapon behaviour.
- [ ] Weapon upgrades.

---

## Combat

- [x] Core player movement.
- [ ] Bullet hell enemy patterns.
- [x] Damage system.
- [x] HP.
- [x] Shields.
- [ ] Ship abilities.
- [x] Buffs.
- [x] Enemy drops.
- [ ] Death / level failure.
- [ ] Level completion.

---

## Enemies

Target:

TBD regular enemies.

Required enemy roles:

- [ ] Basic attacker.
- [ ] Swarm enemy.
- [ ] Shielded enemy.
- [ ] Heavy enemy.
- [ ] Ranged / pressure enemy.

Bosses:

- [ ] At least one boss.

---

## Levels

Target:

TBD levels.

Each level should contain:

- [ ] authored waves;
- [ ] formations;
- [ ] escalating difficulty;
- [ ] rewards;
- [ ] completion state.

---

## Progression

- [ ] Level unlocking.
- [ ] Weapon progression.
- [ ] Ship progression.
- [ ] Persistent inventory.
- [ ] Save system.

---

## Economy

Release currencies:

- [ ] Gold.
- [ ] Metal.
- [ ] TBD.

Additional currencies:

TBD.

---

## UI / UX

Required screens:

- [ ] Main Screen.
- [ ] Map.
- [ ] Hangar.
- [ ] Workshop.
- [ ] Preferences.
- [ ] Battle UI.
- [ ] Pause menu.
- [ ] Results screen.

---

## Art

- [ ] Final or acceptable ship sprites.
- [ ] Enemy visual language.
- [ ] Projectile visual language.
- [ ] Backgrounds.
- [ ] Effects.
- [ ] UI visual style.
- [ ] Consistent pixel-art rules.

Placeholder art should not remain in player-facing critical areas unless explicitly accepted.

---

## Audio

- [ ] Battle music.
- [ ] Menu music if required.
- [ ] Weapon SFX.
- [ ] Enemy SFX.
- [ ] Damage feedback.
- [ ] UI feedback.
- [ ] Ability audio.

FMOD integration must be stable.

---

# Technical Requirements

- [ ] Android build works.
- [ ] Saves survive application restart.
- [ ] No known save-corruption bugs.
- [ ] No blocking gameplay bugs.
- [ ] No frequent crashes.
- [ ] Acceptable performance on target mobile hardware.
- [ ] Battle remains playable under high projectile density.

---

# Not Required for Release 0

The following systems are planned but are not required unless explicitly moved into the release scope:

- Endless.
- Mutation.
- Online account system.
- Server-side saves.
- Cross-platform progression.
- Extended endgame.
- Large weapon catalogue.
- Large ship catalogue.
- Advanced monetization systems.

---

# Release Blockers

A Release Blocker is a problem that prevents Release 0 from shipping.

Examples:

- progression cannot be completed;
- save data is regularly lost;
- game crashes during normal gameplay;
- required level cannot be completed;
- core UI flow is broken;
- severe mobile performance issues;
- required content is missing.

---

# Exit Criteria

Release 0 is considered ready when:

1. The player can start a new game.
2. The player can configure their ships and weapons.
3. The player can enter and complete levels.
4. Progress and inventory are saved.
5. Progression leads through all Release 0 content.
6. The game has no known critical blockers.
7. Performance is acceptable on target hardware.
8. The visual and audio presentation is consistent enough to represent Project: Reforge publicly.

---

# Open Questions

- [ ] Exact number of levels.
- [ ] Exact number of weapons.
- [ ] Exact number of enemy types.
- [ ] Whether premium currency exists in Release 0.
- [ ] Whether chests exist in Release 0.
- [ ] Final difficulty structure.

See: [[Open-Questions]]

---

# Related

- [[Roadmap]]
- [[Milestones]]
- [[Current-State]]
- [[Backlog]]
- [[Known-Issues]]
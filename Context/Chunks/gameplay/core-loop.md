---
chunk: core-loop
title: Crash-site wave survival and planetary scavenging
owns: []
related: [system, state, control-model, player-controller, unity-project]
verifiedAtCommit: cbc008d980ff923abaae0dc8790a745a2ca38f0d
lastVerified: 2026-08-08
---

## What this is

The astronaut's spaceship crashed into the large crater used as the current
spawn and landing zone. Gameplay begins with a completed walled base and
perimeter already occupying that crater. It is the home hub for buying,
upgrading, and crafting, and enemies begin appearing across the planet.

## Session loop

- Enemy pressure is organized into waves whose duration is determined by time,
  not by killing every enemy.
- Enemies can spawn around the whole planet rather than only at the base.
- Players may leave the base and travel around the spherical world to scavenge
  loot while managing the risk created by the active wave.
- Enemy kills award gold and can drop items. Gold and items feed buying,
  upgrades, and crafting at the base.
- Score comes from enemy kills and increases through a wave multiplier, so
  surviving and killing in later waves is more valuable.

## Invariants

- The base and its perimeter are fully built before gameplay starts. There is
  no construction phase or building mechanic.
- The pistol is the player's only weapon for defeating enemies. Progression may
  improve the pistol, astronaut, equipment, or consumables, but must not
  silently become a multi-weapon arsenal.
- A wave completes according to its timer; clearing all currently visible
  enemies is not the completion condition.
- Planetary exploration and scavenging remain useful alongside defending the
  crash-site base.
- Combat rewards and scavenged resources return value to the base economy.

## Design direction

Regions outside sunlight should generally offer a higher-risk, higher-reward
scavenging opportunity, with stronger enemies and better loot. The strength of
that relationship, whether lighting changes over time, and how clearly danger
is communicated remain open rather than fixed tuning rules.

## Undecided details

Wave duration and scaling, breaks between waves, residual-enemy behavior,
enemy types and spawn rules, player/base failure states, loot tables, prices,
recipes, upgrade categories, score formula, progression persistence, and the
final session/endgame structure are not yet selected.

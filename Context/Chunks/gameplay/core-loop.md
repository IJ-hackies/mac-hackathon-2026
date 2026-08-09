---
chunk: core-loop
title: Crash-site wave survival and planetary scavenging
owns: []
related: [system, state, control-model, wave-system, gameplay-areas, progression, player-controller, unity-project]
verifiedAtCommit: a539eb47b10120f7c92bc827a06381aa5eb80fa7
lastVerified: 2026-08-09
---

## What this is

The astronaut's spaceship crashed into the large crater used as the current
spawn and landing zone. Gameplay begins with a completed walled base and
perimeter already occupying that crater. It is the home hub for buying,
upgrading, and crafting, and enemies begin appearing across the planet.

## Session loop

- The run is endless until player death. Intermissions have no timer; the player
  starts each next wave only after leaving every protected area.
- Regular waves last 30 seconds and spawn scaled enemies near the player's
  current planet position. All three protected areas deny entry until time ends.
- Every fifth wave is an untimed arena contract: odd multiples of five are
  Arena1 swarms; multiples of ten are the complete two-stage Barbara fight.
- Enemy kills and arena clears award run gold for the existing base upgrades.
  Health and ammo persist through waves. Residual regular enemies retreat
  without rewards at timeout.
- After a wave, the player may return to the base and take as long as needed
  before leaving and starting the next one. The in-game settings console also
  offers an intermission-only Teleport to Base shortcut; it is unavailable from
  every active-wave and game-over phase. See [wave-system](wave-system.md)
  for exact scaling, mixes, rewards, and state transitions.

## Invariants

- The base and its perimeter are fully built before gameplay starts. There is
  no construction phase or building mechanic.
- The pistol is the player's only weapon for defeating enemies. Progression may
  improve the pistol, astronaut, equipment, or consumables, but must not
  silently become a multi-weapon arsenal.
- A regular wave completes according to its timer; arena waves require their
  complete objective.
- Planetary exploration and scavenging remain useful alongside defending the
  crash-site base.
- Combat rewards and scavenged resources return value to the base economy.

## Deferred direction

Regions outside sunlight should generally offer a higher-risk, higher-reward
scavenging opportunity, with stronger enemies and better loot. The strength of
that relationship, whether lighting changes over time, and how clearly danger
is communicated remain open rather than fixed tuning rules.

Pickup drops, score, crafting, final balance, and the planned online furthest-wave
leaderboard remain deferred. There is intentionally no local best-wave record.
The three base consoles and run-only station upgrades remain fixed in
[progression](progression.md).

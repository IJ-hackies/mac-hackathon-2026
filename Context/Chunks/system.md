---
chunk: system
title: Project identity and current architecture
owns: []
related: [control-model, asset-library, unity-project, git-collaboration, player-controller, player-combat, enemies, boss-fight]
verifiedAtCommit: 71b7468850b4e64c25da49ef3deff2ff354c4778
lastVerified: 2026-08-08
---

## What this is

A space-themed full-3D game centered on one astronaut body. The primary
cooperative mode has two players sharing control of that body. A single-player
mode gives one player control of the complete body and all required actions.

## Current stage

The project is at early bootstrap stage. The repository root is a Unity
`6000.3.10f1` Universal 3D project using Universal Render Pipeline `17.3.0` and
Input System `1.18.0`. The prototype sample scene now has its first collidable
ground surface and runtime art material. Target platforms, the camera model,
networking model, game-specific input design, and build pipeline have not been
selected. The prototype world is now a small spherical planet intended for
circumnavigation, with a 50-unit radius that keeps the horizon gently curved
and a full lap compact. Its current visual treatment is the pale, cratered
`Planet_3` mesh from the Ultimate Space Kit, subdivided once to smooth its
silhouette and finished in a matte warm clay/ochre color over a separate
spherical collider. The scene now has a procedural starfield and a visible sun
whose world direction matches the planet's directional light source.

A separate `Player.unity` prototype scene (not yet merged into the planet
scene) now carries a working single-player third-person controller with
melee/hitscan combat, health/death, three fightable basic enemy AI types, and
a full two-stage Barbara-the-Bee boss fight (Astronaut -> Mech, unlocked via
a scripted transformation cutscene) — all sharing a common
`Combat.Health`/`Combat.IDamageable` damage system. The boss-fight tool
disables the three basic enemies when it builds the boss, leaving the boss as
the sole fightable target. See [player-controller](gameplay/
player-controller.md), [player-combat](gameplay/player-combat.md),
[enemies](gameplay/enemies.md), and [boss-fight](gameplay/boss-fight.md).
Several imported VFX/audio asset packs have been added and some later
removed during this work — see [asset-library](assets/asset-library.md) and
[boss-fight](gameplay/boss-fight.md) Gotchas.

## Confirmed product invariants

- The playable character is one astronaut, not two separate avatars.
- Cooperative play supports two players controlling that shared body.
- Single-player play preserves the complete playable capability set under one
  player's control.
- The presentation and game world are full 3D, and the setting is space-themed.
- The playable world is a spherical planet that players travel around.
- The game will be developed in Unity.
- The exact division of controls and responsibilities between cooperative
  players is deliberately undecided.

## Team and source control

This is a two-person project. Parallel work will use separate Git branches and
worktrees so each contributor or task has an isolated checkout. Exact branch
naming, integration, review, and Unity asset-conflict rules remain to be
defined before concurrent implementation begins.

## Intentionally unowned

Repository metadata, `Context/**`, `.agents/**`, and `.claude/**` are context
infrastructure rather than product-source ownership. Unity and IDE caches,
logs, user settings, generated solutions, and build outputs excluded by the
root `.gitignore` are also intentionally unowned. Add new product files to an
existing chunk or explicitly classify them during context maintenance.

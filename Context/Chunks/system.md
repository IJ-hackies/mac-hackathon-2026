---
chunk: system
title: Project identity and current architecture
owns: []
related: [control-model, core-loop, wave-system, progression, gameplay-areas, asset-library, runtime-art, unity-project, main-menu, world-authoring, world-runtime, git-collaboration, player-controller, tutorial]
verifiedAtCommit: a539eb47b10120f7c92bc827a06381aa5eb80fa7
lastVerified: 2026-08-09
---

## What this is

A space-themed full-3D single-player wave-survival game centered on one
astronaut body. The astronaut has crash-landed on a small spherical planet. Gameplay begins with a
walled base and perimeter already established in the landing crater, then the
astronaut survives timed enemy waves and ventures around the planet for loot.
One PC keyboard-and-mouse player controls the complete body and all required
actions; cooperative play is no longer in the hackathon scope.

## Current stage

The project is at early bootstrap stage. The repository root is a Unity
`6000.3.10f1` Universal 3D project using Universal Render Pipeline `17.3.0` and
Input System `1.18.0`. WebGL with WebGL2 is the confirmed publication target;
the build/deployment pipeline has not been selected. The build starts in a
dedicated mission-console menu with Singleplayer and shared settings, including
one persisted keyboard/mouse rebind map shared with the in-game pause console.
During wave intermissions, that pause console can also
recall the astronaut to the authored base spawn. SampleScene now contains the endless tiered-duration wave
director, planet-surface enemy spawning, protected-area locks, alternating
arena contracts, run rewards, StartWave binding, HUD, and game-over summary.
Spawning uses full scaled physical footprints, and enemy movement shares radial
terrain probing, obstacle detours, and stuck recovery around the rocky shell.
Pickup drops, score, final balance, and online leaderboard remain deferred. The
prototype world is a small spherical planet intended for circumnavigation, with
an approximately 150-unit radius and 942-unit full lap.
Its current visual treatment is the pale, cratered
`Planet_3` mesh from the Ultimate Space Kit, subdivided once to smooth its
silhouette and shaded with seam-free procedural lunar mottling over its matte
warm clay/ochre color. Its active non-convex mesh collider matches the rendered
craters exactly, and the complete planet hierarchy is a reusable prefab
instantiated by the prototype scene. A single-player astronaut prototype now
snaps to that surface, uses a planet-aligned capsule motor with grounded surface
adhesion, radial airborne gravity, and tangent movement, and keeps stable radial
body/camera up. The scene
also has a procedural starfield over slowly drifting, texture-sampled cosmic
fog, warm/cool twinkling stars, a layered HDR sun whose world direction matches
the directional light, and rare pooled shooting-star streaks.
Play opens with one continuous, skippable radial planet shot, followed by the
overhead landing base/NAUT artwork, astronaut Wave, and gameplay shoulder camera.
The landing crater is now dressed with a project-owned `LandingBase` hierarchy:
curated structure instances, a pole-and-curved-sheet perimeter with an explicit
opening, and decoration. This is authored environment art rather than a base
gameplay or construction system. The rest of the authored crater shell carries
compact baked data for 16,000 oversized orange/dark-orange vegetation instances
plus 800 small and 300 large clustered rocks; all three walled gameplay areas
remain clear of rock generation. At runtime, those records are regrouped into
spherical sectors for WebGL2 GPU instancing, distance, frustum, and horizon
culling. Vegetation authoring objects are absent from the runtime scene. The
1,100 rock objects retain only their static mesh colliders; the landing base and
arenas stay outside this prop system.

The landing base and both arenas now have reusable gameplay-area membership
derived from their authored perimeter poles. One tracker follows the
astronaut body and publishes transitions. A separate consumer doubles movement
speed while that body is inside the landing base; other area-specific effects
remain undecided.

The LandingBase now also has a run-scoped economy starting at 100g. `Base_Large`,
`GeodesicDome`, and `SolarPanel_Structure` host supply, stat-upgrade, and
Hold-to-Fire consoles with pause-safe interaction UI; see [progression].

`Tutorial.unity` is a hand-authored, gated onboarding path for movement,
combat, pickups, Ultimate/Shield, base stations, and the wave loop; see
[tutorial](gameplay/tutorial.md).

A separate `Player.unity` prototype scene (not yet merged into the planet
scene) now carries a working single-player third-person controller with
melee/hitscan combat, health/death, three fightable basic enemy AI types, and
a full two-stage Barbara-the-Bee boss fight (Astronaut -> Mech, unlocked via
a scripted transformation cutscene) — all sharing a common
`Combat.Health`/`Combat.IDamageable` damage system. The boss-fight tool
disables the three basic enemies when it builds the boss, leaving the boss as
the sole fightable target. See [player-controller](gameplay/player-controller.md),
[player-combat](gameplay/player-combat.md),
[enemies](gameplay/enemies.md), and [boss-fight](gameplay/boss-fight.md).
Several imported VFX/audio asset packs have been added and some later
removed during this work — see [asset-library](assets/asset-library.md) and
[boss-fight](gameplay/boss-fight.md) Gotchas.

## Confirmed product invariants

- The playable character is one astronaut, not two separate avatars.
- The hackathon release is single-player; do not build cooperative authority or
  shared-input layers unless scope is explicitly reopened.
- The presentation and game world are full 3D, and the setting is space-themed.
- The playable world is a spherical planet that players travel around.
- The landing crater is the crash site and home base for the survival loop.
- The base and perimeter are complete when gameplay begins; there is no
  construction phase or building mechanic.
- Enemy waves are time-based, with enemies spawning around the planet.
- The pistol is the sole player weapon used to defeat enemies.
- Progression is run-scoped. Runs start at 100g; basic kills and completed arena
  contracts award scaled gold. Pickup drops remain deferred.
- Regular waves lock all protected areas and last 30 seconds through wave 10,
  25 seconds on waves 11-20, then 20 seconds; every fifth wave is an untimed,
  mandatory arena contract. The run ends only on death.
- The game will be developed in Unity.
- The published game targets WebGL using the production WebGL2 graphics path.
- Gameplay and menu input are PC-only keyboard and mouse; console/gamepad
  bindings and control schemes are not part of the product.

## Team and source control

This is a two-person project. Parallel work will use separate Git branches and
worktrees so each contributor or task has an isolated checkout. Exact branch
naming, integration, review, and Unity asset-conflict rules remain to be
defined before concurrent implementation begins.

## Intentionally unowned

Repository metadata, `Context/**`, `.agents/**`, and `.claude/**` are context
infrastructure rather than product-source ownership. Unity and IDE caches,
logs, user settings, generated solution metadata (including the tracked root
`.slnx`), and build outputs excluded by the root `.gitignore` are also
intentionally unowned. Add new product files to an
existing chunk or explicitly classify them during context maintenance.

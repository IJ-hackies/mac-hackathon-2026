---
chunk: system
title: Project identity and current architecture
owns: []
related: [control-model, core-loop, asset-library, runtime-art, unity-project, world-authoring, git-collaboration, player-controller]
verifiedAtCommit: 927321aeae479a32412bb0928052db406373cf8a
lastVerified: 2026-08-08
---

## What this is

A space-themed full-3D wave-survival game centered on one astronaut body. The
astronaut has crash-landed on a small spherical planet. Gameplay begins with a
walled base and perimeter already established in the landing crater, then the
astronaut survives timed enemy waves and ventures around the planet for loot.
The primary cooperative mode has two players sharing control of that body. A
single-player mode gives one player control of the complete body and all
required actions.

## Current stage

The project is at early bootstrap stage. The repository root is a Unity
`6000.3.10f1` Universal 3D project using Universal Render Pipeline `17.3.0` and
Input System `1.18.0`. Target platforms, networking topology, cooperative input
ownership, and the build pipeline have not been selected. Enemy, wave, damage,
loot, economy, base-interaction, and scoring systems are not implemented. The
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
also has a procedural
starfield and a visible sun whose world direction matches the planet's
directional light source.
The landing crater is now dressed with a project-owned `LandingBase` hierarchy:
curated structure instances, a pole-and-curved-sheet perimeter with an explicit
opening, and decoration. This is authored environment art rather than a base
gameplay or construction system. The rest of the authored crater shell carries
a dense planet-wide scatter of oversized vegetation in dark orange and red.

## Confirmed product invariants

- The playable character is one astronaut, not two separate avatars.
- Cooperative play supports two players controlling that shared body.
- Single-player play preserves the complete playable capability set under one
  player's control.
- The presentation and game world are full 3D, and the setting is space-themed.
- The playable world is a spherical planet that players travel around.
- The landing crater is the crash site and home base for the survival loop.
- The base and perimeter are complete when gameplay begins; there is no
  construction phase or building mechanic.
- Enemy waves are time-based, with enemies spawning around the planet.
- The pistol is the sole player weapon used to defeat enemies.
- Kills award gold and may drop items; progression at the base uses buying,
  upgrading, and crafting, while kill score scales through a wave multiplier.
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

---
chunk: system
title: Project identity and current architecture
owns: []
related: [control-model, asset-library, unity-project, git-collaboration, player-controller]
verifiedAtCommit: 99146a500bb84fc2d74955cca7988e918c9092e2
lastVerified: 2026-08-07
---

## What this is

A space-themed full-3D game centered on one astronaut body. The primary
cooperative mode has two players sharing control of that body. A single-player
mode gives one player control of the complete body and all required actions.

## Current stage

The project is at early bootstrap stage. The repository root is a Unity
`6000.3.10f1` Universal 3D project using Universal Render Pipeline `17.3.0` and
Input System `1.18.0`. Target platforms, networking topology, cooperative input
ownership, and the build pipeline have not been selected. The prototype world
is a small spherical planet intended for
circumnavigation, with an approximately 100-unit radius and 628-unit full lap.
Its current visual treatment is the pale, cratered
`Planet_3` mesh from the Ultimate Space Kit, subdivided once to smooth its
silhouette and finished in a matte warm clay/ochre color. Its active non-convex
mesh collider matches the rendered craters exactly. A single-player astronaut
prototype now snaps to that surface, uses radial gravity and tangent movement,
aligns to the planet, and carries an independent radial-up camera. The scene
also has a procedural starfield and a visible sun whose world direction matches
the planet's directional light source.

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

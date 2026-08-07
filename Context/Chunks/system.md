---
chunk: system
title: Project identity and current architecture
owns: []
related: [control-model, asset-library, unity-project, git-collaboration]
verifiedAtCommit: 8a7f1dd273e1c329ecd10e4219ebdef8bd06b620
lastVerified: 2026-08-07
---

## What this is

A space-themed 2.5D game centered on one astronaut body. The primary
cooperative mode has two players sharing control of that body. A single-player
mode gives one player control of the complete body and all required actions.

## Current stage

The project is at concept/bootstrap stage. Unity is the selected engine, but
the Unity version, render pipeline, project layout, target platforms, camera
model, networking model, and build pipeline have not been selected. The
repository currently contains source asset packs only; no Unity project has
been initialized.

## Confirmed product invariants

- The playable character is one astronaut, not two separate avatars.
- Cooperative play supports two players controlling that shared body.
- Single-player play preserves the complete playable capability set under one
  player's control.
- The presentation is 2.5D and the setting is space-themed.
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
infrastructure rather than product-source ownership. Add new product files to
an existing chunk or explicitly classify them during context maintenance.

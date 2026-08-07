---
chunk: control-model
title: Shared-body cooperative and single-player control model
owns: []
related: [system, state]
verifiedAtCommit: 99146a500bb84fc2d74955cca7988e918c9092e2
lastVerified: 2026-08-07
---

## What this is

The central interaction concept: two cooperative players control different
responsibilities of one astronaut body. In single-player, one player controls
the complete body and can perform everything required to play.

## Invariants

- Both cooperative players act through one shared astronaut state and avatar.
- Cooperative responsibility should create coordination rather than two
  independent characters occupying the same model.
- Single-player must expose the full functional capability of the shared body.
- The game must not require a second human for actions that single-player
  cannot perform.

## Undecided design

The exact split is open. Possible responsibility areas include locomotion,
balance or body orientation, arms or tools, equipment, interaction, aiming,
and camera control. These are examples, not approved mappings.

Local versus online cooperative play, input devices, drop-in behavior,
accessibility assists, and the way single-player switches or combines control
responsibilities are also undecided.

## How to extend

Once the control design is approved, record the responsibility matrix, shared
state transitions, conflict-resolution rules, single-player mapping, and input
abstraction here. Add concrete implementation paths to `owns` only after those
files exist.

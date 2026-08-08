---
chunk: control-model
title: PC-only single-player control model
owns: []
related: [system, state, core-loop]
verifiedAtCommit: e4caa898457d6a2d25ff205625898ecf4fbe2635
lastVerified: 2026-08-09
---

## What this is

The hackathon release is single-player: one keyboard-and-mouse player controls
the complete astronaut body and every required action. Cooperative play has
been removed from scope because of the remaining schedule.

## Invariants

- There is one astronaut state and one locally controlled avatar.
- Every gameplay, station, menu, and progression action is reachable by one
  player without controller/gamepad fallbacks.
- The product is PC-only and its gameplay input baseline is keyboard and mouse.

## How to extend

Do not add multiplayer authority or shared-input abstractions unless product
scope is explicitly reopened. Concrete PC bindings and rebinding ownership live
in [player-controller](player-controller.md).

---
chunk: state
title: Active decisions, hazards, and open work
owns: []
landmines: []
openWork:
  - unity-project-bootstrap
  - cooperative-control-partition
  - multiplayer-topology
  - core-game-loop
  - branching-and-integration-policy
related: [control-model, unity-project, git-collaboration]
verifiedAtCommit: fe8fdaab6f46c2c16e89bea445042fdb31134a1d
lastVerified: 2026-08-07
---

# STATE

## Open work

- **`unity-project-bootstrap`** - finish the initialized Unity/URP foundation:
  replace or remove template content, define game-specific input maps, select
  3D movement and camera conventions, target platforms, assembly layout, and
  verification/build commands.
- **`cooperative-control-partition`** - decide which movement, body, equipment,
  interaction, and camera responsibilities belong to each player.
- **`multiplayer-topology`** - decide whether two-player play is local,
  networked, or both, including input-device expectations.
- **`core-game-loop`** - define the player's objective, failure states,
  progression, session structure, and the role shared-body coordination plays.
- **`branching-and-integration-policy`** - define branch naming, worktree
  placement, integration and review rules, and ownership of conflict-prone
  Unity scenes, prefabs, and project settings.

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
related: [control-model, unity-project, git-collaboration, player-controller, enemies]
verifiedAtCommit: 99146a500bb84fc2d74955cca7988e918c9092e2
lastVerified: 2026-08-08
---

# STATE

## Open work

- **`unity-project-bootstrap`** - finish the initialized Unity/URP foundation:
  replace or remove remaining template content, define game-specific input
  maps, implement planet-aligned locomotion, radial gravity, body orientation,
  and camera conventions, then select target platforms, assembly layout, and
  verification/build commands.
- **`cooperative-control-partition`** - decide which movement, body, equipment,
  interaction, and camera responsibilities belong to each player. A
  single-player-only third-person controller with combat (melee, hitscan
  shooting) now exists (see [player-controller](gameplay/player-controller.md))
  but does not resolve this split.
- **`multiplayer-topology`** - decide whether two-player play is local,
  networked, or both, including input-device expectations.
- **`core-game-loop`** - define the player's objective, failure states,
  progression, session structure, and the role shared-body coordination
  plays. Three fightable enemy types with a shared damage/health system now
  exist (see [enemies](gameplay/enemies.md)) as raw combat capability, but
  win/lose conditions, progression, and session structure remain undecided;
  Barbara the Bee / Mech boss AI is also deferred.
- **`branching-and-integration-policy`** - define branch naming, worktree
  placement, integration and review rules, and ownership of conflict-prone
  Unity scenes, prefabs, and project settings.

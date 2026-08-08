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
  - enemy-wave-prototype
  - progression-economy
  - branching-and-integration-policy
related: [control-model, core-loop, gameplay-areas, unity-project, main-menu, world-runtime, git-collaboration, player-controller]
verifiedAtCommit: 10712abb643f2ed039720b40bf9ba14a72b8b4dd
lastVerified: 2026-08-08
---

# STATE

## Open work

- **`unity-project-bootstrap`** - finish the initialized Unity/URP foundation:
  replace or remove remaining template content, define cooperative input and
  authority, then establish the assembly layout and repeatable WebGL
  build/test/deployment workflows. A dedicated startup menu and explicit
  menu/gameplay build order now exist. WebGL2 is the confirmed publication target.
  Planet-aligned locomotion, grounded surface adhesion with radial
  airborne gravity, stable radial body orientation, and camera conventions now
  exist in the single-player prototype.
- **`cooperative-control-partition`** - decide which movement, body, equipment,
  interaction, and camera responsibilities belong to each player. A
  single-player-only third-person controller now exists (see
  [player-controller](gameplay/player-controller.md)) but does not resolve
  this split.
- **`multiplayer-topology`** - decide whether two-player play is local,
  networked, or both, including input-device expectations. Its main-menu entry
  remains visibly unavailable until this is resolved.
- **`core-game-loop`** - turn the confirmed crash-site defense and planetary
  scavenging loop into exact phases. Decide wave timing/scaling, breaks,
  residual enemies, failure states, dark-region risk/reward behavior,
  progression persistence, remaining area-specific effects, and the endgame.
  Reusable perimeter membership now drives a 2x landing-base movement-speed
  benefit; both arenas currently have membership only.
- **`enemy-wave-prototype`** - implement planet-wide spawning, a timed wave
  director, drops, and player/base failure. Health, melee/hitscan damage, three
  basic enemy types, and a two-stage boss now work in the flat-ground
  `Player.unity` sandbox, but their movement/spawning is not planet-ready and
  no wave orchestration exists.
- **`progression-economy`** - define and implement gold, dropped/scavenged
  items, buying, pistol/player/equipment upgrades, crafting, loot tables, and
  the kill-score formula with its wave multiplier.
- **`branching-and-integration-policy`** - define branch naming, worktree
  placement, integration and review rules, and ownership of conflict-prone
  Unity scenes, prefabs, and project settings.

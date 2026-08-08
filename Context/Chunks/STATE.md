---
chunk: state
title: Active decisions, hazards, and open work
owns: []
landmines: []
openWork:
  - unity-project-bootstrap
  - core-game-loop
  - enemy-wave-prototype
  - progression-rewards
  - branching-and-integration-policy
related: [control-model, core-loop, progression, gameplay-areas, unity-project, main-menu, world-runtime, git-collaboration, player-controller, items, ultimate]
verifiedAtCommit: e4caa898457d6a2d25ff205625898ecf4fbe2635
lastVerified: 2026-08-09
---

# STATE

## Open work

- **`unity-project-bootstrap`** - finish the initialized Unity/URP foundation:
  replace or remove remaining template content, then establish the assembly
  layout and repeatable WebGL build/test/deployment workflows. A dedicated startup menu and explicit
  menu/gameplay build order now exist. WebGL2 is the confirmed publication target.
  Planet-aligned locomotion, grounded surface adhesion with radial
  airborne gravity, stable radial body orientation, and camera conventions now
  exist in the single-player prototype.
- **`core-game-loop`** - turn the confirmed crash-site defense and planetary
  scavenging loop into exact phases. Decide wave timing/scaling, breaks,
  residual enemies, failure states, dark-region risk/reward behavior,
  remaining area-specific effects, and the endgame.
  Reusable perimeter membership now drives a 2x landing-base movement-speed
  benefit; both arenas currently have membership only.
- **`enemy-wave-prototype`** - implement planet-wide spawning, a timed wave
  director, drops, and player/base failure. Health, melee/projectile damage, three
  basic enemy types, and a two-stage boss now work in the flat-ground
  `Player.unity` sandbox, but their movement/spawning is not planet-ready and
  no wave orchestration exists.
- **`progression-rewards`** - connect the implemented run economy and base
  stations to future waves: decide enemy gold/drop rewards, loot tables,
  crafting, and score multiplier rules. Current station prices/stats and the
  10,000g test start are fixed in [progression](gameplay/progression.md).
- **`branching-and-integration-policy`** - define branch naming, worktree
  placement, integration and review rules, and ownership of conflict-prone
  Unity scenes, prefabs, and project settings.

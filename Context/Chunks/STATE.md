---
chunk: state
title: Active decisions, hazards, and open work
owns: []
landmines: []
openWork:
  - unity-project-bootstrap
  - wave-followups
  - branching-and-integration-policy
related: [control-model, core-loop, wave-system, progression, gameplay-areas, unity-project, main-menu, world-runtime, git-collaboration, player-controller, items, ultimate]
verifiedAtCommit: 5880217f80f1e06cbc5b770ce9d0b680dcccf6f9
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
- **`wave-followups`** - playtest rocky-terrain detours, Round 5's complete
  ten-enemy Arena1 spawn on its uneven surface, large boss spawn clearance, the
  controller-grounding/navigation-reach/range-scaling fix, radial Stage 2 cutscene,
  and the newly tuned duration/stat/reward curves;
  later add deliberately deferred enemy-drop loot and
  the planned online furthest-wave leaderboard. Score and a local best-wave
  record are intentionally absent. See [wave-system](gameplay/wave-system.md).
- **`branching-and-integration-policy`** - define branch naming, worktree
  placement, integration and review rules, and ownership of conflict-prone
  Unity scenes, prefabs, and project settings.

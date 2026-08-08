---
chunk: progression
title: Run-scoped gold, base stations, stat upgrades, and special skills
owns:
  - "Assets/Scripts/Progression.meta"
  - "Assets/Scripts/Progression/**"
  - "Assets/Scripts/UI/Progression.meta"
  - "Assets/Scripts/UI/Progression/**"
  - "Assets/Scripts/Gameplay/Interaction.meta"
  - "Assets/Scripts/Gameplay/Interaction/**"
  - "Assets/Editor/Progression.meta"
  - "Assets/Editor/Progression/**"
  - "Assets/Tests/EditMode/Progression.meta"
  - "Assets/Tests/EditMode/Progression/**"
  - "Assets/Art/Textures/UI/Progression.meta"
  - "Assets/Art/Textures/UI/Progression/**"
  - "Assets/Prefabs/PlayerRig.prefab*"
  - "Assets/Scenes/SampleScene.unity*"
related: [system, state, core-loop, gameplay-areas, player-controller, player-combat, items, ultimate, runtime-art, unity-project]
verifiedAtCommit: e4caa898457d6a2d25ff205625898ecf4fbe2635
lastVerified: 2026-08-09
---

## What this is

`PlayerProgression` owns single-run gold and purchased upgrades. A fresh run
starts with 10,000 test gold; nothing persists between runs and enemies do not
award gold yet. The HUD shows a gold icon/value beside health, and the ammo bar
shows `magazine / reserve` while its fill continues to represent the magazine.

Three LandingBase structures have radial ground-level interaction zones sized
from each building footprint, plus large floating color-coded icon/halo beacons
and labels above their roofs. Entering a zone shows an `E - INTERACT` prompt:

- `Base_Large` opens Supply: full heal for 50g and full magazine+reserve refill
  for 100g. A pack is disabled as `FULL` when it would have no effect.
- `GeodesicDome` opens seven independent level 1-10 upgrades at 100g per level.
- `SolarPanel_Structure` sells Hold to Fire once for 500g. The pistol otherwise
  fires once per click; Ultimate always retains its own continuous primary fire.

## Upgrade contract

- Max HP +10/level (100 -> 190) and grants the added 10 current HP.
- Movement +3% base/level (100% -> 127%).
- Pistol fire rate +5% base/level (100% -> 145%).
- Shooting and melee damage +10% base/level (15 -> 28.5; 20 -> 38).
- Defense +4 percentage points/level (0% -> 36%) through a keyed incoming-
  damage modifier that composes with Shield.
- Max Ammo +2 loaded magazine rounds/level (12 -> 30); reserve remains 90 and
  the two granted rounds do not consume reserve.

## UI and interaction

Station consoles pause time, suspend movement/combat/abilities/look, hide the
crosshair, unlock the cursor, and close with `E`, Escape, or the Close button.
They refuse to steal ownership while Settings is open and restore only state
they captured. Holding Tab shows a non-pausing read-only run overview below the
persistent HUD; releasing Tab hides it. It reports purchased stats, live HP and
ammo, and owned special skills, excluding temporary LandingBase/Ultimate buffs.

`StationInteractionController` evaluates world-space distance every frame and
chooses the closest in-range station. Trigger enter/exit callbacks remain as a
fast path, but prompts and `E` do not depend on a physics callback firing.
The menu ignores close input during the frame in which it opens so the same
`E` press cannot both open and immediately close it; later `E` or Escape input
still closes normally.

## Authoring and verification

`Tools > Progression > Configure All` rebuilds only the generated Progression
UI subtree, repairs the three radial station markers, saves the prefab/scene,
and validates ground placement, footprint radius, roof beacon placement, world
scale, labels, and serialized references. The visual shell uses selected
Cartoon UI and Space Expansion UI copies under the owned texture folder; vendor
packs remain untouched. `Run Progression Contract Tests` executes the targeted
EditMode assembly. Runtime preview menu items open each console or hold/release
the Tab overview; QA approach commands can place the player in each station's
normal proximity range.

## Gotchas

- The structures sit on a spherical world and have heavily scaled parents.
  Marker placement must use planet-radial up, not global `Vector3.up`, and must
  normalize scale after applying its radial rotation. Otherwise interaction
  moves toward the roof or its world radius is distorted.
- Interaction radii deliberately cover each structure's tangential footprint
  plus approach clearance. Do not replace them with a small fixed sphere at the
  model bounds center.
- `StationMenuController`, prompt, adapter, and Tab controller live on the
  always-active Progression UI host while their visual roots may be disabled.
- Interaction input is evaluated before menu close input. Preserve the
  opening-frame guard if execution order or input routing changes.
- Use keyed stat modifiers. Do not overwrite LandingBase speed, Shield defense,
  Ultimate combat profiles, or other independent effects.

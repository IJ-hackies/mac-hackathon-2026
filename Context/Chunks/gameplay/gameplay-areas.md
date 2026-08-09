---
chunk: gameplay-areas
title: Perimeter-derived gameplay area membership
owns:
  - "Assets/Scripts/Gameplay.meta"
  - "Assets/Scripts/Gameplay/Areas.meta"
  - "Assets/Scripts/Gameplay/Areas/**"
  - "Assets/Editor/Gameplay.meta"
  - "Assets/Editor/Gameplay/**"
  - "Assets/Tests.meta"
  - "Assets/Tests/EditMode.meta"
  - "Assets/Tests/EditMode/GameplayAreas.meta"
  - "Assets/Tests/EditMode/GameplayAreas/**"
  - "Assets/Scripts/Player/LandingBaseMovementSpeedEffect.cs*"
  - "Assets/Prefabs/PlayerRig.prefab*"
  - "Assets/Scenes/SampleScene.unity*"
related: [system, state, core-loop, wave-system, progression, player-controller, unity-project, world-authoring]
verifiedAtCommit: 51dd8f3150f2f142886af2218c43c4d0c0875e41
lastVerified: 2026-08-09
---

## What this is

`LandingBase`, `Arena1`, and `Arena2` expose reusable runtime membership. Each
scene root owns a `GameplayArea` whose boundary comes from its direct
`Perimeter/Poles` children. No radius, physics trigger, or center marker defines
gameplay membership. The first consumer doubles locomotion inside LandingBase.

`PlayerRig.prefab` owns one `PlayerAreaTracker` that follows the nested shared
astronaut body. It discovers active areas when its serialized list is empty and
publishes enter, exit, and change events for future gameplay, VFX, audio, and UI
consumers. A separate `LandingBaseMovementSpeedEffect` consumes those events to
apply a 2x movement-speed modifier only inside the landing base, raising the
current `9.75` baseline to `19.5`; arena effects remain deliberately unimplemented.

`WaveAreaBarrier` builds collidable panels around each complete pole ring. The
[wave system](wave-system.md) changes their lock state: regular waves lock all
three; arena travel locks the base and non-target arena; entering the target
seals it until the objective completes. Colliders enforce entry/exit but never
replace polygon-derived membership.

Progression stations use separate local 4-unit trigger spheres on three
structure markers; they do not alter or reuse perimeter membership geometry.

## Key files

- `GameplayArea.cs` - area identity, overlap priority, perimeter cache,
  1.5-unit exit padding, validation, and scene gizmos.
- `SphericalPerimeterPolygon.cs` - runtime-only gnomonic projection, angular
  pole sorting, point-in-polygon membership, and outward boundary padding.
- `PlayerAreaTracker.cs` - shared-body evaluation, deterministic overlap
  choice, `CurrentArea`, and `AreaEntered`/`AreaExited`/`AreaChanged` events.
- `LandingBaseMovementSpeedEffect.cs` - subscribes to area changes and owns the
  keyed 2x landing-base modifier without coupling locomotion to perimeter math.
- `GameplayAreaSceneSetup.cs` - idempotent configure and validate commands for
  the active scene.
- `Assets/Tests/EditMode/GameplayAreas/` - geometry, height invariance,
  hysteresis, event, overlap, invalid-input, and SampleScene wiring coverage.

## Invariants

- The polygon uses normalized directions from `Planet Ground`, so jumping or
  other radial-height changes do not make the astronaut leave an area.
- A valid area has at least three direct perimeter poles and all poles must fit
  in one projectable hemisphere. Pole sibling order is irrelevant.
- Entry uses the exact authored polygon. The current area remains active until
  the astronaut also leaves its outward exit padding, preventing edge flicker.
- Higher `Priority` wins overlaps; equal priority resolves by the lower
  `GameplayAreaId`, making results independent of discovery order.
- Transition order is exit, enter, then change, and unchanged evaluations emit
  nothing. Consumers may query `CurrentArea` when they initialize.
- The landing-base effect removes only its own keyed speed modifier on exit or
  disable, so independent movement modifiers compose and remain intact. When
  it is removed, current horizontal speed scales down with the effective limit
  instead of lingering above the restored target.
- Perimeters are authored static. Call `RebuildPerimeter` after any runtime
  pole movement before relying on membership again.

## Scene authoring

`SampleScene` has all three components wired to the same `Planet Ground` and
their own `Perimeter/Poles` roots. The checked-in rings currently contain 27
landing-base poles and 17 poles in each arena. `PlayerRig.prefab` tracks its
nested Player transform, discovers these scene areas at startup, and carries
the one 2x landing-base speed consumer.

Use `Tools > Gameplay > Configure Area Membership` (`Ctrl+Shift+G`) to repair
or explicitly wire the active scene, then `Validate Area Membership` to check
the three unique areas and shared-body tracker. Configuration saves the scene.

## Gotchas

Do not attach future effects directly to containment math. Subscribe a separate
consumer to tracker transitions so spatial membership stays reusable. Scripts
that need the initial area should subscribe during `Awake`/`OnEnable` or read
`CurrentArea`; the tracker performs its first published evaluation in `Start`.

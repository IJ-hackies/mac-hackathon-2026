---
chunk: player-controller
title: Single-player third-person prototype (movement, camera, emotes)
owns:
  - "Assets/Scripts/Player/PlayerController.cs*"
  - "Assets/Scripts/Player/LandingBaseMovementSpeedEffect.cs*"
  - "Assets/Scripts/Player/PlayerAnimatorRelay.cs*"
  - "Assets/Scripts/Player/PlayerEmoteController.cs*"
  - "Assets/Scripts/Player/OpeningCutsceneController.cs*"
  - "Assets/Scripts/Player/PlayerVisualGroundConformer.cs*"
  - "Assets/Scripts/Player/RadialCapsuleMotor.cs*"
  - "Assets/Scripts/Player/ThirdPersonCameraController.cs*"
  - "Assets/Scripts/UI/CrosshairUI.cs*"
  - "Assets/Scripts/UI/EmoteWheelUI.cs*"
  - "Assets/Editor/Player/**"
  - "Assets/Prefabs/PlayerRig.prefab*"
  - "Assets/Tests/EditMode/Player.meta"
  - "Assets/Tests/EditMode/Player/**"
related: [control-model, core-loop, gameplay-areas, unity-project, runtime-art, world-authoring, state, ultimate]
verifiedAtCommit: 10712abb643f2ed039720b40bf9ba14a72b8b4dd
lastVerified: 2026-08-09
---

## What this is

The single-player prototype controls one astronaut with full locomotion,
camera, combat, and emotes. `Player.unity` remains a flat-ground sandbox;
`SampleScene.unity` instantiates `PlayerRig.prefab` on the spherical
planet. This does not resolve the two-player responsibility split.

The astronaut uses a rotatable `CapsuleCollider` and kinematic `Rigidbody`
driven by `RadialCapsuleMotor`; unlike Unity's world-upright
`CharacterController`, its physical capsule follows planetary up at every
latitude. The motor sweeps, slides, depenetrates, and preserves the former
`0.3`-unit step behavior in local radial coordinates - its gate accepts
steep upward seam/bevel normals, while clearance and 45-degree landing
checks stay authoritative. A centerline foot ray selects the real support
normal that may steer grounded adhesion; a broad foot sphere bridges stair
lips/broken slope triangles only after a physical bottom contact and never
consumes a wall normal. Surface pull is capped at 35 degrees from radial
gravity, following support changes at 120 degrees/s; losing direct/bottom
support releases adhesion immediately, pulling the player toward a hole
floor via radial gravity. Body and camera retain stable center-radial up.
Falling speed is capped at 30 units/s. If no `Planet Ground` exists, the
controller uses world up for the flat sandbox; a startup raycast places
the capsule's feet on rendered ground.

The camera orbit is controlled independently and its horizontal direction is
parallel-transported as radial up changes, preventing snaps while walking
around the sphere. The astronaut body continuously turns toward that camera/
aim direction; lateral and reverse movement therefore strafe and backpedal
instead of turning away from the crosshair. The camera retains shoulder
offset, collision pull-in, and mouse-look suspension while the emote wheel
is active.

`SampleScene` opens with a six-second terminator-to-NAUT orbit/dolly, then runs
through the NAUT zoom, Wave, and collision-resolved handoff while suspending input
and HUD. It temporarily raises the active URP shadow distance to 500, then restores
the captured value on every exit so Mobile gameplay returns to its 50-unit range.

## Key files

- `PlayerController.cs` - tangent locomotion at a single `moveSpeed` (the
  walk/sprint split was removed - "the character doesn't even walk" - so the
  old `Sprint` binding was repurposed as `Ability`, see [ultimate]
  (ultimate.md)), acceleration-smoothed, composable source-owned speed
  modifiers, surface-relative gravity/jump, slope-filtered ground probing,
  body alignment, boss stagger gating, and one-time surface snap. Also
  exposes `Dash(direction, speed, duration)` (`PlayerDash`'s hook, bypassing
  the acceleration ramp) and `GetCameraRelativeTangentDirection(input)`
  (shares `FixedUpdate`'s camera-relative math). `Stagger()` spawns an
  imported Stun VFX under a hand-authored `headAnchor` transform (same
  convention as `Muzzle` - Generic rig, no bone to query) for
  `Mathf.Max(duration, DuckClipLength)`, exposed publicly for callers to
  match a one-off VFX's lifetime.
- `LandingBaseMovementSpeedEffect.cs` owns the keyed 2x modifier inside
  LandingBase. `RadialCapsuleMotor.cs` is rotation-aware capsule sweep/
  slide, local-radial stepping across vertical or steep upward lips,
  contact classification, and overlap depenetration through a kinematic
  Rigidbody. `PlayerVisualGroundConformer.cs` tilts/lifts only the
  astronaut visual root toward reported support while the physical capsule
  and camera stay radial.
- `ThirdPersonCameraController.cs` - radial-up orbit, shoulder framing,
  `SphereCast` collision, smoothing, mouse look, boss shake, and
  collision-consistent cutscene follow-pose. `SetExtraDistance(float)`/
  `SetExtraHeight(float)` add to `distance`/`targetOffset.y` while set -
  used by [ultimate](ultimate.md) to pull back/raise for the Mech.
- `OpeningCutsceneController.cs` - radial/Bezier paths, independent angular,
  dolly, aim, and beat curves, NAUT framing, temporary shadow range, and
  handoff. Owned by `SampleScene`, not `PlayerRig.prefab`.
- `PlayerAnimatorRelay.cs` maps state to `Speed`/`Grounded`/`Jump`.
  `PlayerCombat.cs` and the health/death HUD are documented in
  [player-combat](player-combat.md). `PlayerEmoteController.cs`/
  `Assets/Scripts/UI/**` is a locked-cursor virtual joystick emote wheel
  plus an input-suspended cinematic Wave API; movement/jumping/attacking
  interrupts it. `EmoteWheelUI.Configure(string[] labels)` rebuilds the
  wheel's wedges at runtime for any label count (3 normally, 4 - `+Dance` -
  in [ultimate](ultimate.md)'s Mech mode); `PlayerEmoteController`/
  `PlayerAnimatorRelay`/`PlayerController`/`Combat.Health` all gained
  `SetAnimator(Animator)`, used by `PlayerUltimate` to retarget astronaut
  vs. Mech animators - see [ultimate](ultimate.md).
- `PlayerRig.prefab` - normalized rig root containing the nested radial
  `Player.prefab`, area tracker, LandingBase speed effect, camera pivot, UI,
  and health HUD; the repair command validates its links.
  `PlayerSceneSetup.cs`'s `Build Test Scene` creates the sandbox artifacts,
  now including ammo/ability/ultimate HUDs/components (see [items]
  (items.md)/[ultimate](ultimate.md)); `Repair Player Rig Prefab` replaces
  an orphaned nested Player reference and rewires/validates the prefab.

## Invariants

- Planet up uses the assigned `planetCenter` / exact scene object named
  `Planet Ground`; the motor's capsule axis must rotate with this local up
  (a world-upright `CharacterController` recreates latitude-based sinking).
  A center ray exclusively gates surface-normal adhesion; a broad foot
  sphere may preserve grounding for slopes/stairs only with a real bottom
  contact and may not steer gravity; side-only contacts cannot grab hole
  walls. Unsupported/falling movement returns to radial gravity, then
  flat-world behavior when the planet is absent. Ground collision must
  have outward-facing normals and be in `groundMask`; the controller
  ignores its own colliders during casts.
- Surface gravity is filtered independently from center-radial body/camera
  up: ground casts use the body's radial local up, tangent movement is
  projected against the filtered support direction so the controller
  settles on the same terrain it is being pulled toward. Step eligibility
  may admit a non-walkable upward seam but stays separate from contact
  classification and must not broaden grounding or adhesion. Character
  facing follows the camera's tangent forward/aim direction, not the
  movement vector - preserve strafe/backpedal when changing turn smoothing
  or input projection.
- `VisualRoot` is a direct Player child containing the astronaut render
  hierarchy and muzzle; the ground conformer may tilt it at most 30 degrees
  and lift it at most `0.12` units from support clearance, then returns it
  to its authored local pose while airborne. Never place the capsule,
  controller, rig camera, or camera pivot beneath it.
- The Player capsule is `height 2.55`, `radius 0.55`, `center.y 1.275`,
  origin at the feet; Rigidbody stays kinematic (gravity off, interpolation
  on, Continuous Speculative). `PlayerRig` root/nested Player local
  transforms stay identity/zero/one; `Player.unity` compensates with a zero
  root override after rig normalization. The rig camera is the sole active
  runtime camera/audio listener (shadows/post-processing/FXAA enabled).
- `InputSystem_Actions` map name is `Player`. `Attack` is reused for
  shooting; `Melee`/`EmoteWheel`/`Reload`/`Ability`/`Attack2` are separate
  typed actions (`Ability` repurposed from removed `Sprint`, see [ultimate]
  (ultimate.md)); `Crouch` is reserved with no behavior. The `Player`
  physics layer is excluded from camera collision/aim masks;
  `Animator.applyRootMotion` is disabled (the controller owns movement);
  keyed speed modifiers multiply and removal downscales current speed
  immediately. Emote `AnyState` entry uses the `PlayEmote` trigger plus
  `EmoteIndex`; the held `Emoting` bool is only an interrupt condition.

## Gotchas

- Do not replace the crater mesh collider with an approximate sphere - the
  visible crater floor differs enough to make the player appear buried or
  floating even when grounded; its support normal also defines adhesion. Do
  not hand-edit a broken nested Player GUID - run `Repair Player Rig
  Prefab`; it validates the current prefab source and every critical object
  reference. `Build Test Scene` rebuilds generated assets and is
  destructive to the sandbox; do not run it merely to repair the rig.
- When changing planet scale, move scene spawns by the same center-relative
  factor - the startup snap's short outward cast can't find the exterior
  surface from inside a newly enlarged shell. Move the top-level `PlayerRig`
  scene instance (not its nested Player or the prefab asset) via the radial
  surface-snap window (radial-up alignment off, heading preserved, zero
  offset); keep the spawn's radial cast path clear of roofs/other
  `groundMask` art since startup snapping uses the nearest collider in it.
- The opening cutscene's wide path stays spherical around `Planet Ground`;
  its top shot uses art-bounds radial up and actual N-to-T screen-right,
  not `BaseCenter`. Missing contracts skip safely; every exit restores
  gameplay, presentation, and the captured URP shadow distance.

## How to extend

Split locomotion versus camera/tool authority at system boundaries when the
cooperative control model is approved; do not add player-index branches
inside this single-player controller. Extend combat through
[player-combat](player-combat.md) rather than the locomotion component.

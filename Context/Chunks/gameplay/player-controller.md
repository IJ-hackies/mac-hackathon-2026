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
related: [control-model, core-loop, gameplay-areas, unity-project, runtime-art, world-authoring, state]
verifiedAtCommit: 148a3fe3150d9a1b051c8129dbc8e3051832eff7
lastVerified: 2026-08-08
---

## What this is

The single-player prototype controls one astronaut with full locomotion,
camera, combat, and emotes. `Player.unity` is the flat sandbox; `SampleScene`
uses `PlayerRig.prefab` on the planet. The future two-player split is undecided.

The astronaut uses a rotatable `CapsuleCollider` and kinematic `Rigidbody`
driven by `RadialCapsuleMotor`; unlike Unity's world-upright
`CharacterController`, its physical capsule follows planetary up at every
latitude. The motor sweeps, slides, depenetrates, and preserves the former
`0.3`-unit local-radial step behavior. Its gate accepts steep upward seam and
bevel normals, while clearance and 45-degree landing checks stay authoritative.
A centerline foot ray selects the support normal that may steer adhesion. A broad foot
sphere bridges stair lips and broken slope triangles only after a physical
bottom contact; it never consumes a wall normal. Surface pull is capped at 35
degrees from radial gravity and follows support changes at 120 degrees per
second. Losing direct/bottom support releases adhesion immediately, so radial
gravity pulls the player toward a hole floor. Body and camera retain stable
center-radial up. Falling speed is capped at 30 units per second. If no
`Planet Ground` exists, the controller uses world up for the flat sandbox. A
startup raycast places the capsule's feet on rendered ground.

The camera orbit is controlled independently and its horizontal direction is
parallel-transported as radial up changes, preventing snaps while walking
around the sphere. The astronaut body continuously turns toward that camera/
aim direction; lateral and reverse movement therefore strafe and backpedal
instead of turning the character away from the crosshair. The camera retains
shoulder offset, collision pull-in, and mouse-look suspension while the emote
wheel is active.

`SampleScene` opens with a six-second terminator-to-NAUT orbit/dolly, then runs
through the NAUT zoom, Wave, and collision-resolved handoff while suspending input
and HUD. It temporarily raises the active URP shadow distance to 500, then restores
the captured value on every exit so Mobile gameplay returns to its 50-unit range.

## Key files

- `PlayerController.cs` - tangent locomotion, speed modifiers, surface-relative
  gravity/jump, ground probing, alignment, stagger gating, and spawn snap.
- `LandingBaseMovementSpeedEffect.cs` - owns the keyed 2x modifier inside LandingBase.
- `RadialCapsuleMotor.cs` - rotation-aware sweep/slide, local-radial stepping
  across vertical or steep upward lips, contact classification, and depenetration.
- `PlayerVisualGroundConformer.cs` - tilts and lifts only the astronaut visual
  root toward reported support while the physical capsule and camera stay
  radial.
- `ThirdPersonCameraController.cs` - radial-up orbit, shoulder framing,
  collision, mouse look, shake, and collision-consistent cutscene handoff APIs.
- `OpeningCutsceneController.cs` - radial/Bezier paths, independent angular,
  dolly, aim, and beat curves, NAUT framing, temporary shadow range, and handoff.
- `PlayerAnimatorRelay.cs` - maps state to `Speed`, `Grounded`, and `Jump`.
- `PlayerCombat.cs` and the health/death HUD are documented in
  [player-combat](player-combat.md). Ranged damage is hitscan; its traveling
  bolt is cosmetic and the former `Projectile.cs`/prefab were removed.
- `PlayerEmoteController.cs` / `Assets/Scripts/UI/**` - locked-cursor emote
  wheel plus an input-suspended cinematic Wave API.
- `PlayerRig.prefab` - normalized radial player, area/speed components, camera, and UI.
- `PlayerSceneSetup.cs` - builds the sandbox; its repair command safely relinks,
  normalizes, reloads, and validates `PlayerRig.prefab`.

## Invariants

- Planet up uses the assigned `planetCenter` / exact scene object named
  `Planet Ground`. The motor's capsule axis must rotate with this local up;
  reintroducing a world-upright `CharacterController` recreates latitude-based
  sinking. A center ray exclusively gates surface-normal adhesion. A broad foot
  sphere may preserve grounding for slopes/stairs only with a real bottom
  contact and may not steer gravity; side-only contacts cannot grab hole walls.
  Unsupported or falling movement returns to radial gravity, then flat-world
  behavior when the planet is absent.
- Planet ground collision must have outward-facing normals and be included in
  `groundMask`. The controller ignores its own colliders during casts.
- Surface gravity is filtered independently from center-radial body/camera up.
  Ground casts use the body's radial local up, while tangent movement is
  projected against the filtered support direction so the controller settles
  on the same terrain it is being pulled toward.
- Step eligibility may admit a non-walkable upward seam, but remains separate
  from contact classification and must not broaden grounding or adhesion.
- Character facing follows the camera's tangent forward/aim direction, not the
  movement vector. Preserve strafe and backpedal behavior when changing turn
  smoothing or input projection.
- `VisualRoot` directly contains the astronaut render hierarchy and muzzle. It
  may tilt 30 degrees and lift `0.12` units, then returns to its authored pose
  airborne. Never put physics/controller/camera objects beneath it.
- The Player capsule is `height 2.55`, `radius 0.55`, `center.y 1.275`, origin
  at its feet. Its Rigidbody stays kinematic, interpolated, gravity-off, and
  Continuous Speculative; keep mesh, motor, and snap math scale-consistent.
- `PlayerRig` root and nested Player local transforms stay identity/zero/one.
  Scene instances own world placement; `Player.unity` compensates with a zero
  root override after rig normalization.
- The rig camera is the sole active runtime camera/audio listener and must keep
  shadows, post-processing, and FXAA enabled in its URP camera data.
- The opening is owned by `SampleScene`, not `PlayerRig.prefab`. Its wide path
  stays spherical around `Planet Ground`; its top shot uses art-bounds radial up and
  actual N-to-T screen-right, not BaseCenter. Missing contracts skip safely. Every
  exit restores gameplay, presentation, and the captured URP shadow distance.
- `InputSystem_Actions` map name is `Player`. `Attack` is reused for shooting,
  while `Melee` and `EmoteWheel` are separate typed actions. `Crouch` remains
  reserved and has no behavior.
- The `Player` physics layer is excluded from camera collision and aim masks.
- `Animator.applyRootMotion` is disabled; the controller owns movement.
- Keyed speed modifiers multiply; removal downscales current speed immediately.
- Emote `AnyState` entry uses `PlayEmote` trigger plus `EmoteIndex`; the held
  `Emoting` bool is only an interrupt condition.

## Gotchas

- Do not replace the crater mesh collider with an approximate sphere. The
  visible crater floor differs enough from a sphere to make the player appear
  buried or floating even when physics reports grounded; its support normal
  also defines grounded adhesion.
- When scaling the planet, scale the spawn's center-relative position too; an
  inside-shell spawn cannot use the short outward startup cast reliably.
- Move the top-level `PlayerRig` scene instance, never its nested Player or
  prefab. Radial-snap it with radial-up alignment off, heading preserved, and
  zero offset; keep the cast clear of roofs and other `groundMask` art.
- Do not hand-edit a broken nested Player GUID. Run `Repair Player Rig Prefab`;
  it validates the current prefab source and every critical object reference.
- `Build Test Scene` rebuilds generated assets and is destructive to the
  sandbox scene/prefab. Do not run it merely to repair the rig.

## How to extend

Split locomotion versus camera/tool authority at system boundaries when the
cooperative control model is approved; do not add player-index branches inside
this single-player controller. Extend combat through [player-combat]
(player-combat.md) and the shared damage interfaces rather than adding it to
the locomotion component.

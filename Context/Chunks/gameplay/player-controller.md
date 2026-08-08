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
  - "Assets/Scripts/UI/SettingsMenuController.cs*"
  - "Assets/Editor/Player/**"
  - "Assets/Prefabs/PlayerRig.prefab*"
  - "Assets/Tests/EditMode/Player.meta"
  - "Assets/Tests/EditMode/Player/**"
related: [control-model, core-loop, gameplay-areas, unity-project, main-menu, runtime-art, world-authoring, state, ultimate]
verifiedAtCommit: 262413a1cda18eaed7a50511bb0aa8f10bcb533a
lastVerified: 2026-08-09
---

## What this is

The single-player prototype controls one astronaut with full locomotion, camera,
combat, and emotes. `Player.unity` is a flat-ground sandbox; `SampleScene.unity`
instantiates `PlayerRig.prefab` on the spherical planet. It does not decide the
two-player responsibility split.

`RadialCapsuleMotor` drives a rotatable capsule and kinematic Rigidbody, so its
physical capsule follows planetary up at every latitude. It sweeps, slides,
depenetrates, and keeps the former `0.3`-unit local-radial step behavior. A
centerline foot ray alone may select the support normal for adhesion; a broad
foot sphere bridges lips/broken slope triangles only after real bottom contact
and never consumes a wall normal. Adhesion accepts support within 35 degrees of
radial gravity, follows it at 120 degrees/s, and releases immediately on loss of
direct/bottom support; airborne gravity is radial and caps falling at 30 units/s.
Without `Planet Ground`, the sandbox uses world up and startup snaps feet to
rendered ground.

Camera orbit parallel-transports horizontal direction as radial up changes,
preventing sphere-walk snaps. The body faces the camera/aim tangent, preserving
strafe and backpedal; camera keeps shoulder offset, collision pull-in, and
mouse-look suspension while the emote wheel is open. The opening cutscene runs
terminator-to-NAUT orbit/dolly, NAUT zoom, Wave, then collision-resolved handoff
with input/HUD suspended. It raises URP shadow distance to 500 temporarily and
always restores the captured gameplay value (Mobile is 50).

## Key files

- `PlayerController.cs` - one `moveSpeed` (walk/sprint removed; `Sprint` was
  repurposed as `Ability`), acceleration-smoothed tangent movement, composable
  keyed speed modifiers, ground probing, jump/gravity, body alignment, boss
  stagger gate, and one-time surface snap. `Dash(direction, speed, duration)`
  bypasses acceleration for `PlayerDash`; `GetCameraRelativeTangentDirection`
  shares its `FixedUpdate` camera-relative math. `Stagger()` parents imported
  Stun VFX to the hand-authored `headAnchor` for `Max(duration, DuckClipLength)`.
- `RadialCapsuleMotor.cs` - rotation-aware sweep/slide, local-radial stepping,
  contact classification, and overlap depenetration through the kinematic body.
  `PlayerVisualGroundConformer.cs` only tilts/lifts the astronaut visual root.
  `LandingBaseMovementSpeedEffect.cs` owns LandingBase's keyed 2x modifier.
- `ThirdPersonCameraController.cs` - radial-up orbit, shoulder framing,
  `SphereCast` collision, smoothing, mouse look, boss shake, and cutscene
  follow pose. Its clamped `MouseSensitivity` API is the settings hook;
  `SetExtraDistance`/`SetExtraHeight` are Mech camera hooks from [ultimate].
- `OpeningCutsceneController.cs` - radial/Bezier paths and beat curves, NAUT
  framing, temporary shadow range, and safe handoff; it belongs to `SampleScene`,
  not `PlayerRig.prefab`.
- `PlayerAnimatorRelay.cs` writes `Speed`/`Grounded`/`Jump`. `PlayerEmoteController`
  and `EmoteWheelUI` provide the locked-cursor virtual-joystick wheel; movement,
  jump, or attack interrupts it. `Configure(labels)` rebuilds for three labels,
  or four including `+Dance` in Mech mode. Ultimate retargets astronaut/Mech
  animators through `SetAnimator` on relay/controller/health; see [ultimate].
- `SettingsMenuController.cs` owns the rig's Escape settings console: it toggles
  pause, input/cursor/look and crosshair ownership, persists `GameSettings`
  master volume and `MouseSensitivity`, and opens a placeholder Controls page.
  Closing restores the main page and only state it acquired.
- `PlayerRig.prefab` contains nested radial `Player.prefab`, area tracker,
  LandingBase effect, camera pivot, UI, and health HUD. `Repair Player Rig
  Prefab` safely rewires/validates it; destructive `Build Test Scene` recreates
  sandbox artifacts including ammo/ability/ultimate HUDs (see [items]/[ultimate]).

## Invariants

- `planetCenter` or the exact active scene object `Planet Ground` defines radial
  up. The capsule axis rotates with it; a world-upright `CharacterController`
  causes latitude sinking. Ground must have outward normals and `groundMask`;
  all casts ignore the player's colliders.
- Surface gravity is independently filtered from radial body/camera up. Center
  ray gates adhesion; the broad sphere may retain grounding only with bottom
  contact and cannot steer gravity. Side contacts cannot grab hole walls.
  Step eligibility for an upward seam remains separate from grounding/adhesion.
- Facing follows camera tangent/aim, never movement. `VisualRoot` is the direct
  Player child holding render hierarchy and muzzle; conform it no more than 30
  degrees or `0.12` units. Never put capsule, controller, camera, or pivot below it.
- Capsule: height `2.55`, radius `0.55`, center.y `1.275`, feet-origin.
  Rigidbody is kinematic, gravity off, interpolation on, Continuous Speculative.
  Rig/nested Player transforms stay identity; the sandbox uses a zero root
  override. The rig camera is the sole active runtime camera/audio listener.
- Input map is `Player`: `Attack` shoots; `Melee`, `EmoteWheel`, `Reload`,
  `Ability`, and `Attack2` are distinct; `Crouch` is reserved. Exclude Player
  layer from camera collision/aim masks; root motion stays off. Removing a keyed
  speed modifier immediately downscales speed. Emotes use `PlayEmote` +
  `EmoteIndex`; `Emoting` only interrupts.

## Gotchas

- Do not replace the crater mesh collider with a sphere: its floor/support
  normals visibly affect grounding. Do not hand-edit nested Player GUIDs; repair
  the rig. For planet-scale changes, move top-level scene `PlayerRig` by the same
  center-relative factor with radial-snap (alignment off, heading preserved), and
  keep its short outward startup cast clear of roofs/other `groundMask` art.
- The cutscene wide path is spherical around `Planet Ground`; its top shot uses
  art-bounds radial up and true N-to-T screen-right, not `BaseCenter`. Missing
  contracts must skip safely and every exit must restore gameplay, presentation,
  and shadow distance.

## How to extend

Split cooperative locomotion, camera, and tool authority at system boundaries;
do not add player-index branches to this controller. Extend combat via
[player-combat](player-combat.md), not the locomotion component.

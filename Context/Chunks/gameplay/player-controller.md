---
chunk: player-controller
title: Single-player third-person prototype (movement, camera, emotes)
owns:
  - "Assets/Scripts/Player/PlayerController.cs*"
  - "Assets/Scripts/Player/LandingBaseMovementSpeedEffect.cs*"
  - "Assets/Scripts/Player/PlayerAnimatorRelay.cs*"
  - "Assets/Scripts/Player/PlayerEmoteController.cs*"
  - "Assets/Scripts/Player/PlayerInputBindings.cs*"
  - "Assets/Scripts/Player/OpeningCutsceneController.cs*"
  - "Assets/Scripts/Player/PlayerVisualGroundConformer.cs*"
  - "Assets/Scripts/Player/RadialCapsuleMotor.cs*"
  - "Assets/Scripts/Player/ThirdPersonCameraController.cs*"
  - "Assets/Scripts/UI/CrosshairUI.cs*"
  - "Assets/Scripts/UI/EmoteWheelUI.cs*"
  - "Assets/Scripts/UI/ControlsRebindingUI.cs*"
  - "Assets/Scripts/UI/SettingsMenuController.cs*"
  - "Assets/Scripts/UI/CutsceneSkipPromptUI.cs*"
  - "Assets/Editor/Player/**"
  - "Assets/Art/Models/Characters/Player.prefab*"
  - "Assets/Prefabs/PlayerRig.prefab*"
  - "Assets/Tests/EditMode/Player.meta"
  - "Assets/Tests/EditMode/Player/**"
related: [control-model, core-loop, wave-system, gameplay-areas, progression, unity-project, main-menu, runtime-art, world-authoring, state, ultimate]
verifiedAtCommit: 5880217f80f1e06cbc5b770ce9d0b680dcccf6f9
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
requests all visible-face props, then restores Mobile's 50 shadow range and the
normal 112.5-unit prop distance.

## Key files

- `PlayerController.cs` - one `9.75` base `moveSpeed` (walk/sprint removed;
  `Sprint` was repurposed as `Ability`), acceleration-smoothed tangent movement, composable
  keyed speed modifiers, ground probing, jump/gravity, body alignment, safe
  surface relocation, boss
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
- `OpeningCutsceneController.cs` - radial/Bezier paths and beat curves, NAUT framing,
  async-load retry, shared skip prompt, temporary shadow/prop visibility, and safe handoff; it belongs to
  `SampleScene`, not `PlayerRig.prefab`.
- `PlayerAnimatorRelay.cs` writes `Speed`/`Grounded`/`Jump`. `PlayerEmoteController`
  and `EmoteWheelUI` provide the locked-cursor virtual-joystick wheel; movement,
  jump, or attack interrupts it. `Configure(labels)` rebuilds for three labels,
  or four including `+Dance` in Mech mode. Ultimate retargets astronaut/Mech
  animators through `SetAnimator` on relay/controller/health; see [ultimate].
- `SettingsMenuController.cs` owns the rig's Escape settings console: it toggles
  pause, input/cursor/look and crosshair ownership, persists `GameSettings`
  master volume and `MouseSensitivity`, opens the live Controls page, and offers
  intermission-only Teleport to Base through [wave-system]. It raises itself to
  the top UI sibling, wires menu SFX, and restores only state it acquired.
  `ControlsRebindingUI` drives 13 two-column rows with Escape-cancel, duplicate
  rejection, and Reset Defaults. `ReturnToMainMenu()` resets time/cursor and
  transitions to `MainMenu`; `Add Return To Main Menu Button` clones Close so both scenes inherit it.
- `PlayerInputBindings.cs` is the factory/registry for every independent
  `InputSystem_Actions` copy. It loads one PlayerPrefs override JSON, fans an
  accepted rebind out to live copies while preserving map enablement, and
  releases each copy at owner destruction. Stable binding GUIDs identify rows.
  Input-owning player components recreate their nonserialized action copies in
  `OnEnable`, so an Editor assembly reload cannot leave restored scene objects
  with null input state.
- `PlayerRig.prefab` contains nested radial `Player.prefab`, area tracker,
  LandingBase effect, camera pivot, and UI. Its sliced bars place Health at
  bottom-center and Ammo at bottom-right; the targeted refresh commands replace
  only their own rig children. `Repair Player Rig Prefab` safely
  rewires/validates the rig; destructive `Build Test Scene` recreates sandbox
  artifacts including ammo/ability/ultimate HUDs (see [items]/[ultimate]).
- The rig also owns the always-active progression UI/controller host. Station
  menus suspend movement/combat/abilities/camera and restore only captured
  state; the Tab overview is non-pausing and leaves movement/look active.

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
- The `Player` map is keyboard/mouse-only. Move (WASD/arrows), pointer Look,
  Escape settings, and Escape/Space cutscene skip stay fixed. Jump, Ability,
  two Attack bindings, Attack2, Melee, Reload, StartWave, EmoteWheel, and reserved
  Interact/Crouch/Previous/Next are configurable; Escape and duplicates are
  rejected. Exclude Player layer from camera collision/aim masks; root motion
  stays off. Removing a keyed speed modifier immediately downscales speed.
  Emotes use `PlayEmote` + `EmoteIndex`; `Emoting` only interrupts.
- Teleporting through a paused menu must update the kinematic Rigidbody and
  Transform together, clear locomotion/dash state, restore radial grounding,
  snap the follow camera, and immediately re-evaluate gameplay-area membership.

## Gotchas

- Do not replace the crater mesh collider with a sphere: its floor/support
  normals visibly affect grounding. Do not hand-edit nested Player GUIDs; repair
  the rig. For planet-scale changes, move top-level scene `PlayerRig` by the same
  center-relative factor with radial-snap (alignment off, heading preserved), and
  keep its short outward startup cast clear of roofs/other `groundMask` art.
- The cutscene wide path is spherical around `Planet Ground`; its top shot uses art-bounds
  radial up and true N-to-T screen-right, not `BaseCenter`. Missing contracts must skip
  safely and every exit must restore gameplay, presentation, shadow, and prop visibility.

## How to extend

Keep input ownership local and PC-only for the hackathon release; do not add
player-index or multiplayer authority branches without reopening scope. Extend combat via
[player-combat](player-combat.md), not the locomotion component.

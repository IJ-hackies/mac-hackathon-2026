---
chunk: player-controller
title: Single-player third-person prototype (movement, camera, emotes)
owns:
  - "Assets/Scripts/Player/PlayerController.cs"
  - "Assets/Scripts/Player/PlayerAnimatorRelay.cs"
  - "Assets/Scripts/Player/PlayerEmoteController.cs"
  - "Assets/Scripts/Player/ThirdPersonCameraController.cs"
  - "Assets/Scripts/UI/**"
  - "Assets/Editor/Player/**"
  - "Assets/Prefabs/**"
related: [control-model, core-loop, unity-project, runtime-art, state]
verifiedAtCommit: cbc008d980ff923abaae0dc8790a745a2ca38f0d
lastVerified: 2026-08-08
---

## What this is

The single-player prototype controls one astronaut with full locomotion,
camera, combat, and emotes. `Player.unity` remains a flat-ground sandbox;
`SampleScene.unity` instantiates `PlayerRig.prefab` on the spherical planet.
This does not resolve the future two-player responsibility split.

The astronaut uses a `CharacterController`. On a planet, gravity points toward
`Planet Ground`, the body aligns its up axis away from that center, movement is
projected onto the local tangent plane, and jump velocity is radial. Radial
gravity accelerates inward at 6.5 Unity units per second squared. If no
`Planet Ground` exists,
the controller uses world up so the flat sandbox keeps working. A startup
raycast places the controller's feet on the rendered ground instead of trusting
an approximate spawn height.

The camera orbits independently of character facing. Its horizontal orbit
direction is parallel-transported as radial up changes, preventing snaps while
walking around the sphere. It retains shoulder offset, collision pull-in, and
mouse-look suspension while the emote wheel is active.

## Key files

- `PlayerController.cs` - tangent locomotion, acceleration-smoothed walk/sprint,
  radial gravity/jump, slope-filtered ground probing, body alignment, and
  one-time surface snap. Exposes `NormalizedSpeed`, `IsGrounded`, and
  `JumpTriggeredThisFrame`.
- `ThirdPersonCameraController.cs` - radial-up orbit, shoulder framing,
  `SphereCast` collision, smoothing, and independent mouse look. The rig camera
  has URP additional-camera data with shadows and post-processing enabled.
- `PlayerAnimatorRelay.cs` - maps controller state to `Speed`, `Grounded`, and
  `Jump` Animator parameters.
- `PlayerCombat.cs` / `Projectile.cs` - camera-crosshair aiming, melee, visual
  shooting, projectile lifetime, and self-collision filtering. There is no
  damage or health system yet.
- `PlayerEmoteController.cs` / `Assets/Scripts/UI/**` - locked-cursor virtual
  joystick emote wheel. Movement, jumping, or attacking interrupts an emote.
- `PlayerRig.prefab` - normalized rig root containing the current nested
  `Player.prefab`, camera pivot, and HUD. Serialized camera/combat/emote links
  are validated by the repair command.
- `PlayerSceneSetup.cs` - `Tools > Player Prototype > Build Test Scene` creates
  the sandbox artifacts. `Repair Player Rig Prefab` safely replaces an
  orphaned nested Player reference, rewires dependencies, normalizes transforms,
  saves, reloads, and validates the prefab.

## Invariants

- The planet center is an assigned `planetCenter` or the exact scene object
  named `Planet Ground`; absence deliberately selects flat-world behavior.
- Planet ground collision must have outward-facing normals and be included in
  `groundMask`. The controller ignores its own colliders during casts.
- The Player capsule is `height 2.55`, `radius 0.55`, `center.y 1.275`; its
  transform origin is at the feet. Keep mesh, capsule, and spawn-snap math in
  agreement when changing character scale.
- `PlayerRig` root and nested Player local transforms stay identity/zero/one.
  Scene instances own world placement; `Player.unity` compensates with a zero
  root override after rig normalization.
- The rig camera is the sole active runtime camera/audio listener and must keep
  `UniversalAdditionalCameraData.renderShadows` and `renderPostProcessing` on.
- `InputSystem_Actions` map name is `Player`. `Attack` is reused for shooting,
  while `Melee` and `EmoteWheel` are separate typed actions. `Crouch` remains
  reserved and has no behavior.
- The `Player` physics layer is excluded from camera collision and aim masks.
- `Animator.applyRootMotion` is disabled; the controller owns movement.
- Emote `AnyState` entry uses `PlayEmote` trigger plus `EmoteIndex`; the held
  `Emoting` bool is only an interrupt condition.

## Gotchas

- Do not replace the crater mesh collider with an approximate sphere. The
  visible crater floor differs enough from a sphere to make the player appear
  buried or floating even when physics reports grounded.
- When changing planet scale, move scene spawns by the same center-relative
  factor. The startup snap begins with a short outward cast, so a spawn left
  inside a newly enlarged shell cannot reliably find the exterior surface.
- Do not hand-edit a broken nested Player GUID. Run `Repair Player Rig Prefab`;
  it validates the current prefab source and every critical object reference.
- A held bool on an `AnyState` entry restarts the destination animation each
  frame. One-shot entries need a trigger; a bool may control their exit.
- FBX clip names may be `<Armature>|<Action>` and can contain trailing spaces.
  The setup script intentionally performs exact, suffix, then trimmed matching.
- `Build Test Scene` rebuilds generated assets and is destructive to the
  sandbox scene/prefab. Do not run it merely to repair the rig.

## How to extend

Split locomotion versus camera/tool authority at system boundaries when the
cooperative control model is approved; do not add player-index branches inside
this single-player controller. Real combat should extend `PlayerCombat` and
`Projectile`. A future stagger mechanic can reuse the dormant `Crouch` input.

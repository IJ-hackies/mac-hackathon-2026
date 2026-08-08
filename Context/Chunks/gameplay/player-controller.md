---
chunk: player-controller
title: Single-player third-person prototype (movement, camera, emotes)
owns:
  - "Assets/Scripts/Player/PlayerController.cs*"
  - "Assets/Scripts/Player/PlayerAnimatorRelay.cs*"
  - "Assets/Scripts/Player/PlayerEmoteController.cs*"
  - "Assets/Scripts/Player/PlayerVisualGroundConformer.cs*"
  - "Assets/Scripts/Player/RadialCapsuleMotor.cs*"
  - "Assets/Scripts/Player/ThirdPersonCameraController.cs*"
  - "Assets/Scripts/UI/CrosshairUI.cs*"
  - "Assets/Scripts/UI/EmoteWheelUI.cs*"
  - "Assets/Editor/Player/**"
  - "Assets/Prefabs/PlayerRig.prefab*"
related: [control-model, core-loop, unity-project, runtime-art, world-authoring, state]
verifiedAtCommit: db81cd848e59c29f89795a89d512b044041e215a
lastVerified: 2026-08-08
---

## What this is

The single-player prototype controls one astronaut with full locomotion,
camera, combat, and emotes. `Player.unity` remains a flat-ground sandbox;
`SampleScene.unity` instantiates `PlayerRig.prefab` on the spherical planet.
This does not resolve the future two-player responsibility split.

The astronaut uses a rotatable `CapsuleCollider` and kinematic `Rigidbody`
driven by `RadialCapsuleMotor`; unlike Unity's world-upright
`CharacterController`, its physical capsule follows planetary up at every
latitude. The motor sweeps, slides, depenetrates, and preserves the former
`0.3`-unit step behavior in local radial coordinates. A centerline foot ray
selects the real support normal that may steer grounded adhesion. A broad foot
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

## Key files

- `PlayerController.cs` - tangent locomotion, acceleration-smoothed walk/sprint,
  surface-relative gravity/jump, slope-filtered ground probing, body alignment,
  camera-facing rotation, boss stagger gating, and one-time surface snap.
  Exposes locomotion state plus the current ground normal/clearance for visual
  consumers.
- `RadialCapsuleMotor.cs` - rotation-aware capsule sweep/slide, local-radial
  stepping, contact classification, and overlap depenetration through a
  kinematic Rigidbody.
- `PlayerVisualGroundConformer.cs` - tilts and lifts only the astronaut visual
  root toward reported support while the physical capsule and camera stay
  radial.
- `ThirdPersonCameraController.cs` - radial-up orbit, shoulder framing,
  `SphereCast` collision, smoothing, independent mouse look, boss shake, and a
  cutscene follow-pose query. The rig camera has URP additional-camera data
  with shadows and post-processing enabled.
- `PlayerAnimatorRelay.cs` - maps controller state to `Speed`, `Grounded`, and
  `Jump` Animator parameters.
- `PlayerCombat.cs` and the health/death HUD are documented in
  [player-combat](player-combat.md). Ranged damage is hitscan; its traveling
  bolt is cosmetic and the former `Projectile.cs`/prefab were removed.
- `PlayerEmoteController.cs` / `Assets/Scripts/UI/**` - locked-cursor virtual
  joystick emote wheel. Movement, jumping, or attacking interrupts an emote.
- `PlayerRig.prefab` - normalized rig root containing the current nested
  radial `Player.prefab`, camera pivot, crosshair/emote UI, and health HUD.
  Serialized camera/combat/emote links are validated by the repair command;
  the health HUD can discover the nested player's `Health` at runtime.
- `PlayerSceneSetup.cs` - `Tools > Player Prototype > Build Test Scene` creates
  the sandbox artifacts. `Repair Player Rig Prefab` safely replaces an
  orphaned nested Player reference, rewires dependencies, normalizes transforms,
  saves, reloads, and validates the prefab.

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
- Character facing follows the camera's tangent forward/aim direction, not the
  movement vector. Preserve strafe and backpedal behavior when changing turn
  smoothing or input projection.
- `VisualRoot` is a direct Player child containing both the astronaut render
  hierarchy and muzzle. The ground conformer may tilt it by at most 30 degrees
  and lift it by at most `0.12` units from measured support clearance, then
  returns it to its authored local pose while airborne. Never place the
  capsule, controller, rig camera, or camera pivot beneath this visual root.
- The Player capsule is `height 2.55`, `radius 0.55`, `center.y 1.275`; its
  transform origin is at the feet. Its Rigidbody stays kinematic with Unity
  gravity off, interpolation on, and Continuous Speculative collision. Keep
  mesh, capsule, motor, and spawn-snap math in agreement when changing scale.
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
  buried or floating even when physics reports grounded; its support normal
  also defines grounded adhesion.
- When changing planet scale, move scene spawns by the same center-relative
  factor. The startup snap begins with a short outward cast, so a spawn left
  inside a newly enlarged shell cannot reliably find the exterior surface.
- Change the planetary spawn by moving the top-level `PlayerRig` scene instance,
  not its nested Player or the prefab asset. The radial surface-snap window can
  place it accurately: use radial-up alignment, preserve heading, and zero
  offset, then save the scene without applying the root transform to the prefab.
  Keep the spawn's radial cast path clear because startup snapping uses the
  nearest collider in `groundMask`, including roofs and other environment art.
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

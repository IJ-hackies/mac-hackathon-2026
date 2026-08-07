---
chunk: player-controller
title: Single-player third-person prototype (movement, camera, combat, emotes)
owns:
  - "Assets/Scripts/Player/**"
  - "Assets/Scripts/UI/**"
  - "Assets/Editor/Player/**"
  - "Assets/Prefabs/**"
related: [control-model, unity-project, runtime-art, state]
verifiedAtCommit: 1a62b900ec593300f3b8cd68ec32e2df106d6e9c
lastVerified: 2026-08-07
---

## What this is

A working, single-player-only third-person prototype built on the `feat/player`
branch in a dedicated `Assets/Scenes/Player.unity` scene, kept separate from
`SampleScene`/`Sandbox` to avoid scene-merge conflicts. It does not implement
the two-player shared-body split (`cooperative-control-partition` in
`STATE.md` remains open); it is full single-control locomotion + actions for
one astronaut, matching the control-model's single-player-parity invariant.

Character rotates to face its movement direction; the camera orbits
independently and never locks to a facing direction (Genshin/Wuthering Waves
style), offset over the shoulder rather than dead-center behind the player so
aiming doesn't mean looking through your own head. Mouse always drives camera
orbit; the cursor stays locked even while the emote wheel is open (see
`PlayerEmoteController` below).

No crouch/stagger exists right now — it was implemented once (held `Crouch`
bool, `Duck` clip) and then deliberately removed; see Gotchas for why, and
`How to extend` for the planned replacement.

## Key files

- `Assets/Scripts/Player/PlayerController.cs` - `CharacterController`-based
  movement, camera-relative direction, acceleration-smoothed speed (so
  sprint toggling eases instead of snapping), gravity/jump,
  face-movement-direction rotation. Exposes `NormalizedSpeed`, `IsGrounded`,
  `JumpTriggeredThisFrame` for other systems to read (non-consuming, reset
  each frame — multiple components read it the same frame).
- `Assets/Scripts/Player/PlayerAnimatorRelay.cs` - pushes controller state
  into Animator parameters `Speed` (float, damped via `Animator.SetFloat`'s
  dampTime overload to avoid blend-tree pops), `Grounded` (bool), `Jump`
  (trigger).
- `Assets/Scripts/Player/PlayerCombat.cs` - `Melee` (Punch) and `Attack`
  (reused as "Fire", plays `Run_Gun_Shoot` and spawns a projectile). Always
  plays the running-gun-shoot pose on every shot regardless of movement —
  the neutral Idle pose has the arms hanging down with no gun mesh to read
  as "aiming", which looked more broken than the mismatched running legs, so
  this was tried both ways and settled on always-play. Aims by raycasting
  from the camera through the screen-center crosshair; the projectile
  spawns from a fixed `Muzzle` child transform (no gun mesh in the asset
  pack to attach to) rather than the camera, and a short point-light muzzle
  flash gives standing shots feedback. No damage/hit system — animation and
  projectile visuals only, since no combat/enemy system exists yet
  (`core-game-loop` in `STATE.md`). Exposes `IsAttacking` for the
  emote-interrupt check.
- `Assets/Scripts/Player/Projectile.cs` - minimal forward-mover with
  lifetime timeout and trigger despawn; ignores collision with the firing
  player's own collider.
- `Assets/Scripts/Player/PlayerEmoteController.cs` - Fortnite-style emote
  wheel, but cursor stays locked throughout (confirmed preference — a
  visible highlight is enough, unlocking the cursor isn't needed). Reads
  the same mouse-delta `Look` input the camera uses and accumulates it into
  a virtual joystick direction (`Vector2`, clamped to magnitude 1) while
  `EmoteWheel` (B) is held; `ThirdPersonCameraController.InputSuspended`
  stops that same delta from also spinning the camera. Selection comes from
  the accumulated direction's angle, not real cursor position. Playback
  duration is timed in script from the selected `AnimationClip.length`, and
  is cancelled early if the player moves, jumps, or attacks mid-emote
  (checked every frame against `PlayerController`/`PlayerCombat` state).
- `Assets/Scripts/UI/EmoteWheelUI.cs` - true hollow-center donut wheel (3
  wedges via `Image.Type.Filled` + `Radial360` on a procedurally generated
  ring sprite, not floating icon buttons) / `CrosshairUI.cs` - both built
  entirely by the Editor setup script, no external art.
- `Assets/Scripts/Player/ThirdPersonCameraController.cs` - orbit rig: mouse
  `Look` drives yaw/pitch of a pivot that follows the player; camera child
  pulls in via `SphereCast` collision against a mask excluding the `Player`
  layer. Offset from dead-center behind the player via
  `shoulderOffsetX`/`shoulderOffsetY` — without it the camera looks
  parallel to (not at) the pivot, so its forward ray runs straight through
  the player's own head/shoulders and the fixed center crosshair sits on
  the character instead of in open space. `InputSuspended` flag lets the
  emote wheel reuse the same Look delta without also turning the camera.
- `Assets/Editor/Player/PlayerSceneSetup.cs` - `Tools ▸ Player Prototype ▸
  Build Test Scene` menu command. Builds `Assets/Art/Animations/
  AC_Player.controller` from the astronaut FBX's baked clips, the
  `Assets/Prefabs/Projectile.prefab`, the HUD `Canvas` (crosshair + emote
  wheel, including the generated `Assets/Art/Textures/T_UIWheelRing.asset`
  ring sprite), assembles the player and camera rig, and saves
  `Assets/Scenes/Player.unity` plus `Assets/Art/Models/Characters/
  Player.prefab`. Always rebuilds the AnimatorController from scratch on
  rerun (not skipped) so animator fixes always take effect without manually
  deleting the asset first.

## Invariants

- `InputSystem_Actions` action map name is `Player`. `Melee` (V) and
  `EmoteWheel` (B) are new actions. `Attack` (left click) is deliberately
  reused for shooting rather than adding a redundant action — read that as
  "Fire" in `PlayerCombat`, not literal melee. `Crouch` (Left Ctrl) still
  exists in the input asset with no behavior wired to it — reserved for the
  planned stagger feature. "Generate C# Class" is enabled on the
  `.inputactions` asset so scripts use the typed `InputSystem_Actions`
  wrapper instead of string lookups.
- Movement/combat/camera scripts assume a `Player` physics layer exists;
  the Editor setup script creates it in `ProjectSettings/TagManager.asset`
  if missing and excludes it from both the camera's collision mask and the
  shooting raycast's aim mask.
- `Animator.applyRootMotion` is disabled — locomotion is fully driven by
  `CharacterController`, not root motion from the clips.
- The Animator stays single-layer/full-body: every clip used (Punch,
  Run_Gun_Shoot, Wave, Yes, No) is full-body, so there's no avatar-masked
  upper/lower body layering here.
- Standing vs. running jump is a genuine visual difference even though the
  pack has only one `Jump` (takeoff) clip: a standing jump plays it, a
  running jump skips straight to the `Jump_Idle` falling loop instead of
  showing the standing pose over running legs (branch on `Speed` at the
  moment the `Jump` trigger fires, in `AC_Player.controller`).
- Every `AnyState` transition in `AC_Player.controller` has
  `canTransitionToSelf = false` explicitly set (defensive, but see Gotchas —
  it alone did not fully fix the emote restart bug).
- Emote entry (`AnyState → Emote_Wave/Yes/No`) is gated by a dedicated
  `PlayEmote` trigger + `EmoteIndex`, not the held `Emoting` bool. `Emoting`
  is only used for the early-interrupt exit transition out of the emote
  states. See Gotchas for why entry had to move to a trigger.

## Gotchas

- **Do not gate an `AnyState` entry transition on a bool that stays true for
  the whole action — use a Trigger.** This was the cause of "Wave/Yes/No
  looping and not completing" and "crouch spasming": `PlayerEmoteController`
  holds `Emoting = true` for the entire clip duration (by design, so it can
  cancel early), so `AnyState → Emote_X` kept re-satisfying its condition
  every frame and restarting the clip from frame 0. Setting
  `canTransitionToSelf = false` on the transition was tried first and did
  **not** fully fix it — emotes still restarted. The actual fix was
  switching entry to a dedicated `PlayEmote` trigger (+ `EmoteIndex`),
  mirroring how `Melee`/`Fire`/`Jump` already worked reliably: triggers
  auto-consume after one use, so the condition can't still be true on the
  next frame. `Emoting` is still used, but now only for the state's
  *outgoing* interrupt transition (`Emoting == false → Idle`), which was
  never the problem since that isn't an `AnyState` transition. If a future
  one-shot state needs "start now, held-flag can cancel early" behavior,
  split it the same way: Trigger for entry, bool for the interrupt exit
  only — never a bool alone for `AnyState` entry.
- Crouch was implemented once (held bool, `CharacterController` height/
  center lerp, `Duck` clip) and removed entirely at the user's request — it
  hit the bug above (visible as jittery "spasming") and a stagger mechanic
  is planned to replace it later rather than fixing crouch further. Nothing
  in the current scripts references crouch; the `Crouch` input binding was
  left in place as a ready-to-use hook for that future work.
- FBX clip names come from Blender as `<ArmatureName>|<ActionName>` (e.g.
  `CharacterArmature|Idle`), not the plain action name, and at least one
  clip has been observed with stray trailing whitespace from the FBX's
  binary data — clip lookups in `PlayerSceneSetup.cs` (`GetClip`) try exact
  match, then suffix-after-`|` match, then a trimmed short-name match, all
  case-insensitive. If a lookup still fails the Console logs a warning
  listing the clips it actually found on the model.
- `AnimatorController`/scene/prefab/UI/projectile assets referenced here
  only exist after a contributor runs the Editor menu command inside
  Unity — they are generated artifacts, not hand-authored, and may not be
  present yet on a fresh checkout of this branch.

## How to extend

When the cooperative control split is designed, split `PlayerController`'s
responsibilities (locomotion vs. camera/tool control) per player rather than
adding player-index branching inside this single-player controller. If real
combat/health is added later, `PlayerCombat.IsAttacking` and `Projectile`
are the natural hook points rather than rebuilding the trigger plumbing. The
planned stagger mechanic should reuse the `Crouch` (Left Ctrl) binding
already present in `InputSystem_Actions` rather than adding a new action,
and should set `canTransitionToSelf = false` on its `AnyState` transition if
it's gated by a held bool (see Gotchas).

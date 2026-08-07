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
  - "Assets/Editor/ModelAnimationUtility.cs"
  - "Assets/Prefabs/PlayerRig.prefab"
  - "Assets/Art/Models/Characters/Player.prefab"
related: [control-model, unity-project, runtime-art, state, enemies, player-combat, boss-fight]
verifiedAtCommit: 71b7468850b4e64c25da49ef3deff2ff354c4778
lastVerified: 2026-08-08
---

## What this is

A working, single-player-only third-person prototype built on a dedicated
`Assets/Scenes/Player.unity` scene, kept separate from `SampleScene`/`Sandbox`
to avoid scene-merge conflicts. Does not implement the two-player shared-body
split (`cooperative-control-partition` in `STATE.md` remains open).
Melee/hitscan combat, player health, and death are covered separately in
[player-combat](player-combat.md).

Character yaw always tracks the camera's yaw (standard third-person-shooter
scheme), so movement input is relative to where the player is looking —
pressing "back" moves behind the character rather than turning to face
wherever it's moving. Camera orbits independently off mouse look, offset over
the shoulder so aiming doesn't mean looking through the character's own head.
Cursor stays locked even while the emote wheel is open.

Locomotion clips (`Walk_Gun`/`Run_Gun`, `Idle_Gun` at rest) are forward-only —
no strafe/backpedal animations in the asset pack — so moving backward/
sideways relative to the camera-locked facing still plays the forward-run
pose; a known limitation, revisit if directional blend clips become
available. This base pose set always holds the gun raised/aimed so the
character doesn't visibly drop/re-raise it around every shot — see
[player-combat](player-combat.md).

Landing only plays the stand-still `Land` recovery pose with no move input at
touchdown (`Fall`→`Land`, gated on `Speed`); landing while still moving skips
straight into `Move` instead, so running off a ledge doesn't freeze the legs.

Stagger now exists for real: `PlayerController.Stagger(duration)` — called
externally by [boss-fight](boss-fight.md)'s mech ground-slam, not bound to
input — locks move/jump and plays `Duck` via a `Stagger` trigger, using a
timestamp-based lock (`IsStaggered`), not the removed held-bool approach (see
Gotchas). `Awake` looks up `Duck`'s clip length once; `Stagger` clamps the
lock to at least that length so a short `duration` still can't cut the
animation short ("the entire duck animation must play").

The scene's root hierarchy is now `PlayerRig` (player + camera + HUD as
children), saved as `Assets/Prefabs/PlayerRig.prefab` for one-drag reuse.
`PlayerSceneSetup.BuildTestScene` separately still saves the inner character
alone as `Assets/Art/Models/Characters/Player.prefab` — not interchangeable;
`PlayerRig.prefab` is the one to reuse for scene setup.

## Key files

- `Assets/Scripts/Player/PlayerController.cs` - `CharacterController`-based
  movement, camera-relative direction, acceleration-smoothed speed (sprint
  toggling eases, not snaps), gravity/jump, and `RotateTowardsCamera` which
  locks yaw to the camera's flattened forward every frame (even standing
  still, so turning in place works). Exposes `NormalizedSpeed`, `IsGrounded`,
  `JumpTriggeredThisFrame` for other systems (non-consuming, reset each
  frame — multiple components read it the same frame).
- `PlayerAnimatorRelay.cs` - pushes controller state into Animator params
  `Speed` (damped), `Grounded`, `Jump`.
- `Assets/Scripts/Player/PlayerEmoteController.cs` - Fortnite-style emote
  wheel, cursor stays locked throughout. Reads the same mouse-delta `Look`
  input into a virtual joystick direction while `EmoteWheel` (B) is held
  (`ThirdPersonCameraController.InputSuspended` stops that delta from also
  spinning the camera); selection is by accumulated-angle. Cancelled early on
  move/jump/attack. `UI/EmoteWheelUI.cs`/`CrosshairUI.cs` (donut wheel via
  `Image.Type.Filled`+`Radial360`) are built entirely by the Editor script.
- `Assets/Scripts/Player/ThirdPersonCameraController.cs` - orbit rig: mouse
  `Look` drives yaw/pitch of a pivot; camera child pulls in via `SphereCast`
  against a mask excluding `Player`/`Enemy` (Invariants). Offset from
  dead-center via `shoulderOffsetX`/`shoulderOffsetY` — without it the
  camera's forward ray runs through the player's own head and the fixed
  crosshair sits on the character instead of open space. `Shake(duration,
  magnitude)` layers decaying jitter onto the pivot each `LateUpdate` — used
  by [boss-fight](boss-fight.md)'s mech ground-slam/cutscene.
- `Assets/Editor/Player/PlayerSceneSetup.cs` - `Tools ▸ Player Prototype ▸
  Build Test Scene`. Builds `AC_Player.controller` (`HitReact`/`Death`
  AnyState overlays; `BuildArmsLayer`/`BuildUpperBodyMask` add a second,
  upper-body-masked `Override` layer for the Shoot poses — mask computed from
  the model's transform names since the rig is Generic, not Humanoid; see
  [player-combat](player-combat.md)) from the FBX's baked clips, builds the
  HUD, assembles the `PlayerRig` (player/camera/HUD) plus combat/death/health
  components, saves `Player.unity` + both prefabs above. Always rebuilds from
  scratch, wiping [enemies](enemies.md)'s output — rerun that after.
- `Assets/Editor/ModelAnimationUtility.cs` - clip-lookup/looping/layer helpers
  shared by `PlayerSceneSetup`/`EnemySceneSetup`. `ConfigureAnimationLooping`
  fully rebuilds the clip-split table from `importedTakeInfos` every run
  rather than only adding missing clips, since that missed new takes on swap.

## Invariants

- `InputSystem_Actions` action map name is `Player`. `Melee` (V)/`EmoteWheel`
  (B) are new actions; `Attack` (left click) is reused for shooting, read as
  "Fire" in `PlayerCombat`. `Crouch` (Left Ctrl) remains unwired — stagger
  ended up entirely externally-triggered instead (see Gotchas).
- Movement/camera scripts assume a `Player` physics layer (Editor script
  creates it if missing). An `Enemy` layer also exists (shared with
  [enemies](enemies.md)); camera collision mask excludes both so it clips
  *through* an enemy instead of snapping closer, for a swarm-heavy game.
- `Animator.applyRootMotion` is disabled; locomotion is fully
  `CharacterController`-driven. `AC_Player.controller` has two layers — base
  (locomotion + one-shots) and `Arms` (upper-body-masked, see
  [player-combat](player-combat.md)).
- Standing vs. running jump differs despite one `Jump` clip: standing plays
  it, running skips straight to `Jump_Idle`. Every `AnyState` transition has
  `canTransitionToSelf = false` (defensive, but alone did not fully fix the
  emote restart bug — see Gotchas).
- Emote entry (`AnyState → Emote_Wave/Yes/No`) is gated by a dedicated
  `PlayEmote` trigger + `EmoteIndex`, not the held `Emoting` bool (only used
  for the early-interrupt exit). See Gotchas.

## Gotchas

- **Do not gate an `AnyState` entry transition on a bool that stays true for
  the whole action — use a Trigger.** Caused "Wave/Yes/No looping and not
  completing": `Emoting` stays true for the whole clip, so `AnyState →
  Emote_X` kept re-satisfying every frame; `canTransitionToSelf = false`
  alone did **not** fix it. Fix: dedicated `PlayEmote` trigger for entry, bool
  only for the outgoing interrupt — reused for [player-combat]
  (player-combat.md)'s `FireStart`/`Firing` split.
- Crouch was implemented once (held bool, height/center lerp, `Duck` clip)
  and removed entirely — hit the bug above. The real stagger mechanic that
  replaced it reused the `Duck` clip but not the `Crouch` binding.
- FBX clip names come from Blender as `<ArmatureName>|<ActionName>`, not the
  plain action name, and at least one clip has stray whitespace —
  `ModelAnimationUtility.GetClip` tries exact match, suffix-after-`|`, then
  trimmed short-name, case-insensitively.
- `AnimatorController`/scene/prefab/UI/mask assets referenced here only exist
  after a contributor runs the Editor menu command inside Unity.
- Repo root sits inside iCloud Drive, which periodically creates " 2"/" 3"
  conflict-copy files while syncing (a stale `InputSystem_Actions 2.cs`
  caused CS0102/CS0111). Search for another " 2"/" 3" file on similar errors.

## How to extend

When the cooperative control split is designed, split `PlayerController`'s
responsibilities per player rather than player-index branching here. Any new
one-shot Animator state gated by a held bool should follow the
Trigger-entry/bool-exit pattern (see Gotchas).

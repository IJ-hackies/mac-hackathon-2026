---
chunk: ultimate
title: Player Ultimate (Mech mode), Dash, Shield, and secondary attacks
owns:
  - "Assets/Scripts/Player/PlayerUltimate.cs*"
  - "Assets/Scripts/Player/PlayerDash.cs*"
  - "Assets/Scripts/Player/PlayerShield.cs*"
  - "Assets/Scripts/Player/PlayerAbilityInput.cs*"
  - "Assets/Scripts/UI/AbilityHudUI.cs*"
  - "Assets/Scripts/UI/UltimateHudUI.cs*"
  - "Assets/Scripts/Vfx/TopDownGroundEffect.cs*"
related: [player-controller, player-combat, enemies, boss-fight, items, state]
verifiedAtCommit: efe8c5547b0a83b0eeadffbff6751ad39f8c28b9
lastVerified: 2026-08-08
---

## What this is

Picking up the Thunder item (`Items.ThunderPickup`, previously a visuals-only
stub) activates a timed "Ultimate": the player becomes a 1.4x-scaled Mech
version of Finn the Frog for 40s (base, no upgrades) with a reworked attack
profile, plus two movement/defense abilities on the shared `Ability` action
(Shift, repurposed from the removed Sprint action): `PlayerDash` normally,
`PlayerShield` while active. `PlayerUltimate.cs` reuses the existing
`PlayerController`/`PlayerCombat` pipeline (locomotion, camera, health,
stagger) - only the active visual model, its `AnimatorController`, and
`PlayerCombat`'s attack profile swap, per the decision not to build a
separate mech controller class.

The Mech has **full animation parity** with the astronaut, not a bind-pose
placeholder: `PlayerSceneSetup.BuildMechAnimatorController`/
`BuildMechArmsLayer` build a second `AnimatorController`
(`AC_PlayerMech.controller`) off `Mech_FinnTheFrog.fbx`, deliberately
reusing the astronaut controller's exact parameter/state names (`Speed`,
`Grounded`, `Jump`, `Melee`, `FireStart`, `Firing`, `Emoting`/`EmoteIndex`/
`PlayEmote`, `Death`, `Stagger`, an `"Arms"` layer with idle state
`"Arms_Idle"`) so every animator-driving component just **retargets which
`Animator` it drives** (all five gained `SetAnimator`; `PlayerUltimate.
ActivateUltimate`/`EndUltimate` call them when swapping `astronautAnimator`/
`mechAnimator`). `Melee` enters `"Kick"` instead of `"Punch"`; `Stagger`
plays the Mech's `Pickup` clip (its closest "reacting" take - the Mech has
no `Duck`); `BuildUpperBodyMask` takes a `MechUpperBodyMaskPath` so building
the Mech's mask doesn't overwrite the astronaut's.

The emote wheel gained a Mech-only 4th option via `EmoteWheelUI.Configure
(string[] labels)` (rebuilds N equal-angle wedges instead of a fixed
3-wedge layout), keeping separate `_baseClips`/`_mechClips` timing arrays.
Dance (index 3) **loops until interrupted** (`_emoteEndTime =
float.PositiveInfinity`, relying on the existing `Emoting`-false interrupt
transition).

## Key files

- `PlayerUltimate.cs` - `ActivateUltimate()` toggles `astronautVisualRoot`/
  `mechVisualRoot` (both pre-built under `VisualRoot`, mech inactive by
  default - same pattern [boss-fight](boss-fight.md)'s inert-mech-until-
  cutscene uses), calls `PlayerCombat.SetUltimateActive(true)`/
  `PlayerShield.ResetEnergy()`, swaps all five components' `Animator`
  references, sets `PlayerAmmo.InfiniteAmmo`, applies `cameraExtraHeight`
  via `ThirdPersonCameraController.SetExtraHeight`, and counts down to
  `EndUltimate()`. `duration`/`mechScale` (1.4) are placeholder tunables the
  user hand-tunes and reports back exact numbers for.
- `PlayerDash.cs` - normal-mode ability. `TryDash()` (3s cooldown) reads
  held `Move` input via `PlayerController.GetCameraRelativeTangentDirection`,
  falling back to current facing if idle, then calls `PlayerController.
  Dash(direction, speed, duration)` - sets fields `FixedUpdate` reads to
  override tangent motion, bypassing body-rotation logic. Must enable its
  own `InputSystem_Actions.Player` map in `OnEnable` (an unenabled map's
  `ReadValue` silently returns zero - previously made every dash ignore
  WASD and launch toward current facing instead). Spawns Lana Studio's
  `Burst/Poof_electric` at the launch point.
- `PlayerShield.cs` - Ultimate-mode ability on the same `Ability` action.
  Holding drains an energy pool faster than it regenerates while released
  (`drainPerSecond > regenPerSecond`, so permanent uptime is impossible);
  while active sets `Combat.Health.IncomingDamageMultiplier = 0` (see
  [enemies](enemies.md)) for full mitigation without attacker-side
  branching. Spawns Lana Studio's `Shields/Shield_electric` under
  `mechVisualRoot`.
- `PlayerAbilityInput.cs` - owns `Ability`, routes `started`/`canceled` to
  `PlayerDash.TryDash()` (one-shot) or `PlayerShield.SetHeld(bool)` (held)
  based on `PlayerUltimate.IsActive`.
- `PlayerCombat.cs` (owned by [player-combat](player-combat.md), documented
  here for its ultimate half): `SetUltimateActive(bool)` switches primary
  fire to `FireElectricBolts()` (both mech muzzles fire every beat, each
  with an `onHit` callback calling `EnemyBase.ApplySlow`). `Attack2`
  (right-click) fires `FireSingleTopDownBeam()` (base, nearest enemy) /
  `FireLightningCircles()` (Ultimate, `lightningCircleCount` nearest
  enemies) through `Vfx.TopDownGroundEffect.Play` (shared with
  [boss-fight](boss-fight.md)'s `BossMechAI.TopDownAttack`).
  `secondaryTelegraphDelay`/`ultimateSecondaryTelegraphDelay` are both `0`
  ("instant vfx and damage on right click" - point is captured and damage
  is checked in the same call, no window for the target to move), with
  `secondaryHitRadius`/`ultimateSecondaryHitRadius` widened (3.2/3.5) so a
  moving target is still caught even at true zero delay. The Mech's
  `Arms_Shoot_Big` clip (cosmetic only, doesn't gate damage timing) is sped
  1.6x (`BuildMechArmsLayer`'s `armsShootBigState.speed`) so it doesn't read
  slower than the attack's actual instant resolution.
- `Vfx/TopDownGroundEffect.cs` - static `Play(prefab, point, telegraphDelay,
  lingerAfterHit, onImpact)`: hides the prefab's `hit_controller` child so
  only `shot_controller`'s telegraph shows, waits `telegraphDelay`, reveals
  `hit_controller`, invokes `onImpact`, self-destructs after.
  `telegraphDelay <= 0` skips the wait entirely (no `yield` at all, not a
  zero-length `WaitForSeconds` - the latter still pushes `onImpact` to the
  next frame) so VFX + damage resolve in the exact same frame/call as
  `StartCoroutine`; [boss-fight](boss-fight.md)'s `BossMechAI.TopDownBeam`/
  `TopDownRocket` keep their own non-zero `topDownTelegraphDelay` (a
  deliberate boss dodge-window, unaffected).
- `AbilityHudUI.cs`/`UltimateHudUI.cs` - generated-UI-rect, no external art.
  `AbilityHudUI` (bottom-left): slot A shows Dash cooldown or Shield energy
  (re-labeled live by `PlayerUltimate.IsActive`), slot B shows
  `PlayerCombat.SecondaryCooldownRemaining`/`SecondaryCooldownDuration`.
  `UltimateHudUI` (top-left, hidden unless active): fill bar + "ULTIMATE Ns"
  countdown. Every procedurally-built filled-bar `Image` must get a sprite
  via `GetOrCreateSolidSprite()` (wraps `Texture2D.whiteTexture`) - a null
  `Image.sprite` on `Type.Filled` silently renders as an unclipped full
  rect regardless of `fillAmount`, which is why these bars first appeared
  permanently full.

**Damage-falloff formula (Ultimate secondary)**: circles beyond the live-
enemy count retarget the closest enemies round-robin. Each target tracks
circles landed *this cast* (`_lightningOccurrences`, cleared per
`FireLightningCircles` call and on `SetUltimateActive`): `damage =
baseDamage * Mathf.Max(0.2f, 1f - 0.2f * occurrenceIndex)` -
100/80/60/40/20/20/20%..., floored at 20%, never zero.

## Invariants

- `PlayerUltimate`/`PlayerDash`/`PlayerShield` never touch `PlayerController`
  locomotion fields except through `Dash(...)`, `GetCameraRelativeTangent
  Direction(...)`, and `SetMovementSpeedModifier` - no mech controller
  subclass. `Health.IncomingDamageMultiplier` is the only damage-mitigation
  hook; reuse it for "briefly invulnerable" rather than per-attacker checks.
  `EnemyBase.SpeedMultiplier`/`ApplySlow` stays a single timed debuff
  (refreshes on reapply, no stacking) since only the electric bolts use it.
- VFX parented under the Mech's own hierarchy (e.g. Stun VFX under
  `mechHeadAnchor`) inherit `mechScale` automatically through normal
  transform scaling. VFX parented directly under the (unscaled) player root
  need explicit `transform.localScale *= PlayerUltimate.VfxScaleMultiplier`
  (`IsActive ? mechScale : 1`) - e.g. `Items.ItemPickup`'s pickup burst.
- All 6 muzzle transforms (player `Muzzle`, Mech `MechMuzzleLeft`/`Right`,
  [boss-fight](boss-fight.md)'s `BossAstronautAI.muzzle` and
  `BossMechAI.firePointLeft`/`firePointRight`) use exact values the user
  hand-tuned in the Editor and reported back - edit the literals in
  `PlayerSceneSetup.BuildUltimate`/`BossSceneSetup.cs` directly for future
  corrections, don't re-guess.
- Camera: `ThirdPersonCameraController.targetOffset.y = 3.2`, `_pitch = 15`,
  `distance = 8`; `PlayerUltimate.cameraExtraHeight = 1` adds on top only
  while the Mech is active via `SetExtraHeight`/`GetEffectiveTargetOffset`
  (Mech pivot height = 4.2), reset to 0 on `EndUltimate`. A moderate
  over-the-shoulder rig needs far less pitch correction than a zoomed-far-
  out one - if "zoom out" is requested again, prefer a modest distance
  increase over re-inflating `_pitch` to compensate.
- Aim uses `PlayerSceneSetup.CrosshairViewportY` (0.62, anchor-fraction, not
  a pixel offset) driving both the crosshair `RectTransform` anchor and
  `PlayerCombat.aimViewportY`'s `ViewportPointToRay` origin - **always
  change both through this one constant**, never one field alone, or the
  reticle and the actual aim ray drift apart. Only affects
  `FireProjectile`/`FireElectricBolts`; the secondary attacks target the
  nearest enemy directly, never the crosshair ray.
- `PlayerController.jumpHeight = 4.2` (x3 from original) is shared physics
  for both forms. The Mech's `Jump` Animator state exits on the shared
  `Grounded` bool (mirroring the astronaut's Fall/Land pattern) rather than
  `exitTime`, and holds its last frame once the clip finishes (non-looping)
  as a hang-time pose - fixes the clip finishing/landing visually before
  the character's actual airtime ends.

## Gotchas

- The mech rendered solid white when first assigned `M_Astronaut.mat`
  directly (UV layout mismatch) - fixed with a dedicated
  `M_MechFinnTheFrog.mat` filling every renderer's `sharedMaterials` slots
  (not just slot 0). **Root cause** was ordering, not the material itself:
  `BuildUltimate` called `ModelAnimationUtility.ConfigureAnimationLooping`
  (which calls `ModelImporter.SaveAndReimport()`) *after* the material
  fixup - reimporting a Model asset re-syncs every existing scene instance
  back to the importer's own default material mapping, silently discarding
  the override. Fixed by moving material assignment to run *after* every
  `SaveAndReimport()`-triggering call in the same build pass. **General
  lesson**: any per-instance material/component tweak on a Model Prefab
  Instance must run after all reimport calls on that source model, or it
  gets silently wiped.
- The Mech-import bug that left the player invisible in Ultimate mode was
  `PlayerSceneSetup.BuildUltimate` using `AssetDatabase.CopyAsset` for
  `Mech_FinnTheFrog.fbx` - that API only works between two `Assets/`-
  internal paths, and the vendor pack lives outside it, so the copy
  silently failed and `mechModel` stayed null. Fixed with `System.IO.
  File.Copy` (same pattern as `Items.ItemAssetSetup.
  CopyVendorModelsWhenMissing`); `ActivateUltimate` now only hides the
  astronaut if `mechVisualRoot` is actually non-null, degrading to "still
  looks like the astronaut" instead of invisible.
- Lana Studio's `Range_attack` prefabs have internal particle children
  baked with a ±90° **Y**-axis (yaw) rotation relative to the prefab root,
  not X (confirmed reading the `.prefab` YAML `m_LocalRotation`) - an
  initial X-axis guess (`Euler(90,0,0)`) was wrong, replaced by
  `Euler(0,90,0)`. Every `...RotationOffset` field wired from a
  `Range_attack` prefab uses this value.
- Crosshair anchor-fraction fix (see Invariants) was needed because the
  original pixel-offset math (`(CrosshairViewportY - 0.5f) * 1080f`
  assuming a fixed 1920x1080 reference resolution) only maps 1:1 onto
  `ViewportPointToRay`'s actual viewport fraction at exactly 16:9 - invisible
  at dead-center (`0.5`, offset always 0) which is why moving the crosshair
  off-center first exposed it as shots landing away from the reticle.
- A `Tools/Player Prototype/Wire Ultimate Into Player Rig` repair command
  was **not** built - `BuildTestScene` already wires everything on rebuild.

## How to extend

`electricSlowPercent`, `lightningCircleCount`, and the cooldown/duration
fields on `PlayerCombat`/`PlayerUltimate`/`PlayerDash`/`PlayerShield` are
placeholders meant to be scaled by a future upgrades system - wire that
into these serialized fields rather than hardcoding new logic paths.

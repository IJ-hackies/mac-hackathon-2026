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
verifiedAtCommit: 262413a1cda18eaed7a50511bb0aa8f10bcb533a
lastVerified: 2026-08-09
---

## What this is

`Items.ThunderPickup` activates a timed Ultimate: Finn becomes a 1.4x Mech for
40 seconds (base tunables) with a new attack profile. It reuses the existing
`PlayerController`/`PlayerCombat` pipeline—locomotion, camera, health, and
stagger—not a separate mech controller. `Ability` (Shift, repurposed from the
removed Sprint action) means Dash normally and Shield during Ultimate.

Both visual roots are pre-built under `VisualRoot` (Mech inactive normally),
then `PlayerUltimate` toggles them, swaps the attack profile, gives infinite
ammo, resets Shield energy, and adds camera distance/height. It only hides the
astronaut when a Mech root exists, so a bad import degrades to astronaut visuals
instead of invisibility. End always releases Shield, restores ammo/camera,
astronaut animator/head anchor, and normal combat.

The Mech has animation parity, not a bind pose. `PlayerSceneSetup` creates
`AC_PlayerMech.controller` and its own upper-body mask from `Mech_FinnTheFrog`
while preserving astronaut parameter/state names: `Speed`, `Grounded`, `Jump`,
`Melee`, `FireStart`, `Firing`, `Emoting`, `EmoteIndex`, `PlayEmote`, `Death`,
`Stagger`, and the `Arms`/`Arms_Idle` contract. On form swap, all five animator
drivers retarget via `SetAnimator`: controller, combat, animator relay, health,
and emotes. Mech melee enters Kick; Stagger uses Pickup (no Duck). Build the
Mech mask with `MechUpperBodyMaskPath` so it cannot overwrite astronaut data.

The emote wheel accepts `Configure(string[] labels)` and rebuilds equal-angle
wedges. Mech uses a separate four-clip timing array (Wave/Yes/No/Dance); Dance
(index 3) loops until movement, attack, or another interruption clears
`Emoting`.

## Abilities and attacks

- `PlayerAbilityInput` owns `Ability`: `started` dashes once outside Ultimate;
  it holds Shield during Ultimate; `canceled` releases Shield. Keep ability
  components input-agnostic apart from this routing.
- `PlayerDash.TryDash()` has a 3-second cooldown and uses held `Move` through
  `GetCameraRelativeTangentDirection`, falling back to tangent-projected facing.
  It calls `PlayerController.Dash(direction, speed, duration)` and emits
  `Burst/Poof_electric`. Its private actions map must be enabled in `OnEnable`:
  a disabled map returns zero, silently causing every dash to use facing.
- `PlayerShield` resets its per-Ultimate energy budget on activation. Holding it
  drains faster than released regeneration, preventing permanent uptime; active
  Shield sets `Health.IncomingDamageMultiplier = 0`, then restores `1` on
  release/depletion. Its `Shields/Shield_electric` VFX is parented to Mech.
- `PlayerCombat.SetUltimateActive(true)` makes primary fire launch electric
  bolts from both Mech muzzles each beat; each hit calls `EnemyBase.ApplySlow`.
  The shared debuff refreshes but does not stack.
- Right-click secondary is nearest-enemy top-down attack: base fires one Beam;
  Ultimate fires Lightning circles at nearest targets, round-robin if circles
  exceed targets. Each cast clears occurrence counts; repeated hits use
  `baseDamage * Max(0.2, 1 - 0.2 * occurrence)` = 100/80/60/40/20/20…%.
  Both player delays are zero for same-call VFX/damage; hit radii are 3.2/3.5
  to catch a moving target. Mech `Arms_Shoot_Big` is cosmetic and sped 1.6x.

`TopDownGroundEffect.Play(prefab, point, telegraphDelay, lingerAfterHit,
onImpact)` hides the prefab `hit_controller`, waits, reveals it, invokes damage,
and destroys after particle lifetime plus linger. For `telegraphDelay <= 0`, it
does not yield: coroutine execution reaches impact in the `StartCoroutine` call,
so VFX/damage are genuinely same-frame. Boss top-down attacks retain their
nonzero dodge window.

## HUD and invariants

- Generated `AbilityHudUI` (bottom-left) shows Dash cooldown or Shield energy
  in slot A, and secondary cooldown in B; it relabels B as Beam/Lightning.
  `UltimateHudUI` (top-left) is hidden out of Ultimate and shows fill plus
  `ULTIMATE Ns`. Every filled `Image` needs a solid sprite
  (`GetOrCreateSolidSprite` wrapping `Texture2D.whiteTexture`): null sprites
  ignore `fillAmount` and look permanently full.
- Ultimate components may interact with `PlayerController` only through
  `Dash`, `GetCameraRelativeTangentDirection`, and `SetMovementSpeedModifier`;
  use `IncomingDamageMultiplier`, never attacker-side invulnerability checks.
- VFX below scaled Mech inherit its scale. VFX under unscaled player root must
  multiply by `PlayerUltimate.VfxScaleMultiplier` (e.g. pickup burst).
- Muzzle literals are hand-tuned in `PlayerSceneSetup.BuildUltimate` and
  `BossSceneSetup`; edit those literals rather than guessing transforms.
- Camera defaults are target offset y=3.2, pitch=15, distance=8; Ultimate adds
  distance=3 and height=1 (Mech pivot 4.2), then resets both. Prefer modest
  distance increases over re-inflating pitch if more room is wanted.
- `PlayerSceneSetup.CrosshairViewportY` (0.62) drives both crosshair anchor and
  `PlayerCombat.aimViewportY`; change only that constant or reticle/ray drift.
  It affects primary fire only—secondary attacks find nearest enemies.
- Shared `jumpHeight` is 4.2. Mech Jump exits on `Grounded`, not exit time, and
  holds its final non-looping frame for the actual airborne duration.

## Gotchas and extension

- Assign `M_MechFinnTheFrog.mat` to every renderer material slot only after all
  `SaveAndReimport()` calls. Reimport re-syncs Model Prefab instances and wipes
  earlier per-instance overrides; the initial white Mech was ordering/UV, not
  simply the chosen material.
- The vendor Mech is outside `Assets/`; `AssetDatabase.CopyAsset` silently
  fails there. Use `System.IO.File.Copy`, as item setup does.
- Lana Studio `Range_attack` internals are yaw-rotated ±90°; use
  `Euler(0,90,0)`, not an X-axis correction, for its rotation offsets.
- Old pixel crosshair math only agreed with viewport rays at 16:9; anchor
  fractions fix this (dead-center previously hid the mismatch).
- No separate “Wire Ultimate Into Player Rig” repair command exists:
  `BuildTestScene` wires the rig during rebuild.
- Balance `electricSlowPercent`, `lightningCircleCount`, and durations/cooldowns
  through their serialized fields for a future upgrades system; do not add
  separate hardcoded logic paths.

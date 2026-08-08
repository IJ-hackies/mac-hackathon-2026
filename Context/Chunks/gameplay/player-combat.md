---
chunk: player-combat
title: Player melee and projectile combat, health, and death
owns:
  - "Assets/Scripts/Player/PlayerCombat.cs*"
  - "Assets/Scripts/Player/PlayerDeathHandler.cs*"
  - "Assets/Scripts/Player/Projectile.cs*"
  - "Assets/Scripts/UI/HealthHudUI.cs*"
  - "Assets/Prefabs/Projectile.prefab*"
related: [player-controller, enemies, runtime-art, state, boss-fight, items, ultimate]
verifiedAtCommit: efe8c5547b0a83b0eeadffbff6751ad39f8c28b9
lastVerified: 2026-08-08
---

## What this is

The player's side of the combat/health system shared with
[enemies](enemies.md) via `Combat.Health`/`Combat.IDamageable`
(`Assets/Scripts/Combat/**`, owned by that chunk). Split out of
[player-controller](player-controller.md) to keep both chunks under the
150-line limit.

Ranged combat resolves as a real travelling, damage-dealing projectile -
`FireProjectile` (formerly `FireHitscan`) raycasts from the crosshair only
to pick an aim **direction**, then spawns an `Enemies.BossProjectile` (the
same generic component the bosses/flying enemy use) carrying the imported
dark-magic bolt visual (Lana Studio's `Projectiles_dark_magic`/
`Hit_dark_magic`, wired via `projectileVisualPrefab`/`impactEffectPrefab` in
`PlayerSceneSetup.BuildCombatAndEmotes`); damage applies in
`BossProjectile.OnTriggerEnter`, not at fire time. The historical
`Projectile.cs`/prefab (removed earlier as "finicky") stay unused/removed;
this reuses `BossProjectile` instead of a player-specific component. When no
`projectileVisualPrefab` is assigned, `FireProjectile` falls back to the old
instant-hitscan-plus-tracer behavior. Muzzle flash similarly prefers
`muzzleFlashEffectPrefab` over the procedural flash `Light`. Landed melee
hits spawn an imported `meleeHitEffectPrefab` (Lana Studio's `Hit_stone`) at
the actual `OverlapSphere` contact point. Shooting arm poses live on their
own upper-body-masked `Arms` Animator layer (`BuildArmsLayer`, see
[player-controller](player-controller.md)) so firing never touches leg/base
locomotion.

## Key files

- `Assets/Scripts/Player/PlayerCombat.cs` - `Melee` (Punch) and `Attack`
  (reused as "Fire"). Both `OnMeleePerformed`/`OnFireStarted` early-return
  while `playerController.IsStaggered` (e.g. mid a [boss-fight]
  (boss-fight.md) ground-slam stagger), and an in-progress fire hold is force
  -stopped the instant a stagger lands (`Update` checks `IsStaggered &&
  _isFiring`) rather than waiting for the player to release `Attack`
  themselves. `ApplyDamage` calls are tagged with `Combat.DamageType` —
  `Melee` from the melee window, `Ranged` from the hitscan raycast — so
  [boss-fight](boss-fight.md)'s mech can tell which `HitRecieve_*` clip a hit
  should have played (see [enemies](enemies.md) for why the mech doesn't
  actually react anymore).
  - **Melee**: `MeleeDamageWindow` coroutine waits `meleeHitDelay` after the
    trigger, then `Physics.OverlapSphere` in front of the player using its
    radial `transform.up`, and damages whatever `IDamageable` it finds
    (skipping the player's own root), then calls `SpawnMeleeHitEffect` at the
    same `hit.ClosestPoint` used for damage.
  - **Fire is held, not click-per-shot**: `Attack.started` fires `FireStart`
    (a one-shot trigger, once per firing *bout*, not once per shot) and sets
    `Firing` true; `Attack.canceled` schedules `Firing` false after
    `armsStopGrace` (0.3s) rather than instantly — spam-clicking Fire would
    otherwise re-trigger `FireStart` (restarting the loop from frame 0) on
    every click, since each click is its own started/canceled pair;
    `_armsActive` plus the grace window lets a click landing inside the
    window keep the same loop running instead. The Arms-layer Shoot clips
    themselves loop continuously for the whole hold (`Idle_Shoot`/
    `Arms_Shoot_Walk`/`Arms_Shoot_Run`/`Arms_Jump_Shoot`, branched on
    `Grounded`/`Speed` the same way `Jump` is) rather than retriggering per
    shot, so sustained fire reads as one smooth cycle.
  - **`CheckShootBeat` (Update, while firing)** fires the hitscan/muzzle
    flash once per loop by watching `Animator.GetCurrentAnimatorStateInfo`
    for the `Arms` layer cross `shootBeatFraction` (0.5, or
    `shootBeatFractionWalk` 0.2 for `Arms_Shoot_Walk`, which plays the same
    clip as `Arms_Shoot_Run` but slowed). Driven off the Animator's own
    `normalizedTime`, not a wall-clock timer - a timer re-armed every shot as
    `now + cooldown` drifts over a sustained hold, since each frame's
    rounding compounds shot over shot; reading Animator playback position has
    no such drift. Baseline resets on any Arms-layer state change (not just
    from `Arms_Idle`), since each state's timeline restarts fresh on entry.
    `FireProjectile` raycasts from the camera through the screen-center
    crosshair for an aim direction only, then spawns the real
    `BossProjectile`; `SpawnTracer` remains the no-imported-prefab fallback.
  - Walking fires slower than running purely because `Arms_Shoot_Walk` plays
    at `WalkShootAnimSpeed` (0.6, see [player-controller](player-controller.md))
    - not a separately tuned rate, just clip length/playback speed.
  - Muzzle transform is built in `PlayerSceneSetup.BuildCombatAndEmotes`,
    positioned forward of the body so the flash doesn't render inside the mesh.
  Exposes `IsAttacking` (`_isFiring || Time.time < _attackingUntil`, covering
  the whole held-fire duration) for the emote-interrupt check.
- `Assets/Scripts/Player/PlayerDeathHandler.cs` - on `Health.Died`, disables
  `PlayerController` and `PlayerCombat` (freezes movement/input, not the
  camera). `PlayerCombat.OnDisable` zeroes the `Arms` layer weight/`Firing`,
  otherwise a frozen shoot pose would override `Death`'s base-layer pose.
- `Assets/Scripts/UI/HealthHudUI.cs` - segmented "HULL INTEGRITY" energy-cell
  readout, top-right, built the same procedural-UI-rect way as the crosshair/
  emote wheel (no external asset). Segments light left-to-right by health
  fraction and shift color from `lowColor` to `fullColor`. `Bind()` (called
  by `PlayerSceneSetup` at edit time) only stores the `Health` reference; the
  `HealthChanged` subscription happens in `OnEnable` - see [enemies]
  (enemies.md) Gotchas (edit-time delegate subscriptions don't survive the
  Play-mode domain reload). If a prefab can't serialize a direct reference
  across the nested player boundary, `OnEnable` discovers the active
  `PlayerController` and binds its colocated `Health`.

`CheckShootBeat` now gates each shot behind `Player.PlayerAmmo.
TryConsumeRound()` before calling `FireProjectile`/`SpawnMuzzleFlash` -
running dry (with storage left) auto-starts a reload, and a reload in
progress silently withholds the shot without touching the Shoot animation
loop. A `Reload` action (keyboard `R`, gamepad West) calls
`PlayerAmmo.StartReload()` directly - see [items](items.md) for
`PlayerAmmo`/`AmmoHudUI` and the `AmmoPickup` that refills them.

`PlayerCombat.SetUltimateActive(bool)` (called by `Player.PlayerUltimate`)
switches `FireProjectile`'s primary-fire path from the single dark-magic
bolt to `FireElectricBolts()` (both mech muzzles fire every beat, each with
a per-hit `EnemyBase.ApplySlow` callback), and a new `Attack2` action
(right-click) adds a cooldown-gated secondary in both modes (nearest-enemy
beam normally, N-nearest lightning circles with falloff damage in Ultimate)
via `Vfx.TopDownGroundEffect.Play`. `Combat.Health` also gained
`IncomingDamageMultiplier` (used by `PlayerShield` to fully mitigate damage
while held). Full detail in [ultimate](ultimate.md).

## Invariants

- `HitReact`/`Death` follow the same "trigger, not held bool" `AnyState`
  pattern as emotes, and — the important part — `Combat.Health` fires them
  fire-and-forget (no caller ever waits on the animation). `PlayerController`/
  `PlayerCombat` keep processing input and moving the radial capsule motor
  underneath a `HitReact` overlay instead of freezing; only `Death` actually
  stops things, via `PlayerDeathHandler` disabling those two components
  directly, not the Animator state blocking anything. See
  [enemies](enemies.md) for why (non-blocking hit reactions were explicit).
- Shooting now *does* use a held-bool (`Firing`) animation split, unlike the
  historical note that a `ShootHold`/`Firing`-bool split "staggered the
  character when moving and firing together, fighting the Move blend tree" —
  that failure predates the `Arms` layer existing; Shoot poses fighting Move
  was specifically because both lived on the same full-body base layer back
  then. Splitting Shoot onto its own upper-body-masked layer removed that
  conflict, so the held-bool approach is safe now. If shooting is ever
  merged back onto a single layer, re-check this before reusing `Firing`.

## How to extend

`PlayerCombat.IsAttacking` and `Combat.IDamageable` are the hook points for
further combat work rather than rebuilding the trigger plumbing. Any new
sustained/looping Animator action should sync off `AnimatorStateInfo.
normalizedTime` (see `CheckShootBeat`), not a wall-clock timer.

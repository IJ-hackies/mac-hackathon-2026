---
chunk: player-combat
title: Player melee/hitscan combat, health, and death
owns:
  - "Assets/Scripts/Player/PlayerCombat.cs"
  - "Assets/Scripts/Player/PlayerDeathHandler.cs"
  - "Assets/Scripts/UI/HealthHudUI.cs"
related: [player-controller, enemies, runtime-art, state, boss-fight]
verifiedAtCommit: 71b7468850b4e64c25da49ef3deff2ff354c4778
lastVerified: 2026-08-08
---

## What this is

The player's side of the combat/health system shared with
[enemies](enemies.md) via `Combat.Health`/`Combat.IDamageable`
(`Assets/Scripts/Combat/**`, owned by that chunk). Split out of
[player-controller](player-controller.md) to keep both chunks under the
150-line limit.

Ranged combat still resolves as hitscan, not a traveling projectile
(`Projectile.cs` and its prefab/material were removed as "finicky"), but now
spawns a purely cosmetic `FlyProjectileVisual` coroutine after the hit is
already resolved — an imported-VFX or procedural bolt that flies from the
muzzle to the already-known hit point, replacing the old instant `LineRenderer`
tracer. Muzzle flash similarly prefers `muzzleFlashEffectPrefab` (an imported
VFX asset) over the procedural flash `Light`. Shooting arm poses live on their
own upper-body-masked `Arms` Animator layer (`BuildArmsLayer`, see
[player-controller](player-controller.md)) so firing never touches leg/base
locomotion — `PlayerCombat` toggles that layer's weight and drives its
`FireStart`/`Firing` parameters; it doesn't touch the base layer at all.

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
    trigger, then `Physics.OverlapSphere` in front of the player and damages
    whatever `IDamageable` it finds (skipping the player's own root).
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
    `shootBeatFractionWalk` — 0.2 — specifically for `Arms_Shoot_Walk`, which
    plays the same clip as `Arms_Shoot_Run` but slowed, compressing its
    recoil arc earlier into the loop). This is driven off the Animator's own
    `normalizedTime`, not a wall-clock timer, because a timer re-armed every
    shot as `now + cooldown` drifts over a sustained hold — each frame's
    unavoidable rounding between "cooldown elapsed" and `Update()` noticing
    compounds shot over shot, since the next target rebases off the already-
    late "now" instead of a fixed schedule. Reading the Animator's playback
    position has no such drift. Baseline resets on any Arms-layer state
    change (not just from `Arms_Idle`), since each state's timeline restarts
    fresh on entry and comparing against the *previous* state's leftover
    phase misreads as a spurious crossing. `FireHitscan` raycasts from the
    camera through the screen-center crosshair; `SpawnTracer` draws a
    short-lived `LineRenderer` purely as a "shot fired" visual, after the
    hit is already resolved.
  - Walking fires slower than running purely because `Arms_Shoot_Walk` plays
    at `WalkShootAnimSpeed` (0.6, see [player-controller](player-controller.md))
    — not a separately tuned rate; the loop period (and so the fire rate)
    follows directly from clip length/playback speed.
  - Muzzle transform is built in `PlayerSceneSetup.BuildCombatAndEmotes`,
    positioned forward of the body so the flash doesn't render inside the
    character mesh.
  Exposes `IsAttacking` (now `_isFiring || Time.time < _attackingUntil`,
  covering the whole held-fire duration) for the emote-interrupt check.
- `Assets/Scripts/Player/PlayerDeathHandler.cs` - on the player's
  `Health.Died`, disables `PlayerController` and `PlayerCombat` (freezes
  movement/input; does not touch the camera). `PlayerCombat.OnDisable` also
  zeroes the `Arms` layer weight and `Firing`, otherwise a frozen shoot pose
  would keep overriding `Death`'s full-body base-layer pose.
- `Assets/Scripts/UI/HealthHudUI.cs` - segmented "HULL INTEGRITY" energy-cell
  readout, top-right, built the same procedural-UI-rect way as the crosshair/
  emote wheel (no external asset). Segments light left-to-right by health
  fraction and shift color from `lowColor` to `fullColor`. `Bind()` (called
  by `PlayerSceneSetup` at edit time) only stores the `Health` reference; the
  `HealthChanged` subscription itself happens in `OnEnable` — see
  [enemies](enemies.md) Gotchas for why (edit-time delegate subscriptions
  don't survive the Play-mode domain reload).

## Invariants

- `HitReact`/`Death` follow the same "trigger, not held bool" `AnyState`
  pattern as emotes, and — the important part — `Combat.Health` fires them
  fire-and-forget (no caller ever waits on the animation). `PlayerController`/
  `PlayerCombat` keep processing input and moving the `CharacterController`
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
normalizedTime` (see `CheckShootBeat`) rather than a wall-clock timer, for
the same drift reason.

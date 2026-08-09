---
chunk: player-combat
title: Player melee and projectile combat, health, and death
owns:
  - "Assets/Scripts/Player/PlayerCombat.cs*"
  - "Assets/Scripts/Player/PlayerDeathHandler.cs*"
  - "Assets/Scripts/Player/Projectile.cs*"
  - "Assets/Scripts/UI/HealthHudUI.cs*"
  - "Assets/Prefabs/Projectile.prefab*"
  - "Assets/Tests/EditMode/Player/PlayerCombatFireRateTests.cs*"
  - "Assets/Tests/EditMode/Player/SpecialCombatSkillTests.cs*"
related: [player-controller, progression, enemies, runtime-art, state, boss-fight, items, ultimate]
verifiedAtCommit: 51dd8f3150f2f142886af2218c43c4d0c0875e41
lastVerified: 2026-08-09
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
the actual `OverlapCapsule` contact point. Shooting arm poses live on their
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
    trigger, then sweeps `Physics.OverlapCapsule` in front of the player using
    its radial `transform.up`, and damages whatever `IDamageable` it finds
    (skipping the player's own root), then calls `SpawnMeleeHitEffect` at the
    same `hit.ClosestPoint` used for damage.
  - **Fire is click-per-shot by default**: `Attack.started` requests one round
    and raises the masked Arms pose. Manual clicks and purchased Hold to Fire
    share one primary-fire cadence gate, so repeated clicks cannot exceed the
    pistol interval. Quick clicks still share the existing `armsStopGrace` pose
    window instead of restarting the animation every time. The progression
    fire-rate multiplier shortens this pistol interval for both input styles;
    it does not change Ultimate fire rate.
  - Ultimate always supports continuous electric primary fire independently of
    the purchase, including when Ultimate activates while Attack is held.
    `FireProjectile` still aims from the camera/crosshair and spawns the real
    `BossProjectile`; `SpawnTracer` is the no-imported-prefab fallback.
  - Muzzle transform is built in `PlayerSceneSetup.BuildCombatAndEmotes`,
    positioned forward of the body so the flash doesn't render inside the mesh.
  Exposes `IsAttacking` (`_isFiring || Time.time < _attackingUntil`, covering
  the whole held-fire duration) for the emote-interrupt check.
  Its runtime action set and component references are idempotently restored on
  enable after Editor assembly reloads; disable remains safe before initialization.
- `Assets/Scripts/Player/PlayerDeathHandler.cs` - on `Health.Died`, disables
  `PlayerController` and `PlayerCombat` (freezes movement/input, not the
  camera), then plays the player-death SFX. `PlayerCombat.OnDisable` zeroes the
  `Arms` layer weight/`Firing`, otherwise a frozen shoot pose would override
  `Death`'s base-layer pose.
- `Assets/Scripts/UI/HealthHudUI.cs` - minimal top-right health readout: one
  fixed-red sliced Space Expansion bar with rounded current HP centered
  inside. It has no panel, labels, percent sign, or damage trail. `Bind()`
  stores `Health`; `OnEnable` subscribes after domain reload and falls back
  to the active `PlayerController`'s colocated `Health` when needed.

`TryFireShot` gates each accepted shot behind `Player.PlayerAmmo.
TryConsumeRound()` before calling `FireProjectile`/`SpawnMuzzleFlash` -
running dry (with storage left) auto-starts a reload, and a reload in
progress silently withholds the shot without touching the Shoot animation
loop. A `Reload` action (`R`) calls
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

Progression applies keyed fire-rate modifiers plus raw shooting/melee bonuses.
The shooting bonus is added to pistol, normal secondary, Ultimate bolt, and
Ultimate lightning base damage before other multipliers/falloff. Health
composes keyed incoming-damage modifiers so Defense and Shield do not overwrite
one another; see [progression](progression.md).

Ordinary pistol rounds also carry the purchased special-round context through
their whole impact chain: Headshot! doubles each fourth accepted round, Bullet
Bounce chooses the nearest distinct living target with line of sight within 15
units until three total targets have been hit, and Explosive Bullets applies 50%
of final impact damage in a 3-unit radius while excluding the direct target.
Minigun applies its raw-damage/capacity/fire-rate profile without changing
Ultimate cadence or turning click fire into held fire. `Combat.Health` reports
actual HP removed after mitigation and overkill clamping; Vampire uses that
value to heal 2% across pistol, bounce/splash, melee, secondary, and Ultimate
damage rather than healing from attempted damage.

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
further combat work rather than rebuilding the trigger plumbing. Keep gameplay
cadence independent from the cosmetic Arms loop when adding fire-rate tuning.

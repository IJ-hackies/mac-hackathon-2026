---
chunk: enemies
title: Enemy AI, attack patterns, and the shared damage/health system
owns:
  - "Assets/Scripts/Enemies.meta"
  - "Assets/Scripts/Enemies/**"
  - "Assets/Scripts/Combat.meta"
  - "Assets/Scripts/Combat/**"
  - "Assets/Editor/Enemies.meta"
  - "Assets/Editor/Enemies/**"
  - "Assets/Editor/ModelAnimationUtility.cs*"
related: [player-controller, player-combat, progression, runtime-art, state, boss-fight, items, ultimate]
verifiedAtCommit: e4caa898457d6a2d25ff205625898ecf4fbe2635
lastVerified: 2026-08-09
---

## What this is

Three fightable basic enemy types, all built from the Ultimate Space Kit's `Enemy_*` FBX files, dropped into
`Assets/Scenes/Player.unity` alongside the player. Combat (both player and enemy sides) runs on a shared
`Combat.Health`/`Combat.IDamageable` pair so melee, projectile, boss, and enemy attacks all resolve damage the same way. `Health.cs` also gained
`Heal(float)`/`FullyHeal()` and safe runtime max-health changes for items and
[progression](progression.md).

`EnemySceneSetup.AddEnemiesToScene` also builds the two-stage Barbara-the-Bee
boss fight via `BossSceneSetup`, then `SetActive(false)`s all three basic
enemies - leaving only the boss fightable, but keeping the basic-enemy build
calls intact (one-line change to re-enable). See [boss-fight](boss-fight.md).

## Key files

- `Assets/Scripts/Combat/IDamageable.cs` / `Health.cs` - shared by the player
  and every enemy. `ApplyDamage` takes a trailing `Combat.DamageType`
  (`Generic`/`Melee`/`Ranged`, default `Generic`) so a source can tag itself -
  `PlayerCombat` tags `Melee`/`Ranged` so [boss-fight](boss-fight.md)'s mech
  can pick `HitRecieve_1`/`_2`. Reduces `CurrentHealth`, fires
  `HealthChanged` and `Hit(DamageType)` (amount first scaled by composed,
  source-keyed incoming-damage modifiers used by progression Defense and
  [ultimate](ultimate.md)'s Shield). If that scaling reduces a real (>0) hit to
  zero, `MitigatedDamage(float)` fires instead with the pre-scaled amount -
  `PlayerShield` uses this for its permanent blue "damage blocked" popup; a
  fully-blocked hit fires `MitigatedDamage`, not `Hit`. Then fires `HitReact`/`Death`
  Animator triggers **fire-and-forget** - never blocks its own caller; `Died`
  is the hook other scripts use to stop something (`EnemyBase.HandleDeath`,
  `PlayerDeathHandler`). `SuppressHitReact` skips `HitReact` while still
  firing `Hit`/`HealthChanged`/`Died` - both boss AIs set it permanently
  `true` (a flinch interrupted their attack coroutines); `hitReactCooldown`
  rate-limits `HitReact` for non-suppressed callers instead.
- `Assets/Scripts/Enemies/EnemyBase.cs` - shared plumbing: finds the player
  via `FindFirstObjectByType<Player.PlayerController>()` at `Awake`,
  `FacePlayer()` (hard requirement), `SpawnMeleeHitVfx` (instantiate/
  `ImportedVfxUtility` fix/destroy boilerplate for `meleeHitVfxPrefab`,
  shared by `EnemySmallAI`/`EnemyLargeAI`), `SpeedMultiplier`/`ApplySlow
  (multiplier, duration)` (a timed movement-speed debuff - refreshes
  duration on reapply rather than stacking, plus a looping `slowVfxPrefab` -
  used by [ultimate](ultimate.md)'s electric bolts), and `Died` → sets
  `isDead`, `StopAllCoroutines()` (an in-flight attack coroutine doesn't
  check `isDead` mid-sequence - `EnemyFlyingAI`'s multi-second `Headbutt`
  telegraph fought the `Death` animation before this fix), then runs
  `DissolveAndDestroy`.

  `DissolveAndDestroy` first runs `FallToGround` (gravity-accelerated fall to `y=0` - the two flyers freeze mid-air at their last hover height once
  `isDead` stops their `Update` loop). Once grounded, swaps every renderer
  onto a per-instance clone of `Custom/EnemyDissolve`
  (`Assets/Art/Shaders/S_EnemyDissolve.shader`) and animates
  `_DissolveAmount` 0→1 (a true per-pixel noise-clip dissolve) - per-instance
  so it never touches the shared `M_Enemy*.mat`.
- `EnemyFlyingAI.cs` - ranged. Wanders while drifting toward the player,
  capped at `maxDistanceFromPlayer`. Attack: brief `chargeStartDelay`
  windup (Attack anim only, no visual telegraph), then fires a real
  travelling `Enemies.BossProjectile` (Lana Studio's
  `Projectiles_green_shuriken`/`Hit_wind`) straight at the player's current
  position - "it should just shoot as normal." The earlier tracking-
  telegraph-then-freeze `LineRenderer` dodge signal (and, before that, an
  even older instant-hitscan laser beam) were both removed entirely, not
  just disabled. Fires from a dedicated `firePoint` child transform (added
  by `EnemySceneSetup.BuildFlyingEnemy`, same "own adjustable Transform"
  convention as the player/mech/boss muzzles - previously defaulted to the
  enemy's own root with no way to reposition it independently).
  `attackCooldown` lowered to `4f / 1.8f` (~2.22s) - 1.8x fire rate.
- `EnemySmallAI.cs` - melee flyer. Closes to `attackRange`, punches on
  cooldown; damage lands only if the player is still in range at the swing's
  midpoint. `attackRange` is set at build time from the model's measured
  collider radius. A landed hit spawns `EnemyBase.SpawnMeleeHitVfx` (Lana
  Studio's `Slash_stone_once`) at `player.position + Vector3.up`.
- `EnemyLargeAI.cs` - grounded `CharacterController` swarmer. Walks at range,
  **runs when closer** (`runDistanceThreshold`, intentionally the opposite
  of "sprint to close, then slow down"). Alternates Punch, Punch, Weapon
  (`_attackStep` cycles 0→1→2, Weapon deals more and resets); a landed hit
  spawns the same `SpawnMeleeHitVfx`. Overrides `HandleDeath` to
  `ResetTrigger` `Punch`/`Weapon` and zero `Speed` first - a killing blow
  mid-swing could otherwise leave an attack trigger armed on the `Death` frame.
- `Assets/Editor/Enemies/EnemySceneSetup.cs` - `Tools ▸ Enemies ▸ Add Enemies
  To Player Scene`. Builds one `AnimatorController` per AI type
  (`BuildHoverController` shared by Flying/Small; `BuildGroundController` for
  Large). In `BuildGroundController`, `AddHitReactAndDeath` is called
  **before** the `Punch`/`Weapon` `AnyState` transitions - Unity evaluates
  `AnyState` transitions in add order, so Death wins if an attack trigger is
  still armed (pairs with `EnemyLargeAI.HandleDeath` above). Also builds a
  per-model palette material (all 5 share `T_SpacePalette`, separate
  `M_<Name>.mat` for traceability), a hit collider, `Health`, and the AI
  script. The hit collider is sized via `GetLocalRenderBounds` - measured
  world-space `Renderer.bounds`, not hand-picked constants (fixed a "shots
  don't register" bug). The same measured radius (+ `MeleeReachMargin`) also
  sets `attackRange`. All 5 models move to the `Enemy` physics layer so
  [player-controller](player-controller.md)'s camera excludes them from its
  collision `SphereCast`. **Opens and re-saves `Player.unity` in place**
  rather than rebuilding it - must run after `PlayerSceneSetup.
  BuildTestScene`, which wipes the scene and would otherwise drop these.
- `Assets/Editor/ModelAnimationUtility.cs` - clip-lookup/looping/layer
  helpers shared with `PlayerSceneSetup`.
- `EnemyHealthBarUI.cs` - world-space bar above each fightable enemy, from
  `EnemySceneSetup.AddHealthBar` using the hit collider's measured bounds.
  Parented under its enemy so the hierarchy saves as one self-contained
  prefab, but drives its transform off an anchor each `LateUpdate` and
  billboards to `Camera.main`. Fill resizes via `anchorMax.x`, not
  `Image.Type.Filled`; hides at 0 health, not after the death dissolve.
  [boss-fight](boss-fight.md)'s bosses reuse it, scale-compensated for their
  2x/4x models.

## Invariants

- Every enemy always faces the player (`EnemyBase.FacePlayer`, every frame
  outside attack windups that intentionally freeze facing).
- Hit reactions never gate movement/AI/attack logic for either side ("hit
  reactions shouldn't cause stoppage in gameplay").
- Player-side damage (melee `OverlapSphere` and travelling `BossProjectile`)
  and enemy-side damage both go through `IDamageable.ApplyDamage`; the aim
  ray only chooses the player's projectile direction.
- `EnemySceneSetup` must run after `PlayerSceneSetup.BuildTestScene`, never
  standalone - `BuildTestScene` wipes the scene and silently drops all
  enemies until `EnemySceneSetup` is re-run.

## Gotchas

- `Health.CurrentHealth` falls back to `MaxHealth` while its backing field is
  the unset sentinel (`-1`), since editor setup scripts bind UI right after
  `AddComponent<Health>()`, before `Awake` (Play-mode only) runs.
  **Never subscribe to a `Health` event from an editor setup script** - Play
  mode reloads the domain, dropping edit-time delegate subscriptions;
  `HealthHudUI`/`EnemyHealthBarUI.Bind()` only store the reference, real
  `+=` happens in `OnEnable`, same as `EnemyBase`/`PlayerDeathHandler`.
- All AI movement is a flat-ground assumption (`CharacterController` +
  constant-Y/hover-height translation) - does **not** implement the planet's
  radial gravity (`unity-project-bootstrap` in `STATE.md`).
- See [player-controller](player-controller.md) Gotchas for the iCloud-Drive
  " 2"/" 3" duplicate-file pattern - affects the whole `Assets/` tree.

## How to extend

The two flyers already share `BuildHoverController`; a third hover-based enemy should reuse it, not duplicate the controller-building code. Boss AI/
cutscene/imported-VFX projectiles live in [boss-fight](boss-fight.md) - this chunk stays scoped to the three basic enemies plus shared `Combat`/
`EnemyBase` plumbing.

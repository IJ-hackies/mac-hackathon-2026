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
related: [player-controller, player-combat, runtime-art, state, boss-fight]
verifiedAtCommit: 71b7468850b4e64c25da49ef3deff2ff354c4778
lastVerified: 2026-08-08
---

## What this is

Three fightable basic enemy types, all built from the Ultimate Space Kit's
`Enemy_*` FBX files, dropped into `Assets/Scenes/Player.unity` alongside the
player. Combat (both player and enemy sides) runs on a shared
`Combat.Health`/`Combat.IDamageable` pair so melee, hitscan, boss, and enemy
attacks all resolve damage the same way.

`EnemySceneSetup.AddEnemiesToScene` also builds the two-stage Barbara-the-Bee
boss fight via `BossSceneSetup`, then `SetActive(false)`s all three basic
enemies right after — leaving only the boss fightable, but keeping the
basic-enemy build calls intact (one-line change to re-enable). See
[boss-fight](boss-fight.md) for the boss itself.

## Key files

- `Assets/Scripts/Combat/IDamageable.cs` / `Health.cs` - shared by the player
  and every enemy. `ApplyDamage` takes a trailing `Combat.DamageType`
  (`Generic`/`Melee`/`Ranged`, default `Generic`) so a source can tag itself —
  `PlayerCombat` tags `Melee`/`Ranged` so [boss-fight](boss-fight.md)'s mech
  can pick `HitRecieve_1`/`_2`. `ApplyDamage` reduces `CurrentHealth`, fires
  `HealthChanged` and a `Hit(DamageType)` event, then fires `HitReact`/`Death`
  Animator triggers **fire-and-forget** — never blocks its own caller
  ("hit reactions shouldn't stall gameplay"); `Died` is the hook other scripts
  use to actually stop something (`EnemyBase.HandleDeath`,
  `PlayerDeathHandler`). `SuppressHitReact` (bool) skips `HitReact` entirely
  while still firing `Hit`/`HealthChanged`/`Died` — both boss AIs set it
  permanently `true` in `OnEnable`, since even a brief flinch was observed
  interrupting their attack coroutines; `hitReactCooldown` rate-limits
  `HitReact` for non-suppressed callers instead of a hard on/off.
- `Assets/Scripts/Enemies/EnemyBase.cs` - shared plumbing: finds the player
  via `FindFirstObjectByType<Player.PlayerController>()` at `Awake`,
  `FacePlayer()` (hard requirement, not just default), and `Died` → sets
  `isDead`, `StopAllCoroutines()` (an in-flight attack coroutine doesn't
  check `isDead` mid-sequence — `EnemyFlyingAI`'s multi-second `Headbutt`
  telegraph was observed fighting the `Death` animation before this fix),
  then runs `DissolveAndDestroy`.

  `DissolveAndDestroy` first runs `FallToGround` — gravity-accelerated fall to
  `y=0`, needed because the two flyers freeze mid-air at their last hover
  height once `isDead` stops their `Update` loop, concurrent with the Death
  animation. Once grounded, swaps every renderer onto a per-instance clone of
  `Custom/EnemyDissolve` (`Assets/Art/Shaders/S_EnemyDissolve.shader`) and
  animates `_DissolveAmount` 0→1 (a true per-pixel noise-clip dissolve, not a
  scale/shrink approximation) — per-instance so it never touches the shared
  `M_Enemy*.mat` and affects other live instances.
- `EnemyFlyingAI.cs` - ranged. Wanders while drifting toward the player,
  capped at `maxDistanceFromPlayer`. `Headbutt`: charges after
  `chargeStartDelay`, a `LineRenderer` telegraph tracks the player for
  `trackDuration`, then **freezes** for `pauseDuration` (dodge window), then
  an instant hitscan `Physics.Raycast` along it. No travel-time projectile.
- `EnemySmallAI.cs` - melee flyer. Closes to `attackRange`, punches on
  cooldown; damage lands only if the player is still in range at the swing's
  midpoint. `attackRange` is set at build time from the model's measured
  collider radius, not the class default.
- `EnemyLargeAI.cs` - grounded `CharacterController` swarmer. Walks at range,
  **runs when closer** (`runDistanceThreshold`, intentionally the opposite of
  "sprint to close, then slow down"). Alternates Punch, Punch, Weapon
  (`_attackStep` cycles 0→1→2, Weapon deals more and resets). Overrides
  `HandleDeath` to `ResetTrigger` `Punch`/`Weapon` and zero `Speed` before
  `base.HandleDeath()` — a killing blow mid-swing could leave an attack
  trigger armed the same frame `Death` fires, seen as Death flashing then
  reverting to running.
- `Assets/Editor/Enemies/EnemySceneSetup.cs` - `Tools ▸ Enemies ▸ Add Enemies
  To Player Scene`. Builds one `AnimatorController` per AI type
  (`BuildHoverController` shared by Flying/Small; `BuildGroundController` for
  Large). In `BuildGroundController`, `AddHitReactAndDeath` is called
  **before** the `Punch`/`Weapon` `AnyState` transitions are added — Unity
  evaluates `AnyState` transitions in add order, so Death wins priority if an
  attack trigger is still armed (pairs with `EnemyLargeAI.HandleDeath` above).
  Also builds a per-model palette material (all 5 share `T_SpacePalette`, but
  get separate `M_<Name>.mat` for traceability), a hit collider, `Health`, and
  the AI script. The hit collider (`CapsuleCollider` for the two flyers;
  `CharacterController` for Large) is sized via `GetLocalRenderBounds` —
  measured world-space `Renderer.bounds`, not hand-picked constants (fixed a
  "shots don't register" bug from mismatched colliders). The same measured
  radius (+ `MeleeReachMargin`) also sets `attackRange`. All 5 models move to
  the `Enemy` physics layer (`ModelAnimationUtility.EnsureLayer`) so
  [player-controller](player-controller.md)'s camera excludes them from its
  collision `SphereCast`. **Opens and re-saves `Player.unity` in place**
  rather than rebuilding it — must run after `PlayerSceneSetup.
  BuildTestScene`, which wipes the scene and would otherwise drop these.
- `Assets/Editor/ModelAnimationUtility.cs` - clip-lookup/looping/layer helpers
  shared with `PlayerSceneSetup` (`GetClip`, `LoadSourceClips`,
  `ConfigureAnimationLooping`, `EnsureLayer`).
- `EnemyHealthBarUI.cs` - world-space bar above each fightable enemy, from
  `EnemySceneSetup.AddHealthBar` using the hit collider's measured bounds.
  Now parented under its enemy (was unparented) so the enemy's whole
  hierarchy saves as one self-contained prefab, but still drives its
  transform off an anchor each `LateUpdate` and billboards to `Camera.main`
  rather than relying on inherited transform. Fill resizes via `anchorMax.x`,
  not `Image.Type.Filled`; hides on the same `HealthChanged` callback that
  resizes it, at 0 health rather than after the death dissolve.
  [boss-fight](boss-fight.md)'s bosses reuse it, scale-compensated for their
  2x/4x models.

## Invariants

- Every enemy always faces the player (`EnemyBase.FacePlayer`, every frame
  outside attack windups that intentionally freeze facing).
- Hit reactions never gate movement/AI/attack logic for either side (see
  `Combat.Health` above) — explicit requirement ("hitreact shouldn't cause
  stoppage in gameplay").
- Player-side damage (melee `OverlapSphere`, hitscan `Raycast`) and
  enemy-side damage (direct distance check) both go through
  `IDamageable.ApplyDamage` — no separate player/enemy damage code path.
- `EnemySceneSetup` must run after `PlayerSceneSetup.BuildTestScene`, never
  standalone as the only path — `BuildTestScene` wipes the scene and
  silently drops all enemies until `EnemySceneSetup` is re-run.

## Gotchas

- `Health.CurrentHealth` falls back to `MaxHealth` while its backing field is
  the unset sentinel (`-1`), since editor setup scripts bind UI to `Health`
  right after `AddComponent<Health>()`, before `Awake` (Play-mode only) runs.
  Relatedly, **never subscribe to a `Health` event from an editor setup
  script** — Play mode reloads the domain, dropping edit-time delegate
  subscriptions. `HealthHudUI`/`EnemyHealthBarUI.Bind()` only store the
  `Health` reference; the real `+=` happens in `OnEnable`, same as
  `EnemyBase`/`PlayerDeathHandler` for `Health.Died`.
- All AI movement is a flat-ground assumption (`CharacterController` +
  constant-Y/hover-height translation) — does **not** implement the planet's
  radial gravity (`unity-project-bootstrap` in `STATE.md`). Revisit before
  spherical use.
- See [player-controller](player-controller.md) Gotchas for the iCloud-Drive
  " 2"/" 3" duplicate-file pattern — affects the whole `Assets/` tree.

## How to extend

The two flyers already share `BuildHoverController`; a third hover-based
enemy should reuse it, not duplicate the controller-building code.
Boss-specific AI, cutscene control, and imported-VFX projectiles live in
[boss-fight](boss-fight.md) — this chunk stays scoped to the three basic
enemies plus the shared `Combat`/`EnemyBase` plumbing both build on.

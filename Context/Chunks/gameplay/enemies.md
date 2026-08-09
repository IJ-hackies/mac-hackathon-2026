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
related: [player-controller, player-combat, progression, wave-system, runtime-art, state, boss-fight, items, ultimate]
verifiedAtCommit: a539eb47b10120f7c92bc827a06381aa5eb80fa7
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

The same enemy prefabs now support [wave-system](wave-system.md) in SampleScene.
`EnemyBase` accepts wave stat scaling, detection radius, immediate/sticky aggro,
reward-free retreat/despawn, and separate killed/despawned events. Basic and boss
AI derive movement, up, facing, and death fall from the planet center when
configured, while retaining their flat-ground fallback for the Player sandbox.
`EnemyBase` explicitly resolves the enabled crater `MeshCollider` rather than
the disabled reference sphere, projects random/chase movement into the local
tangent plane, probes solid props/walls ahead, takes deterministic detours, and
flips detour side after measured stalls. Probes use the lower body/capsule sphere,
its scaled physical radius plus a small margin, and fixed look-ahead; the player
and dynamic enemies are not classified as rocks, and clear direct routes cancel
detours immediately. Hover and grounded movement report the actual post-navigation
tangent displacement after collision sliding and radial
grounding; walking/melee actors face that result instead of the player vector or
pre-detour intent, and their movement animation stops when measured displacement
is zero. Grounded controller roots also retain any clearance required by an
authored capsule bottom below the root instead of being snapped inside terrain.
Hover and grounded movers share this recovery layer. Wave-spawned roots are 3x,
and their authored walking/combat distance bands inherit that wrapper multiplier.
Basic Small/Flying/Large enemies have 100/120/150 base HP and speeds of Small
approach 4, Flying wander 1.5/approach 2.25, and Large walk 2/run 4.5.
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
- `Assets/Scripts/Enemies/EnemyBase.cs` - resolves the player, active crater,
  and radial frame; owns obstacle/stuck recovery plus combat-facing versus
  locomotion-facing. It also centralizes melee-hit VFX and refresh-not-stack
  slow debuffs with their looping VFX. Death stops in-flight attack coroutines
  (otherwise a flyer telegraph can fight the death animation), then runs
  `DissolveAndDestroy`.

  Death disables the enemy's colliders and terminal AI updates before a bounded
  radial `FallToGround`; passive hover/controller maintenance must never run for
  dead, frozen, or retreating actors. Grounding failure times out rather than
  blocking cleanup forever. `DissolveAndDestroy` then swaps renderers onto
  per-instance `Custom/EnemyDissolve` materials and animates `_DissolveAmount`
  0→1 without touching shared `M_Enemy*.mat` assets.
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
- World-space health bars are retired for regular enemies and both boss stages;
  legacy prefab children remain inactive. Arena2 presents boss health only in
  the top-center arena objective HUD.

## Invariants

- Combat aim remains explicitly player-targeted. Walking and melee actors face
  their measured post-navigation tangent displacement while moving, then return
  to player facing while idle or attacking so forward-only clips do not moonwalk
  during detours, recovery steering, or collision slides. Walk animation is also
  driven by actual displacement, not merely a requested velocity.
- Hit reactions never gate movement/AI/attack logic for either side ("hit
  reactions shouldn't cause stoppage in gameplay").
- Player-side damage (melee `OverlapSphere` and travelling `BossProjectile`)
  and enemy-side damage both go through `IDamageable.ApplyDamage`; the aim
  ray only chooses the player's projectile direction.
- `EnemySceneSetup` must run after `PlayerSceneSetup.BuildTestScene`, never
  standalone - `BuildTestScene` wipes the scene and silently drops all
  enemies until `EnemySceneSetup` is re-run.

## Gotchas
- Configure wave-spawned AI through `EnemyWaveScaling`; direct prefab edits
  bypass endless-wave scaling and reward/removal semantics. The iCloud " 2"/
  " 3" duplicate-file warning in [player-controller] applies across `Assets/`.

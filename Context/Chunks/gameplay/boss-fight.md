---
chunk: boss-fight
title: Barbara the Bee two-stage boss fight (Astronaut -> Mech)
owns:
  - "Assets/Scripts/Enemies/BossAstronautAI.cs*"
  - "Assets/Scripts/Enemies/BossMechAI.cs*"
  - "Assets/Scripts/Enemies/BossFightController.cs*"
  - "Assets/Scripts/Enemies/BossProjectile.cs*"
  - "Assets/Scripts/Vfx.meta"
  - "Assets/Scripts/Vfx/**"
  - "Assets/Editor/Enemies/BossSceneSetup.cs*"
  - "Assets/Prefabs/Enemies.meta"
  - "Assets/Prefabs/Enemies/**"
  - "Assets/_Creepy_Cat.meta"
  - "Assets/GabrielAguiarProductions.meta"
  - "Assets/GabrielAguiarProductions/**"
  - "Assets/Lana Studio*"
related: [enemies, player-controller, player-combat, runtime-art, state, ultimate]
verifiedAtCommit: 262413a1cda18eaed7a50511bb0aa8f10bcb533a
lastVerified: 2026-08-09
---

## What this is

The two-stage boss fight on [enemies](enemies.md)'s shared `Combat`/`EnemyBase` plumbing. `BossSceneSetup.BuildBossFight` (from
`EnemySceneSetup.AddEnemiesToScene`) builds both stages plus a
`BossFightController` wiring the Stage 1 -> Stage 2 transformation.

- **Stage 1 - `BossAstronautAI`** (scale 2, `Astronaut_BarbaraTheBee.fbx`,
  shares the player's rig/clips): circle-strafes, picks a **weighted-random**
  attack (ranged burst / punch / heavy `Weapon` melee) - deliberately
  unpredictable, the mirror of Stage 2's fixed rotation. Fires visible
  travel-time `BossProjectile` bolts (`Projectiles_light`/`Hit_light`), not
  a hitscan. Health-threshold `Stagger` (one-shot per 75/50/25%) is
  separate from the permanent hit-react suppression below. Dies
  **without** `EnemyBase`'s dissolve - `BossFightController` owns death.
- **Stage 2 - `BossMechAI`** (scale 4, `Mech_BarbaraTheBee.fbx`, own
  `RobotArmature` rig): inert (scale 0, AI/collider disabled) until the
  cutscene activates it. Wanders within `[minRange, maxRange]`. Attacks: a
  **fixed round-robin**, 8 steps (`TopDownBeam` -> `Shoot_Small` -> `Jump`
  -> `Shoot_Big` -> `TopDownRocket` -> `Shoot_Small` -> `Jump` -> `Shoot_Big`
  -> repeat), deliberately predictable. `Shoot_Small`/`Shoot_Big` alternate
  `firePointLeft`/`firePointRight`, reskinned to `Projectiles_light` (fast,
  low damage) / `Projectiles_magic` (slow, high damage). `Jump` lands into
  camera-shake + `PlayerController.Stagger` (Stun VFX above the player -
  [player-controller](player-controller.md)). Hit reactions bypass
  `Health`'s generic `HitReact` (Gotchas). `TopDownBeam`/`TopDownRocket`
  (`BossMechAI.TopDownAttack`) ground-target the player's position at cast
  time via `Vfx.TopDownGroundEffect.Play` (shared with [ultimate]
  (ultimate.md)'s secondary attacks) - telegraphs `topDownTelegraphDelay`,
  then applies `topDownDamage` if the player is still within
  `topDownHitRadius`.
- **`BossFightController`**: four-phase cutscene on `Health.Died` - Linger ->
  Pan -> Grow (scale `0 -> (4,4,4)`) -> Reveal (mech ground-slam). Player
  input and `PlayerAnimatorRelay` disabled for the sequence (the Relay reads
  frozen `PlayerController` properties, so must be disabled too or the
  Animator holds the last pose instead of Idle). Every `EnemyBase` in the
  scene (except the mech and the dying astronaut) is `SetFrozen(true)` for
  the duration - see Gotchas.
- **`BossProjectile`**: one generic factory shared by the mech, astronaut,
  [player-combat](player-combat.md)'s player shot, and [enemies](enemies.md)'s
  flying-enemy shuriken - speed/damage/homing/lifetime plus optional
  `BossProjectileVisuals` and an `onHit` callback (used by [ultimate]
  (ultimate.md)'s electric bolts). Own kinematic `Rigidbody`,
  `SafeLookRotation` (Gotchas).

Two imported VFX packs live at `Assets/` root, not `Assets/Art/` (self-contained, not [runtime-art](../assets/runtime-art.md) derivatives):
`GabrielAguiarProductions/FreeQuickEffectsVol1/` (muzzle flash,
transformation burst) and `Lana Studio/Casual RPG VFX/` (projectile/hit/
top-down-attack/Stun/melee-hit prefabs).

## Key files

- `Assets/Scripts/Vfx/ImportedVfxUtility.cs` - `FixUrpMaterials(root)`
  branches by renderer type: particle/trail/line renderers get
  `Sprites/Default`, everything else opaque `Universal Render Pipeline/Lit`
  (Built-in-RP packs lose alpha under a naive URP/Lit assignment); always
  clones `renderer.materials`, never `sharedMaterial`.
  `ForceHierarchyParticleScaling(root)` forces `ParticleSystem.main.
  scalingMode = Hierarchy` (many packs default to `Shape`/`Local`, ignoring
  parent scale). Shared by `PlayerCombat`.
- `Assets/Editor/Enemies/BossSceneSetup.cs` - both bosses use **fixed
  local-space `CharacterController` dimensions**, not `GetLocalRenderBounds`
  - bind/T-pose bounds don't match the standing Idle pose. Astronaut:
  `center (0,1.275,0)`, `height 2.55`, `radius 0.55` (matches the player's
  capsule). Mech: `center (0,1.6,0)`, `height 2.9`, `radius 0.75`,
  re-derived from measured vendor OBJ bounds (Gotchas), still an estimate.
  Muzzles (user hand-tuned): Stage 1 `muzzle.localPosition (0.208, 1.574,
  2.558)`; Stage 2 `firePointLeft (-1.001, 1.943, 0.677)`, `firePointRight
  (1.01, 2.0189, 0.612)`. Imported-pack prefab paths degrade to
  `BossProjectile`'s procedural look if an asset fails to load.

## Invariants

- Both bosses set `Health.SuppressHitReact = true` permanently (a flinch
  interrupted attack sequences) - damage still applies, just no reaction
  animation. See [enemies](enemies.md). `EnemySceneSetup.AddEnemiesToScene`
  disables the three basic enemies right after building the boss fight.
  Bosses reuse `PlayerController.Stagger`/`ThirdPersonCameraController.Shake`
  rather than boss-local code.

## Gotchas

- `BossProjectile.Create` gives the projectile its own kinematic `Rigidbody`
  - not for physics, purely because Unity only raises `OnTriggerEnter` if
  **at least one** GameObject in the pair has a `Rigidbody`. The projectile
  moves by direct `transform.position` assignment; several enemy types also
  move by direct transform assignment or drive a `CharacterController`
  (neither counts either), so hits against them silently never fired -
  read as "bullets passing straight through" (flying enemies, mech legs),
  not a sizing issue. Give the moving/trigger side its own kinematic
  Rigidbody for any future projectile/trigger check. Its rotation also uses
  `SafeLookRotation(direction)` instead of bare `Quaternion.LookRotation
  (direction, Vector3.up)` - the bare form destabilizes when `direction` is
  nearly parallel to the up hint (firing near-straight up), visibly
  flipping the projectile frame to frame (travel/hit unaffected); falls
  back to `Vector3.forward` as the hint whenever `|direction.up| > 0.99`.
- Astronaut/Mech `CharacterController` dimensions were originally
  undersized/stale - re-derived per Key Files above. The mech's legs still
  stayed unhittable after that - one capsule can't wrap both a torso and a
  splayed 4-legged stance. Fixed with a **second**, separate trigger
  `CapsuleCollider` for leg hits (`direction=0`, `center (0,0.7,0)`,
  `radius 1.3`, `height 3.2`), additive to the movement `CharacterController`
  - any collider on the `Enemy` layer resolving `IDamageable` via
  `GetComponentInParent` works automatically. A temporary always-visible-
  wireframe `HitboxGizmo` debug component verified both fixes and has since
  been removed - re-add a similar `[ExecuteAlways] OnDrawGizmos` component
  if needed again (built-in collider gizmos only draw when *selected*).
- `EnemyBase` gained `SetFrozen(bool)`/`protected virtual OnFrozen()` for
  the cutscene freeze above: `StopAllCoroutines()` (cancels, doesn't pause)
  then `OnFrozen()`, which `EnemyFlyingAI`/`EnemySmallAI`/`EnemyLargeAI`
  each override to reset their `_isAttacking` flag - otherwise a killed
  attack coroutine never reaches its reset line, stuck on `Update()`'s
  early-return after the freeze lifts (`isDead || isFrozen` in all three).
- `BossMechAI`'s class doc still describes `HitRecieve_1`/`_2` as driven by
  `Health.Hit`'s `DamageType`, but permanent hit-react suppression means
  nothing currently triggers those clips.
- The Creepy Cat, Missile, DuNguyn Bullets, and Hun0FX packs were imported
  and later removed (a stray `MissingReferenceException`/orphaned `.meta`
  is a leftover wiring point; fall back to Free Quick Effects Vol.1 or Lana
  Studio rather than re-importing a removed pack). The `Dance` homing-
  fireball attack was also removed entirely, replaced by `TopDownBeam`/
  `TopDownRocket`. `TopDownAttack`'s `hit_controller`/`shot_controller`
  lookup is a by-name search against the Lana Studio prefab's own child
  naming - a pack update renaming those children breaks the telegraph/
  impact split silently.

**How to extend**: new projectile types get a `ProjectileVisualStyle`/`BossProjectileVisuals` config through `BossProjectile.Create`, not a new
component. New boss attacks extend the relevant AI's weighted-random pool
(Stage 1) or round-robin sequence (Stage 2).

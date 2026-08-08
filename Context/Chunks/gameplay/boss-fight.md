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
related: [enemies, player-controller, player-combat, runtime-art, state]
verifiedAtCommit: 148a3fe3150d9a1b051c8129dbc8e3051832eff7
lastVerified: 2026-08-08
---

## What this is

The two-stage boss fight built on top of [enemies](enemies.md)'s shared
`Combat`/`EnemyBase` plumbing. `BossSceneSetup.BuildBossFight` (called from
`EnemySceneSetup.AddEnemiesToScene`) builds both stages plus a
`BossFightController` that wires the Stage 1 -> Stage 2 transformation.

- **Stage 1 - `BossAstronautAI`** (scale 2, `Astronaut_BarbaraTheBee.fbx`,
  shares the player's `CharacterArmature` rig/clip set): circle-strafes the
  player at `preferredRange`, picks a **weighted-random** attack from
  ranged burst / punch / heavy `Weapon` melee — deliberately unpredictable
  ("random like a boss fight"), the mirror of Stage 2's fixed rotation. Fires
  visible travel-time `BossProjectile` bolts, not a hitscan, so ranged
  attacks are actually dodgeable. A landed `Weapon` hit fires `Taunt` (plays
  `Wave`) right after. Health-threshold `Stagger` (plays `No`, one-shot per
  75/50/25% crossing) is the "boss reacting to damage" beat — separate from,
  and not suppressed by, the permanent hit-react suppression below. Dies
  **without** `EnemyBase`'s dissolve (spec: "die without dissolving and then
  become the mech") — `HandleDeath` is overridden to skip `base.HandleDeath()`
  entirely; `BossFightController` listens to the same `Health.Died` and owns the spectacle.
- **Stage 2 - `BossMechAI`** (scale 4, `Mech_BarbaraTheBee.fbx`, its own
  `RobotArmature` rig): starts inert (scale 0, AI/collider disabled) until
  the cutscene activates it. Wanders to stay within `[minRange, maxRange]` of
  the player — movement direction is independent of the player's position,
  only the distance band matters — with a faster `chaseSpeed` catch-up so a
  player who "just runs away and never attacks" can't stall the fight.
  Attacks are a **fixed round-robin** (`Dance` -> `Shoot_Small` -> `Jump` ->
  `Shoot_Big` -> repeat), deliberately predictable so "the player can
  understand and counter." `Dance` conjures+launches homing fireballs from
  ring points around the mech (biased to the hemisphere facing the player, a
  brief conjure-VFX windup before each launch, 2 fireballs per portal a beat
  apart); `Shoot_Small`/`Shoot_Big` alternate `firePointLeft`/`firePointRight`
  and fire a burst (not one shot per trigger) off a single clip play;
  `Jump` ascends then, on landing, unconditionally camera-shakes and staggers
  the player into the `Duck` animation via `PlayerController.Stagger`. Hit
  reactions bypass `Health`'s generic `HitReact` — `HitRecieve_1`/`_2` are
  meant to be driven by `Health.Hit`'s `DamageType`, but see Gotchas.
- **`BossFightController`**: four-phase cutscene on the astronaut's
  `Health.Died` — Linger (push in on the dissolving astronaut) -> Pan (travel
  to the inert mech) -> Grow (multi-loop bottom-to-top camera spiral while
  scaling `0 -> (4,4,4)` with overshoot, screen flashes, one imported/
  procedural particle burst) -> Reveal (mech's ground-slam plays as the
  "stagger" payoff, then camera blends smoothly back to the normal follow
  view). Player input and `PlayerAnimatorRelay` are disabled for the whole
  sequence — the Relay keeps reading `PlayerController`'s frozen properties
  while the controller is disabled, so it too must be disabled or the Animator
  keeps the last pose instead of settling to Idle.
- **`BossProjectile`**: one generic component/factory shared by the mech's
  fireballs (homing)/bullets/missiles and the astronaut's ranged bolts —
  speed/damage/homing/lifetime plus an optional `BossProjectileVisuals`
  struct (imported prefab + scale/rotation/loop + impact effect) that swaps
  in an imported VFX asset instead of the procedural primitive look. Homing
  steers turn-rate-limited toward a live `Transform`, not an instant snap.

`Assets/GabrielAguiarProductions/FreeQuickEffectsVol1/` (imported directly at
`Assets/` root, not moved into `Assets/Art/` per [runtime-art]
(../assets/runtime-art.md)'s usual convention, since it's a self-contained
prefab pack) is the one imported VFX pack still active, used by both this
chunk and [player-combat](player-combat.md)'s muzzle-flash/projectile visuals.

## Key files

- `Assets/Scripts/Vfx/ImportedVfxUtility.cs` - `FixUrpMaterials(root)`
  branches by renderer type: `ParticleSystemRenderer`/`TrailRenderer`/
  `LineRenderer` get `Sprites/Default` (proven alpha-blended elsewhere in this
  project); everything else gets an opaque `Universal Render Pipeline/Lit`.
  Needed because Built-in-RP packs (particle shaders like
  `Particles/Alpha Blended`) lose alpha under a naive URP/Lit assignment and
  render as solid opaque cards. Always clones `renderer.materials` (never
  `sharedMaterial`), so vendor assets are never modified on disk; packs
  already shipped URP-native (Free Quick Effects Vol.1's URP build) pass
  through untouched. `ForceHierarchyParticleScaling(root)` forces every
  `ParticleSystem.main.scalingMode = Hierarchy`, since many packs default to
  `Shape`/`Local` scaling (particle size ignores parent transform scale) —
  without it, scaling an imported effect's instance root does nothing
  visible. Shared by `PlayerCombat` (muzzle flash/projectile visuals) too.
- `Assets/Editor/Enemies/BossSceneSetup.cs` - both bosses use
  **fixed local-space `CharacterController` dimensions**, not
  `GetLocalRenderBounds` (the enemies-chunk pattern) — the skinned mesh's
  bind/T-pose bounds at edit time don't match the standing Idle pose, and
  measuring them left both bosses floating/hovering. Optional imported-pack
  prefab paths (`VfxPackFolder` = `Assets/GabrielAguiarProductions/
  FreeQuickEffectsVol1/Prefabs/`) are wired here at edit time (only the
  Editor can use `AssetDatabase`); every wiring point degrades to
  `BossProjectile`'s procedural look if the asset fails to load, so a missing
  pack doesn't hard-break the fight. `EnemyHealthBarUI`'s parent-scale
  compensation divides the bar's `localScale` by the boss's own 2x/4x scale.

## Invariants

- Both bosses set `Health.SuppressHitReact = true` permanently in
  `OnEnable` — even a rate-limited flinch was observed cutting into
  Punch/Weapon/the ranged burst/the attack rotation often enough to read as
  "stuck, can't attack." Damage still applies normally; there is just no
  reaction animation. See [enemies](enemies.md) for the shared mechanism.
- `EnemySceneSetup.AddEnemiesToScene` disables the three basic enemies right
  after building the boss fight — see [enemies](enemies.md).
- Bosses reuse `PlayerController.Stagger`/`ThirdPersonCameraController.Shake`
  ([player-controller](player-controller.md)) rather than boss-local code.

## Gotchas

- `BossMechAI`'s class doc still describes `HitRecieve_1`/`HitRecieve_2` as
  driven by `Health.Hit`'s `DamageType`, but the permanent hit-react
  suppression above means that reaction path no longer fires in practice —
  the clips exist and are wired in `AC_BossMech.controller`, but nothing
  currently triggers them. Revisit if per-damage-type mech reactions are
  wanted back without reintroducing the attack-interrupting flinch.
  `fireballLoopEffectPrefab` is intentionally left unset — an imported
  directional flamethrower stream looked wrong flown as a travelling
  projectile, so only the conjure windup (ring-portal effect) uses an
  imported asset; the flight visual stays procedural (solid emissive sphere +
  layered flame particles, tuned across several rounds of "too flamey,"
  "looks hollow," "too big and bright" feedback).
- The Creepy Cat pack was removed from the project mid-session; its former
  wiring points (`Effect_06`/`Effect_07`) now point at Free Quick Effects
  Vol.1 instead. The Missile pack, DuNguyn Bullets pack, and Hun0FX fireball
  pack were also imported and then removed as not fitting the aesthetic — if
  any `.meta`-only orphaned folder or a `MissingReferenceException` on a
  prefab field shows up, it is very likely a leftover wiring point from one
  of these; fall back to Free Quick Effects Vol.1 or the procedural look
  rather than re-importing a removed pack.

## How to extend

New projectile types should get a new `ProjectileVisualStyle`/
`BossProjectileVisuals` config through `BossProjectile.Create`, not a new
component. New boss attacks should extend the relevant AI's existing
weighted-random pool (Stage 1) or round-robin sequence (Stage 2).

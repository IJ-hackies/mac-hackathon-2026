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
  - "Assets/Editor/ModelAnimationUtility.cs"
related: [player-controller, player-combat, runtime-art, state]
verifiedAtCommit: 99146a500bb84fc2d74955cca7988e918c9092e2
lastVerified: 2026-08-08
---

## What this is

Three fightable enemy types plus two undressed boss placeholders, all built
from the Ultimate Space Kit's `Enemy_*`/`*_BarbaraTheBee` FBX files, dropped
into `Assets/Scenes/Player.unity` alongside the player. Combat (both player
and enemy sides) runs on a shared `Combat.Health`/`Combat.IDamageable` pair
so melee, hitscan, and enemy attacks all resolve damage the same way.

Barbara the Bee (`Astronaut_BarbaraTheBee.fbx`) and her Mech
(`Mech_BarbaraTheBee.fbx`) are placeholders only — textured and in the scene,
but no `Health`/AI component — reserved for a later boss-fight pass.

## Key files

- `Assets/Scripts/Combat/IDamageable.cs` / `Health.cs` - shared by the player
  and every enemy. `Health.ApplyDamage` reduces `CurrentHealth`, fires
  `HealthChanged`, and fires `HitReact`/`Death` Animator triggers
  **fire-and-forget** — never blocks its own caller. Load-bearing decision
  behind "hit reactions shouldn't stall gameplay": no script waits on or
  checks "is reacting", so movement/attack logic keeps running underneath a
  `HitReact` overlay, same as the pre-existing `Melee`/`Fire` AnyState
  pattern. `Died` is the hook other scripts use to actually stop something
  (`EnemyBase.HandleDeath`, `PlayerDeathHandler`).
- `Assets/Scripts/Enemies/EnemyBase.cs` - shared plumbing: finds the player
  via `FindFirstObjectByType<Player.PlayerController>()` at `Awake`,
  `FacePlayer()` (hard requirement, not just default), and `Died` → sets
  `isDead`, `StopAllCoroutines()` (an in-flight attack coroutine doesn't
  check `isDead` mid-sequence — `EnemyFlyingAI`'s multi-second `Headbutt`
  telegraph was observed fighting the `Death` animation before this fix),
  then runs `DissolveAndDestroy`.

  `DissolveAndDestroy` first runs `FallToGround` — gravity-accelerated fall
  to `y=0`, needed because the two flyers freeze mid-air at their last hover
  height once `isDead` stops their `Update` loop. Runs concurrently with the
  Death animation and continues past `deathAnimationHold` if not yet
  grounded (grounded `EnemyLargeAI` is already at `y=0`, no-op wait). Once
  grounded, swaps every renderer onto a per-instance clone of
  `Custom/EnemyDissolve` (`Assets/Art/Shaders/S_EnemyDissolve.shader`) and
  animates `_DissolveAmount` 0→1 — a true per-pixel noise-clip dissolve with
  a glowing edge ("Thanos snap"), not a scale/shrink approximation.
  Per-instance clone specifically so it never touches the shared
  `M_Enemy*.mat` asset and affects other live instances of that enemy type.
- `Assets/Scripts/Enemies/EnemyFlyingAI.cs` - ranged. Wanders while drifting
  toward the player, capped at `maxDistanceFromPlayer`. `Headbutt`: charges
  after `chargeStartDelay`, a `LineRenderer` telegraph tracks the player for
  `trackDuration`, then **freezes** for `pauseDuration` (dodge window — only
  the frozen line matters), then an instant hitscan `Physics.Raycast` along
  it against the `Player` layer. No travel-time projectile.
- `Assets/Scripts/Enemies/EnemySmallAI.cs` - melee flyer. Closes to
  `attackRange`, punches on cooldown; damage lands only if the player is
  still in range at the swing's midpoint. `attackRange` is set at build time
  from the model's measured collider radius, not the class default.
- `Assets/Scripts/Enemies/EnemyLargeAI.cs` - grounded `CharacterController`
  swarmer. Walks at range, **runs when closer** (`runDistanceThreshold` —
  intentionally the opposite of "sprint to close, then slow down"; matches
  the user's literal spec). Alternates Punch, Punch, Weapon (`_attackStep`
  cycles 0→1→2, Weapon deals more and resets). Overrides `HandleDeath` to
  `ResetTrigger` `Punch`/`Weapon` and zero `Speed` before `base.HandleDeath()`
  — a killing blow mid-swing could leave an attack trigger armed the same
  frame `Death` fires (and `Speed` frozen at a running value), observed as
  the Death pose showing for a split second before reverting to running.
- `Assets/Editor/Enemies/EnemySceneSetup.cs` - `Tools ▸ Enemies ▸ Add Enemies
  To Player Scene`. Builds one `AnimatorController` per AI type
  (`BuildHoverController` shared by Flying/Small; `BuildGroundController` for
  Large). In `BuildGroundController`, `AddHitReactAndDeath` is called
  **before** the `Punch`/`Weapon` `AnyState` transitions are added — Unity
  evaluates `AnyState` transitions in add order, so this ordering is what
  guarantees Death wins priority if an attack trigger is still armed (pairs
  with `EnemyLargeAI.HandleDeath` above). Also builds a per-model palette
  material (all 5 share `T_SpacePalette`, confirmed by checksum, but get
  separate `M_<Name>.mat` for traceability), a hit collider, `Health`, and
  the AI script. The hit collider (`CapsuleCollider` for the two flyers;
  `CharacterController` for Large) is sized via `GetLocalRenderBounds` —
  combined world-space `Renderer.bounds`, not hand-picked constants (the
  originals were a humanoid-shaped guess and the actual cause of a "shots
  don't register" bug — collider didn't line up with the mesh). The same
  measured radius (+ `MeleeReachMargin`) also sets `attackRange`, fixing the
  analogous "enemy right in front but won't attack" bug. All 5 models are
  recursively moved to the `Enemy` physics layer
  (`ModelAnimationUtility.EnsureLayer`, shared with `PlayerSceneSetup`) so
  [player-controller](player-controller.md)'s camera can exclude them from
  its collision `SphereCast`. **Opens and re-saves `Player.unity` in place**
  rather than rebuilding it — must run after `PlayerSceneSetup.
  BuildTestScene`, which rebuilds the scene from empty and would otherwise
  wipe these out.
- `Assets/Editor/ModelAnimationUtility.cs` - clip-lookup/looping/layer
  helpers shared with `PlayerSceneSetup` (`GetClip`, `LoadSourceClips`,
  `ConfigureAnimationLooping`, `EnsureLayer`).
- `Assets/Scripts/Enemies/EnemyHealthBarUI.cs` - world-space bar above each
  fightable enemy, from `EnemySceneSetup.AddHealthBar` using the hit
  collider's measured bounds. Unparented - drives its own transform off an
  anchor each `LateUpdate`, billboards to `Camera.main`. Fill resizes via
  `anchorMax.x`, not `Image.Type.Filled` (unreliable without a sprite), and
  hides via the same `HealthChanged` callback that resizes it, vanishing
  exactly at 0 health rather than after the death dissolve. Barbara/Mech
  placeholders skip it (no `Health` yet).

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

- `Health.CurrentHealth` falls back to `MaxHealth` while its backing field is the unset
  sentinel (`-1`), since editor setup scripts bind UI to `Health` right after
  `AddComponent<Health>()`, before `Awake` (Play-mode only) ever runs. Relatedly, **never
  subscribe to a `Health` event from an editor setup script** — entering Play mode reloads
  the domain, silently dropping any edit-time delegate subscription. `HealthHudUI`/
  `EnemyHealthBarUI.Bind()` only store the `Health` reference (survives; a delegate
  wouldn't); the real `+=` happens in `OnEnable`, same as `EnemyBase`/`PlayerDeathHandler`
  for `Health.Died`. Violating this: health changes correctly but bound UI never updates.
- All AI movement is a flat-ground assumption (`CharacterController` +
  constant-Y/hover-height translation), consistent with `Player.unity`'s flat
  test plane — does **not** implement the planet's radial gravity
  (`unity-project-bootstrap` in `STATE.md`). Revisit before spherical use.
- See [player-controller](player-controller.md) Gotchas for the iCloud-Drive
  " 2"/" 3" duplicate-file compile-error pattern — affects the whole `Assets/` tree.

## How to extend

Barbara the Bee / Mech boss AI is intentionally deferred — follow the same
`EnemyBase` + dedicated `AnimatorController` pattern rather than a new shape.
The two flyers already share `BuildHoverController`; a third hover-based
enemy should reuse it rather than duplicating the controller-building code.

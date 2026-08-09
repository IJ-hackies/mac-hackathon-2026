---
chunk: wave-system
title: Endless wave director, protected-area locks, rewards, and HUD
owns:
  - "Assets/Scripts/Gameplay/Waves.meta"
  - "Assets/Scripts/Gameplay/Waves/**"
  - "Assets/Scripts/Gameplay/Perimeters.meta"
  - "Assets/Scripts/Gameplay/Perimeters/**"
  - "Assets/Scripts/UI/Waves.meta"
  - "Assets/Scripts/UI/Waves/**"
  - "Assets/Editor/Waves.meta"
  - "Assets/Editor/Waves/**"
  - "Assets/Tests/EditMode/Waves.meta"
  - "Assets/Tests/EditMode/Waves/**"
  - "Assets/Prefabs/PlayerRig.prefab*"
  - "Assets/Scenes/SampleScene.unity*"
related: [system, state, core-loop, gameplay-areas, progression, enemies, boss-fight, player-controller, items, ultimate, unity-project]
verifiedAtCommit: 5880217f80f1e06cbc5b770ce9d0b680dcccf6f9
lastVerified: 2026-08-09
---

## What this is

`WaveDirector` owns one endless run: intermission, regular wave, arena travel,
arena seal/countdown, arena combat, and game over. `WaveGameController` owns
the rebindable one-second StartWave hold, protected-area tracking, UI, barrier
presentation, the intermission-only base recall, and circle-wipe scene restart/
main-menu return. `Tools > Waves > Configure
Complete Wave Loop` idempotently wires the prefab and SampleScene; validation
checks serialized references without changing gameplay.
The isolated Wave UI rebuild also rewires its six prefab-local controller view
references, so rebuilding the generated subtree cannot leave stale links.
`WaveGameController` recreates its nonserialized input copy, StartWave action,
and base-recall pose when re-enabled after an Editor assembly reload.

## Contracts

- A run starts with 300g and no active wave. Intermission is indefinite.
  StartWave defaults to F and works only while the player is outside all three
  protected areas.
- The in-game settings console may recall the astronaut to the authored run-start
  pose only during `Intermission`. Regular, arena-travel, seal, arena-combat,
  and game-over phases all deny it. A successful recall re-grounds the radial
  motor and immediately refreshes camera follow and LandingBase membership.
- Regular waves last 30 seconds through wave 10, 25 seconds on waves 11-20,
  and 20 seconds from wave 21. LandingBase, Arena1, and Arena2 are locked for
  the duration. Survivors retreat/despawn without gold when time expires;
  player health and ammo persist.
- Regular enemies spawn off-camera near the player at 24-45 surface units,
  clear all area perimeters and physical terrain props using the prospective
  hierarchy's 3x non-trigger collider/`CharacterController` footprint, remain
  separated from live enemy footprints, detect at 40 units, and retain aggro.
  Unsafe samples retry without consuming the spawn request. Off-screen enemies
  farther than 90 units become recyclable after five seconds.
- Every directly instantiated wave enemy keeps its authored proportions at 3x
  root scale, including both stages of the Arena2 Barbara encounter. Walking,
  melee, and boss range bands receive the same wrapper multiplier.
- Active cap is `min(5 + wave, 40)`; interval is
  `max(2.2 - .04 * (wave - 1), .55)`. Wave 1 is Small only, wave 2 is 70/30
  Small/Flying, and later regular waves are 50/30/20 Small/Flying/Large.
- Waves 5, 15, 25... travel to Arena1 and spawn an all-at-once 50/30/20 swarm.
  Count is `10 + 10 * ((wave - 5) / 10)` with no cap.
- Waves 10, 20, 30... travel to Arena2 and run both Barbara stages. Boss waves
  have no timer or ambient spawns and complete only when the final Mech dies.
- Arena combat objectives stay in the top-center HUD safe area. Arena1 shows
  defeated and remaining counts from the director's objective ledger, including
  enemies still queued for a collision-safe spawn. Pending enemies therefore
  display as remaining rather than defeated. Arena2 keeps Barbara's active-stage
  HP bar and numeric value visible through the Astronaut-to-Mech transition.
- Starting a boss wave locks the base and non-target arena and shows a HUD
  marker. Entering the target seals its perimeter, plays a three-second sweep,
  then starts combat. The target remains locked until completion.
- Arena travel guidance targets each arena's authored `Perimeter/Entrance`
  anchor at the omitted wall-sheet span, then uses the surface great-circle
  tangent as a local camera-relative compass bearing. It stays continuous as a
  target passes behind the camera, retains its chosen route through a
  170-to-165-degree antipodal hysteresis band, and refreshes after the gameplay
  camera.
- With `n = max(0, wave - 1)`, regular-enemy health uses
  `.10n + 1.03^n` and damage uses `.075n + 1.03^n`; both are uncapped.
  Barbara retains her separate uncapped `1 + .15n` health curve. Movement
  remains `1 + .015n` capped at 2x; attack rate and projectile speed remain
  `1 + .02n` capped at 2x.
- Small/Flying/Large pay 20/25/30g through
  `min(1 + ln(max(1, n)), 4)`: waves 1-2 remain at base and the 4x cap starts
  at wave 22. Arena1 completion pays 200g and Arena2 400g; their separate
  `1 + .05n` multiplier remains capped at 3x.
  Barbara pays only on final completion. Fortune adds 15% to each regular-wave
  kill award; Fortune II independently adds 15% to every arena kill and
  completion award, with normal per-award integer rounding.
- Med Kit and Ammo Kit respectively create exactly 15 Health and 10 Ammo
  pickups once at the beginning of each regular wave. They use lightly jittered,
  globe-wide spacing and only valid planet-surface sites outside all three
  protected areas. Uncollected wave pickups are destroyed at wave end, game
  over, and the next run. Ultimate creates one grounded Thunder pickup at the
  radial center of each arena fight and uses the same cleanup lifecycle.
- Player death wins same-frame ties. Game over shows wave reached, kills, gold
  earned, and duration; restart reloads SampleScene and replays the opening.
- The intermission prompt occupies the top-center safe area. Inside a protected
  area it shows only the leave-area warning; outside it shows the larger
  `HOLD <binding> TO START WAVE` affordance and switches to percentage feedback
  while the StartWave binding is held.
- The persistent wave readout is a transparent bottom-right stack: timer and
  progress line, wave number, then state text. The prefab setup validator keeps
  that corner anchor and vertical order stable across generated UI rebuilds.

## Implementation traps

- Regular spawning selects the radial ground-facing ray hit closest to the
  player's current shell radius; a nearest-hit ray can stop on props or fencing.
  Arena perimeter poles define the playable boundary, not its floor height:
  Arena1's crater floor is roughly 41 units below its average pole radius.
  Arena candidates and the center fallback therefore accept only an outward-facing
  collider in the configured planet hierarchy instead of comparing hits to pole
  radius. Roots are lifted by the footprint's inward extent and rejected when
  the full oriented footprint overlaps a non-ground collider. Trigger-only attack
  volumes do not inflate physical clearance. Shared grounded movement preserves
  the same below-root controller clearance after spawning instead of snapping the
  root back inside the surface. Navigation inflates rocks by the actor's physical
  radius plus a small fixed margin, but uses only the configured look-ahead and
  excludes the player/dynamic enemies from static-rock avoidance. Failed arena
  samples stay queued and retry; they do not decrement the objective ledger.
- `WaveArenaObjective` completes on Barbara's final Mech health. HUD presentation
  separately follows the stage-one health until death, then the Mech health.
- Enemy death and reward events are distinct from retreat/recycle removal.
- Perimeter colliders are generated panels owned by `WaveAreaBarrier`; membership
  still comes only from `GameplayArea` perimeter polygons. If a non-objective
  enemy crosses one, it rolls back that single crossing and receives a sustained
  tangent recovery direction; do not restore the former every-frame rewind.
- Barrier panels use the Resources-backed `Custom/WaveEnergyBarrier` shader:
  an almost-clear fill beneath a world-scaled luminous lattice, animated scan,
  and area tint. Its pass hardcodes alpha blending and disabled depth writes;
  do not return to runtime-mutated URP Unlit surface flags, which remained
  opaque. Presentation stays separate from collision and polygon membership.
- Project arena bearings through the player's radial tangent plane. A raw
  behind-camera screen projection flips at 90 degrees, while an unstabilized
  shortest great-circle tangent flips when the player crosses an antipode.
- Arena poles are evenly spaced, so their geometry cannot identify the visual
  entrance. Keep the explicit entrance anchors aligned with the omitted
  wall-sheet spans; center averaging is only a compatibility fallback when an
  anchor is absent.
- Enemy pickup drops, score, local best, and online leaderboard remain absent;
  special-skill pickups are deliberate wave/arena allocations, not enemy drops.

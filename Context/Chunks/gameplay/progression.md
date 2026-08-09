---
chunk: progression
title: Run-scoped gold, base stations, stat upgrades, and special skills
owns:
  - "Assets/Scripts/Progression.meta"
  - "Assets/Scripts/Progression/**"
  - "Assets/Scripts/UI/Progression.meta"
  - "Assets/Scripts/UI/Progression/**"
  - "Assets/Scripts/Gameplay/Interaction.meta"
  - "Assets/Scripts/Gameplay/Interaction/**"
  - "Assets/Editor/Progression.meta"
  - "Assets/Editor/Progression/**"
  - "Assets/Tests/EditMode/Progression.meta"
  - "Assets/Tests/EditMode/Progression/**"
  - "Assets/Art/Textures/UI/Progression.meta"
  - "Assets/Art/Textures/UI/Progression/**"
  - "Assets/Prefabs/PlayerRig.prefab*"
  - "Assets/Scenes/SampleScene.unity*"
related: [system, state, core-loop, wave-system, gameplay-areas, player-controller, player-combat, items, ultimate, runtime-art, unity-project]
verifiedAtCommit: 5880217f80f1e06cbc5b770ce9d0b680dcccf6f9
lastVerified: 2026-08-09
---

## What this is

`PlayerProgression` owns single-run gold and purchased upgrades. A fresh run
starts with 100 gold; nothing persists between runs. Enemy kills and completed
arena contracts award gold through [wave-system](wave-system.md). The HUD shows a gold icon/value beside health, and the ammo bar
shows `magazine / reserve` while its fill continues to represent the magazine.

Three LandingBase structures have radial ground-level interaction zones sized
from each building footprint, plus large floating color-coded icon/halo beacons
and labels above their roofs. Entering a zone shows an `E - INTERACT` prompt:

- `Base_Large` opens Supply: 50 HP for 50g, 150 HP for 100g, and a full
  magazine+reserve refill for 100g. A pack is disabled as `FULL` when it would
  have no effect; healing clamps at max HP.
- `GeodesicDome` opens seven independent level 1-10 upgrades. Purchases to
  levels 2-10 cost 50/100/200/400/800/900/1000/1100/1200g.
- `SolarPanel_Structure` opens a single-column scrolling catalog of 13
  independent, one-time special skills ordered from lowest to highest gold
  cost. Hold to Fire costs 50g; the other cards are Bullet Bounce (750),
  Fortune (500), Fortune II (500), Med Kit (400), Ammo Kit
  (600), Ultimate! (800), Quickdraw (1200), Vampire (2000), Explosive
  Bullets (750), Headshot! (800), Minigun (4000), and `???` (10000).

## Upgrade contract

- Max HP purchases add 20/25/.../60 (100 -> 460) and grant new capacity as HP.
- Movement purchases add 3/5/.../19 percentage points (1x -> 1.99x); fire rate
  adds 5/7/.../21 points (1x -> 2.17x).
- Shooting adds raw 4/5/.../12 (pistol 15 -> 87), shared by pistol, normal
  secondary, Ultimate bolts, and Ultimate lightning. Melee adds raw 3/6/.../27
  (20 -> 155).
- Defense adds 2/3/.../10 percentage points (0% -> 54%) through a keyed
  incoming-damage modifier that composes with Shield.
- Max Ammo adds 2/3/.../10 magazine capacity (15 -> 69) and 5/10/.../45 reserve
  capacity (120 -> 345), immediately granting each newly added capacity.

## Special-skill contract

- Hold to Fire adds ordinary-pistol auto-fire while Attack is held. Quickdraw
  makes reloads 0.1 seconds. Fortune and Fortune II independently boost their
  regular-wave and arena reward scopes by 15%; [wave-system](wave-system.md)
  owns those awards.
- Bullet Bounce, Explosive Bullets, Headshot!, and Minigun affect only ordinary
  pistol rounds. A round may hit up to three distinct visible enemies within 15
  units, each impact may splash 50% final impact damage in a 3-unit radius, and
  every fourth successful round deals double damage through its full chain.
  Minigun removes the ordinary-pistol rate cap, doubles its rate, adds 30
  magazine and 200 reserve capacity, and subtracts 20 raw pistol damage with a
  minimum of one; it does not implicitly grant Hold to Fire.
- Vampire heals 2% of actual enemy HP removed by any player attack, clamped to
  max HP. Med Kit scatters 50-HP pickups, Ammo Kit scatters reserve-only pickups
  worth two current magazines, and Ultimate! enables the 20-second Thunder
  allocation documented by [wave-system](wave-system.md).
- `???` always shows its title, 10000g price, and `"how'd you get here?"` flavor,
  but never reveals its effect in the shop. Ownership permanently triples the
  seven archive stat families (including later upgrades and Minigun-modified
  ammo/fire values); defense remains capped at 90%.
- Specials and the Headshot! round counter reset on a new run. The counter
  advances only on successful ordinary-pistol rounds and survives reloads and
  wave transitions.

## UI and interaction

Station consoles pause time, suspend movement/combat/abilities/look, hide the
crosshair, unlock the cursor, and close with `E`, Escape, or the Close button.
They refuse to steal ownership while Settings is open and restore only state
they captured. Holding Tab opens a centered `1180x700` non-pausing telemetry
board; releasing Tab hides it. Its rectangular navy shell leads with large live
HP/ammo rails, then a two-column archive grid with distinct left-side atlas
icons and two-line level/value typography, plus a full-width installed-systems
strip. It reports effective post-special stats, live HP/ammo, and owned special
names, excluding temporary LandingBase/Ultimate buffs.

`StationInteractionController` evaluates world-space distance every frame and
chooses the closest in-range station. Trigger enter/exit callbacks remain as a
fast path, but prompts and `E` do not depend on a physics callback firing.
The menu ignores close input during the frame in which it opens so the same
`E` press cannot both open and immediately close it; later `E` or Escape input
still closes normally.

The three station screens share a centered `1500x800` rectangular field-console
shell and pause-layer dimmer. Each uses the same wide, single-column card
language: Supply fits its three rows in one viewport, while Archive and Special
scroll vertically behind `RectMask2D` viewports. Station text receives one
readability scale pass when the menu initializes. Every row reserves a
left-anchored `128x128` plate for one of 23 distinct project-owned atlas glyphs,
then starts its left-aligned text lane after the icon while keeping actions at
the far right. `SmoothStationScrollRect` turns wheel
steps into bounded inertial velocity using ScrollRect's unscaled-time update, so
scrolling remains eased while the station menu pauses gameplay. Special rows
run top-to-bottom by nondecreasing gold cost, with catalog order retained as the
deterministic tie-breaker.

## Authoring and verification

The serialized `PlayerRig.prefab` Progression UI and SampleScene station
markers are authoritative. The former broad configurator/preview tools were
removed because they could overwrite hand-authored scene work. The visual shell
uses selected Cartoon UI and Space Expansion copies plus the project-owned
progression icon atlas; vendor packs remain untouched. Compile contracts with
`dotnet build Progression.Contracts.Tests.csproj --no-restore`; run the EditMode
assembly in Unity when execution coverage is required.

## Gotchas

- The structures sit on a spherical world and have heavily scaled parents.
  Marker placement must use planet-radial up, not global `Vector3.up`, and must
  normalize scale after applying its radial rotation. Otherwise interaction
  moves toward the roof or its world radius is distorted.
- Interaction radii deliberately cover each structure's tangential footprint
  plus approach clearance. Do not replace them with a small fixed sphere at the
  model bounds center.
- `StationMenuController`, prompt, adapter, and Tab controller live on the
  always-active Progression UI host while their visual roots may be disabled.
- Every station catalog viewport uses `RectMask2D`; keep those masks when
  changing the rectangular shell or content height, or off-viewport rows will
  render over the title and controls.
- The 23 card icons share one multi-sprite atlas. Keep its sprite names/internal
  IDs and the prefab image references aligned when replacing or re-slicing it.
- Special card labels are prefab-authored. Keep the cost-sorted definition copy,
  serialized button array, and visual card positions aligned; sorting only one
  of those can display one skill while purchasing another.
- Interaction input is evaluated before menu close input. Preserve the
  opening-frame guard if execution order or input routing changes.
- Use keyed stat modifiers. Do not overwrite LandingBase speed, Shield defense,
  Ultimate combat profiles, or other independent effects.

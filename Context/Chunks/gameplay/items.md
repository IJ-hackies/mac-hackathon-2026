---
chunk: items
title: Floating power-up pickups (Health, Ammo, Thunder) and the ammo/reload system
owns:
  - "Assets/Scripts/Items.meta"
  - "Assets/Scripts/Items/**"
  - "Assets/Editor/Items.meta"
  - "Assets/Editor/Items/**"
  - "Assets/Prefabs/Items.meta"
  - "Assets/Prefabs/Items/**"
  - "Assets/Art/Models/Items/**"
  - "Assets/Art/Materials/Items/**"
  - "Assets/Scripts/Player/PlayerAmmo.cs*"
  - "Assets/Scripts/UI/AmmoHudUI.cs*"
related: [player-controller, player-combat, progression, runtime-art, asset-library, state, ultimate]
verifiedAtCommit: e4caa898457d6a2d25ff205625898ecf4fbe2635
lastVerified: 2026-08-09
---

## What this is

Three Ultimate Space Kit pickup models (`Pickup_Health`, `Pickup_Bullets` ->
`Pickup_Ammo`, `Pickup_Thunder`) act as floating power-ups, using Lana
Studio's `Casual RPG VFX` for their auras/spawn/pickup effects. `Items.
ItemPickup` (`Assets/Scripts/Items/ItemPickup.cs`) is the shared base:
hover at `hoverHeight` (torso height), continuous spin around local up, a
looping backlight aura VFX, a one-shot spawn VFX, and on player contact a
pickup VFX plus a brief non-looping copy of the same backlight burst on the
player before destroying itself. `HealthPickup`/`AmmoPickup` implement
`ApplyEffect`; Thunder's activates the player's timed Ultimate (see
[ultimate](ultimate.md)) via `Player.PlayerUltimate.ActivateUltimate()` and
no longer overrides `CollectibleOnContact`.

Per-item backlight assignment: Health -> `Regeneration/
Regeneration_health_loop`, Ammo -> `States/Aura_acceleration`, Thunder ->
`Fog/Fog_electric`. All three share `Burst/Poof_generic` (spawn) and
`Loot/Loot_pick_up` (pickup).

This also introduced the project's first ammo/reload system:
`Player.PlayerAmmo` (magazine/reserve storage/reload timer, `TryConsumeRound`/
`StartReload`/`RefillFull`) and `Player.UI.AmmoHudUI` (a sliced blue bar
directly below health, with `magazine / reserve` centered inside).
Its fill tracks `CurrentMagazine / MagazineSize`; Ultimate's infinite-ammo
state shows a full bar and infinity symbol. `PlayerCombat.CheckShootBeat` gates
`FireProjectile` behind `playerAmmo.TryConsumeRound()` (see
[player-combat](player-combat.md)); `Reload` (`R`) triggers a manual reload.

## Key files

- `Assets/Editor/Items/ItemAssetSetup.cs` - `Tools/Items/Prepare Item
  Assets`. Copies the three FBXs from the vendor `Ultimate Space Kit -
  March 2023/Items/FBX` pack into `Assets/Art/Models/Items/`, disables
  animation/mesh-collider import (pickups use a runtime trigger
  `SphereCollider` instead), and binds them to a shared
  `M_PlanetItem.mat` off `T_SpacePalette` - same shape as
  `WorldEditor.UltimateSpaceRockAssetSetup`.
- `Assets/Editor/Items/ItemSceneSetup.cs` - three menu items:
  `Tools/Items/Build Item Prefabs` (builds the three prefabs under
  `Assets/Prefabs/Items/`, wiring the model + pickup component + VFX
  references), `Tools/Items/Place Items In Test Scene` (opens/edits the
  already-authored `Player.unity` non-destructively, instantiating the
  three prefabs near the player spawn - unlike the destructive
  `PlayerSceneSetup.BuildTestScene`), and `Tools/Items/Wire Ammo Into
  Player Rig` (adds `PlayerAmmo` to the nested `Player` and `AmmoHudUI`
  into `HUD Canvas` inside `PlayerRig.prefab`, using the same Space Expansion
  sliced-bar presentation as the main player builder, via
  `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`, same pattern as
  `PlayerSceneSetup.RepairPlayerRigPrefab`).
- `Assets/Scripts/Combat/Health.cs` also gained `Heal`/`FullyHeal` (owned
  by [player-combat](player-combat.md), not this chunk) for `HealthPickup`.

## Invariants

- Pickups detect the player via `GetComponentInParent<Player.
  PlayerController>()` on the trigger collider, not a physics layer check -
  matches `PlayerCombat`/`BossProjectile`'s existing detection pattern.
- `ItemPickup` instances are never solid obstacles; imported models have
  `addCollider` disabled at import, and the only collider is the runtime
  trigger `SphereCollider` added in `Awake`.
- Ammo is drawn once per shot *beat* (not per click) in
  `PlayerCombat.CheckShootBeat`; an empty magazine with storage remaining
  auto-starts a reload, and a reload in progress silently withholds shots
  without cancelling the Shoot animation loop.
- The HUD bar represents the magazine, not reserve storage: reload transfers
  storage into the magazine and refills the bar only when that transfer ends.
- The progression Max Ammo stat changes magazine capacity only: +2 per level,
  grants two loaded rounds without consuming reserve, and leaves reserve at 90.

## How to extend

Ammo/reload tuning (`magazineSize`, `maxStorage`, `reloadTime` on
`PlayerAmmo`) is serialized placeholder balance meant to be retuned in the
Inspector. The current magazine-size upgrade is owned by [progression]; reload
speed remains a future tuning option. Thunder's
Ultimate-duration/mech-scale tuning lives on `PlayerUltimate` - see
[ultimate](ultimate.md).

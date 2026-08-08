---
chunk: world-authoring
title: Radial planet-surface and environment asset authoring
owns:
  - "Assets/Editor/World.meta"
  - "Assets/Editor/World/**"
related: [system, runtime-art, unity-project, player-controller]
verifiedAtCommit: 927321aeae479a32412bb0928052db406373cf8a
lastVerified: 2026-08-08
---

## What this is

`Assets/Editor/World/` contains edit-mode tools for dressing the spherical
planet and configuring curated environment imports. These tools do not add
runtime components or alter the radial player controller.

## Key files

- `RadialSurfaceSnapWindow.cs` - `Tools > Planet Design > Radial Surface Snap`
  window plus a one-click `Snap Selection To Planet` command. It resolves the
  exact `Planet Ground` center, prefers its enabled `MeshCollider`, casts inward
  from outside the collider bounds, and snaps selected scene roots to the
  authored crater surface.
- `LandingBaseAssetSetup.cs` - idempotent `Configure Landing Base Assets`
  command. It creates the shared URP landing-base materials, configures MegaKit
  normal/ORM texture color space, remaps source material slots, and validates
  all 19 curated static FBXs.
- `PlanetaryWallRingWindow.cs` - `Tools > Planet Design > Wall Ring Builder`
  arranges three or more selected scene objects into a closed, surface-snapped
  ring, generates curved solid wall sheets between poles, and can place a
  fitted connector prefab between every adjacent pair.
- `CurvedWallMeshBuilder.cs` - pure mesh construction for the wall builder. It
  turns surface-sampled paths into UV-mapped, closed solids with smooth curved
  faces and separate hard-edged top, bottom, and end-cap normals.
- `UltimateSpaceVegetationScatter.cs` - idempotent whole-planet dressing tool.
  It imports/configures nine Ultimate Space Kit vegetation FBXs, samples random
  directions uniformly, raycasts the crater mesh, and replaces only its named
  generated scene root with a randomized 1,100-1,300-instance pass at 65x-75x
  scale and a fixed -90-degree local-X model correction. Bushes and plants use
  2x selection weight while grasses use 10x. Each instance receives either the
  persistent dark-orange or red shared material. It reuses the radial snap
  tool's collider cast, iteratively fits transformed mesh vertices to the real
  crater surface,
  then embeds them inward by `0.075` world units.

## Invariants

- Surface placement uses the active authored collider, never the disabled
  reference sphere. `Planet Ground` remains the default center contract.
- Snapping changes world position and rotation only, records Unity Undo and
  prefab-instance overrides, and snaps only the highest selected roots so a
  selected hierarchy is not transformed twice.
- Surface-normal alignment is the default for crater slopes; radial-up
  alignment, heading reset, cast padding, and pivot offset remain explicit
  author controls.
- The landing-base setup edits runtime copies only. Vendor files under
  `asset packs/` are never changed. Its static FBXs import with mesh colliders
  so linked scene instances block the player's radial capsule motor.
- Vegetation setup also copies from the vendor library without editing it, but
  deliberately disables imported colliders so hundreds of small props do not
  snag the player. Regeneration is seed-driven, Undoable, saves the active
  scene, and replaces only `Generated Planet Vegetation`. Grounding is computed
  after the -90-degree local-X correction, final rotation, and final scale
  against the shared surface-snap raycast; do not replace it with a fixed pivot
  offset or approximate sphere.
- Wall-ring arrangement is explicit, atomic, Undoable, and preserves the
  selected objects, their scales, and prefab-instance overrides. Connector
  generation creates a new scene-root group on every run and never removes an
  earlier generated group automatically.
- Curved sheets sample the exact planet collider between every neighboring
  pole. Close Loop optionally includes the closing pair; when disabled, the
  two explicitly assigned, adjacent Opening Pole references identify the exact
  omitted span independent of selection or geometric sort direction.
  Generation does not move the poles and can add a static non-convex
  `MeshCollider`; the scene object is Undoable while its reusable mesh remains
  under `Assets/Art/Generated/LandingBaseWalls`.

## Gotchas

Imported model pivots are not universally at the visible base. Use Surface
Offset when a model appears buried or floating after snapping. The one-click
command always uses surface-normal alignment, preserves heading, and uses zero
offset; open the full window for other settings. Planet vegetation compensates
for these pivot differences automatically from the instantiated mesh geometry.

`Regenerate Planet Vegetation` is a reroll, not an additive pass: it removes the
previous generated root before placing a new random count. The checked-in pass
uses seed `80` and a fixed authored count of `1,200`; call
`Regenerate(seed, min, max)` to reproduce or tune
it.

The asset configuration command intentionally overwrites its three generated
material assets with the documented defaults when run. Do not run it after
hand-tuning those materials unless resetting them is intended.

Generated landing-base mesh colliders are non-convex and intended for static
environment objects. Do not add a non-kinematic Rigidbody to those objects;
use a simplified convex collider setup for anything that must move. A fully
unpacked scene copy no longer inherits later FBX importer changes.

The wall-ring builder uses the active selected wall as its phase reference.
Assign a Center Anchor for repeatable edits; otherwise the base-center direction
is inferred from the selected walls and works best for a roughly complete,
evenly distributed ring. Radius fitting assumes the chosen local X or Z axis is
the wall's end-to-end length. Use End Inset when a fitted connector would
overlap the wall posts.

Curved-sheet Pole Clearance is measured from each pole center along its span;
for default-scale `Column_Hollow`, approximately `0.6` reaches its outer edge.
The first material on the active pole is reused when Wall Material is empty.
Undoing sheet generation removes the scene hierarchy but intentionally keeps
the generated mesh asset, which can be reused or deleted from the Project
window. Higher Curve Segments improve silhouette smoothness at added geometry
cost, and radial-up alignment is the cleanest default for architecture.

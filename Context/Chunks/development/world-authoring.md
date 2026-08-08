---
chunk: world-authoring
title: Radial planet-surface and environment asset authoring
owns:
  - "Assets/Editor/World.meta"
  - "Assets/Editor/World/**"
related: [system, runtime-art, unity-project, world-runtime, player-controller]
verifiedAtCommit: db81cd848e59c29f89795a89d512b044041e215a
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
  It imports/configures nine Ultimate Space Kit vegetation FBXs, raycasts the
  crater mesh, and replaces only its named generated scene root with exactly
  16,000 instances with 60x-70x grass, 40x-50x bushes, and 50x-60x plants,
  plus a fixed -90-degree local-X correction.
  It shuffles an exact 8:1:1 grass:bush:plant allocation. Seventy-five percent
  of directions remain uniform; 25% use 64 well-spaced, soft 10-14-degree
  clusters for mild natural density variation. Each instance receives either
  the persistent dark-orange or orange shared material. It reuses the radial snap
  tool's collider cast, iteratively fits transformed mesh vertices to the real
  crater surface,
  then embeds them inward by `0.075` world units.
- `UltimateSpaceRockAssetSetup.cs` - idempotent import/configuration command for
  all seven Ultimate Space Kit rock FBXs. It keeps vendor sources untouched,
  creates missing runtime copies, remaps `Atlas` to the dedicated shared-palette
  rock material, disables animation/readability, and enables mesh colliders.
- `UltimateSpaceRockScatter.cs` - seed-driven clustered whole-planet rock pass.
  It guarantees separate small/large quotas, supports small-only, large-only,
  and mixed clusters, grounds transformed vertices through the shared surface
  cast, and derives closed polygons from each area's direct perimeter poles.
- `LandingBaseNautRockArt.cs` - deterministic `Rock_1` dot-matrix lettering
  tool. It geodesically maps `NAUT` around `LandingBase/Layout/BaseCenter`,
  surface-fits every scaled rock, and replaces only the generated base child.

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
  deliberately disables imported colliders so the small props do not
  snag the player. Regeneration is seed-driven, Undoable, saves the active
  scene, and replaces only `Generated Planet Vegetation`. Grounding is computed
  after the -90-degree local-X correction, final rotation, and final scale
  against the shared surface-snap raycast; do not replace it with a fixed pivot
  offset or approximate sphere.
- Rock setup follows the same source-preservation rule. Its generated colliders
  are non-convex and intended only for static environment instances.
- Rock regeneration is Undoable, saves the scene, replaces only `Generated
  Planet Rocks`, and uses best-candidate cluster-center separation. Its closed
  pole-ring polygons receive the largest 200x rock radius plus 2 units of
  clearance, including at entrances, so meshes cannot overhang protected land.
- NAUT regeneration preflights surface hits, is atomic and Undoable, and saves
  the scene. Defaults: 59 rocks, `(180, 100, 180)` local scale, 2.88-unit pitch,
  seed 1401, collision off; X/Z multiplier is separate from preserved Y scale.
- Vegetation and rock regeneration mark generated instances as reflection-probe
  static only, not batching static. Runtime prop batching is owned by
  `SphericalPropInstancingRenderer`, and WebGL static batching is disabled to
  avoid duplicating geometry for these large authored hierarchies.
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
previous generated root before placing the new fixed 16,000-instance pass. The
checked-in pass uses seed `80`: 12,800 grasses, 1,600 bushes, and 1,600 plants.
Its 4,000 clustered placements are interleaved with 12,000 uniform placements;
cluster centers and radii are seed-driven. Call `Regenerate(seed, min, max)` to
reproduce or deliberately tune the pass.

The asset configuration command intentionally overwrites its three generated
material assets with the documented defaults when run. Do not run it after
hand-tuning those materials unless resetting them is intended.

Rock imports retain the pack's small source-unit scale and need the same
-90-degree local-X placement correction used by other Environment models.
Adjust instance scale rather than changing importer defaults.

`Regenerate Planet Rocks` is a full reroll, not an additive pass. The authored
pass uses seed `80826`: 800 small and 300 large rocks, both at literal
100x-200x Transform scale, across 146 clusters: 26 small-only, 89 large-only,
and 31 mixed. Small-bearing clusters hold 10-20 small rocks;
large-bearing clusters hold 1-3 large rocks, and some clusters mix both types.
Malformed `Area/Perimeter/Poles` paths fail instead of populating that area.

`BaseCenter` is currently a renamed, scale-1 `Roof_Opening` prefab rather than
an empty marker. The NAUT tool surface-projects its position and derives a
tangent heading safely; use the tool's Heading field to rotate the word.

Generated landing-base mesh colliders are non-convex and intended for static
environment objects. Do not add a non-kinematic Rigidbody to those objects;
use a simplified convex collider setup for anything that must move. A fully
unpacked scene copy no longer inherits later FBX importer changes.

The wall-ring builder uses the active selected wall as its phase reference.
Assign a Center Anchor for repeatable edits. Radius fitting assumes the chosen
local X or Z axis is the wall's length; use End Inset for connector overlap.

Curved-sheet Pole Clearance is measured from pole center along its span; about
`0.6` reaches a default `Column_Hollow` edge. Empty Wall Material reuses the
active pole material. Undo keeps the generated mesh asset for reuse or cleanup.

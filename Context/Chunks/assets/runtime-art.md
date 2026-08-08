---
chunk: runtime-art
title: Unity-ready art derived from source packs
owns:
  - "Assets/Art.meta"
  - "Assets/Art/**"
related: [asset-library, unity-project, player-controller, world-authoring]
verifiedAtCommit: 927321aeae479a32412bb0928052db406373cf8a
lastVerified: 2026-08-08
---

## What this is

`Assets/Art/` contains project-owned Unity imports and materials derived from
the preserved vendor packs under `asset packs/`. Runtime assets are unified by
type and gameplay role rather than separated by vendor.

## Key files

- `Textures/T_SpacePalette.png` - shared 512x512 color atlas, imported without
  mipmaps and with point filtering.
- `Materials/M_Ground.mat` - URP Lit material used by the sample-scene ground;
  its solid burnt-orange base color is sampled from the palette without binding
  the non-tileable atlas to the plane.
- `Models/Environment/Planet_CrateredMoon.fbx` - the original 1,202-position,
  2,400-triangle runtime import retained as a low-resolution source and future
  distant LOD.
- `Models/Environment/Planet_CrateredMoon_Subdivided.obj` - the active static
  visual shell, produced by one Catmull-Clark pass over `Planet_3`: 4,802
  geometric positions, 4,800 quads, and 9,600 rendered triangles. Unity imports
  5,496 render vertices after the authored UV seams are split.
- `Prefabs/Planet.prefab` - reusable approximately 150-unit-radius planet
  hierarchy. Its stable `Planet Ground` root contains the scaled shell, active
  crater-matched `MeshCollider`, and disabled reference `SphereCollider`.
- `Shaders/S_ProceduralMoonSurface.shader` - opaque URP-lit, texture-free planet
  surface shader. Object-space 3D value noise creates seam-free large lunar
  mottling, fine dusty breakup, and matching analytic bump normals without
  depending on the source mesh UVs. It supplies forward, shadow, depth, and
  depth-normal passes for the project's forward/Forward+ renderers and SSAO.
- `Materials/M_PlanetCrateredMoon.mat` - active matte planet material using the
  procedural moon shader. It preserves the established warm clay/ochre base
  color and low smoothness while adding tonal, roughness, and normal variation.
- `Models/Environment/PlanetVegetation/` - runtime FBX copies of `Bush_1..3`,
  `Grass_1..3`, and `Plant_1..3`. Animation and generated colliders are disabled
  because these are lightweight, static visual dressing rather than obstacles.
- `Materials/PlanetVegetation/` - the active instancing-enabled vegetation
  palette is `M_PlanetVegetation.mat` in dark orange and
  `M_PlanetVegetation_Red.mat` in red. Older generated palette materials may
  remain in this folder, but the scatter generator and authored scene do not
  reference them.
- `Shaders/S_ProceduralSpaceSkybox.shader` - texture-free starfield shader with
  a subtle galactic band and an HDR sun disc in a fixed world direction.
- `Materials/M_ProceduralSpaceSkybox.mat` - SampleScene's configured space
  skybox, including star density, deep-space colors, and sun appearance.
- `Models/` - canonical FBX imports grouped into `Characters`, `Environment`,
  `Props`, and `Vehicles`, regardless of the source pack.
- `Models/Environment/LandingBase/` - a curated 19-model base-building kit:
  18 Ultimate Space Kit structures plus the Modular SciFi MegaKit
  `Column_Hollow`. Static-model animation is disabled and Unity generates
  non-convex mesh colliders for the placed environment instances.
- `Materials/LandingBase/` - shared URP materials remap the imported FBX slots.
  The arena-specific Trim 01/02 variants use solid albedo colors with the
  MegaKit normal maps: Arena1 dark orange and Arena2 near-black blood red;
  scene overrides keep those palettes independent of the shared red defaults.
- `Textures/ModularSciFi/` - the base-color, normal, and packed ORM textures
  required by `Column_Hollow`. Normal maps use Unity's Normal Map import type;
  ORM maps remain linear source data.
- `Generated/LandingBaseWalls/` - persistent mesh assets produced explicitly
  by the Wall Ring Builder's curved-sheet command. Each generation uses a
  unique asset path so earlier authored walls are never overwritten.
- `Models/Characters/Astronaut_FinnTheFrog.fbx` - rigged playable-character
  source (Ultimate Space Kit), imported with its baked animation takes
  (Idle, Walk, Run, Jump, Jump_Idle, Jump_Land, plus unused takes). See
  [player-controller](../gameplay/player-controller.md).
- `Models/Characters/Player.prefab` - gameplay-ready astronaut root with a
  feet-origin `CapsuleCollider` (`height 2.55`, `radius 0.55`, `center.y 1.275`),
  kinematic Rigidbody, and rotation-aware radial motor. Its direct `VisualRoot`
  contains the astronaut model and muzzle so terrain conforming remains
  cosmetic and cannot independently tilt the physical capsule or camera.
  `PlayerRig.prefab` nests this asset rather than duplicating its components.
- `Materials/M_Astronaut.mat` - URP Lit material binding the shared
  `T_SpacePalette` texture directly (unlike `M_Ground`, this mesh's UVs
  sample the atlas rather than using a flat sampled color).
- `Animations/AC_Player.controller` - AnimatorController generated by
  `Assets/Editor/Player/PlayerSceneSetup.cs`; not hand-authored.
- `Materials/M_Projectile.mat` - URP Lit material with emission enabled, used
  by the projectile prefab (`Assets/Prefabs/Projectile.prefab`, owned by
  [player-controller](../gameplay/player-controller.md) since it's a
  gameplay prefab rather than an art import).

## Invariants

- Keep vendor originals and license files under `asset packs/`; never edit them
  in place.
- Commit every Unity `.meta` file with its asset and preserve stable GUIDs.
- Organize runtime imports by asset type and gameplay role, not by vendor. Keep
  source attribution traceable through the preserved vendor library and clear
  runtime filenames.
- Use FBX as the default runtime model format and keep alternate source formats
  out of the Unity project unless a concrete requirement justifies them.
- Prefer one shared palette import over duplicating the same texture per model
  category.
- Keep the planet prefab root named `Planet Ground`; the radial player
  controller resolves that exact active scene name when no center is assigned.
- Planet vegetation is authored as FBX prefab instances beneath the scene-level
  `Generated Planet Vegetation` root. The root stays at world scale 1 so each
  instance's 65x-75x uniform scale remains relative to the source FBX. A fixed
  -90-degree local-X correction is applied before surface fitting, followed by
  a slight `0.075`-unit terrain embed. Instances use shared material assets
  rather than per-renderer material clones, and the active palette stays limited
  to dark orange and red.
- Generated wall meshes are project-owned runtime art. Keep their `.asset` and
  `.meta` files while referenced by a scene; remove confirmed orphaned versions
  from the Project window when iterative authoring leaves unused generations.

## Gotchas

The Ultimate Space Kit atlas is a palette for the pack's authored mesh UVs, not
a seamless surface texture. Do not apply the whole atlas to generic planes or
the planet; interpolated atlas regions introduced unwanted purple color
variation. The planet's procedural shader intentionally derives its pattern
from local 3D position instead of those UVs, keeping the color variation
seam-free and attached to the object.

The MegaKit ORM textures are preserved and imported as linear data, but the
current standard URP Lit landing-base materials use only their base-color and
normal maps. Using the packed occlusion/roughness/metallic channels requires a
deliberate repack or a compatible custom shader; do not bind the ORM texture to
an incompatible single-channel slot.

The procedural sky material stores the visible sun direction. If the scene's
directional `Sun Light` is rotated later, update `_SunDirection` to the opposite
of the light's forward direction so the disc and illumination remain aligned.

FBX preserves the mesh, UV, normals, pivots, rigs, and animation data Unity
needs without an external authoring-tool dependency. OBJ is suitable only for
simple static meshes; the subdivided planet is an intentional static-derivative
exception that keeps its interpolated palette UVs and shared smooth normals.
Blend imports depend on a compatible local Blender installation, and glTF needs
an importer package that is not currently part of the project.

## How to extend

Copy the selected FBX into the appropriate semantic model folder. Create shared
project materials and textures in their type folders, use collision-resistant
descriptive filenames, and record non-default import settings or UV assumptions
here when they affect multiple assets. Run `Tools > Planet Design > Configure
Landing Base Assets` to recreate or validate the curated kit's material remaps.
Use `Regenerate Planet Vegetation` for a fresh whole-planet scatter pass; it
prepares the nine runtime FBXs and their planet-matched material as needed.

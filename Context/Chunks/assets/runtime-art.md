---
chunk: runtime-art
title: Unity-ready art derived from source packs
owns:
  - "Assets/Art.meta"
  - "Assets/Art/**"
related: [asset-library, unity-project]
verifiedAtCommit: 1c61802889ac0de025fcfaaa12c8f0ce77c07422
lastVerified: 2026-08-07
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
- `Materials/M_PlanetCrateredMoon.mat` - URP Lit material that applies the
  shared palette atlas to the crater mesh's authored UVs.
- `Shaders/S_ProceduralSpaceSkybox.shader` - texture-free starfield shader with
  a subtle galactic band and an HDR sun disc in a fixed world direction.
- `Materials/M_ProceduralSpaceSkybox.mat` - SampleScene's configured space
  skybox, including star density, deep-space colors, and sun appearance.
- `Models/` - canonical FBX imports grouped into `Characters`, `Environment`,
  `Props`, and `Vehicles`, regardless of the source pack.

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

## Gotchas

The Ultimate Space Kit atlas is a palette for the pack's authored mesh UVs, not
a seamless surface texture. Do not apply the whole atlas to generic planes;
use a palette-matched solid material or an authored kit mesh instead.

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
here when they affect multiple assets.

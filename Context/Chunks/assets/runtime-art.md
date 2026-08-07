---
chunk: runtime-art
title: Unity-ready art derived from source packs
owns:
  - "Assets/Art.meta"
  - "Assets/Art/**"
related: [asset-library, unity-project]
verifiedAtCommit: a80a51fe877d37e45775ce047baf8b28caaddf41
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

FBX preserves the mesh, UV, normals, pivots, rigs, and animation data Unity
needs without an external authoring-tool dependency. OBJ is suitable only for
simple static meshes, Blend imports depend on a compatible local Blender
installation, and glTF needs an importer package that is not currently part of
the project.

## How to extend

Copy the selected FBX into the appropriate semantic model folder. Create shared
project materials and textures in their type folders, use collision-resistant
descriptive filenames, and record non-default import settings or UV assumptions
here when they affect multiple assets.

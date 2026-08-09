---
chunk: asset-library
title: Audio and visual source asset packs
owns:
  - "asset packs/**"
related: [system, runtime-art]
verifiedAtCommit: bbe3799f82348f2367d9a308b9fd87ed7f9601ee
lastVerified: 2026-08-09
---

## What this is

The repository currently carries eleven third-party source-pack directories:

- Audio: 400 Sounds Pack; Kenney Impact Sounds, Interface Sounds, and Sci-Fi
  Sounds; Shapeforms Audio Free Sound Effects; and Space Music Pack.
- Visuals: Cartoon UI, JDSherbert Sci-Fi UI SFX Pack, Space Expansion UI,
  Modular SciFi MegaKit Standard, and Ultimate Space Kit.

The audio source root also contains one separately sourced freesound heal clip.

The library contains OGG audio plus source and interchange 3D formats including
Blend, FBX, glTF, OBJ/MTL, textures, and preview images. These files remain the
vendor source library. Unity-ready copies and game-specific derivatives live in
the project-owned runtime-art area documented separately.

## Invariants

- Preserve every pack's license file alongside the source assets.
- Check the applicable license before distributing raw or derived assets.
- Do not edit vendor source files in place when creating game-specific
  variants; place derived/runtime-ready assets under `Assets/Art/`.
- Do not ship every duplicated source representation. FBX is the default Unity
  runtime format; retain OBJ, glTF, and Blend variants in the vendor source
  library unless a specific asset requires otherwise.

## Gotchas

The packs contain multiple representations of the same models. File count is
not a unique-asset count. Large binary/source trees should be summarized by
pack and usage rather than documented file by file.

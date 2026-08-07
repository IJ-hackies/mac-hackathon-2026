---
chunk: asset-library
title: Audio and visual source asset packs
owns:
  - "asset packs/**"
related: [system, runtime-art]
verifiedAtCommit: a80a51fe877d37e45775ce047baf8b28caaddf41
lastVerified: 2026-08-07
---

## What this is

The repository currently carries five third-party source packs:

- Audio: Kenney Impact Sounds, Interface Sounds, and Sci-Fi Sounds.
- Visuals: Modular SciFi MegaKit Standard and Ultimate Space Kit.

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

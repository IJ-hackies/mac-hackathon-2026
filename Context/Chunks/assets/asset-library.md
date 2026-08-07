---
chunk: asset-library
title: Audio and visual source asset packs
owns:
  - "asset packs/**"
related: [system]
verifiedAtCommit: 8a7f1dd273e1c329ecd10e4219ebdef8bd06b620
lastVerified: 2026-08-07
---

## What this is

The repository currently carries five third-party source packs:

- Audio: Kenney Impact Sounds, Interface Sounds, and Sci-Fi Sounds.
- Visuals: Modular SciFi MegaKit Standard and Ultimate Space Kit.

The library contains OGG audio plus source and interchange 3D formats including
Blend, FBX, glTF, OBJ/MTL, textures, and preview images. These files are source
material, not evidence of an established runtime import pipeline.

## Invariants

- Preserve every pack's license file alongside the source assets.
- Check the applicable license before distributing raw or derived assets.
- Do not edit vendor source files in place when creating game-specific
  variants; place derived/runtime-ready assets in a separate project-owned
  location once the engine and import layout are selected.
- Do not assume every duplicated format should ship with the game. Choose the
  canonical runtime format after selecting the engine.

## Gotchas

The packs contain multiple representations of the same models. File count is
not a unique-asset count. Large binary/source trees should be summarized by
pack and usage rather than documented file by file.

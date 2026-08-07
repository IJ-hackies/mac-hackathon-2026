---
chunk: unity-project
title: Unity project foundation
owns:
  - "MAC-gamejam/.vsconfig"
  - "MAC-gamejam/Packages/**"
  - "MAC-gamejam/ProjectSettings/**"
  - "MAC-gamejam/Assets/InputSystem_Actions.inputactions*"
  - "MAC-gamejam/Assets/Readme.asset*"
  - "MAC-gamejam/Assets/Scenes.meta"
  - "MAC-gamejam/Assets/Scenes/SampleScene.unity*"
  - "MAC-gamejam/Assets/Settings.meta"
  - "MAC-gamejam/Assets/Settings/**"
  - "MAC-gamejam/Assets/TutorialInfo.meta"
  - "MAC-gamejam/Assets/TutorialInfo/**"
related: [system, control-model, git-collaboration]
verifiedAtCommit: fe8fdaab6f46c2c16e89bea445042fdb31134a1d
lastVerified: 2026-08-07
---

## What this is

The full-3D game is initialized under `MAC-gamejam/` with Unity `6000.3.10f1`
and the Universal Render Pipeline empty template. The project uses Universal
Render Pipeline `17.3.0` and Input System `1.18.0`, and includes Unity's 3D
physics module. The template's `Assets/Scenes/SampleScene.unity` is the only
enabled build scene.

Unity asset serialization is Force Text. Commit Unity `.meta` files with their
assets; generated caches and local IDE files are excluded by the repository
root `.gitignore`.

## Remaining bootstrap choices

- Detailed 3D movement, physics, and camera conventions.
- Game-specific Input System maps and the abstraction for cooperative versus
  single-player controls.
- Target platforms, intentional package baseline, assembly layout, testing,
  and builds.
- Whether to retain or remove the template readme, tutorial, and sample scene.

## How to extend

Record verified editor, build, and test commands when established. Move new
gameplay scenes, scripts, prefabs, and runtime assets into focused owning chunks
rather than expanding this chunk to own the entire Unity tree.

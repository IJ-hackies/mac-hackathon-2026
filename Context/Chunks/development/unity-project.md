---
chunk: unity-project
title: Unity project foundation
owns:
  - ".vsconfig"
  - "Packages/**"
  - "ProjectSettings/**"
  - "Assets/InputSystem_Actions.inputactions*"
  - "Assets/Readme.asset*"
  - "Assets/Scenes.meta"
  - "Assets/Scenes/*.unity*"
  - "Assets/Settings.meta"
  - "Assets/Settings/**"
  - "Assets/TutorialInfo.meta"
  - "Assets/TutorialInfo/**"
related: [system, control-model, git-collaboration, runtime-art, player-controller]
verifiedAtCommit: 1a62b900ec593300f3b8cd68ec32e2df106d6e9c
lastVerified: 2026-08-07
---

## What this is

The repository root is the Unity `6000.3.10f1` project root, initialized from
the Universal Render Pipeline empty template. The project uses Universal Render
Pipeline `17.3.0` and Input System `1.18.0`, and includes Unity's 3D physics
module. `Assets/Scenes/SampleScene.unity` is the only enabled build scene and is
the current prototype scene. Other top-level scenes may be used as sandboxes
without implying build inclusion.

The sample scene contains the template camera, directional light, and global
volume plus a collidable `Ground` plane. The ground uses the Ultimate Space Kit
palette through the project-owned solid-color `M_Ground` URP Lit material.

Unity asset serialization is Force Text. Commit Unity `.meta` files with their
assets; generated caches and local IDE files are excluded by the repository
root `.gitignore`.

`Assets/InputSystem_Actions.inputactions` has C# class generation enabled
(`InputSystem_Actions` wrapper) for the `feat/player` prototype; see
[player-controller](../gameplay/player-controller.md).

## Remaining bootstrap choices

- Detailed 3D movement, physics, and camera conventions.
- Game-specific Input System maps and the abstraction for cooperative versus
  single-player controls.
- Target platforms, intentional package baseline, assembly layout, testing,
  and builds.
- Whether to retain or remove the template readme and tutorial content, and
  when to rename or replace the prototype sample scene.

## How to extend

Record verified editor, build, and test commands when established. Move new
gameplay scenes, scripts, prefabs, and runtime assets into focused owning chunks
rather than expanding this chunk to own the entire Unity tree.

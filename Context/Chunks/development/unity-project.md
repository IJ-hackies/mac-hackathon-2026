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
related: [system, control-model, git-collaboration, runtime-art]
verifiedAtCommit: 2c0429d60b09c157356997f9eb19681221a9e274
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
volume plus a static spherical `Planet Ground`. Its root has an exact 50-unit
`SphereCollider`, with the north-pole surface at world height zero. A child
`Planet Visual` uses a one-level Catmull-Clark
derivative of the Ultimate Space Kit `Planet_3` crater mesh, normalized per axis
to the collider bounds and shaded with a low-gloss warm clay/ochre material.
The active shell has 4,802 geometric positions and 9,600 triangles; Unity
reports 5,496 imported vertices after UV seam splits. Keeping collision on the
root decouples future walking physics from the stylized crater geometry. A
complete lap remains about 314 units with a gently curved horizon.

SampleScene uses a project-owned procedural space skybox with dense stars, a
faint galactic band, and a bloom-ready HDR sun disc. The scene's directional
`Sun Light` points from that same fixed world direction, is assigned as
`RenderSettings.sun`, and provides soft realtime shadows across the planet.
Skybox ambient and reflection intensities are deliberately low so unlit regions
read as space without becoming completely black.

Unity asset serialization is Force Text. Commit Unity `.meta` files with their
assets; generated caches and local IDE files are excluded by the repository
root `.gitignore`.

## Remaining bootstrap choices

- Planet-aligned 3D movement, radial gravity, body orientation, and camera
  conventions; no player or controller exists yet.
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

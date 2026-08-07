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
related: [system, control-model, core-loop, git-collaboration, runtime-art, player-controller]
verifiedAtCommit: cbc008d980ff923abaae0dc8790a745a2ca38f0d
lastVerified: 2026-08-08
---

## What this is

The repository root is the Unity `6000.3.10f1` project root, initialized from
the Universal Render Pipeline empty template. The project uses Universal Render
Pipeline `17.3.0` and Input System `1.18.0`, and includes Unity's 3D physics
module. `Assets/Scenes/SampleScene.unity` is the only enabled build scene and is
the current prototype scene. Other top-level scenes may be used as sandboxes
without implying build inclusion.

The sample scene contains a directional light and global volume plus an
instance of `Assets/Art/Prefabs/Planet.prefab`, placed at the established
planet center. Its `Planet Ground` root owns a child `Planet Visual` using a
one-level Catmull-Clark derivative of the Ultimate Space Kit `Planet_3` crater
mesh, shaded with a low-gloss warm clay/ochre material. The same imported mesh
drives a non-convex `MeshCollider`, so the walkable crater floor matches what
is rendered. The prefab root is uniformly scaled to 3, giving the shell an
approximately 150-unit world radius. The 50-unit root `SphereCollider` scales
with it but remains disabled as a reference, not active ground collision. The
active shell has 4,802 geometric positions and 9,600 triangles; Unity reports
5,496 imported vertices after UV seam splits.

`PlayerRig.prefab` is placed 156 units from the planet center near the
north-pole crater and snaps its capsule feet to that mesh at startup. The
template Main Camera remains in the scene but is inactive; the rig camera is
the single runtime camera/audio listener. Its URP camera data explicitly
enables shadow rendering and post-processing.

SampleScene uses a project-owned procedural space skybox with dense stars, a
faint galactic band, and a bloom-ready HDR sun disc. The scene's directional
`Sun Light` points from that same fixed world direction, is assigned as
`RenderSettings.sun`, and provides soft realtime shadows across the planet.
Skybox ambient and reflection intensities are deliberately low so unlit regions
read as space without becoming completely black.

The `Sun Light` GameObject and its `Light` component must both stay enabled.
Disabling only the component leaves the hierarchy looking valid while removing
all direct illumination and realtime shadows in Play mode.

Unity asset serialization is Force Text. Commit Unity `.meta` files with their
assets; generated caches and local IDE files are excluded by the repository
root `.gitignore`.

`Assets/InputSystem_Actions.inputactions` has C# class generation enabled
(`InputSystem_Actions` wrapper) for the current prototype; see
[player-controller](../gameplay/player-controller.md).

## Remaining bootstrap choices

- The cooperative versus single-player input/authority abstraction; the current
  radial controller is deliberately single-player.
- Target platforms, intentional package baseline, assembly layout, testing,
  and builds.
- Whether to retain or remove the template readme and tutorial content, and
  when to rename or replace the prototype sample scene.

## How to extend

Record verified editor, build, and test commands when established. Move new
gameplay scenes, scripts, prefabs, and runtime assets into focused owning chunks
rather than expanding this chunk to own the entire Unity tree.

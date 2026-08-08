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
related: [system, control-model, core-loop, git-collaboration, runtime-art, world-authoring, player-controller]
verifiedAtCommit: 927321aeae479a32412bb0928052db406373cf8a
lastVerified: 2026-08-08
---

## What this is

The repository root is the Unity `6000.3.10f1` project root, initialized from
the Universal Render Pipeline empty template. The project uses Universal Render
Pipeline and Visual Effect Graph `17.3.0`, Input System `1.18.0`, and Unity's
3D physics module. `Assets/Scenes/SampleScene.unity` is the only enabled build
scene and current prototype. Other top-level scenes may be used as sandboxes
without implying build inclusion.

The sample scene contains a directional light and global volume plus an
instance of `Assets/Art/Prefabs/Planet.prefab`, placed at the established
planet center. Its `Planet Ground` root owns a child `Planet Visual` using a
one-level Catmull-Clark derivative of the Ultimate Space Kit `Planet_3` crater
mesh, shaded with a low-gloss warm clay/ochre material whose texture-free 3D
noise adds subtle seam-free lunar mottling and dust detail. The same imported
mesh drives a non-convex `MeshCollider`, so the walkable crater floor matches
what is rendered. The prefab root is uniformly scaled to 3, giving the shell
an approximately 150-unit world radius. The 50-unit root `SphereCollider`
scales with it but remains disabled as a reference, not active ground
collision. The active shell has 4,802 geometric positions and 9,600 triangles;
Unity reports 5,496 imported vertices after UV seam splits.

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

The scene's top-level `LandingBase` authoring hierarchy dresses the landing
crater with layout, structures, and a perimeter. Its runtime art comes from the
curated landing-base imports, while the perimeter combines `Column_Hollow`
poles with a generated curved-wall mesh and an intentional opening. These are
static collidable scene assets; no base interaction, economy, or construction
logic is attached.

Separate top-level `Arena1` and `Arena2` perimeter hierarchies each contain 17
`Column_Hollow` poles plus one curved wall sheet. Arena1 uses dark-orange
material overrides; Arena2 uses near-black blood-red overrides. Their dedicated
materials use solid albedo with the imported normal maps and avoid recoloring
shared base or importer materials.

The scene also contains one top-level `Generated Planet Vegetation` hierarchy
with 1,200 static, non-colliding prefab instances sampled across the full crater
mesh: 193 bushes, 810 grasses, and 197 plants. It mixes all nine selected
variants, uses uniform 65x-75x source-relative scale with a -90-degree local-X
model correction, and distributes only shared dark-orange and red materials.

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

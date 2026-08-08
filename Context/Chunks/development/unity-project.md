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
related: [system, control-model, core-loop, gameplay-areas, git-collaboration, runtime-art, world-authoring, world-runtime, player-controller]
verifiedAtCommit: 0411d4ebb374b9de109cb0c17f0e69577a36cb44
lastVerified: 2026-08-08
---

## What this is

The repository root is the Unity `6000.3.10f1` project root, initialized from
the Universal Render Pipeline empty template. The project uses Universal Render
Pipeline and Visual Effect Graph `17.3.0`, Input System `1.18.0`, and Unity's
3D physics module. `Assets/Scenes/SampleScene.unity` is the only enabled build
scene and current prototype. Other top-level scenes may be used as sandboxes
without implying build inclusion. WebGL using the production WebGL2 graphics
path is the confirmed publication target. WebGL selects the Mobile quality
tier and its forward URP asset; project settings disable WebGL static and
dynamic batching because generated planet props use explicit runtime
instancing.

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
enables shadow rendering, post-processing, and low-cost FXAA.

SampleScene uses a project-owned procedural space skybox with dense stars, a
faint galactic band, and a bloom-ready HDR sun disc. The scene's directional
`Sun Light` points from that same fixed world direction, is assigned as
`RenderSettings.sun`, and provides the sole realtime key light: a restrained
warm tint at intensity 3.6 with realtime shadows. A low-intensity cool Trilight
ambient gradient supplies fill without another Light object; the visible
skybox and its reflection intensity remain dark enough to read as space.

The global volume uses ACES tonemapping, subtle +10 contrast/-4 saturation,
vignette, and restrained bloom. Bloom uses its lower-cost filtering path for
WebGL; motion blur remains disabled. The rig camera applies FXAA after the
Mobile tier's 0.8 render scale.

The `Sun Light` GameObject and its `Light` component must both stay enabled.
Disabling only the component leaves the hierarchy looking valid while removing
all direct illumination and realtime shadows in Play mode.

The scene's top-level `LandingBase` authoring hierarchy dresses the landing
crater with layout, structures, and a perimeter. Its runtime art comes from the
curated landing-base imports, while the perimeter combines `Column_Hollow`
poles with a generated curved-wall mesh and an intentional opening. These are
static collidable scene assets; no base interaction, economy, or construction
logic is attached.

`LandingBase/Generated NAUT Rock Art` contains 59 deterministic, terrain-fitted
`Rock_1` instances arranged as 5x7 lettering around `Layout/BaseCenter`. The
authored default uses local scale `(180, 100, 180)`, 2.88-unit pitch, seed 1401,
and disabled per-instance collision so the decorative word does not snag the
player.

Separate top-level `Arena1` and `Arena2` perimeter hierarchies each contain 17
`Column_Hollow` poles plus one curved wall sheet. Arena1 uses dark-orange
material overrides; Arena2 uses near-black blood-red overrides. Their dedicated
materials use solid albedo with the imported normal maps and avoid recoloring
shared base or importer materials.

`LandingBase`, `Arena1`, and `Arena2` each own a `GameplayArea` derived from
their direct perimeter poles. `PlayerRig.prefab` carries the one shared-body
tracker plus a separate consumer that doubles player movement while inside
LandingBase. Arena effects remain open; no trigger colliders are used.

The scene also contains one top-level `Generated Planet Vegetation` hierarchy
with 16,000 static, non-colliding prefab instances sampled across the full
crater mesh: 1,600 bushes, 12,800 grasses, and 1,600 plants. The exact 8:1:1
grass:bush:plant mix blends 12,000 uniform placements with 4,000 placements in
64 broad, mild-density clusters. It uses all nine variants: grasses at 60x-70x,
bushes at 40x-50x, and plants at 50x-60x source-relative uniform scale. Every
instance has the -90-degree local-X correction and uses a shared dark-orange or
orange material.

`Generated Planet Rocks` contains the authored seed-`80826` pass: exactly
800 small and 300 large rocks, both at literal 100x-200x Transform scale,
grouped into 146 clusters. There are 26 small-only, 89 large-only, and 31 mixed
clusters; small-bearing clusters hold 10-20 small rocks while every
large-bearing cluster holds 1-3 large rocks. Exact crater grounding and closed
pole-ring exclusions keep `LandingBase`, `Arena1`, and `Arena2` clear.

The planet prefab owns `SphericalPropInstancingRenderer`, which captures only
the top-level `Generated Planet Vegetation` and `Generated Planet Rocks`
hierarchies in Play mode. It submits WebGL2-compatible instanced mesh batches
after spherical-sector distance, frustum, and horizon culling. `LandingBase`,
`Arena1`, and `Arena2` remain ordinary renderers, so the prop draw distance does
not prevent arenas from being visible farther away.

Unity asset serialization is Force Text. Commit Unity `.meta` files with their
assets; generated caches and local IDE files are excluded by the repository
root `.gitignore`.

`Assets/InputSystem_Actions.inputactions` has C# class generation enabled
(`InputSystem_Actions` wrapper) for the current prototype; see
[player-controller](../gameplay/player-controller.md).

## Remaining bootstrap choices

- The cooperative versus single-player input/authority abstraction; the current
  radial controller is deliberately single-player.
- The intentional package baseline, assembly layout, and repeatable WebGL
  build, browser test, and deployment workflows.
- Whether to retain or remove the template readme and tutorial content, and
  when to rename or replace the prototype sample scene.

## How to extend

Record verified editor, build, and test commands when established. Move new
gameplay scenes, scripts, prefabs, and runtime assets into focused owning chunks
rather than expanding this chunk to own the entire Unity tree.

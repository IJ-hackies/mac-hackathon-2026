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
verifiedAtCommit: 148a3fe3150d9a1b051c8129dbc8e3051832eff7
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

`PlayerRig.prefab` is placed 156 units from the planet center near the north-pole
crater and snaps its capsule feet to that mesh at startup. The template Main
Camera remains inactive; the rig camera is the sole runtime camera/audio listener
and its URP data enables shadows, post-processing, and low-cost FXAA.

The scene-only `OpeningCutscene` begins with a six-second radial orbit-and-dolly
from the terminator to the wide NAUT reveal, then proceeds through the NAUT zoom,
playerward pan, astronaut Wave, and shoulder-camera arc. Its frame follows the
actual N-to-T art axis rather than the BaseCenter pose.
It suspends gameplay/emote input and the HUD, supports Escape/Space/gamepad-Start
skip, and collision-resolves the final pose. Separate angular, dolly, aim, and
per-beat `AnimationCurve` fields are the intended tuning points.

The PC URP asset uses a 500-unit shadow distance so Scene-view shadows remain useful
while editing the planet. WebGL keeps the Mobile asset's 50-unit gameplay distance;
the opening temporarily raises the active asset to 500 and restores it on every exit.

SampleScene uses a project-owned procedural space skybox with dense warm/cool
twinkling stars, a faint galactic band, three-sample triplanar cosmic fog, and
a bloom-ready layered HDR sun. A self-bootstrapped presentation component adds
one pooled camera-relative shooting-star quad at rare intervals without scene
or particle-system state. The scene's directional `Sun Light` points from that
same fixed world direction, is assigned as `RenderSettings.sun`, and provides
the sole realtime key light: a restrained warm tint at intensity 3.6 and 0.75
shadow strength for the flat-surface/cast-shadow planet experiment. A cool
Trilight ambient gradient at 0.6 intensity supplies fill without another Light
object; the visible skybox
and its reflection intensity remain dark enough to read as space.

The global volume uses ACES tonemapping, subtle +5 contrast/-4 saturation,
vignette, and restrained bloom with lower-cost WebGL filtering. Motion blur is
disabled; the rig camera applies FXAA after the Mobile tier's 0.8 render scale.

The `Sun Light` GameObject and `Light` must both stay enabled. Disabling only the
component leaves the hierarchy looking valid but removes direct light and shadows.

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

- Cooperative versus single-player input/authority; the radial controller is single-player.
- The intentional package baseline, assembly layout, and repeatable WebGL
  build, browser test, and deployment workflows.
- Whether to retain or remove the template readme and tutorial content, and
  when to rename or replace the prototype sample scene.

## How to extend

Record verified editor, build, and test commands when established. Move new
gameplay scenes, scripts, prefabs, and runtime assets into focused owning chunks
rather than expanding this chunk to own the entire Unity tree.

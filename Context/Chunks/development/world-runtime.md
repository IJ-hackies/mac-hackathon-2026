---
chunk: world-runtime
title: WebGL spherical prop rendering
owns:
  - "Assets/Scripts/World.meta"
  - "Assets/Scripts/World/**"
related: [system, unity-project, world-authoring, runtime-art, player-controller]
verifiedAtCommit: db81cd848e59c29f89795a89d512b044041e215a
lastVerified: 2026-08-08
---

## What this is

`Assets/Scripts/World/SphericalPropInstancingRenderer.cs` reduces the rendering
cost of the authored whole-planet vegetation and rocks on the WebGL2 target. It
is attached to the stable `Planet Ground` root in `Planet.prefab` and uses
regular `Graphics.RenderMeshInstanced`; it does not depend on compute shaders,
indirect drawing, GPU Resident Drawer, or experimental WebGPU support.

## Runtime flow

In Play mode, the component resolves the active gameplay camera and the planet
center/radius from the prefab's disabled reference `SphereCollider`. It finds
the exact top-level scene roots `Generated Planet Vegetation` and `Generated
Planet Rocks`, validates their enabled `MeshRenderer`/`MeshFilter`/material
state, caches world matrices, then disables only those source renderers.

Instances are grouped by 15-degree latitude/longitude sector and compatible
draw state: mesh, submesh, material, layer, shadows, rendering layer, light
probes, and reflection probes. Each draw contains at most 511 instances, with
an explicit world bounds. Per frame, a sector must pass all of:

- maximum-distance culling against its bounds;
- camera-frustum culling;
- spherical horizon culling, including the sector's angular extent and an
  8-degree padding margin.

The default maximum prop distance is `0.375` of the planet diameter. On the
current 300-unit-diameter planet this is 112.5 world units, midway between the
requested one-quarter and one-half diameter range. The prefab inspector keeps
the value adjustable from `0.25` to `0.5` without changing code.

## Invariants

- Only the two named generated roots are captured. `LandingBase`, `Arena1`,
  `Arena2`, the planet, player, enemies, and other scene renderers remain on
  their ordinary rendering path and are not limited by the prop draw distance.
- Source GameObjects stay active. Vegetation objects remain non-colliding, and
  rock `MeshCollider`s remain active even while their source renderers are off.
- All captured materials must have GPU instancing enabled. A renderer with a
  property block, baked lightmap, missing mesh/material, or non-instanced
  material is left on its original renderer path.
- If instancing is unsupported or initialization cannot proceed, source
  renderers remain enabled. Disabling the component or catching a runtime draw
  failure restores every captured renderer to its previous enabled state.
- Batches are built once; camera frustum planes are reused and no matrix/list
  arrays are rebuilt each frame.

## WebGL boundary

This first pass reduces visible prop submissions and draw calls. It does not
remove the 17,100 authored GameObjects/Transforms, the serialized scene data,
or the rock colliders, so WebGL build size, browser memory, scripting overhead,
and physics cost must be profiled separately. If those become limiting, the
next architectural step is baking compact instance data and simplified rock
collision rather than increasing the scope of this renderer.

The verified Editor smoke test captured 17,100 renderers into 288 sectors and
4,082 cached draw batches. A production decision still requires profiling an
actual WebGL build on representative browsers and hardware.

---
chunk: world-runtime
title: WebGL spherical prop rendering
owns:
  - "Assets/Scripts/World.meta"
  - "Assets/Scripts/World/**"
  - "Assets/Tests/EditMode/WorldRuntime.meta"
  - "Assets/Tests/EditMode/WorldRuntime/**"
related: [system, unity-project, world-authoring, runtime-art, player-controller]
verifiedAtCommit: bbe3799f82348f2367d9a308b9fd87ed7f9601ee
lastVerified: 2026-08-09
---

## What this is

`SphericalPropInstanceData` stores renderer-local TRS records and shared mesh,
material, layer, shadow, light-probe, and reflection-probe prototype state in
binary ScriptableObjects. `SphericalPropInstancingRenderer` consumes those
records on the WebGL2 target. It is attached to the stable `Planet Ground` root
in `Planet.prefab` and uses regular `Graphics.RenderMeshInstanced`; it does not
depend on compute shaders, indirect drawing, GPU Resident Drawer, or experimental
WebGPU support.

## Runtime flow

In Play mode, the component resolves the active gameplay camera and the planet
center/radius from the prefab's disabled reference `SphereCollider`. Valid baked
datasets replace only the matching named authoring category. Their local TRS is
composed with the renderer's current transform, so the planet instance can move
without rebaking. Missing or invalid categories fall back independently to the
legacy `Generated Planet Vegetation` or `Generated Planet Rocks` hierarchy.

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

The opening cutscene acquires a nested disposable full-planet visibility
request from this renderer. While held, only maximum-distance culling is
bypassed; frustum and spherical-horizon culling still reject off-screen and
hidden-backside sectors. Completion, skip, disable, exceptions, and teardown
dispose the request and restore the normal gameplay distance.

## Invariants

- Only datasets or legacy roots representing the two named generated categories
  are rendered. `LandingBase`, `Arena1`, `Arena2`, the planet, player, enemies,
  and other scene renderers remain on their ordinary rendering path and are not
  limited by the prop draw distance.
- SampleScene has no vegetation source hierarchy. Its rock hierarchy is
  collision-only: exactly 1,100 enabled static `MeshCollider`s and no
  `MeshRenderer`/`MeshFilter` components.
- A valid baked category supersedes a same-named legacy root. An absent or
  invalid category leaves that root on the capture/fallback path, allowing one
  scatter category to be regenerated without invalidating the other.
- All captured materials must have GPU instancing enabled. A renderer with a
  property block, baked lightmap, missing mesh/material, or non-instanced
  material is left on its original renderer path.
- If instancing is unsupported or initialization cannot proceed, source
  renderers remain enabled. Disabling the component or catching a runtime draw
  failure restores every captured renderer to its previous enabled state.
- Batches are built once; camera frustum planes are reused and no matrix/list
  arrays are rebuilt each frame.
- Cinematic visibility requests must be disposed. Use the renderer's request
  API instead of changing the serialized gameplay distance or disabling
  horizon/frustum culling.

## WebGL boundary

The compact bake removes all 16,000 vegetation objects and render-only rock
components from the scene. SampleScene shrank from 60.34 MiB to 4.54 MiB; its
two binary datasets total about 0.73 MiB. Rock collision remains object-based,
so browser physics and memory still require profiling; simplified or pooled
collision is the next step only if measurements justify it.

The verified Editor smoke test still captured all 17,100 records into 288
sectors and 4,082 cached draw batches at a 112.5-unit maximum distance. EditMode
tests verify the binary round trip, dataset/prototype counts, scene assignment,
absence of vegetation authoring objects, and retained rock collision contract.
An actual WebGL player still needs representative browser profiling.

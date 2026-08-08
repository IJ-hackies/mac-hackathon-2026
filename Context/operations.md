---
stack:
  - Unity 6000.3.10f1
  - Universal Render Pipeline 17.3.0
  - Visual Effect Graph 17.3.0
  - Input System 1.18.0
projectRoot: ./
---

# Operations

The repository root is also the Unity project root; open
`mac-hackathon-2026/` directly in Unity Hub. It uses the Universal Render
Pipeline with separate template PC and mobile renderer assets, Force Text asset
serialization, and the new Input System. `Assets/Scenes/SampleScene.unity` is
currently the only enabled build scene.

No repeatable project-level build or test command has been established, and
target platforms remain undecided. A one-off Unity batch-mode check using the
local package cache has successfully recognized the repository root, loaded
both prototype scenes, and confirmed the build-scene configuration; its
temporary verifier is not a shared project workflow. Development is shared by
two people and will use separate Git branches and worktrees for parallel tasks.

For an import plus runtime/editor script compile check, close the interactive
editor and run Unity `6000.3.10f1` with `-batchmode -quit`,
`-projectPath <root>`, and `-logFile <path>`. This was verified after the
player/enemy merge: the asset
refresh completed, both `Assembly-CSharp` assemblies compiled, and Unity exited
with code 0. It is an editor validation pass, not a player build or Play-mode
test.

`dotnet build Assembly-CSharp.csproj --no-restore` is a verified fast compile
check for runtime scripts when Unity has generated the project file; it is not
a player build or substitute for Play mode. Editor-only tooling can be checked
with `dotnet build Assembly-CSharp-Editor.csproj --no-restore` after Unity has
regenerated that project file. In Unity,
`Tools > Player Prototype > Repair Player Rig Prefab` is the safe idempotent
command for relinking and validating `PlayerRig.prefab`. Do not use the
destructive `Build Test Scene` command for that repair.

Planet dressing tools live under `Tools > Planet Design`. `Radial Surface
Snap` opens the configurable selection-snap window; `Snap Selection To Planet`
uses surface-normal alignment, preserved heading, and zero pivot offset.
`Configure Landing Base Assets` is the explicit idempotent import/material
repair command for the curated landing-base FBXs; it also restores their static
mesh-collider import setting.

`Regenerate Planet Vegetation` (shortcut `Ctrl+Shift+V`) replaces the existing
`Generated Planet Vegetation` scene root with a randomized 1,100-1,300-instance
scatter of all nine configured bush, grass, and plant FBXs. Placement samples
the active crater `MeshCollider` through the shared radial-snap cast, aligns to
its normals, and iteratively fits transformed mesh vertices to the real terrain
before applying a slight `0.075`-unit inward embed. It applies a fixed
-90-degree local-X model correction, uses uniform 65x-75x scale, weights bushes
and plants at 2x and grasses at 10x, assigns only shared dark-orange or red
materials, disables vegetation collision, and saves the active scene. The
authored SampleScene pass contains exactly 1,200 instances from seed `80` (193
bushes, 810 grasses, and 197 plants).
Unity also binds `Ctrl+Shift+V` to Paste as Child; if its shortcut-conflict
dialog appears, select `Regenerate Planet Vegetation`, or invoke it from the
`Tools > Planet Design` menu.

For a closed landing-base wall, select at least three top-level wall instances
and open `Tools > Planet Design > Wall Ring Builder`. Use `Fit Radius
End-To-End`, then arrange the selection. The active selected wall anchors the
starting angle. To bridge the gaps automatically, assign a project prefab to
Connector Prefab, set its local length axis and optional End Inset, then use
`Generate Closed-Loop Connectors`. Both operations support Unity Undo; each
connector pass creates a separate `Generated Wall Connectors` scene root.

To turn the pole skeleton into a continuous perimeter, keep the poles selected
and use `Generate Curved Sheets Between Poles` in the same window. Set Height,
Thickness, Pole Clearance, Curve Segments, and optional Wall Material first;
an empty material field reuses the active pole's first material. The command
surface-samples every span, generates a closed UV-mapped mesh, and optionally
adds a static MeshCollider. Disable Close Loop to omit the span between the
two explicitly assigned Opening Pole references. They must both be in the full
ring selection and geometrically adjacent. Alternatively, select only those
two poles, capture them with `Use Two Selected Poles As Opening`, then reselect
the full ring before generating. Undo removes the scene hierarchy, while the
unique mesh asset remains in
`Assets/Art/Generated/LandingBaseWalls` until explicitly deleted from the
Project window.

To move the planetary player spawn, exit Play mode, move the top-level
`PlayerRig` scene instance roughly above the intended location, then use the
Radial Surface Snap window with surface-normal alignment off, preserved heading,
and zero offset. Save the scene instance override; do not apply that root
transform back to `PlayerRig.prefab`.

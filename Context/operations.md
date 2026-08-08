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
serialization, and the new Input System. `Assets/Scenes/MainMenu.unity` and
`SampleScene.unity` are enabled build scenes 0 and 1.

WebGL with WebGL2 is the confirmed publication target, but no repeatable
project-level WebGL build, browser test, or deployment command has been
established. WebGL maps to quality tier 0 (`Mobile`) and its forward URP asset.
WebGL-specific static and dynamic batching are disabled because the generated
planet props use explicit runtime instancing. A one-off Unity batch-mode check
using the local package cache has successfully recognized the repository root,
loaded both prototype scenes, and confirmed the build-scene configuration; its
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
command for relinking and validating `PlayerRig.prefab`, including its camera
shadows, post-processing, and FXAA. Do not use the destructive `Build Test
Scene` command for that repair.

`Tools > Player Prototype > Configure Settings Menu` (`Ctrl+Shift+U`)
idempotently imports/configures the selected Cartoon UI and Space Expansion UI
sprites and rebuilds the rig-owned Escape settings console. In Play mode,
`Preview Settings Menu` (`Ctrl+Shift+O`) opens it for visual inspection. The
current dark-header/high-contrast pass compiled through
`Assembly-CSharp-Editor.csproj` with zero warnings/errors and was checked in
the live Game view.

`Tools > Main Menu > Rebuild Main Menu Scene` imports its Cartoon UI icons,
regenerates `MainMenu.unity`, and restores MainMenu/SampleScene as enabled build
scenes 0/1. It safely replaces an already-open generated MainMenu scene and
builds a deterministic 61-prop menu-only planet vignette against the exact
crater collider. `Validate Main Menu Scene` checks page references, disabled
Multiplayer, Singleplayer's SampleScene target, settings controls, build order,
the dressing hierarchy, and disabled presentation colliders. The refined
alignment/dressing pass was checked in the live 2560x1440 Game view; both
generated assemblies compile with zero warnings/errors.

`Tools > Player Prototype > Refresh Health HUD` (`Ctrl+Shift+H`) replaces only
the rig's `HealthHud` child with the minimal red Space Expansion bar and
preserves the other prefab UI. Runtime/editor builds pass with zero warnings/
errors; a live SampleScene handoff verified the centered raw HP value, absence
of labels/panel, imported sprites, and top-right placement.

Player movement EditMode tests live at `Assets/Tests/EditMode/Player`.
`dotnet build Player.Movement.Tests.csproj --no-restore` is a verified compile
check for that generated test assembly; it does not execute the Unity tests.

The opening-cutscene change passed both generated-project builds with zero
warnings/errors. The active Unity editor also compiled all assemblies and
deserialized `SampleScene`; visual pacing and skip/handoff still require a
Play-mode smoke test because a second batch editor cannot open the live project.

Gameplay-area runtime and editor sources also compile with zero warnings via
`dotnet build Gameplay.Areas.csproj --no-restore` followed by the editor build
above, after Unity has refreshed the generated projects. EditMode tests live at
`Assets/Tests/EditMode/GameplayAreas`. A Unity batch run with `-runTests
-testPlatform EditMode -testFilter Gameplay.Areas.Tests` passes all 11 tests.
If a backgrounded interactive editor omits newly created area scripts from the
`Gameplay.Areas` compiler response and reports downstream `CS0246` errors,
close it cleanly and run a fresh Unity import; the assembly references do not
need to be rewritten.

`Tools > Gameplay > Configure Area Membership` (`Ctrl+Shift+G`) idempotently
wires `LandingBase`, `Arena1`, and `Arena2` to their direct perimeter poles and
configures the shared astronaut tracker and its 2x LandingBase speed consumer.
`Validate Area Membership` checks that full contract without changing it.

A 2026-08-08 interactive Play-mode smoke test verified the planet prop runtime:
it captured 17,100 generated vegetation/rock renderers into 288 spherical
sectors and 4,082 instanced draw batches, with a 112.5-unit maximum prop
distance. Both generated C# projects compiled with zero warnings and errors.
This verifies Editor runtime initialization, not an exported WebGL player;
browser memory, frame timing, and final build size still require a WebGL build.

A 2026-08-08 batch import attempt at current `HEAD` was blocked during initial
package resolution with `The "path" argument must be of type string. Received
undefined`; `-noUpm` is not a substitute because Input System and UI assemblies
then cannot load. The rock setup script was instead checked against Unity's
editor/core/physics assemblies, and its asset metadata was validated directly.

Planet dressing tools live under `Tools > Planet Design`. `Radial Surface
Snap` opens the configurable selection-snap window; `Snap Selection To Planet`
uses surface-normal alignment, preserved heading, and zero pivot offset.
`Configure Landing Base Assets` is the explicit idempotent import/material
repair command for the curated FBXs. It restores static mesh colliders, creates
one linear mipmapped Trim01 mask capped at 1024, and assigns teal LandingBase,
amber Arena1, and red Arena2 accents. Trim02 stays dark; no Light objects are added.

`Prepare Planet Rock Assets` imports/configures all seven Ultimate Space Kit
rock FBXs under `Assets/Art/Models/Environment/PlanetRocks`, remaps their
`Atlas` slot to the shared-palette `M_PlanetRock` material, disables animation
and mesh readability, and enables static non-convex mesh colliders.

`Landing Base NAUT Rock Art` opens the configurable lettering window. Its
default command (`Ctrl+Alt+Shift+N`) replaces only
`LandingBase/Generated NAUT Rock Art` with 59 `Rock_1` instances at literal
local scale `(180, 100, 180)` and 2.88-unit pitch around `Layout/BaseCenter`.
The seed-`1401` pass maps the grid geodesically, fits every mesh to the crater
collider, and saves the scene. Collision defaults off for walkable-base
aesthetics; adjust Y scale, the X/Z multiplier, pitch, heading, seed, or
collision in the window before regenerating.

`Regenerate Planet Rocks` (shortcut `Ctrl+Shift+R`) replaces `Generated Planet
Rocks` with exactly 800 small and 300 large rocks, both at literal 100x-200x
Transform scale. The recipe creates 146 clusters: 26 small-only, 89 large-only,
and 31 mixed. Small-bearing clusters contain 10-20 small rocks; large-bearing
clusters contain 1-3 large rocks. It uses exact crater grounding and closed
pole-ring exclusions with the largest 200x rock radius plus 2 units of clearance.
Regeneration fails on malformed rings or incomplete quotas, is Undoable, and
saves the scene. The authored SampleScene seed is `80826`.

`Regenerate Planet Vegetation` (shortcut `Ctrl+Shift+V`) replaces the existing
`Generated Planet Vegetation` scene root with an exactly 16,000-instance
scatter of all nine configured bush, grass, and plant FBXs. Placement samples
the active crater `MeshCollider` through the shared radial-snap cast, aligns to
its normals, and iteratively fits transformed mesh vertices to the real terrain
before applying a slight `0.075`-unit inward embed. It applies a fixed
-90-degree local-X model correction, uses uniform 60x-70x grass, 40x-50x bush,
and 50x-60x plant scale, and shuffles an exact 8:1:1 allocation. Of the 16,000
placements, 12,000
remain uniform and 4,000 use 64 best-candidate-spaced clusters with randomized
10-14-degree radii for mild density variation. It assigns only shared dark
orange or orange materials, disables vegetation collision, and saves the active
scene. The authored seed-`80` pass has 12,800 grasses, 1,600 bushes, and 1,600
plants. The former red material is GUID-preservingly migrated to orange.
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

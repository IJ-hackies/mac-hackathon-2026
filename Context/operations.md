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
sprites and rebuilds the rig-owned Escape settings console, including all 13
configurable keyboard/mouse rows and the intermission-only Teleport to Base
action. `Configure PC-Only Input` removes non-PC
bindings from every action map, retains only the Keyboard&Mouse scheme, and
regenerates `InputSystem_Actions.cs`; `PcUiInputBinding` also replaces Unity's
cross-platform default menu actions at runtime.
In Play mode,
`Preview Settings Menu` (`Ctrl+Shift+O`) opens it for visual inspection. The
current dark-header/high-contrast pass compiled through
`Assembly-CSharp-Editor.csproj` with zero warnings/errors and was checked in
the live Game view.

`Tools > Main Menu > Rebuild Main Menu Scene` imports its Cartoon UI icons,
regenerates `MainMenu.unity`, and restores MainMenu/SampleScene as enabled build
scenes 0/1. It safely replaces an already-open generated MainMenu scene and
builds a deterministic 61-prop menu-only planet vignette against the exact
crater collider. `Validate Main Menu Scene` checks page references,
Singleplayer's SampleScene target, settings controls, build order, the dressing
hierarchy, and disabled presentation colliders. The home page contains only
Singleplayer and Settings, with its title and console directly over the space
background. The earlier alignment/dressing pass was checked in the live
2560x1440 Game view; the current no-overlay, single-player-only rebuild passes
both generated assembly compile checks with zero warnings/errors.

`Tools > Player Prototype > Refresh Health HUD` (`Ctrl+Shift+H`) replaces only
the rig's `HealthHud` child with the minimal red Space Expansion bar and
preserves the other prefab UI. Runtime/editor builds pass with zero warnings/
errors; a live SampleScene handoff verified the centered raw HP value, absence
of labels/panel, imported sprites, and top-right placement.

`Tools > Player Prototype > Refresh Ammo HUD` (`Ctrl+Shift+Alt+A`) replaces
only the rig's `AmmoHud` child with a matching blue Space Expansion bar directly
below health. Its centered value and fill show current magazine rounds rather
than reserve storage; the text shows `magazine / reserve`. Runtime/editor builds pass with zero warnings/errors; a
live SampleScene smoke test verified the initial `12`, then `10` with a
proportionally shorter fill after sustained fire consumed two shot beats.

`Tools > Progression > Configure All` (`Ctrl+Shift+Alt+P`) copies only the
selected Cartoon UI/Space Expansion UI sprites into the project-owned
Progression texture folder, rebuilds only `HUD Canvas/Progression UI` in
`PlayerRig.prefab`, repairs the three SampleScene station markers, saves, and
runs strict serialized-reference, radial ground-placement, footprint-radius,
roof-beacon, label, and world-scale validation. `Validate Sample Scene` is
read-only. In Play mode the Preview submenu opens Supply, Skill Archive, or
Special System directly, can hold/release the Run Overview, and provides QA
approach commands for each station.

`Tools > Progression > Run Progression Contract Tests`
(`Ctrl+Shift+Alt+T`) executes the `Progression.Contracts.Tests` EditMode
assembly in the active editor and logs failures plus a final count. The latest
run passed all 17 tests, including the 13-skill catalog, upgrade/special stat
composition, reset behavior, supply healing/ammo capacity, world-distance
proximity, prompt visibility,
in-range menu opening, and the opening-frame close-input guard. `dotnet build Progression.Contracts.Tests.csproj
--no-restore` is the corresponding compile-only check. Runtime, editor, and
test assemblies all build with zero warnings/errors. The latest Configure All
run regenerated three Supply cards and the 13-card scrolling Special System,
then passed its station/UI validator. The live Special System preview verified
that its `RectMask2D` viewport renders both card columns; do not restore the
former sprite-less `Image + Mask`, which hid every card while leaving the shell
visible. Live SampleScene QA previously verified the gold HUD/coin,
FULL supply state, aligned
three console layouts, a 500g Hold-to-Fire purchase becoming OWNED, a 100g HP
upgrade updating gold/level/live max HP, and the non-pausing Tab overview. A
Unity Input System `E` pulse at the in-range Supply Console also verified that
the menu opens and remains visible after the input frame.

`Tools > Waves > Configure Complete Wave Loop` idempotently rebuilds only the
PlayerRig `HUD Canvas/Wave UI` subtree, attaches the progression adapter, wires
the SampleScene director/controller, three runtime barriers, areas, planet,
player, and enemy prefabs, then saves and runs strict validation. `Validate
Complete Wave Loop` and `Validate Player Rig Wave UI` are read-only checks.
The wave UI uses the imported Kenney fonts through Unity UI, not TMP resources.
The current top-center arena objective and Barbara HP-bar rebuild passes
`Validate Player Rig Wave UI`, including its safe-area and serialized-reference
checks. The standalone rebuild also reconnects all six `WaveGameController` UI
references before saving the prefab. Arena navigation now has compiled EditMode
coverage for cardinal camera-relative bearings, continuity across the rear-camera
seam, and retained great-circle direction through the antipodal hysteresis band.

Wave contracts compile with `dotnet build Waves.Tests.csproj --no-restore`.
The current SampleScene wave configurator validation also confirms non-null
Health, Ammo, and Thunder pickup prefabs. Compile-only coverage now includes
the 15/10 pickup allocation and scene references; the player tests cover actual
fourth-round double damage plus explosive direct-target exclusion and Vampire
healing from actual splash damage.
The balance boundary coverage includes 30/25/20-second duration tiers, 2x/3x
kill-gold milestones, the 2x movement cap, enemy prefab base stats, Barbara
Stage 1's 300 base/705 wave-10 HP under Barbara's dedicated +15%-per-wave
health curve, and the approximately 402-damage wave-10 small burst. Spawn-safety coverage
also checks full 3x physical footprints and that arena surface selection ignores
closer props in favor of the configured planet hierarchy even when boundary poles
are far above the ground. The latest runtime and wave-test assembly compile checks
pass with zero warnings/errors. A live SampleScene physics probe confirmed that
Arena1 now finds a collision-safe Small-enemy position on its crater floor; the
controller-grounding follow-up spawned a real 3x Large enemy there and measured
0.581 tangent units of travel over 3.07 seconds while retaining about 0.35 units
of below-root controller clearance. The complete ten-enemy Round 5 flow still
needs a Play-mode smoke test.
The base-recall test matrix covers all six wave phases and permits only
`Intermission`; its runtime, editor, and test assemblies compile with zero
warnings/errors, while an in-editor execution of that new test remains pending.
The latest completed in-editor EditMode run passed the earlier 22/22 Wave and
Progression tests; the four StartWave/input persistence tests passed 4/4. The
current test assembly also contains Arena1 defeated/left and Arena2 HP-bar
presentation coverage plus arena-navigation direction regressions. The new
radial boss-camera, active-crater/navigation, hierarchy-aware spawn-safety, and
post-navigation tangent-displacement facing/animation, scaled controller-root
ground clearance, fixed-look-ahead obstacle classification, 3x combat-band
scaling, plus all-five-AI death-update/fall-timeout test sources compile with zero
warnings/errors. The latest navigation follow-up also passes runtime, editor, and
Waves generated-project builds. A fresh integrated Arena1 probe ran a real 3x
Large enemy for eight seconds: it traveled 12.856 tangent units, reduced player
distance by 9.102 units, averaged 0.852 alignment toward the player, and recorded
2,347 toward-moving frames versus zero away-moving frames. These tests have not run in
Unity Test Runner because the interactive editor still owns the project,
blocking a batch runner, and Waves has no in-editor test command. A live SampleScene probe started
wave 1 from outside the base, confirmed Regular phase with about 25 seconds
remaining, three spawned enemies, all three area locks, timed cleanup back to
intermission, and the 100g HUD. The only red Console item during that check was
the pre-existing Package Manager `path ... undefined` error documented below.

Player movement EditMode tests live at `Assets/Tests/EditMode/Player`.
`dotnet build Player.Movement.Tests.csproj --no-restore` is a verified compile
check for that generated test assembly; it now includes binding scope,
live-copy propagation, persistence/reset, and reserved-Escape coverage. It does
not execute the Unity tests. The same assembly now includes primary-fire cadence
coverage for repeated-click rate limiting, the pistol fire-rate multiplier, and
the Ultimate's fixed primary-fire interval.

The opening cutscene passed both generated-project builds and was exercised in
the live wave smoke: its completion/skip restored gameplay camera, input, and HUD.

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

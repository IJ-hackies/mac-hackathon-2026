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
serialization, and the new Input System. `Assets/Scenes/MainMenu.unity`,
`SampleScene.unity`, and `Tutorial.unity` are enabled build scenes 0, 1, and 2.

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

The serialized MainMenu scene, PlayerRig settings console, and Progression UI/
station layouts are authoritative. Their former broad rebuild/configure tools
were removed because they could overwrite hand-authored work. MainMenu stays at
build index 0, with SampleScene at 1 and Tutorial at 2. Use targeted
tools for repairs and previews. `Configure PC-Only Input` retains only the
Keyboard&Mouse scheme and regenerates `InputSystem_Actions.cs`;
`PcUiInputBinding` replaces Unity's cross-platform default menu actions.

`Tools > Player Prototype > Refresh Health HUD` (`Ctrl+Shift+H`) replaces only
the rig's `HealthHud` child with the minimal red Space Expansion bar and
preserves the other prefab UI. Runtime/editor builds pass with zero warnings/
errors; a live SampleScene handoff verified the centered raw HP value, absence
of labels/panel, imported sprites, and bottom-center placement.

`Tools > Player Prototype > Refresh Ammo HUD` (`Ctrl+Shift+Alt+A`) replaces
only the rig's `AmmoHud` child with a matching blue Space Expansion bar at
bottom-right. Its centered value/fill show magazine rounds and the text shows
`magazine / reserve`. Runtime/editor builds pass with zero warnings/errors; a
live SampleScene smoke test verified the initial `12`, then `10` with a
proportionally shorter fill after sustained fire consumed two shot beats.

The targeted `Add Return To Main Menu Button` tool updates the settings console,
while the four `Refresh ... HUD` commands update Health, Ammo, Ability, or
Ultimate without rebuilding unrelated prefab UI. During this merge all four
refreshes and the return-button addition completed successfully in Unity.

`dotnet build Progression.Contracts.Tests.csproj --no-restore` is the current
compile-only progression check. Its balance contracts cover the 460-HP and
87-pistol-damage Archive endpoints plus the 400g/600g/800g pickup-skill prices.
All three station catalogs use `RectMask2D`
viewports and `SmoothStationScrollRect`; its wheel impulses decay through
ScrollRect's unscaled-time inertia while station menus pause gameplay. Previous
live QA verified supply, purchases, upgrade effects, Tab overview, and in-range
`E` opening. Run the EditMode assembly in Unity for execution coverage.

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
seam, retained great-circle direction through the antipodal hysteresis band,
and preference for each arena's authored entrance over its perimeter center.

Wave contracts compile with `dotnet build Waves.Tests.csproj --no-restore`.
The current SampleScene wave configurator validation also confirms non-null
Health, Ammo, and Thunder pickup prefabs. Compile-only coverage now includes
the 15/10 pickup allocation and scene references; the player tests cover actual
fourth-round double damage plus explosive direct-target exclusion and Vampire
healing from actual splash damage.
The balance boundary coverage includes 30/25/20-second duration tiers, the
natural-log kill-gold curve and 4x cap, 200g/400g arena bases, hybrid
regular-enemy HP/damage curves, the unchanged 2x movement cap, 40-unit
regular-enemy aggro, enemy prefab base stats, Barbara Stage 1's 300 base/705
wave-10 HP under her dedicated +15%-per-wave health curve, and the approximately
475.15-damage wave-10 small burst. Spawn-safety coverage
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
Its planet-wide opening now temporarily bypasses only the instanced-prop distance
cull while keeping frustum and horizon culling; runtime and WorldRuntime test
projects compile with zero warnings/errors. An updated live opening-shot visual
check remains pending because the interactive editor owns the project.

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

A 2026-08-09 interactive Play-mode smoke test verified the compact planet-prop
runtime: it captured all 17,100 baked vegetation/rock records into 288 spherical
sectors and 4,082 instanced draw batches, with a 112.5-unit maximum distance.
SampleScene fell from 60.34 MiB to 4.54 MiB and its two binary datasets total
about 0.73 MiB. Runtime, editor, and WorldRuntime test projects compile with zero
warnings/errors. Unity EditMode execution passes 2/2 tests. This is not an
exported WebGL player; browser memory, physics, frame timing, and build size
still require a representative WebGL build.

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

After the final vegetation/rock regeneration, save SampleScene and run
`Tools > Planet Design > Bake Planet Props for Runtime`. The scene must be clean.
The command writes binary `SampleScene_Vegetation.asset` and
`SampleScene_Rocks.asset`, assigns them to the scene's planet instance, removes
the vegetation hierarchy, and keeps a collider-only 1,100-object rock hierarchy.
Run `Validate Baked Planet Props` to check the compact scene contract. Either
scatter command clears only its category's baked assignment, so rebake after a
reroll. Execute the `WorldRuntime.Tests` EditMode assembly for dataset and scene
contract coverage.

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

`Tools > Tutorial > Import Modular Kit Assets` copies the entire Modular SciFi MegaKit pack
(~190 FBXs across Walls/Platforms/Columns/Props/Decals/Aliens, plus its full texture set) into
`Assets/Art/Models/Environment/ModularSciFi/<Category>/` and builds their shared trim materials,
so every piece is available to drag into the scene and already matches. There is no automated
scene builder anymore - two scripted passes (a white one-tile/one-band tube, then a scripted
three-tile/three-band modular assembly) were both discarded as too rough without live visual
feedback, and the room is now hand-built in the editor. `Tools > Tutorial > Strip Scene For
Manual Build` was a one-shot cleanup that removed the discarded auto-builder's generated hierarchy
from `Assets/Scenes/Tutorial.unity`, keeping only the `Sun Light` and a `PlayerRig` reference
instance; delete that script once no longer needed. The tutorial's gameplay scripts
(`TutorialManager` and friends, `Assets/Scripts/Tutorial/`) are unaffected by any of this and get
wired to the hand-built room's gates/zones/pickups via the Inspector.

To move the planetary player spawn, exit Play mode, move the top-level
`PlayerRig` scene instance roughly above the intended location, then use the
Radial Surface Snap window with surface-normal alignment off, preserved heading,
and zero offset. Save the scene instance override; do not apply that root
transform back to `PlayerRig.prefab`.

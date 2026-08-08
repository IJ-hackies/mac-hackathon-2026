---
chunk: main-menu
title: Startup menu, settings, and scene navigation
owns:
  - "Assets/Scenes/MainMenu.unity*"
  - "Assets/Scripts/UI/GameSettings.cs*"
  - "Assets/Scripts/UI/MainMenuController.cs*"
  - "Assets/Scripts/UI/ControlsRebindingUI.cs*"
  - "Assets/Scripts/UI/PcUiInputBinding.cs*"
  - "Assets/Editor/MainMenu.meta"
  - "Assets/Editor/MainMenu/**"
  - "ProjectSettings/EditorBuildSettings.asset"
related: [system, unity-project, player-controller, runtime-art, control-model]
verifiedAtCommit: e4caa898457d6a2d25ff205625898ecf4fbe2635
lastVerified: 2026-08-09
---

## What this is

`MainMenu.unity` is the lightweight startup scene and enabled build scene 0;
`SampleScene.unity` is enabled build scene 1. Its mission-control presentation
reuses the procedural space sky and cratered planet while selected Cartoon UI
and Space Expansion UI sprites form the interactive console. The planet carries
a menu-only, deterministic crash-site vignette: three outpost structures,
16 rocks, and 42 vegetation props surface-fit to its exact crater mesh. This is
a curated presentation pass, not a copy of SampleScene's 17,100-object scatter.

The home page exposes Singleplayer, disabled Multiplayer, and Settings.
Singleplayer saves settings and replaces the menu with `SampleScene` in Single
mode, preserving that scene's opening cinematic. Multiplayer is deliberately
non-interactable because it is out of hackathon scope. Settings contains
master volume, look sensitivity, and the same live, persisted 12-binding PC
control map exposed by the in-game pause console.

## Key files

- `MainMenuController.cs` - navigation, scene loading, settings, selection,
  cursor/time reset, Escape behavior, and slow menu-planet rotation.
- `GameSettings.cs` - shared PlayerPrefs keys, clamping, application, and saves
  used by both startup and gameplay settings menus.
- `ControlsRebindingUI.cs`/`PlayerInputBindings.cs` - shared two-column binding
  UI and live/persisted Input System override registry; the latter is owned by
  [player-controller](../gameplay/player-controller.md).
- `PcUiInputBinding.cs` - replaces Unity's cross-platform default UI actions
  with the project's keyboard/mouse-only UI action map.
- `MainMenuSceneSetup.cs` - revisioned/idempotent scene generation, runtime icon
  copies, sprite imports, deterministic planet dressing, build order, and
  contract validation.
- `MainMenuPreviewCapture.cs` - optional isolated 1920x1080 preview render.

## Invariants

- MainMenu and SampleScene stay enabled at build indexes 0 and 1 respectively.
- Singleplayer loads `SampleScene` directly with `LoadSceneMode.Single`.
- Multiplayer has no listener and remains non-interactable until its product
  and authority model are confirmed.
- The menu and gameplay console share `settings.masterVolume` and
  `settings.mouseSensitivity` through `GameSettings`, plus one versioned
  binding-override JSON through `PlayerInputBindings`; do not fork their keys.
- Controls exposes 12 keyboard/mouse bindings. Movement, pointer look, Escape
  settings, and Escape/Space cinematic skip are fixed and never editable.
- Menu entry restores time scale 1 and an unlocked visible cursor. Settings
  never instantiates or depends on a player rig.
- The menu planet has collision and spherical prop instancing disabled; it is
  presentation only and must not search for gameplay-generated prop roots.
- Menu dressing stays parented to the rotating planet, uses the same -90-degree
  local-X environment-model correction as world authoring, and remains a small
  deterministic subset concentrated on the camera-facing hemisphere.
- The left title, console, full-width instrument header, action rows, and footer
  share one alignment grid. Background separation uses low-alpha stepped shade
  bands; do not restore the overlapping yellow rail or a single hard shade seam.

## Gotchas

`Rebuild Main Menu Scene` replaces the generated scene. Make layout changes in
the setup tool or preserve them there before rebuilding. Runtime copies belong
under `Assets/Art/`; never reference `asset packs/` from the scene. Do not call
the gameplay vegetation/rock generators for this scene: they require gameplay
roots and would destroy the menu's lightweight budget.

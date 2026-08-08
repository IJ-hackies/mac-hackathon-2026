---
chunk: main-menu
title: Startup menu, settings, and scene navigation
owns:
  - "Assets/Scenes/MainMenu.unity*"
  - "Assets/Scripts/UI/GameSettings.cs*"
  - "Assets/Scripts/UI/MainMenuController.cs*"
  - "Assets/Editor/MainMenu.meta"
  - "Assets/Editor/MainMenu/**"
  - "ProjectSettings/EditorBuildSettings.asset"
related: [system, unity-project, player-controller, runtime-art, control-model]
verifiedAtCommit: 262413a1cda18eaed7a50511bb0aa8f10bcb533a
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
non-interactable until the cooperative topology is decided. Settings contains
master volume, look sensitivity, and the current single-player control map.

## Key files

- `MainMenuController.cs` - navigation, scene loading, settings, selection,
  cursor/time reset, Escape behavior, and slow menu-planet rotation.
- `GameSettings.cs` - shared PlayerPrefs keys, clamping, application, and saves
  used by both startup and gameplay settings menus.
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
  `settings.mouseSensitivity` through `GameSettings`; do not fork their keys.
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

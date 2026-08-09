---
chunk: promo-video
title: Deterministic promotional video capture
owns:
  - "Assets/Editor/Promo.meta"
  - "Assets/Editor/Promo/**"
related: [unity-project, player-controller, runtime-art, enemies, boss-fight]
verifiedAtCommit: 841ef536a55749b7d4423241ae3c6be510c9b12f
lastVerified: 2026-08-09
---

## What this is

Editor-only tooling renders the authored starting cutscene and three reusable
promo-only scenes to 1920x1080, 30-fps PNG sequences under the gitignored
`Recordings/Promo/frames/` directory. The custom scenes are a tumbling astronaut
crossing empty space, Finn cycling emotes on a stripped planet, and a rave with
all seven unique character forms. They do not modify or save `SampleScene`.

## Key files

- `PromoVideoCapture.cs` owns the menu commands, command-line entry point,
  fixed camera/render-texture readback, temporary scene staging, raw FBX clip
  sampling, and the editor request watcher used when a live editor owns the
  project.
- The rave cast is Finn and Barbara in astronaut and Mech forms plus Small,
  Large, and Flying enemies. Raw character models and shared materials avoid
  enabling gameplay AI, combat, input, physics, or boss-stage scripts.
- `OpeningCutsceneController.SetCaptureDeltaTimeOverride` advances only the
  authored opening's coroutine with a fixed capture step. With its default zero
  value, gameplay continues to use real unscaled time.

## Invariants

- Promo staging is temporary Editor state. Never serialize its camera, actors,
  lights, disabled roots, or stripped planet back into a gameplay scene.
- Use `Camera.Render` to a 1080p render texture with graphics enabled. The PNG
  sequence is silent and must be encoded or muxed separately.
- Custom character animation samples imported clips directly. Do not stage live
  enemy or boss prefabs, which would activate AI and transformation state.
- The space-float shot has no planet; the emote and rave shots retain only the
  planet shell, sky, sun, and volume from `SampleScene`.

## Gotchas

- The opening coroutine normally advances with `Time.unscaledDeltaTime`; a slow
  PNG encoder therefore undersamples it unless the capture-only fixed step is
  enabled before its `Start` sequence.
- Unity batch startup can be blocked by the project's known Package Manager
  `path ... undefined` fault. The live-editor Tools commands are the verified
  fallback and regenerate their target frame directory before capture.

---
chunk: tutorial
title: Overwatch-style onboarding tutorial (Tutorial.unity)
owns:
  - "Assets/Scripts/Tutorial.meta"
  - "Assets/Scripts/Tutorial/**"
  - "Assets/Editor/Tutorial.meta"
  - "Assets/Editor/Tutorial/**"
  - "Assets/Scenes/Tutorial.unity*"
  - "Assets/Prefabs/TutorialPlanet.prefab*"
  - "Assets/Art/Models/Environment/ModularSciFi/**"
  - "Assets/Art/Materials/ModularSciFi/**"
  - "Assets/Art/Materials/Tutorial/**"
  - "Assets/Art/Textures/ModularSciFi/**"
related: [system, state, player-controller, player-combat, enemies, items, ultimate, progression, core-loop, world-authoring, main-menu]
verifiedAtCommit: 5880217f80f1e06cbc5b770ce9d0b680dcccf6f9
lastVerified: 2026-08-09
---

## What this is

A tutorial scene (`Assets/Scenes/Tutorial.unity`) that teaches every core mechanic through gated rooms.
Room geometry is hand-built; targeted commands under `Assets/Editor/Tutorial/` add or repair individual gameplay
objects without repositioning existing work.

Stage order: Movement -> Jump -> Dash -> Light Attack (5 hits) -> Heavy Attack (1 hit) -> Power-Ups
(three pickups plus 30 Shield-mitigated damage) -> Overview -> MainMenu transition without a completion screen.

The scene opens with a skippable planet/base hold, room overview, astronaut Wave, and camera handoff.
`TutorialManager` begins Movement only after `TutorialOpeningCutscene` completes or is skipped.

## Key files

- `Assets/Scripts/Tutorial/TutorialManager.cs` - the stage state machine. WASD is read directly
  off `Keyboard.current` (Move is the one fixed, non-rebindable binding); Jump/Dash key off
  `PlayerController.JumpTriggeredThisFrame`/`PlayerDash.DashPerformed` so rebinding still works.
  Also resets the player to a per-stage checkpoint position if they fall below y=-6 (e.g. through
  a missed hand-authored Jump/Dash gap), instead of authoring a walk-back path under every gap.
  The Power-Ups stage requires all three items collected AND `_mitigatedDamage >= 30`
  (`TryCompleteItemsStage`), tallied via `TutorialShieldTrainerAI.DamageMitigated` - both
  conditions are checked from both directions (item collected, damage mitigated) so completion
  can't be missed regardless of which happens last. Collecting Thunder immediately re-activates
  Ultimate with an effectively-infinite duration (`TutorialUltimateDuration`), overriding the
  finite duration `ThunderPickup.ApplyEffect` already started, so it can never expire mid-stage.
  Reaching `Complete` uses the shared scene transition to load MainMenu - no completion UI.
- `Assets/Scripts/Tutorial/TutorialShieldTrainerAI.cs` - a stationary flying enemy for the
  Power-Ups room. It turns to face the player every frame and, on a fixed interval, fires a real
  travelling `Enemies.BossProjectile` (the same shuriken VFX `EnemyFlyingAI` uses) after a short
  telegraph. On impact: if `PlayerShield.IsActive`, nothing touches the player's real `Health` at
  all - the attempt is reported via `DamageMitigated` for `TutorialManager` to tally, and a blue
  `Combat.DamageNumberSpawner` popup (optional `Color?` param added for this) shows the mitigated
  amount at the impact point; if Shield is not active, the projectile's normal damage pipeline
  applies real damage and this immediately heals it back, so the player sees an authentic hit but
  this trainer can never actually kill them - the same "never lethal" guarantee `TutorialDummyAI`
  gives the combat stage, applied to the player's side instead of the target's.
- `Assets/Scripts/Tutorial/TutorialDummyAI.cs` - the practice dummy: an `Enemies.EnemyBase`
  subclass (required so [ultimate](ultimate.md)'s right-click secondary, which only ever targets
  `EnemyBase` instances, can hit it), meant to wear the Enemy_Large model with a `Health` **and a
  Collider** alongside it - a missing Collider is the most common cause of "shots pass straight
  through" (see Gotchas). Never dies (huge MaxHealth, `FullyHeal()` after every hit) rather than
  zeroing incoming damage, since a zero multiplier would also silently swallow `Health.Hit` and
  break hit counting. `Combat.DamageType` can't tell a light (pistol) hit from a heavy (secondary)
  hit apart - both tag `Ranged` - so it correlates `Health.Hit` timing against two small
  [player-combat](player-combat.md) events instead.
- `PlayerCombat.ShotFired`/`SecondaryFired` provide mode-agnostic attribution.
- `Assets/Scripts/Tutorial/TutorialGate.cs` - a solid collider that `SetActive(false)`s itself
  once, physically opening the way forward. Supports a `Linked Gates` list so several separate
  barrier pieces spanning one doorway can open together - `TutorialManager` only ever holds one
  `TutorialGate` reference per boundary, so the other pieces are wired into that one gate's list
  (`Tools/Tutorial/Link Selected Gates` automates this).
- `Assets/Scripts/Tutorial/TutorialZone.cs` - reusable trigger for the Overview stage (or
  anywhere else): opens a **modal** info popup the moment the player steps in (see
  `TutorialUIController`/`TutorialManager` below), and/or (only where wanted, e.g. the final Exit
  Zone) tells the manager the tutorial is finished. Logs a runtime warning if entered with no
  Manager wired, or with both Message empty and Advances To Complete off (nothing to do) - see
  `Tools/Tutorial/Diagnose Info Zones`. Not used for stage-gating.
- `Assets/Scripts/Tutorial/TutorialPickupWatcher.cs` - sibling component to add on each item
  pickup instance; `Items.ItemPickup` destroys its own GameObject on collection, so this
  component's `OnDestroy` is the collection signal, with no edits needed to the shared pickup
  script. Set its `Kind` (Health/Ammo/Thunder) in the Inspector.
- `Assets/Scripts/Tutorial/TutorialUIController.cs` - self-builds its whole Canvas hierarchy at
  `Awake` (progress-dot breadcrumb, a fading banner, key-prompt row with a completion pulse, item
  icons, and a modal info popup - dim full-screen backdrop, title/message/X close button,
  `PopupClosed` event). No completion panel - reaching Complete loads MainMenu directly (see
  below). Optional `panelSprite`/`hudFont` serialized fields let it reuse the exact sliced Space
  Expansion UI sprite and Kenney font the health/ammo/ability HUD bars use (`Tools/Tutorial/Polish
  Tutorial UI` assigns them); falls back to flat-color panels and the default font if left unset.
  Self-built rather than built by an editor tool and only bound at runtime like the `Player/UI`
  HUD scripts - there was no existing HUD pattern worth matching for a one-off standalone
  subsystem.
- `TutorialManager.ShowInfo`/`OnPopupClosed` suspend/restore `PlayerController`/`PlayerCombat`/
  `PlayerAbilityInput`/camera look/crosshair/cursor for exactly as long as the popup is open -
  the same cache-then-restore shape as
  `Gameplay.Interaction.StationMenuController`'s base-station consoles, scoped down for the
  tutorial (no `Time.timeScale` pause, no Settings-menu interplay). Only the popup's own X button
  restores gameplay; walking away doesn't, since the player can't move while it's open anyway.

### Editor tools (`Assets/Editor/Tutorial/`)

Tools are additive and position-preserving, and skip objects already present.

- `ModularKitAssetSetup.cs` imports the complete Modular SciFi MegaKit into
  semantic model/texture folders and remaps vendor slots to shared materials.
- `TutorialGameplayStarterKit.cs` adds one reference instance of each gameplay
  object, creates the manager first, wires empty references, and offers isolated
  Exit VFX and Shield Trainer additions.
- `TutorialFixups.cs` contains targeted collider, gate-link/style, pickup-pivot,
  trainer-targeting, Power-Ups wiring, info-zone diagnosis, and UI polish fixes.
- `TutorialSceneStrip.cs` is a completed one-shot cleanup and is safe to delete.

## Invariants

- The room is hand-authored from here on. Do not reintroduce a destructive scene-builder for it
  without the owner's agreement - that's exactly what was scrapped.
- The dummy must stay an `EnemyBase` subclass - `PlayerCombat.FindNearestEnemies` (secondary/
  heavy attack targeting) only ever considers `EnemyBase` instances.
- Light vs heavy attack attribution depends on `PlayerCombat.ShotFired`/`SecondaryFired` staying
  wired; if a future refactor removes them, `TutorialDummyAI.HandleTutorialHit` silently stops
  attributing hits to either counter.
- Gates only make sense between stages that require physical travel; `LightAttack -> HeavyAttack`
  can share one room with no gate, just a requirement/instruction swap in `TutorialManager`.
- `TutorialShieldTrainerAI` must never call anything that could actually kill the player - always
  pair a real `ApplyDamage` call with an immediate `Heal` of the same amount.

## Gotchas

- A travelling shot only registers a hit via Unity's own `OnTriggerEnter`, which requires the
  target to have *some* `Collider` (the projectile already carries its own trigger collider +
  kinematic Rigidbody - see `Enemies/BossProjectile.cs`). A hand-placed dummy that never got a
  `Collider` added is the most common cause of "shots pass straight through" -
  `Tools/Tutorial/Fix Training Dummy Hit Collider` fixes this without touching its transform.
- `Items.ItemPickup.Update()` only ever spins its own transform in place, but two different root
  causes both read as "orbiting": (1) it's nested as a child under something that itself rotates
  every frame (`TutorialDummyAI.FacePlayer()`, or another `ItemPickup`'s own spin) - fixed by
  `Tools/Tutorial/Fix Orbiting Item Pickups`; (2) the model's own import pivot isn't at its visual
  center (the vendor Ultimate Space Kit models are inconsistent about this - see
  [world-authoring](../development/world-authoring.md)'s "pivots are not universally at the visible base"), so it
  sweeps around that off-center point even with no unusual parenting at all - fixed by
  `Tools/Tutorial/Fix Off-Center Pickup Pivots`, which recenters the instance without moving it on
  screen. Try both if one doesn't resolve it.

## How to extend

Add a new stage by extending `TutorialStage` and adding its `EnterStage`/`CompleteStage` cases in
`TutorialManager`; wire any new `TutorialGate`/`TutorialZone`/`TutorialPickupWatcher` instances
through the Inspector (or a new targeted command in `TutorialFixups.cs`/
`TutorialGameplayStarterKit.cs`) rather than a scene-building script. `SampleScene`'s skybox
material (`Assets/Art/Materials/M_ProceduralSpaceSkybox.mat`), ambient Trilight settings, and
`Assets/Art/Prefabs/Planet.prefab` are what to reuse if/when the space-atmosphere backdrop (sky,
sun, the planet visible through windows) gets rebuilt into the hand-authored scene.

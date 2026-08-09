using Player;
using Player.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tutorial
{
    /// Drives the whole Overwatch-style tutorial: one TutorialGate physically blocks the
    /// corridor at the start of every stage after Movement, and this component decides when that
    /// stage's requirement is satisfied, opens the next gate, and updates TutorialUIController.
    /// The Move binding (WASD) is fixed and non-rebindable, so WASD is read directly off
    /// Keyboard.current rather than through the action system. Jump and Dash instead key off
    /// PlayerController.JumpTriggeredThisFrame / PlayerDash.DashPerformed, and Attack/Attack2 key
    /// off TutorialDummyAI's landed-hit events - all three stay correct even if the player has
    /// rebound those actions away from their defaults.
    public class TutorialManager : MonoBehaviour
    {
        private const int LightAttacksRequired = 5;
        private const int HeavyAttacksRequired = 1;
        private const int MeleeHitsRequired = 1;
        private const float MitigatedDamageRequired = 30f;
        // Effectively forever - Thunder's own ItemPickup.ApplyEffect already activates Ultimate
        // with its normal (finite) duration, so the shield-training requirement can't be missed
        // just because the player took a while collecting Health/Ammo or dodging the trainer.
        private const float TutorialUltimateDuration = 999999f;
        private const string MainMenuSceneName = "MainMenu";

        [SerializeField] private TutorialUIController ui;
        [SerializeField] private TutorialDummyAI dummy;
        [SerializeField] private TutorialShieldTrainerAI shieldTrainer;

        [SerializeField] private TutorialGate gateToJump;
        [SerializeField] private TutorialGate gateToDash;
        [SerializeField] private TutorialGate gateToCombat;
        [SerializeField] private TutorialGate gateToItems;
        [SerializeField] private TutorialGate gateToOverview;

        private PlayerController _playerController;
        private PlayerDash _playerDash;
        private PlayerUltimate _playerUltimate;
        private PlayerCombat _playerCombat;
        private PlayerAbilityInput _playerAbilityInput;
        private PlayerEmoteController _playerEmote;
        private PlayerAmmo _playerAmmo;
        private ThirdPersonCameraController _cameraController;
        private Player.UI.CrosshairUI _crosshair;

        // Cached only while the info popup is open, mirroring
        // Gameplay.Interaction.StationMenuController's suspend/restore pattern.
        private bool _playerWasEnabled;
        private bool _combatWasEnabled;
        private bool _abilityWasEnabled;
        private bool _cameraWasSuspended;
        private bool _crosshairWasVisible;
        private CursorLockMode _cursorLockBeforePopup;
        private bool _cursorVisibleBeforePopup;

        public TutorialStage CurrentStage { get; private set; } = TutorialStage.Movement;

        private readonly bool[] _wasdPressed = new bool[4];
        private bool _jumped;
        private bool _waved;
        private bool _dashed;
        private int _lightHits;
        private bool _reloaded;
        private int _heavyHits;
        private int _meleeHits;
        private bool _healthCollected;
        private bool _ammoCollected;
        private bool _thunderCollected;
        private float _mitigatedDamage;

        public void Configure(TutorialUIController uiController, TutorialDummyAI trainingDummy,
            TutorialShieldTrainerAI trainer, TutorialGate jump, TutorialGate dash, TutorialGate combat,
            TutorialGate items, TutorialGate overview)
        {
            ui = uiController;
            dummy = trainingDummy;
            shieldTrainer = trainer;
            gateToJump = jump;
            gateToDash = dash;
            gateToCombat = combat;
            gateToItems = items;
            gateToOverview = overview;
        }

        private void Awake()
        {
            _playerController = FindFirstObjectByType<PlayerController>();
            _playerDash = _playerController != null ? _playerController.GetComponent<PlayerDash>() : null;
            _playerUltimate = _playerController != null ? _playerController.GetComponent<PlayerUltimate>() : null;
            _playerCombat = _playerController != null ? _playerController.GetComponent<PlayerCombat>() : null;
            _playerAbilityInput = _playerController != null ? _playerController.GetComponent<PlayerAbilityInput>() : null;
            _playerEmote = _playerController != null ? _playerController.GetComponent<PlayerEmoteController>() : null;
            _playerAmmo = _playerController != null ? _playerController.GetComponent<PlayerAmmo>() : null;
            _cameraController = FindFirstObjectByType<ThirdPersonCameraController>();
            _crosshair = FindFirstObjectByType<Player.UI.CrosshairUI>();
        }

        private void OnEnable()
        {
            if (dummy != null)
            {
                dummy.LightHitLanded += OnLightHitLanded;
                dummy.HeavyHitLanded += OnHeavyHitLanded;
                dummy.MeleeHitLanded += OnMeleeHitLanded;
            }
            if (shieldTrainer != null) shieldTrainer.DamageMitigated += OnDamageMitigated;
            if (_playerDash != null) _playerDash.DashPerformed += OnDashPerformed;
            if (_playerEmote != null) _playerEmote.EmoteTriggered += OnEmoteTriggered;
            if (_playerAmmo != null) _playerAmmo.ReloadStarted += OnReloadStarted;
            if (ui != null) ui.PopupClosed += OnPopupClosed;
        }

        private void OnDisable()
        {
            if (dummy != null)
            {
                dummy.LightHitLanded -= OnLightHitLanded;
                dummy.HeavyHitLanded -= OnHeavyHitLanded;
                dummy.MeleeHitLanded -= OnMeleeHitLanded;
            }
            if (shieldTrainer != null) shieldTrainer.DamageMitigated -= OnDamageMitigated;
            if (_playerDash != null) _playerDash.DashPerformed -= OnDashPerformed;
            if (_playerEmote != null) _playerEmote.EmoteTriggered -= OnEmoteTriggered;
            if (_playerAmmo != null) _playerAmmo.ReloadStarted -= OnReloadStarted;
            if (ui != null) ui.PopupClosed -= OnPopupClosed;
        }

        private const float FallResetHeight = -6f;
        private Vector3 _checkpointPosition;
        private bool _started;

        private void Start()
        {
            if (_playerController != null) _checkpointPosition = _playerController.transform.position;

            // A TutorialOpeningCutscene, if present, holds the player in its own intro shot and
            // calls BeginTutorial() itself once it hands control back - starting Movement here
            // too would race it.
            if (FindFirstObjectByType<TutorialOpeningCutscene>() == null)
            {
                BeginTutorial();
            }
        }

        /// Enters the Movement stage. Safe to call once; a second call (e.g. if both this and a
        /// TutorialOpeningCutscene tried to start it) is ignored.
        public void BeginTutorial()
        {
            if (_started) return;
            _started = true;
            EnterStage(TutorialStage.Movement);
        }

        private void Update()
        {
            // The Jump/Dash gaps have no floor underneath - a missed attempt drops the player
            // into open space instead of stranding them, so just return them to where the
            // current stage began rather than authoring a walk-back path under every gap.
            if (_playerController != null && _playerController.transform.position.y < FallResetHeight)
            {
                _playerController.transform.position = _checkpointPosition;
            }

            switch (CurrentStage)
            {
                case TutorialStage.Movement:
                    TickMovement();
                    break;
                case TutorialStage.Jump:
                    TickJump();
                    break;
            }
        }

        private void TickMovement()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            SetPressed(0, keyboard.wKey.isPressed);
            SetPressed(1, keyboard.aKey.isPressed);
            SetPressed(2, keyboard.sKey.isPressed);
            SetPressed(3, keyboard.dKey.isPressed);

            if (_wasdPressed[0] && _wasdPressed[1] && _wasdPressed[2] && _wasdPressed[3])
            {
                CompleteStage(TutorialStage.Movement);
            }
        }

        private void SetPressed(int index, bool isPressed)
        {
            if (!isPressed || _wasdPressed[index]) return;
            _wasdPressed[index] = true;
            ui.SetKeyComplete(index, true);
        }

        private void TickJump()
        {
            if (_jumped || _playerController == null) return;
            if (_playerController.JumpTriggeredThisFrame)
            {
                _jumped = true;
                ui.SetKeyComplete(0, true);
                CompleteStage(TutorialStage.Jump);
            }
        }

        private void OnDashPerformed()
        {
            if (CurrentStage != TutorialStage.Dash || _dashed) return;
            _dashed = true;
            ui.SetKeyComplete(0, true);
            CompleteStage(TutorialStage.Dash);
        }

        // Wave is index 0 in both the base and Mech emote-wheel label sets, so this check stays
        // correct even if the tutorial is ever reached mid-Ultimate.
        private void OnEmoteTriggered(int index)
        {
            if (CurrentStage != TutorialStage.Emote || _waved || index != 0) return;
            _waved = true;
            ui.SetKeyComplete(0, true);
            CompleteStage(TutorialStage.Emote);
        }

        // PlayerAmmo.ReloadStarted only fires once the magazine has actually room to reload
        // (see PlayerAmmo.StartReload) - by the time the player reaches this stage they've just
        // fired the required light-attack shots, so the magazine is no longer full and pressing R
        // reloads for real.
        private void OnReloadStarted()
        {
            if (CurrentStage != TutorialStage.Reload || _reloaded) return;
            _reloaded = true;
            ui.SetKeyComplete(0, true);
            CompleteStage(TutorialStage.Reload);
        }

        private void OnLightHitLanded()
        {
            if (CurrentStage != TutorialStage.LightAttack) return;
            _lightHits++;
            ui.SetCounter(_lightHits, LightAttacksRequired);
            if (_lightHits >= LightAttacksRequired) CompleteStage(TutorialStage.LightAttack);
        }

        private void OnHeavyHitLanded()
        {
            if (CurrentStage != TutorialStage.HeavyAttack) return;
            _heavyHits++;
            ui.SetCounter(_heavyHits, HeavyAttacksRequired);
            if (_heavyHits >= HeavyAttacksRequired) CompleteStage(TutorialStage.HeavyAttack);
        }

        private void OnMeleeHitLanded()
        {
            if (CurrentStage != TutorialStage.Melee) return;
            _meleeHits++;
            ui.SetCounter(_meleeHits, MeleeHitsRequired);
            if (_meleeHits >= MeleeHitsRequired) CompleteStage(TutorialStage.Melee);
        }

        public void NotifyItemCollected(TutorialPickupWatcher.Kind kind)
        {
            if (CurrentStage != TutorialStage.Items) return;

            switch (kind)
            {
                case TutorialPickupWatcher.Kind.Health: _healthCollected = true; break;
                case TutorialPickupWatcher.Kind.Ammo: _ammoCollected = true; break;
                case TutorialPickupWatcher.Kind.Thunder:
                    _thunderCollected = true;
                    // ThunderPickup.ApplyEffect already activated Ultimate with its normal
                    // duration a moment ago (see Items/ThunderPickup.cs) - immediately re-trigger
                    // it with an effectively-infinite one instead, purely for this tutorial.
                    _playerUltimate?.ActivateUltimate(TutorialUltimateDuration);
                    break;
            }
            ui.SetItemCollected(kind);

            // Only switches the instructions once all three are actually in hand, regardless of
            // which order they were collected in - picking Thunder up first (before Health/Ammo)
            // must not show the shield instructions early.
            if (_healthCollected && _ammoCollected && _thunderCollected)
            {
                ui.ShowStage("Shield Training",
                    "SHIFT now raises a SHIELD instead of dashing - the flying enemy can't hurt " +
                    "you through it. Mitigate 30 damage to move on.");
                ui.SetCounter(Mathf.RoundToInt(_mitigatedDamage), Mathf.RoundToInt(MitigatedDamageRequired));
            }

            TryCompleteItemsStage();
        }

        private void OnDamageMitigated(float amount)
        {
            if (CurrentStage != TutorialStage.Items) return;

            _mitigatedDamage += amount;
            ui.SetCounter(Mathf.RoundToInt(_mitigatedDamage), Mathf.RoundToInt(MitigatedDamageRequired));
            TryCompleteItemsStage();
        }

        private void TryCompleteItemsStage()
        {
            if (_healthCollected && _ammoCollected && _thunderCollected && _mitigatedDamage >= MitigatedDamageRequired)
            {
                CompleteStage(TutorialStage.Items);
            }
        }

        public void NotifyOverviewFinished()
        {
            if (CurrentStage != TutorialStage.Overview) return;
            CompleteStage(TutorialStage.Overview);
        }

        // The popup is modal: movement/combat/abilities/look are suspended and the cursor is
        // freed for the moment it's open (same pattern as
        // Gameplay.Interaction.StationMenuController's base-station consoles), and only restored
        // when the player clicks the popup's own X button (OnPopupClosed) - walking away doesn't
        // dismiss it, since the player can't move while it's up anyway.
        public void ShowInfo(string message)
        {
            SuspendForPopup();
            ui.ShowInfo(message);
        }

        public void HideInfo() => ui.HideInfo();

        private void OnPopupClosed() => RestoreFromPopup();

        private void SuspendForPopup()
        {
            _playerWasEnabled = _playerController != null && _playerController.enabled;
            _combatWasEnabled = _playerCombat != null && _playerCombat.enabled;
            _abilityWasEnabled = _playerAbilityInput != null && _playerAbilityInput.enabled;
            _cameraWasSuspended = _cameraController != null && _cameraController.InputSuspended;
            _crosshairWasVisible = _crosshair != null && _crosshair.IsVisible;
            _cursorLockBeforePopup = Cursor.lockState;
            _cursorVisibleBeforePopup = Cursor.visible;

            if (_playerController != null) _playerController.enabled = false;
            if (_playerCombat != null) _playerCombat.enabled = false;
            if (_playerAbilityInput != null) _playerAbilityInput.enabled = false;
            if (_cameraController != null) _cameraController.InputSuspended = true;
            if (_crosshair != null) _crosshair.SetVisible(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestoreFromPopup()
        {
            if (_playerController != null) _playerController.enabled = _playerWasEnabled;
            if (_playerCombat != null) _playerCombat.enabled = _combatWasEnabled;
            if (_playerAbilityInput != null) _playerAbilityInput.enabled = _abilityWasEnabled;
            if (_cameraController != null) _cameraController.InputSuspended = _cameraWasSuspended;
            if (_crosshair != null) _crosshair.SetVisible(_crosshairWasVisible);
            Cursor.lockState = _cursorLockBeforePopup;
            Cursor.visible = _cursorVisibleBeforePopup;
        }

        private void CompleteStage(TutorialStage justCompleted)
        {
            switch (justCompleted)
            {
                // Movement -> Emote happens in the same starting room - only the
                // instructions/requirement change, no further gate to open.
                case TutorialStage.Movement: EnterStage(TutorialStage.Emote); break;
                case TutorialStage.Emote: gateToJump?.Open(); EnterStage(TutorialStage.Jump); break;
                case TutorialStage.Jump: gateToDash?.Open(); EnterStage(TutorialStage.Dash); break;
                case TutorialStage.Dash: gateToCombat?.Open(); EnterStage(TutorialStage.LightAttack); break;
                // LightAttack -> Reload -> HeavyAttack -> Melee all happen in the same combat
                // room - only the instructions/requirement change, no further gate to open.
                case TutorialStage.LightAttack: EnterStage(TutorialStage.Reload); break;
                case TutorialStage.Reload: EnterStage(TutorialStage.HeavyAttack); break;
                case TutorialStage.HeavyAttack: EnterStage(TutorialStage.Melee); break;
                case TutorialStage.Melee: gateToItems?.Open(); EnterStage(TutorialStage.Items); break;
                case TutorialStage.Items: gateToOverview?.Open(); EnterStage(TutorialStage.Overview); break;
                case TutorialStage.Overview: EnterStage(TutorialStage.Complete); break;
            }
        }

        private void EnterStage(TutorialStage stage)
        {
            CurrentStage = stage;
            if (_playerController != null) _checkpointPosition = _playerController.transform.position;
            ui.SetProgress(stage);
            switch (stage)
            {
                case TutorialStage.Movement:
                    ui.ShowStage("Movement", "Use W, A, S, and D to move around. Press every key at least once.");
                    ui.SetKeyPrompts("W", "A", "S", "D");
                    break;
                case TutorialStage.Emote:
                    ui.ShowStage("Say Hello", "Hold B to open the emote wheel, aim at WAVE, and release to greet the crew.");
                    ui.SetKeyPrompts("B");
                    break;
                case TutorialStage.Jump:
                    ui.ShowStage("Jump", "Press SPACE to jump across the gap ahead.");
                    ui.SetKeyPrompts("SPACE");
                    break;
                case TutorialStage.Dash:
                    ui.ShowStage("Dash", "Press SHIFT to dash forward and cross the wide gap.");
                    ui.SetKeyPrompts("SHIFT");
                    break;
                case TutorialStage.LightAttack:
                    ui.ShowStage("Light Attack", "Left-click to fire your pistol at the training dummy.");
                    ui.SetKeyPrompts("LMB");
                    ui.SetCounter(0, LightAttacksRequired);
                    break;
                case TutorialStage.Reload:
                    ui.ShowStage("Reload", "Press R to reload your pistol before learning the heavy attack.");
                    ui.SetKeyPrompts("R");
                    break;
                case TutorialStage.HeavyAttack:
                    ui.ShowStage("Heavy Attack", "Right-click for a heavy strike on the training dummy.");
                    ui.SetKeyPrompts("RMB");
                    ui.SetCounter(0, HeavyAttacksRequired);
                    break;
                case TutorialStage.Melee:
                    ui.ShowStage("Melee", "Press V to melee the training dummy up close.");
                    ui.SetKeyPrompts("V");
                    ui.SetCounter(0, MeleeHitsRequired);
                    break;
                case TutorialStage.Items:
                    ui.ShowStage("Power-Ups", "Collect the HEALTH pack, the AMMO crate, and the THUNDER icon ahead.");
                    ui.ShowItemRow();
                    break;
                case TutorialStage.Overview:
                    ui.ShowStage("Stations & Waves",
                        "Walk past the markers ahead to learn about base stations and enemy waves.");
                    ui.SetKeyPrompts();
                    break;
                case TutorialStage.Complete:
                    // No completion screen/button - touching the Exit Zone should return to
                    // MainMenu immediately, not prompt for confirmation.
                    SceneTransitionController.LoadScene(MainMenuSceneName);
                    break;
            }
        }
    }
}

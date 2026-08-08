using System.Collections;
using Player.UI;
using Player.UI.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gameplay.Interaction
{
    /// <summary>
    /// Shared pause-and-cursor owner for all base consoles. It intentionally suspends the same
    /// gameplay components as Settings without changing Settings itself; Settings is temporarily
    /// disabled so an E/Escape close cannot leak through and open the settings console.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class StationMenuController : MonoBehaviour
    {
        [Header("Station roots")]
        [SerializeField] private GameObject shellRoot;
        [SerializeField] private GameObject supplyRoot;
        [SerializeField] private GameObject skillTreeRoot;
        [SerializeField] private GameObject specialShopRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private ProgressionDataAdapter progression;

        [Header("Gameplay suspension")]
        [SerializeField] private global::Player.PlayerController playerController;
        [SerializeField] private global::Player.PlayerCombat playerCombat;
        [SerializeField] private global::Player.PlayerEmoteController emoteController;
        [SerializeField] private global::Player.PlayerAbilityInput abilityInput;
        [SerializeField] private global::Player.ThirdPersonCameraController cameraController;
        [SerializeField] private CrosshairUI crosshair;
        [SerializeField] private SettingsMenuController settingsMenu;

        private bool _isOpen;
        private int _openedFrame = -1;
        // True only between CacheGameplay and its paired RestoreGameplay. Lifecycle callbacks
        // can run before the first station open, so they must never apply default field values.
        private bool _ownsGameplayState;
        private bool _playerWasEnabled;
        private bool _combatWasEnabled;
        private bool _abilityWasEnabled;
        private bool _cameraWasSuspended;
        private bool _emoteWasSuspended;
        private bool _crosshairWasVisible;
        private bool _settingsWasEnabled;
        private float _timeScaleBeforeOpen = 1f;
        private CursorLockMode _cursorLockBeforeOpen;
        private bool _cursorVisibleBeforeOpen;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            ResolveReferences();
            SetRoot(shellRoot, false);
            SetRoot(supplyRoot, false);
            SetRoot(skillTreeRoot, false);
            SetRoot(specialShopRoot, false);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            RestoreGameplay();
        }

        private void OnDisable() => RestoreGameplay();

        private void Update()
        {
            if (!_isOpen || Keyboard.current == null) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)
            {
                TryCloseFromInput();
            }
        }

        public void Open(InteractableStation station)
        {
            if (_isOpen || station == null) return;
            ResolveReferences();
            // A nearby trigger can remain occupied while Settings is open. Do not steal its
            // cursor/suspension ownership; the player must close Settings first.
            if (settingsMenu != null && settingsMenu.IsOpen) return;
            CacheGameplay();
            _isOpen = true;
            _openedFrame = Time.frameCount;

            if (settingsMenu != null) settingsMenu.enabled = false;
            SuspendGameplay();
            SetRoot(shellRoot, true);
            SetRoot(supplyRoot, station.Kind == StationKind.Supply);
            SetRoot(skillTreeRoot, station.Kind == StationKind.SkillTree);
            SetRoot(specialShopRoot, station.Kind == StationKind.SpecialShop);
            progression?.RefreshNow();
            SelectFirstButton();
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            SetRoot(shellRoot, false);
            SetRoot(supplyRoot, false);
            SetRoot(skillTreeRoot, false);
            SetRoot(specialShopRoot, false);
            RestoreGameplay();
            StartCoroutine(RestoreSettingsNextFrame());
        }

        private bool TryCloseFromInput()
        {
            // The interaction controller opens earlier in the same Update frame. Without this
            // guard, the opening E press reaches this later-running controller and immediately
            // closes the console again, making it appear as though E did nothing.
            if (!_isOpen || Time.frameCount == _openedFrame) return false;
            Close();
            return true;
        }

        private IEnumerator RestoreSettingsNextFrame()
        {
            yield return null;
            if (settingsMenu != null) settingsMenu.enabled = _settingsWasEnabled;
        }

        private void ResolveReferences()
        {
            if (progression == null) progression = GetComponentInChildren<ProgressionDataAdapter>(true);
            if (playerController == null) playerController = FindFirstObjectByType<global::Player.PlayerController>();
            if (playerCombat == null) playerCombat = FindFirstObjectByType<global::Player.PlayerCombat>();
            if (emoteController == null) emoteController = FindFirstObjectByType<global::Player.PlayerEmoteController>();
            if (abilityInput == null) abilityInput = FindFirstObjectByType<global::Player.PlayerAbilityInput>();
            if (cameraController == null) cameraController = FindFirstObjectByType<global::Player.ThirdPersonCameraController>();
            if (crosshair == null) crosshair = FindFirstObjectByType<CrosshairUI>();
            if (settingsMenu == null) settingsMenu = FindFirstObjectByType<SettingsMenuController>();
        }

        private void CacheGameplay()
        {
            _playerWasEnabled = playerController != null && playerController.enabled;
            _combatWasEnabled = playerCombat != null && playerCombat.enabled;
            _abilityWasEnabled = abilityInput != null && abilityInput.enabled;
            _cameraWasSuspended = cameraController != null && cameraController.InputSuspended;
            _emoteWasSuspended = emoteController != null && emoteController.InputSuspended;
            _crosshairWasVisible = crosshair != null && crosshair.IsVisible;
            _settingsWasEnabled = settingsMenu != null && settingsMenu.enabled;
            _timeScaleBeforeOpen = Time.timeScale;
            _cursorLockBeforeOpen = Cursor.lockState;
            _cursorVisibleBeforeOpen = Cursor.visible;
            _ownsGameplayState = true;
        }

        private void SuspendGameplay()
        {
            if (emoteController != null) emoteController.SetInputSuspended(true);
            if (abilityInput != null) abilityInput.enabled = false;
            if (cameraController != null) cameraController.InputSuspended = true;
            if (playerCombat != null) playerCombat.enabled = false;
            if (playerController != null) playerController.enabled = false;
            if (crosshair != null) crosshair.SetVisible(false);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestoreGameplay()
        {
            if (!_ownsGameplayState) return;
            if (playerController != null) playerController.enabled = _playerWasEnabled;
            if (playerCombat != null) playerCombat.enabled = _combatWasEnabled;
            if (abilityInput != null) abilityInput.enabled = _abilityWasEnabled;
            if (cameraController != null) cameraController.InputSuspended = _cameraWasSuspended;
            if (emoteController != null) emoteController.SetInputSuspended(_emoteWasSuspended);
            if (crosshair != null) crosshair.SetVisible(_crosshairWasVisible);
            Time.timeScale = _timeScaleBeforeOpen;
            Cursor.lockState = _cursorLockBeforeOpen;
            Cursor.visible = _cursorVisibleBeforeOpen;
            _ownsGameplayState = false;
        }

        private void SelectFirstButton()
        {
            if (EventSystem.current == null) return;
            Button selected = closeButton;
            if (selected == null && shellRoot != null) selected = shellRoot.GetComponentInChildren<Button>(true);
            EventSystem.current.SetSelectedGameObject(selected != null ? selected.gameObject : null);
        }

        private static void SetRoot(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Gameplay.Waves;

namespace Player.UI
{
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public sealed class SettingsMenuController : MonoBehaviour
    {
        [Header("Menu")]
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private GameObject mainPage;
        [SerializeField] private GameObject controlsPage;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Text volumeValue;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Text sensitivityValue;
        [SerializeField] private Button controlsButton;
        [SerializeField] private Button teleportToBaseButton;
        [SerializeField] private Text teleportToBaseLabel;
        [SerializeField] private Button backButton;
        [SerializeField] private Button closeButton;
        [Tooltip("Available from SampleScene and Tutorial's pause menu - both instantiate this " +
                 "same PlayerRig prefab, so wiring it here covers both scenes at once.")]
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private ControlsRebindingUI controlsRebindingUi;

        [Header("Gameplay Suspension")]
        [SerializeField] private WaveGameController waveGameController;
        [SerializeField] private global::Player.PlayerController playerController;
        [SerializeField] private global::Player.PlayerCombat playerCombat;
        [SerializeField] private global::Player.PlayerEmoteController emoteController;
        [SerializeField] private global::Player.PlayerAbilityInput abilityInput;
        [SerializeField] private global::Player.ThirdPersonCameraController cameraController;
        [SerializeField] private CrosshairUI crosshairUi;

        private const string SingleplayerSceneName = "SampleScene";

        private GameObject _confirmDialog;

        private bool _isOpen;
        private bool _restoring;
        private float _timeScaleBeforeMenu = 1f;
        private CursorLockMode _cursorLockBeforeMenu;
        private bool _cursorVisibleBeforeMenu;
        private bool _playerWasEnabled;
        private bool _combatWasEnabled;
        private bool _abilityInputWasEnabled;
        private bool _cameraInputWasSuspended;
        private bool _emoteInputWasSuspended;
        private bool _crosshairWasVisible;
        private PcUiInputBinding _pcUiInput;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            ResolveReferences();
            EnsureEventSystem();
            EnsurePcUiInput();
            ConfigureSliders();

            if (controlsButton != null) controlsButton.onClick.AddListener(ShowControlsPage);
            if (teleportToBaseButton != null) teleportToBaseButton.onClick.AddListener(TeleportToBase);
            if (backButton != null) backButton.onClick.AddListener(ShowMainPage);
            if (closeButton != null) closeButton.onClick.AddListener(CloseSettings);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(ApplyMasterVolume);
            if (sensitivitySlider != null)
            {
                sensitivitySlider.onValueChanged.AddListener(ApplyMouseSensitivity);
            }

            if (menuRoot != null) menuRoot.SetActive(false);
            ApplySavedSettings();
            UiSfxWirer.WireAll(gameObject);
        }

        private void Update()
        {
            if (controlsRebindingUi != null && controlsRebindingUi.BlocksMenuEscape)
            {
                return;
            }

            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            if (!_isOpen)
            {
                // The opening cinematic disables the one shared HUD Canvas and consumes Escape
                // itself. Waiting for that Canvas to be active keeps a skip press from also
                // opening Settings on the same frame.
                if (hudCanvas != null && !hudCanvas.enabled)
                {
                    return;
                }

                OpenSettings();
                return;
            }

            if (_confirmDialog != null && _confirmDialog.activeSelf)
            {
                HideReturnToMainMenuConfirm();
            }
            else if (controlsPage != null && controlsPage.activeSelf)
            {
                ShowMainPage();
            }
            else
            {
                CloseSettings();
            }
        }

        private void OnDisable()
        {
            RestoreGameplayState();
        }

        private void OnDestroy()
        {
            RestoreGameplayState();
            _pcUiInput?.Dispose();
            _pcUiInput = null;

            if (controlsButton != null) controlsButton.onClick.RemoveListener(ShowControlsPage);
            if (teleportToBaseButton != null) teleportToBaseButton.onClick.RemoveListener(TeleportToBase);
            if (backButton != null) backButton.onClick.RemoveListener(ShowMainPage);
            if (closeButton != null) closeButton.onClick.RemoveListener(CloseSettings);
            if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
            if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(ApplyMasterVolume);
            if (sensitivitySlider != null)
            {
                sensitivitySlider.onValueChanged.RemoveListener(ApplyMouseSensitivity);
            }
        }

        public void OpenSettings()
        {
            if (_isOpen || menuRoot == null || (hudCanvas != null && !hudCanvas.enabled))
            {
                return;
            }

            ResolveReferences();
            CacheGameplayState();

            Audio.AudioManager.Instance.PlaySfx(Audio.SfxId.UiOpen);
            _isOpen = true;
            ShowMainPage();
            menuRoot.SetActive(true);
            RefreshTeleportToBaseState();
            // Ensure the pause menu always draws above sibling HUD elements (e.g. the "E -
            // INTERACT" prompt), which otherwise share this Canvas and can render on top.
            menuRoot.transform.SetAsLastSibling();

            if (emoteController != null) emoteController.SetInputSuspended(true);
            if (abilityInput != null) abilityInput.enabled = false;
            if (cameraController != null) cameraController.InputSuspended = true;
            if (playerCombat != null) playerCombat.enabled = false;
            if (playerController != null) playerController.enabled = false;
            if (crosshairUi != null) crosshairUi.SetVisible(false);

            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (EventSystem.current != null && volumeSlider != null)
            {
                EventSystem.current.SetSelectedGameObject(volumeSlider.gameObject);
            }
        }

        public void CloseSettings()
        {
            if (!_isOpen)
            {
                return;
            }

            Audio.AudioManager.Instance.PlaySfx(Audio.SfxId.UiClose);
            PersistSettings();
            if (menuRoot != null) menuRoot.SetActive(false);
            RestoreGameplayState();
        }

        /// Entry point wired to the button - gates through a "you'll lose your run" confirmation
        /// while playing SampleScene (the singleplayer run has gold/upgrades/progress to lose).
        /// Tutorial has no run state, so it leaves immediately without prompting.
        public void ReturnToMainMenu()
        {
            if (SceneManager.GetActiveScene().name == SingleplayerSceneName)
            {
                ShowReturnToMainMenuConfirm();
                return;
            }

            ReturnToMainMenuConfirmed();
        }

        /// Leaves gameplay entirely rather than just closing the menu - resets time scale/cursor
        /// itself (rather than routing through RestoreGameplayState, which restores to whatever
        /// state gameplay was in before pausing) since a scene load is about to discard all of
        /// that anyway, and MainMenuController.Awake also independently resets both as a backstop.
        private void ReturnToMainMenuConfirmed()
        {
            Audio.AudioManager.Instance.PlaySfx(Audio.SfxId.UiClose);
            PersistSettings();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneTransitionController.LoadScene("MainMenu");
        }

        private void ShowReturnToMainMenuConfirm()
        {
            EnsureConfirmDialog();
            Audio.AudioManager.Instance.PlaySfx(Audio.SfxId.UiOpen);
            _confirmDialog.transform.SetAsLastSibling();
            _confirmDialog.SetActive(true);
        }

        private void HideReturnToMainMenuConfirm()
        {
            if (_confirmDialog != null) _confirmDialog.SetActive(false);
        }

        // Self-built the same way TutorialUIController's info popup is (dim full-screen backdrop
        // + centered panel) - built lazily on first use since it only ever matters for the
        // singleplayer pause menu, not every scene that shares this prefab.
        private void EnsureConfirmDialog()
        {
            if (_confirmDialog != null) return;

            Transform parent = hudCanvas != null ? hudCanvas.transform : transform;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _confirmDialog = new GameObject("ReturnToMainMenuConfirm", typeof(RectTransform), typeof(Image));
            _confirmDialog.transform.SetParent(parent, false);
            var backdropRect = (RectTransform)_confirmDialog.transform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            _confirmDialog.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_confirmDialog.transform, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 240f);
            panelRect.anchoredPosition = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.1f, 0.96f);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleText = titleGo.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 24;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.UpperCenter;
            titleText.color = Color.white;
            titleText.text = "RETURN TO MAIN MENU?";
            var titleRect = (RectTransform)titleGo.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -24f);
            titleRect.sizeDelta = new Vector2(-40f, 36f);

            var messageGo = new GameObject("Message", typeof(RectTransform), typeof(Text));
            messageGo.transform.SetParent(panelGo.transform, false);
            var messageText = messageGo.GetComponent<Text>();
            messageText.font = font;
            messageText.fontSize = 18;
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.color = new Color(0.85f, 0.85f, 0.9f, 1f);
            messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            messageText.text = "You will lose all progress in the current run - gold, upgrades, " +
                "and items will not be saved.";
            var messageRect = (RectTransform)messageGo.transform;
            messageRect.anchorMin = new Vector2(0f, 0.32f);
            messageRect.anchorMax = new Vector2(1f, 0.82f);
            messageRect.offsetMin = new Vector2(30f, 0f);
            messageRect.offsetMax = new Vector2(-30f, 0f);

            Button confirmButton = CreateConfirmDialogButton(panelGo.transform, font, "ConfirmButton",
                "YES, QUIT RUN", new Vector2(-140f, 36f), new Color(0.75f, 0.25f, 0.25f, 0.95f));
            confirmButton.onClick.AddListener(() =>
            {
                HideReturnToMainMenuConfirm();
                ReturnToMainMenuConfirmed();
            });

            Button cancelButton = CreateConfirmDialogButton(panelGo.transform, font, "CancelButton",
                "CANCEL", new Vector2(140f, 36f), new Color(0.25f, 0.3f, 0.35f, 0.95f));
            cancelButton.onClick.AddListener(HideReturnToMainMenuConfirm);

            _confirmDialog.SetActive(false);
        }

        private static Button CreateConfirmDialogButton(Transform parent, Font font, string name, string label,
            Vector2 anchoredPosition, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(220f, 56f);
            rect.anchoredPosition = anchoredPosition;
            go.GetComponent<Image>().color = color;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        public void ShowMainPage()
        {
            if (mainPage != null) mainPage.SetActive(true);
            if (controlsPage != null) controlsPage.SetActive(false);
            RefreshTeleportToBaseState();

            if (_isOpen && EventSystem.current != null && volumeSlider != null)
            {
                EventSystem.current.SetSelectedGameObject(volumeSlider.gameObject);
            }
        }

        public void ShowControlsPage()
        {
            if (mainPage != null) mainPage.SetActive(false);
            if (controlsPage != null) controlsPage.SetActive(true);

            if (_isOpen && EventSystem.current != null)
            {
                GameObject selection = controlsRebindingUi != null
                    ? controlsRebindingUi.FirstSelectable
                    : null;
                EventSystem.current.SetSelectedGameObject(
                    selection != null
                        ? selection
                        : backButton != null ? backButton.gameObject : null);
            }
        }

        public void TeleportToBase()
        {
            ResolveReferences();
            if (waveGameController == null || !waveGameController.TryTeleportToBase())
            {
                RefreshTeleportToBaseState();
                return;
            }

            CloseSettings();
        }

        private void ResolveReferences()
        {
            if (hudCanvas == null) hudCanvas = GetComponentInChildren<Canvas>(true);
            if (waveGameController == null) waveGameController = GetComponent<WaveGameController>();
            if (playerController == null) playerController = GetComponentInChildren<global::Player.PlayerController>(true);
            if (playerCombat == null) playerCombat = GetComponentInChildren<global::Player.PlayerCombat>(true);
            if (emoteController == null) emoteController = GetComponentInChildren<global::Player.PlayerEmoteController>(true);
            if (abilityInput == null) abilityInput = GetComponentInChildren<global::Player.PlayerAbilityInput>(true);
            if (controlsRebindingUi == null) controlsRebindingUi = GetComponentInChildren<ControlsRebindingUI>(true);
            if (cameraController == null)
            {
                cameraController = GetComponentInChildren<global::Player.ThirdPersonCameraController>(true);
            }
            if (crosshairUi == null) crosshairUi = GetComponentInChildren<CrosshairUI>(true);
        }

        private void RefreshTeleportToBaseState()
        {
            bool available = waveGameController != null && waveGameController.CanTeleportToBase;
            if (teleportToBaseButton != null)
            {
                teleportToBaseButton.interactable = available;
            }

            if (teleportToBaseLabel != null)
            {
                teleportToBaseLabel.text = available
                    ? "TELEPORT TO BASE"
                    : waveGameController != null
                        ? "TELEPORT TO BASE // LOCKED"
                        : "BASE RECALL UNAVAILABLE";
            }
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject(
                "Settings EventSystem",
                typeof(EventSystem));
            eventSystemObject.transform.SetParent(transform, false);
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private void EnsurePcUiInput()
        {
            if (_pcUiInput != null)
            {
                return;
            }

            InputSystemUIInputModule inputModule =
                Object.FindFirstObjectByType<InputSystemUIInputModule>();
            if (inputModule != null)
            {
                _pcUiInput = new PcUiInputBinding(inputModule);
            }
        }

        private void ConfigureSliders()
        {
            if (volumeSlider != null)
            {
                volumeSlider.minValue = 0f;
                volumeSlider.maxValue = 1f;
                volumeSlider.wholeNumbers = false;
            }

            if (sensitivitySlider != null)
            {
                sensitivitySlider.minValue = global::Player.ThirdPersonCameraController.MinimumMouseSensitivity;
                sensitivitySlider.maxValue = global::Player.ThirdPersonCameraController.MaximumMouseSensitivity;
                sensitivitySlider.wholeNumbers = false;
            }
        }

        private void ApplySavedSettings()
        {
            float masterVolume = GameSettings.LoadMasterVolume();
            float defaultSensitivity = cameraController != null
                ? cameraController.MouseSensitivity
                : GameSettings.DefaultMouseSensitivity;
            float mouseSensitivity = GameSettings.LoadMouseSensitivity(defaultSensitivity);

            if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(masterVolume);
            if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(mouseSensitivity);

            ApplyMasterVolume(masterVolume);
            ApplyMouseSensitivity(mouseSensitivity);
        }

        private void ApplyMasterVolume(float value)
        {
            float clamped = Mathf.Clamp01(value);
            GameSettings.ApplyMasterVolume(clamped);
            if (volumeValue != null) volumeValue.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
        }

        private void ApplyMouseSensitivity(float value)
        {
            float clamped = Mathf.Clamp(
                value,
                global::Player.ThirdPersonCameraController.MinimumMouseSensitivity,
                global::Player.ThirdPersonCameraController.MaximumMouseSensitivity);
            if (cameraController != null) cameraController.MouseSensitivity = clamped;

            float normalized = Mathf.InverseLerp(
                global::Player.ThirdPersonCameraController.MinimumMouseSensitivity,
                global::Player.ThirdPersonCameraController.MaximumMouseSensitivity,
                clamped);
            if (sensitivityValue != null)
            {
                sensitivityValue.text = $"{Mathf.RoundToInt(normalized * 100f)}%";
            }
        }

        private void PersistSettings()
        {
            float mouseSensitivity = cameraController != null
                ? cameraController.MouseSensitivity
                : sensitivitySlider != null
                    ? sensitivitySlider.value
                    : GameSettings.LoadMouseSensitivity();
            // AudioListener.volume is now pinned to 1 by GameSettings.ApplyMasterVolume (master
            // volume is applied via AudioManager/MusicManager instead) - read the slider itself
            // rather than the no-longer-meaningful AudioListener.volume.
            float masterVolume = volumeSlider != null ? volumeSlider.value : GameSettings.LoadMasterVolume();
            GameSettings.Save(masterVolume, mouseSensitivity);
        }

        private void CacheGameplayState()
        {
            _timeScaleBeforeMenu = Time.timeScale;
            _cursorLockBeforeMenu = Cursor.lockState;
            _cursorVisibleBeforeMenu = Cursor.visible;
            _playerWasEnabled = playerController != null && playerController.enabled;
            _combatWasEnabled = playerCombat != null && playerCombat.enabled;
            _abilityInputWasEnabled = abilityInput != null && abilityInput.enabled;
            _cameraInputWasSuspended = cameraController != null && cameraController.InputSuspended;
            _emoteInputWasSuspended = emoteController != null && emoteController.InputSuspended;
            _crosshairWasVisible = crosshairUi == null || crosshairUi.IsVisible;
        }

        private void RestoreGameplayState()
        {
            if (!_isOpen || _restoring)
            {
                return;
            }

            _restoring = true;
            try
            {
                _isOpen = false;
                Time.timeScale = _timeScaleBeforeMenu;

                if (playerController != null) playerController.enabled = _playerWasEnabled;
                if (playerCombat != null) playerCombat.enabled = _combatWasEnabled;
                if (abilityInput != null) abilityInput.enabled = _abilityInputWasEnabled;
                if (emoteController != null)
                {
                    emoteController.SetInputSuspended(_emoteInputWasSuspended);
                }
                if (cameraController != null)
                {
                    cameraController.InputSuspended = _cameraInputWasSuspended;
                }
                if (crosshairUi != null) crosshairUi.SetVisible(_crosshairWasVisible);

                Cursor.lockState = _cursorLockBeforeMenu;
                Cursor.visible = _cursorVisibleBeforeMenu;
            }
            finally
            {
                _restoring = false;
            }
        }
    }
}

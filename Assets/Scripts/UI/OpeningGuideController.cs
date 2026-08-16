using System.Collections;
using Gameplay.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Player.UI
{
    /// <summary>
    /// Three-page, informational field guide shown once at the beginning of SampleScene.
    /// It deliberately opens only after OpeningCutsceneController has restored gameplay, then
    /// borrows and restores the same modal state used by the base station consoles.
    /// The overlay itself is built in the Editor (see Assets/Editor/Player/OpeningGuideSceneSetup.cs)
    /// and only referenced here via Configure, so its layout is hand-editable like the rest of the
    /// project's UI instead of only existing once Play mode constructs it.
    /// </summary>
    [DefaultExecutionOrder(1010)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(OpeningCutsceneController))]
    public sealed class OpeningGuideController : MonoBehaviour
    {
        public const int TotalPages = 3;

        [Header("Opening handoff")]
        [SerializeField] private OpeningCutsceneController openingCutscene;

        [Header("Guide screenshots (read by the Editor build tool)")]
        [SerializeField] private Texture2D skillImage;
        [SerializeField] private Texture2D baseImage;
        [SerializeField] private Texture2D specialImage;
        [SerializeField] private Texture2D outsideImage;
        [SerializeField] private Texture2D arenaImage;

        [Header("Shared UI style (read by the Editor build tool)")]
        [SerializeField] private Font hudFont;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite buttonSprite;

        [Header("Built overlay (wired by Tools > Player Prototype > Rebuild Opening Field Guide)")]
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private GameObject[] pageRoots = new GameObject[TotalPages];
        [SerializeField] private Text pageTitle;
        [SerializeField] private Text pageSubtitle;
        [SerializeField] private Text progressText;
        [SerializeField] private Button nextButton;
        [SerializeField] private Text nextLabel;
        [SerializeField] private Button skipButton;

        private global::Player.PlayerController _playerController;
        private global::Player.PlayerCombat _playerCombat;
        private global::Player.PlayerAbilityInput _abilityInput;
        private global::Player.PlayerEmoteController _emoteController;
        private global::Player.ThirdPersonCameraController _cameraController;
        private CrosshairUI _crosshair;
        private SettingsMenuController _settingsMenu;
        private StationInteractionController _stationInteraction;

        private bool _isOpen;
        private bool _hasShown;
        private bool _ownsGameplayState;
        private int _currentPageIndex;

        private bool _playerWasEnabled;
        private bool _combatWasEnabled;
        private bool _abilityWasEnabled;
        private bool _cameraWasSuspended;
        private bool _emoteWasSuspended;
        private bool _crosshairWasVisible;
        private bool _settingsWasEnabled;
        private bool _stationInteractionWasEnabled;
        private bool _settingsRestorePending;
        private float _timeScaleBeforeOpen = 1f;
        private CursorLockMode _cursorLockBeforeOpen;
        private bool _cursorVisibleBeforeOpen;
        private GameObject _selectedBeforeOpen;

        public bool IsOpen => _isOpen;
        public int CurrentPageIndex => _currentPageIndex;

        /// <summary>Wires the Editor-built overlay hierarchy. Called once by the Editor build tool.</summary>
        public void Configure(
            GameObject builtOverlayRoot,
            GameObject[] builtPageRoots,
            Text builtPageTitle,
            Text builtPageSubtitle,
            Text builtProgressText,
            Button builtNextButton,
            Text builtNextLabel,
            Button builtSkipButton)
        {
            overlayRoot = builtOverlayRoot;
            pageRoots = builtPageRoots;
            pageTitle = builtPageTitle;
            pageSubtitle = builtPageSubtitle;
            progressText = builtProgressText;
            nextButton = builtNextButton;
            nextLabel = builtNextLabel;
            skipButton = builtSkipButton;
        }

        private void Awake()
        {
            if (openingCutscene == null)
            {
                openingCutscene = GetComponent<OpeningCutsceneController>();
            }

            if (openingCutscene != null)
            {
                openingCutscene.Completed += HandleOpeningCompleted;
            }
        }

        private void OnEnable()
        {
            if (nextButton != null) nextButton.onClick.AddListener(Advance);
            if (skipButton != null) skipButton.onClick.AddListener(Skip);
        }

        private void OnDisableButtons()
        {
            if (nextButton != null) nextButton.onClick.RemoveListener(Advance);
            if (skipButton != null) skipButton.onClick.RemoveListener(Skip);
        }

        private IEnumerator Start()
        {
            // The opening normally resolves its scene references immediately. Keep a short
            // fallback for a missing/disabled cinematic so the guide can never disappear from
            // the start of a run because another presentation object failed to initialize.
            const float idleFallbackDelay = 2.5f;
            float idleTime = 0f;

            while (!_hasShown)
            {
                if (openingCutscene == null || openingCutscene.IsCompleted)
                {
                    OpenGuide();
                    yield break;
                }

                if (openingCutscene.IsPlaying)
                {
                    idleTime = 0f;
                }
                else
                {
                    idleTime += Time.unscaledDeltaTime;
                    if (idleTime >= idleFallbackDelay)
                    {
                        OpenGuide();
                        yield break;
                    }
                }

                yield return null;
            }
        }

        private void Update()
        {
            if (!_isOpen || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Skip();
            }
        }

        private void OnDisable()
        {
            OnDisableButtons();
            RestorePendingSettings();
            _isOpen = false;
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            RestoreGameplay();
        }

        private void OnDestroy()
        {
            RestorePendingSettings();
            if (openingCutscene != null)
            {
                openingCutscene.Completed -= HandleOpeningCompleted;
            }

            RestoreGameplay();
        }

        private void HandleOpeningCompleted(bool skipped)
        {
            if (isActiveAndEnabled)
            {
                OpenGuide();
            }
        }

        private void OpenGuide()
        {
            if (_hasShown || _isOpen)
            {
                return;
            }

            if (overlayRoot == null)
            {
                Debug.LogError("OpeningGuideController has no built overlay - run " +
                    "Tools > Player Prototype > Rebuild Opening Field Guide.", this);
                return;
            }

            _hasShown = true;
            ResolveGameplayReferences();
            EnsureEventSystem();
            CacheGameplay();
            SuspendGameplay();

            _currentPageIndex = 0;
            _isOpen = true;
            overlayRoot.SetActive(true);
            ShowCurrentPage();
            SelectNextButton();
        }

        /// <summary>Moves to the next guide page; the final page closes the field guide.</summary>
        public void Advance()
        {
            if (!_isOpen)
            {
                return;
            }

            if (_currentPageIndex >= TotalPages - 1)
            {
                CloseGuide();
                return;
            }

            _currentPageIndex++;
            ShowCurrentPage();
            SelectNextButton();
        }

        /// <summary>Immediately dismisses the informational guide without persisting a flag.</summary>
        public void Skip()
        {
            if (_isOpen)
            {
                CloseGuide();
            }
        }

        private void CloseGuide()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            RestoreGameplay(true);
        }

        private void ResolveGameplayReferences()
        {
            _playerController = FindFirstObjectByType<global::Player.PlayerController>();
            _playerCombat = FindFirstObjectByType<global::Player.PlayerCombat>();
            _abilityInput = FindFirstObjectByType<global::Player.PlayerAbilityInput>();
            _emoteController = FindFirstObjectByType<global::Player.PlayerEmoteController>();
            _cameraController = FindFirstObjectByType<global::Player.ThirdPersonCameraController>();
            _crosshair = FindFirstObjectByType<CrosshairUI>();
            _settingsMenu = FindFirstObjectByType<SettingsMenuController>();
            _stationInteraction = FindFirstObjectByType<StationInteractionController>();
        }

        private void CacheGameplay()
        {
            _playerWasEnabled = _playerController != null && _playerController.enabled;
            _combatWasEnabled = _playerCombat != null && _playerCombat.enabled;
            _abilityWasEnabled = _abilityInput != null && _abilityInput.enabled;
            _cameraWasSuspended = _cameraController != null && _cameraController.InputSuspended;
            _emoteWasSuspended = _emoteController != null && _emoteController.InputSuspended;
            _crosshairWasVisible = _crosshair != null && _crosshair.IsVisible;
            _settingsWasEnabled = _settingsMenu != null && _settingsMenu.enabled;
            _stationInteractionWasEnabled = _stationInteraction != null && _stationInteraction.enabled;
            _timeScaleBeforeOpen = Time.timeScale;
            _cursorLockBeforeOpen = Cursor.lockState;
            _cursorVisibleBeforeOpen = Cursor.visible;
            _selectedBeforeOpen = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            _ownsGameplayState = true;
        }

        private void SuspendGameplay()
        {
            if (_settingsMenu != null) _settingsMenu.enabled = false;
            if (_stationInteraction != null) _stationInteraction.enabled = false;
            if (_emoteController != null) _emoteController.SetInputSuspended(true);
            if (_abilityInput != null) _abilityInput.enabled = false;
            if (_cameraController != null) _cameraController.InputSuspended = true;
            if (_playerCombat != null) _playerCombat.enabled = false;
            if (_playerController != null) _playerController.enabled = false;
            if (_crosshair != null) _crosshair.SetVisible(false);

            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestoreGameplay(bool deferSettings = false)
        {
            if (!_ownsGameplayState)
            {
                return;
            }

            if (_playerController != null) _playerController.enabled = _playerWasEnabled;
            if (_playerCombat != null) _playerCombat.enabled = _combatWasEnabled;
            if (_abilityInput != null) _abilityInput.enabled = _abilityWasEnabled;
            if (_cameraController != null) _cameraController.InputSuspended = _cameraWasSuspended;
            if (_emoteController != null) _emoteController.SetInputSuspended(_emoteWasSuspended);
            if (_crosshair != null) _crosshair.SetVisible(_crosshairWasVisible);
            if (_settingsMenu != null)
            {
                if (deferSettings)
                {
                    _settingsRestorePending = true;
                    StartCoroutine(RestoreSettingsNextFrame());
                }
                else
                {
                    _settingsMenu.enabled = _settingsWasEnabled;
                }
            }
            if (_stationInteraction != null) _stationInteraction.enabled = _stationInteractionWasEnabled;

            Time.timeScale = _timeScaleBeforeOpen;
            Cursor.lockState = _cursorLockBeforeOpen;
            Cursor.visible = _cursorVisibleBeforeOpen;

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_selectedBeforeOpen);
            }

            _ownsGameplayState = false;
        }

        private IEnumerator RestoreSettingsNextFrame()
        {
            yield return null;
            RestorePendingSettings();
        }

        private void RestorePendingSettings()
        {
            if (!_settingsRestorePending)
            {
                return;
            }

            _settingsRestorePending = false;
            if (_settingsMenu != null)
            {
                _settingsMenu.enabled = _settingsWasEnabled;
            }
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("Opening Guide EventSystem", typeof(EventSystem));
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private void ShowCurrentPage()
        {
            for (int i = 0; i < pageRoots.Length; i++)
            {
                if (pageRoots[i] != null)
                {
                    pageRoots[i].SetActive(i == _currentPageIndex);
                }
            }

            switch (_currentPageIndex)
            {
                case 0:
                    pageTitle.text = "PREPARE AT BASE";
                    pageSubtitle.text = "Three stations. One run. Spend your starting gold before deployment.";
                    break;
                case 1:
                    pageTitle.text = "START THE WAVE";
                    pageSubtitle.text = "Leave the safe zone, deploy on your terms, and survive the timer.";
                    break;
                default:
                    pageTitle.text = "ARENA CONTRACTS";
                    pageSubtitle.text = "Every fifth wave changes the rules. Follow the arrow and enter ready.";
                    break;
            }

            progressText.text = BuildProgressLabel(_currentPageIndex);
            nextLabel.text = _currentPageIndex == TotalPages - 1 ? "BEGIN RUN" : "NEXT  >";
        }

        private static string BuildProgressLabel(int pageIndex)
        {
            switch (Mathf.Clamp(pageIndex, 0, TotalPages - 1))
            {
                case 0: return "BASE  [*]  WAVE  [ ]  ARENA  [ ]";
                case 1: return "BASE  [*]  WAVE  [*]  ARENA  [ ]";
                default: return "BASE  [*]  WAVE  [*]  ARENA  [*]";
            }
        }

        private void SelectNextButton()
        {
            if (EventSystem.current != null && nextButton != null)
            {
                EventSystem.current.SetSelectedGameObject(nextButton.gameObject);
            }
        }
    }
}

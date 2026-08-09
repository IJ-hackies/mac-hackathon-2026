using Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Player.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Navigation")]
        [SerializeField] private string singleplayerScene = "SampleScene";
        [SerializeField] private string tutorialScene = "Tutorial";
        [SerializeField] private GameObject homePage;
        [SerializeField] private GameObject settingsPage;
        [SerializeField] private GameObject controlsPage;
        [SerializeField] private Button singleplayerButton;
        [FormerlySerializedAs("multiplayerButton")]
        [SerializeField] private Button tutorialButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button settingsBackButton;
        [SerializeField] private Button controlsButton;
        [SerializeField] private Button controlsBackButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private ControlsRebindingUI controlsRebindingUi;

        [Header("Settings")]
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Text volumeValue;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Text sensitivityValue;

        [Header("Presentation")]
        [SerializeField] private Transform menuPlanet;
        [SerializeField] private Vector3 planetRotationAxis = new Vector3(0.12f, 1f, -0.08f);
        [SerializeField, Min(0f)] private float planetRotationSpeed = 0.55f;

        private enum Page
        {
            Home,
            Settings,
            Controls
        }

        private Page _currentPage;
        private PcUiInputBinding _pcUiInput;

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (controlsRebindingUi == null)
            {
                controlsRebindingUi = GetComponentInChildren<ControlsRebindingUI>(true);
            }

            EnsurePcUiInput();
            ConfigureSettings();
            RegisterListeners();
            UiSfxWirer.WireAll(gameObject);

            var musicManager = MusicManager.Instance;
            if (musicManager != null) musicManager.PlayMusic(musicManager.menuMusic);

            ShowPage(Page.Home);
        }

        private void Start()
        {
            EnsurePcUiInput();

            // EventSystem initialization order is not guaranteed relative to this root.
            // Re-select once every Awake has completed so keyboard navigation
            // always starts on the primary action.
            ShowPage(Page.Home);
        }

        private void Update()
        {
            if (menuPlanet != null && planetRotationSpeed > 0f)
            {
                menuPlanet.Rotate(
                    planetRotationAxis.normalized,
                    planetRotationSpeed * Time.unscaledDeltaTime,
                    Space.World);
            }

            if (controlsRebindingUi != null && controlsRebindingUi.BlocksMenuEscape)
            {
                return;
            }

            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            if (_currentPage == Page.Controls)
            {
                Audio.AudioManager.Instance.PlaySfx(Audio.SfxId.UiClose);
                ShowPage(Page.Settings);
            }
            else if (_currentPage == Page.Settings)
            {
                Audio.AudioManager.Instance.PlaySfx(Audio.SfxId.UiClose);
                SaveSettings();
                ShowPage(Page.Home);
            }
        }

        private void OnDestroy()
        {
            SaveSettings();
            UnregisterListeners();
            _pcUiInput?.Dispose();
            _pcUiInput = null;
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

        public void LoadSingleplayer()
        {
            SaveSettings();
            Time.timeScale = 1f;
            SceneTransitionController.LoadScene(singleplayerScene);
        }

        public void LoadTutorial()
        {
            SaveSettings();
            Time.timeScale = 1f;
            SceneTransitionController.LoadScene(tutorialScene);
        }

        public void QuitGame()
        {
            SaveSettings();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ShowHome()
        {
            SaveSettings();
            ShowPage(Page.Home);
        }

        public void ShowSettings()
        {
            ShowPage(Page.Settings);
        }

        public void ShowControls()
        {
            ShowPage(Page.Controls);
        }

        private void ConfigureSettings()
        {
            if (volumeSlider != null)
            {
                volumeSlider.minValue = 0f;
                volumeSlider.maxValue = 1f;
                volumeSlider.wholeNumbers = false;
                volumeSlider.SetValueWithoutNotify(GameSettings.LoadMasterVolume());
                ApplyMasterVolume(volumeSlider.value);
            }

            if (sensitivitySlider != null)
            {
                sensitivitySlider.minValue = global::Player.ThirdPersonCameraController.MinimumMouseSensitivity;
                sensitivitySlider.maxValue = global::Player.ThirdPersonCameraController.MaximumMouseSensitivity;
                sensitivitySlider.wholeNumbers = false;
                sensitivitySlider.SetValueWithoutNotify(GameSettings.LoadMouseSensitivity());
                ApplyMouseSensitivity(sensitivitySlider.value);
            }
        }

        private void RegisterListeners()
        {
            if (singleplayerButton != null) singleplayerButton.onClick.AddListener(LoadSingleplayer);
            if (tutorialButton != null) tutorialButton.onClick.AddListener(LoadTutorial);
            if (settingsButton != null) settingsButton.onClick.AddListener(ShowSettings);
            if (settingsBackButton != null) settingsBackButton.onClick.AddListener(ShowHome);
            if (controlsButton != null) controlsButton.onClick.AddListener(ShowControls);
            if (controlsBackButton != null) controlsBackButton.onClick.AddListener(ShowSettings);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
            if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(ApplyMasterVolume);
            if (sensitivitySlider != null)
            {
                sensitivitySlider.onValueChanged.AddListener(ApplyMouseSensitivity);
            }
        }

        private void UnregisterListeners()
        {
            if (singleplayerButton != null) singleplayerButton.onClick.RemoveListener(LoadSingleplayer);
            if (tutorialButton != null) tutorialButton.onClick.RemoveListener(LoadTutorial);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(ShowSettings);
            if (settingsBackButton != null) settingsBackButton.onClick.RemoveListener(ShowHome);
            if (controlsButton != null) controlsButton.onClick.RemoveListener(ShowControls);
            if (controlsBackButton != null) controlsBackButton.onClick.RemoveListener(ShowSettings);
            if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);
            if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(ApplyMasterVolume);
            if (sensitivitySlider != null)
            {
                sensitivitySlider.onValueChanged.RemoveListener(ApplyMouseSensitivity);
            }
        }

        private void ShowPage(Page page)
        {
            _currentPage = page;
            if (homePage != null) homePage.SetActive(page == Page.Home);
            if (settingsPage != null) settingsPage.SetActive(page == Page.Settings);
            if (controlsPage != null) controlsPage.SetActive(page == Page.Controls);

            GameObject selection = page switch
            {
                Page.Home => singleplayerButton != null ? singleplayerButton.gameObject : null,
                Page.Settings => volumeSlider != null ? volumeSlider.gameObject : null,
                Page.Controls => controlsRebindingUi != null && controlsRebindingUi.FirstSelectable != null
                    ? controlsRebindingUi.FirstSelectable
                    : controlsBackButton != null ? controlsBackButton.gameObject : null,
                _ => null
            };

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(selection);
            }
        }

        private void ApplyMasterVolume(float value)
        {
            float clamped = Mathf.Clamp01(value);
            // GameSettings.ApplyMasterVolume is now the single funnel that applies to both
            // AudioManager and MusicManager - see GameSettings.cs.
            GameSettings.ApplyMasterVolume(clamped);
            if (volumeValue != null)
            {
                volumeValue.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
            }
        }

        private void ApplyMouseSensitivity(float value)
        {
            float clamped = Mathf.Clamp(
                value,
                global::Player.ThirdPersonCameraController.MinimumMouseSensitivity,
                global::Player.ThirdPersonCameraController.MaximumMouseSensitivity);
            float normalized = Mathf.InverseLerp(
                global::Player.ThirdPersonCameraController.MinimumMouseSensitivity,
                global::Player.ThirdPersonCameraController.MaximumMouseSensitivity,
                clamped);

            if (sensitivityValue != null)
            {
                sensitivityValue.text = $"{Mathf.RoundToInt(normalized * 100f)}%";
            }
        }

        private void SaveSettings()
        {
            float volume = volumeSlider != null
                ? volumeSlider.value
                : GameSettings.LoadMasterVolume();
            float sensitivity = sensitivitySlider != null
                ? sensitivitySlider.value
                : GameSettings.LoadMouseSensitivity();
            GameSettings.Save(volume, sensitivity);
        }
    }
}

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
    /// </summary>
    [DefaultExecutionOrder(1010)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(OpeningCutsceneController))]
    public sealed class OpeningGuideController : MonoBehaviour
    {
        public const int TotalPages = 3;

        private static readonly Color BackdropColor = new Color(0.005f, 0.018f, 0.04f, 0.88f);
        private static readonly Color PanelColor = new Color(0.025f, 0.065f, 0.105f, 0.98f);
        private static readonly Color CardColor = new Color(0.025f, 0.045f, 0.075f, 0.98f);
        private static readonly Color MutedTextColor = new Color(0.65f, 0.76f, 0.84f, 1f);
        private static readonly Color Cyan = new Color(0.2f, 0.78f, 1f, 1f);
        private static readonly Color Blue = new Color(0.37f, 0.36f, 1f, 1f);
        private static readonly Color Green = new Color(0.18f, 0.88f, 0.62f, 1f);
        private static readonly Color Amber = new Color(1f, 0.68f, 0.12f, 1f);

        [Header("Opening handoff")]
        [SerializeField] private OpeningCutsceneController openingCutscene;

        [Header("Guide screenshots")]
        [SerializeField] private Texture2D skillImage;
        [SerializeField] private Texture2D baseImage;
        [SerializeField] private Texture2D specialImage;
        [SerializeField] private Texture2D outsideImage;
        [SerializeField] private Texture2D arenaImage;

        [Header("Shared UI style")]
        [SerializeField] private Font hudFont;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite buttonSprite;

        private global::Player.PlayerController _playerController;
        private global::Player.PlayerCombat _playerCombat;
        private global::Player.PlayerAbilityInput _abilityInput;
        private global::Player.PlayerEmoteController _emoteController;
        private global::Player.ThirdPersonCameraController _cameraController;
        private CrosshairUI _crosshair;
        private SettingsMenuController _settingsMenu;
        private StationInteractionController _stationInteraction;

        private GameObject _overlayRoot;
        private readonly GameObject[] _pageRoots = new GameObject[TotalPages];
        private Text _pageTitle;
        private Text _pageSubtitle;
        private Text _progressText;
        private Text _nextLabel;
        private Button _nextButton;

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
            RestorePendingSettings();
            _isOpen = false;
            if (_overlayRoot != null)
            {
                _overlayRoot.SetActive(false);
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

            _hasShown = true;
            ResolveGameplayReferences();
            EnsureEventSystem();
            BuildOverlay();
            CacheGameplay();
            SuspendGameplay();

            _currentPageIndex = 0;
            _isOpen = true;
            _overlayRoot.SetActive(true);
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
            if (_overlayRoot != null)
            {
                _overlayRoot.SetActive(false);
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

        private void BuildOverlay()
        {
            if (_overlayRoot != null)
            {
                return;
            }

            _overlayRoot = new GameObject(
                "Opening Field Guide",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            _overlayRoot.transform.SetParent(transform, false);

            Canvas canvas = _overlayRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = _overlayRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rootRect = _overlayRoot.GetComponent<RectTransform>();
            Stretch(rootRect, Vector2.zero, Vector2.zero);

            Image backdrop = CreateImage("Backdrop", _overlayRoot.transform, BackdropColor);
            Stretch(backdrop.rectTransform, Vector2.zero, Vector2.zero);

            Image panel = CreateImage("Mission Panel", _overlayRoot.transform, PanelColor, panelSprite);
            RectTransform panelRect = panel.rectTransform;
            Anchor(panelRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1680f, 900f));

            Image accent = CreateImage("Accent", panel.transform, Cyan);
            RectTransform accentRect = accent.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.offsetMin = Vector2.zero;
            accentRect.offsetMax = new Vector2(8f, 0f);

            Text kicker = CreateText(panel.transform, "Kicker", 18, FontStyle.Bold, TextAnchor.UpperLeft, Cyan);
            SetRect(kicker.rectTransform, new Vector2(56f, 834f), new Vector2(700f, 30f));
            kicker.text = "NAUT // FIELD GUIDE";

            _pageTitle = CreateText(panel.transform, "Page Title", 42, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
            SetRect(_pageTitle.rectTransform, new Vector2(56f, 770f), new Vector2(1100f, 56f));

            _pageSubtitle = CreateText(panel.transform, "Page Subtitle", 20, FontStyle.Normal, TextAnchor.UpperLeft, MutedTextColor);
            SetRect(_pageSubtitle.rectTransform, new Vector2(58f, 728f), new Vector2(1100f, 38f));

            _progressText = CreateText(panel.transform, "Page Progress", 20, FontStyle.Bold, TextAnchor.MiddleRight, MutedTextColor);
            SetRect(_progressText.rectTransform, new Vector2(1220f, 785f), new Vector2(360f, 44f));

            BuildBasePage(panel.transform);
            BuildWavePage(panel.transform);
            BuildArenaPage(panel.transform);

            Text footer = CreateText(panel.transform, "Skip Hint", 15, FontStyle.Normal, TextAnchor.MiddleLeft, MutedTextColor);
            SetRect(footer.rectTransform, new Vector2(58f, 34f), new Vector2(650f, 44f));
            footer.text = "CLICK NEXT TO SKIP THROUGH  //  ESC SKIPS GUIDE";

            Button skipButton = CreateButton(panel.transform, "Skip Guide", new Vector2(1340f, 836f), new Vector2(240f, 44f), false);
            Text skipLabel = skipButton.GetComponentInChildren<Text>();
            skipLabel.text = "SKIP GUIDE";
            skipLabel.fontSize = 16;
            skipLabel.color = MutedTextColor;
            skipButton.onClick.AddListener(Skip);

            _nextButton = CreateButton(panel.transform, "Next", new Vector2(1320f, 30f), new Vector2(260f, 64f), true);
            _nextLabel = _nextButton.GetComponentInChildren<Text>();
            _nextButton.onClick.AddListener(Advance);

            UiSfxWirer.WireAll(_overlayRoot);
            _overlayRoot.SetActive(false);
        }

        private void BuildBasePage(Transform panel)
        {
            GameObject page = CreatePageRoot(panel, "Page 1 - Base Stations");
            _pageRoots[0] = page;
            CreateStationCard(page.transform, "ARCHIVE // BLUE", "UPGRADE YOUR STATS",
                "Prioritise DAMAGE and MAX AMMO early.", skillImage, Blue, -520f);
            CreateStationCard(page.transform, "SUPPLY // GREEN", "RESTOCK FOR THE NEXT WAVE",
                "Buy health packs and ammo refills. Top up before heading out.", baseImage, Green, 0f);
            CreateStationCard(page.transform, "SPECIAL // YELLOW", "UNLOCK SPECIAL SKILLS",
                "Powerful run-changing skills live here. Check the catalog.", specialImage, Amber, 520f);
        }

        private void BuildWavePage(Transform panel)
        {
            GameObject page = CreatePageRoot(panel, "Page 2 - Start The Wave");
            _pageRoots[1] = page;

            CreateScreenshotFrame(page.transform, "Base Exit", outsideImage,
                new Vector2(-260f, 0f), new Vector2(1010f, 530f), Cyan);

            Image rail = CreateImage("Wave Brief", page.transform, CardColor);
            Anchor(rail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(545f, 0f), new Vector2(470f, 530f));
            AddAccentBar(rail.transform, Cyan);

            Text step = CreateText(rail.transform, "Step", 17, FontStyle.Bold, TextAnchor.UpperLeft, Cyan);
            SetRect(step.rectTransform, new Vector2(34f, 458f), new Vector2(390f, 32f));
            step.text = "DEPLOYMENT // REGULAR WAVE";

            Text body = CreateText(rail.transform, "Body", 25, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
            SetRect(body.rectTransform, new Vector2(34f, 120f), new Vector2(390f, 325f));
            body.text = "Leave the base, then HOLD F to start the first wave.\n\nSurvive until the wave timer reaches zero.\n\nEvery enemy you eliminate pays gold - fight more, earn more, return stronger.";
        }

        private void BuildArenaPage(Transform panel)
        {
            GameObject page = CreatePageRoot(panel, "Page 3 - Arena Contracts");
            _pageRoots[2] = page;

            CreateScreenshotFrame(page.transform, "Arena Arrow", arenaImage,
                new Vector2(-390f, 0f), new Vector2(720f, 570f), Amber);

            Image rail = CreateImage("Arena Brief", page.transform, CardColor);
            Anchor(rail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(440f, 0f), new Vector2(650f, 570f));
            AddAccentBar(rail.transform, Amber);

            Text step = CreateText(rail.transform, "Step", 17, FontStyle.Bold, TextAnchor.UpperLeft, Amber);
            SetRect(step.rectTransform, new Vector2(38f, 496f), new Vector2(540f, 32f));
            step.text = "EVERY 5 WAVES // MANDATORY CONTRACT";

            Text body = CreateText(rail.transform, "Body", 25, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
            SetRect(body.rectTransform, new Vector2(38f, 125f), new Vector2(550f, 350f));
            body.text = "Follow the HUD arrow to the arena.\n\nOnce combat begins, the arena seals. Defeat every enemy to survive - you cannot leave until the contract is complete.\n\nPrepare health, ammo, and upgrades before entering.";

            Text warning = CreateText(rail.transform, "Warning", 18, FontStyle.Bold, TextAnchor.MiddleLeft, Amber);
            SetRect(warning.rectTransform, new Vector2(38f, 48f), new Vector2(550f, 50f));
            warning.text = "NO EXIT // PREPARE BEFORE ENTRY";
        }

        private void CreateStationCard(
            Transform parent,
            string routeLabel,
            string title,
            string body,
            Texture texture,
            Color accentColor,
            float x)
        {
            Image card = CreateImage(title, parent, CardColor);
            Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(480f, 570f));
            AddAccentBar(card.transform, accentColor);

            Text route = CreateText(card.transform, "Route", 15, FontStyle.Bold, TextAnchor.MiddleLeft, accentColor);
            SetRect(route.rectTransform, new Vector2(24f, 516f), new Vector2(420f, 30f));
            route.text = routeLabel;

            CreateScreenshotFrame(card.transform, "Screenshot", texture,
                new Vector2(0f, 65f), new Vector2(430f, 330f), accentColor);

            Text name = CreateText(card.transform, "Title", 20, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
            SetRect(name.rectTransform, new Vector2(24f, 128f), new Vector2(420f, 54f));
            name.text = title;

            Text detail = CreateText(card.transform, "Detail", 18, FontStyle.Normal, TextAnchor.UpperLeft, MutedTextColor);
            SetRect(detail.rectTransform, new Vector2(24f, 28f), new Vector2(425f, 96f));
            detail.text = body;
        }

        private void CreateScreenshotFrame(
            Transform parent,
            string name,
            Texture texture,
            Vector2 position,
            Vector2 size,
            Color accentColor)
        {
            Image frame = CreateImage(name + " Frame", parent, new Color(0.005f, 0.012f, 0.022f, 1f));
            Anchor(frame.rectTransform, new Vector2(0.5f, 0.5f), position, size);

            Image outline = CreateImage("Outline", frame.transform, new Color(accentColor.r, accentColor.g, accentColor.b, 0.72f));
            Stretch(outline.rectTransform, Vector2.zero, Vector2.zero);

            Image inset = CreateImage("Inset", outline.transform, new Color(0.005f, 0.012f, 0.022f, 1f));
            Stretch(inset.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));

            var rawObject = new GameObject("Image", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            rawObject.transform.SetParent(inset.transform, false);
            RawImage raw = rawObject.GetComponent<RawImage>();
            raw.texture = texture;
            raw.color = texture != null ? Color.white : new Color(0.15f, 0.22f, 0.28f, 1f);
            raw.raycastTarget = false;
            Anchor(raw.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(Mathf.Max(1f, size.x - 12f), Mathf.Max(1f, size.y - 12f)));

            AspectRatioFitter fitter = rawObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = texture != null && texture.height > 0
                ? (float)texture.width / texture.height
                : 16f / 9f;
        }

        private static void AddAccentBar(Transform parent, Color color)
        {
            Image bar = CreateImage("Accent", parent, color);
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(5f, 0f);
        }

        private GameObject CreatePageRoot(Transform panel, string name)
        {
            var page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(panel, false);
            RectTransform rect = page.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(58f, 116f);
            rect.offsetMax = new Vector2(-58f, -210f);
            return page;
        }

        private void ShowCurrentPage()
        {
            for (int i = 0; i < _pageRoots.Length; i++)
            {
                if (_pageRoots[i] != null)
                {
                    _pageRoots[i].SetActive(i == _currentPageIndex);
                }
            }

            switch (_currentPageIndex)
            {
                case 0:
                    _pageTitle.text = "PREPARE AT BASE";
                    _pageSubtitle.text = "Three stations. One run. Spend your starting gold before deployment.";
                    break;
                case 1:
                    _pageTitle.text = "START THE WAVE";
                    _pageSubtitle.text = "Leave the safe zone, deploy on your terms, and survive the timer.";
                    break;
                default:
                    _pageTitle.text = "ARENA CONTRACTS";
                    _pageSubtitle.text = "Every fifth wave changes the rules. Follow the arrow and enter ready.";
                    break;
            }

            _progressText.text = BuildProgressLabel(_currentPageIndex);
            _nextLabel.text = _currentPageIndex == TotalPages - 1 ? "BEGIN RUN" : "NEXT  >";
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
            if (EventSystem.current != null && _nextButton != null)
            {
                EventSystem.current.SetSelectedGameObject(_nextButton.gameObject);
            }
        }

        private Button CreateButton(Transform parent, string name, Vector2 position, Vector2 size, bool primary)
        {
            Image image = CreateImage(name, parent,
                primary ? Cyan : new Color(0.05f, 0.11f, 0.16f, 0.85f),
                primary ? buttonSprite : null);
            SetRect(image.rectTransform, position, size);

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = primary ? new Color(0.8f, 1f, 1f, 1f) : new Color(0.18f, 0.28f, 0.36f, 1f);
            colors.pressedColor = primary ? new Color(0.55f, 0.88f, 1f, 1f) : new Color(0.1f, 0.2f, 0.28f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            Text label = CreateText(button.transform, "Label", primary ? 22 : 16,
                FontStyle.Bold, TextAnchor.MiddleCenter, primary ? new Color(0.02f, 0.08f, 0.12f, 1f) : MutedTextColor);
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        private static Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            return image;
        }

        private Text CreateText(
            Transform parent,
            string name,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = hudFont != null
                ? hudFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetRect(RectTransform rect, Vector2 bottomLeft, Vector2 size)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = bottomLeft;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}

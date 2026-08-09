using System.Collections;
using System.Collections.Generic;
using Player.UI;
using Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Leaderboard
{
    /// <summary>Dedicated Leaderboard.unity scene content: two tabs (Furthest Wave / Highest
    /// Score), a top-3 podium, and a paginated 50-row table, styled with the project's real
    /// CartoonSciFi UI kit (assigned by Tools > Leaderboard > Build Leaderboard Scene, baked into
    /// the saved scene - no runtime AssetDatabase access needed) instead of procedural flat
    /// rectangles. A "MAIN MENU" button returns via SceneTransitionController.</summary>
    [DisallowMultipleComponent]
    public sealed class LeaderboardSceneController : MonoBehaviour
    {
        [Header("UI Kit Art - assigned by Tools > Leaderboard > Build Leaderboard Scene")]
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite goldMedalSprite;
        [SerializeField] private Sprite silverMedalSprite;
        [SerializeField] private Sprite bronzeMedalSprite;

        private const int RowsPerPage = LeaderboardsClient.TopPageSize;
        private const float RowHeight = 24f;
        private static readonly Color AccentColor = new Color(1f, 0.85f, 0.25f, 1f);
        private static readonly Color RowColorA = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color RowColorB = new Color(1f, 1f, 1f, 0f);
        // Matches the main menu's own selected-item highlight (light blue), not the gold accent
        // - tinting the button-kit sprite gold/yellow was multiplying into a murky green.
        private static readonly Color SelectedTabColor = new Color(0.55f, 0.8f, 1f, 1f);
        private static readonly Color UnselectedTabColor = new Color(0.15f, 0.18f, 0.24f, 0.95f);

        private Font _font;
        private Button _waveTabButton;
        private Button _scoreTabButton;
        private Text _waveTabLabel;
        private Text _scoreTabLabel;
        private Transform _podiumRoot;
        private Transform _tableContent;
        private Text _pageLabel;
        private Text _statusText;
        private Button _prevButton;
        private Button _nextButton;

        private string _activeLeaderboardId = LeaderboardIds.FurthestWave;
        private int _page;
        private int _requestToken;
        private readonly List<GameObject> _pooledRows = new List<GameObject>();
        private readonly List<GameObject> _podiumEntries = new List<GameObject>();

        private void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
            RefreshTabVisuals();
            StartCoroutine(LoadPageRoutine());

            // Same call MainMenuController makes - MusicManager is a DontDestroyOnLoad singleton
            // so navigating here from the main menu already keeps it playing uninterrupted; this
            // covers opening Leaderboard.unity directly in the Editor, where no MainMenuController
            // ever ran to start it in the first place.
            var musicManager = Audio.MusicManager.Instance;
            if (musicManager != null) musicManager.PlayMusic(musicManager.menuMusic);
        }

        private void Update() => AnimateGlimmer();

        // ---------------------------------------------------------------- Build

        private void Build()
        {
            var canvas = GetComponentInParent<Canvas>();
            Transform canvasTransform = canvas != null ? canvas.transform : transform;

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasTransform, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1000f, 820f);
            panelRect.anchoredPosition = new Vector2(0f, -10f);
            var panelImage = panelGo.GetComponent<Image>();
            if (panelSprite != null)
            {
                panelImage.sprite = panelSprite;
                panelImage.type = Image.Type.Sliced;
                panelImage.color = Color.white;
            }
            else
            {
                panelImage.color = new Color(0.05f, 0.07f, 0.1f, 0.97f);
            }

            BuildTitleAndBack(panelGo.transform, canvasTransform);
            BuildTabs(panelGo.transform);
            BuildPodium(panelGo.transform);
            BuildTable(panelGo.transform);
            BuildPagination(panelGo.transform);

            UiSfxWirer.WireAll(gameObject);
        }

        private void BuildTitleAndBack(Transform parent, Transform canvasTransform)
        {
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(parent, false);
            var title = titleGo.GetComponent<Text>();
            title.font = _font;
            title.fontSize = 34;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            title.text = "LEADERBOARD";
            var titleRect = (RectTransform)titleGo.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -28f);
            titleRect.sizeDelta = new Vector2(-40f, 44f);

            var backGo = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
            backGo.transform.SetParent(canvasTransform, false);
            var backRect = (RectTransform)backGo.transform;
            backRect.anchorMin = new Vector2(0f, 1f);
            backRect.anchorMax = new Vector2(0f, 1f);
            backRect.pivot = new Vector2(0f, 1f);
            backRect.sizeDelta = new Vector2(200f, 56f);
            backRect.anchoredPosition = new Vector2(32f, -32f);
            var backImage = backGo.GetComponent<Image>();
            if (buttonSprite != null)
            {
                backImage.sprite = buttonSprite;
                backImage.type = Image.Type.Sliced;
                backImage.color = Color.white;
            }
            else
            {
                backImage.color = new Color(0.15f, 0.18f, 0.24f, 0.95f);
            }

            var backLabelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            backLabelGo.transform.SetParent(backGo.transform, false);
            var backLabel = backLabelGo.GetComponent<Text>();
            backLabel.font = _font;
            backLabel.fontSize = 16;
            backLabel.fontStyle = FontStyle.Bold;
            backLabel.alignment = TextAnchor.MiddleCenter;
            backLabel.color = Color.white;
            backLabel.text = "< MAIN MENU";
            var backLabelRect = (RectTransform)backLabelGo.transform;
            backLabelRect.anchorMin = Vector2.zero;
            backLabelRect.anchorMax = Vector2.one;
            backLabelRect.offsetMin = new Vector2(8f, 4f);
            backLabelRect.offsetMax = new Vector2(-8f, -8f);

            var backButton = backGo.GetComponent<Button>();
            backButton.targetGraphic = backImage;
            backButton.onClick.AddListener(() => SceneTransitionController.LoadScene("MainMenu"));
        }

        private void BuildTabs(Transform parent)
        {
            _waveTabButton = CreateTabButton(parent, "WaveTab", "FURTHEST WAVE", new Vector2(-160f, -88f), out _waveTabLabel);
            _scoreTabButton = CreateTabButton(parent, "ScoreTab", "HIGHEST SCORE", new Vector2(160f, -88f), out _scoreTabLabel);

            _waveTabButton.onClick.AddListener(() => SwitchTab(LeaderboardIds.FurthestWave));
            _scoreTabButton.onClick.AddListener(() => SwitchTab(LeaderboardIds.HighestScore));
        }

        private Button CreateTabButton(Transform parent, string name, string label, Vector2 anchoredPosition, out Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(300f, 48f);
            rect.anchoredPosition = anchoredPosition;
            var tabImage = go.GetComponent<Image>();
            if (buttonSprite != null)
            {
                tabImage.sprite = buttonSprite;
                tabImage.type = Image.Type.Sliced;
            }
            tabImage.color = new Color(0.15f, 0.18f, 0.24f, 0.95f);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            labelText = textGo.GetComponent<Text>();
            labelText.font = _font;
            labelText.fontSize = 19;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(4f, 4f);
            textRect.offsetMax = new Vector2(-4f, -6f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = tabImage;
            return button;
        }

        private void BuildPodium(Transform parent)
        {
            var podiumGo = new GameObject("Podium", typeof(RectTransform));
            podiumGo.transform.SetParent(parent, false);
            var rect = (RectTransform)podiumGo.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(900f, 200f);
            rect.anchoredPosition = new Vector2(0f, -134f);
            _podiumRoot = podiumGo.transform;

            CreatePodiumSlot(1, new Vector2(0f, 0f), 186f, new Color(0.85f, 0.68f, 0.15f, 1f), goldMedalSprite);
            CreatePodiumSlot(2, new Vector2(-230f, -16f), 168f, new Color(0.6f, 0.62f, 0.66f, 1f), silverMedalSprite);
            CreatePodiumSlot(3, new Vector2(230f, -26f), 158f, new Color(0.65f, 0.42f, 0.24f, 1f), bronzeMedalSprite);
        }

        private void CreatePodiumSlot(int rank, Vector2 anchoredPosition, float height, Color fallbackColor, Sprite sprite)
        {
            var slotGo = new GameObject($"PodiumSlot{rank}", typeof(RectTransform));
            slotGo.transform.SetParent(_podiumRoot, false);
            var slotRect = (RectTransform)slotGo.transform;
            slotRect.anchorMin = new Vector2(0.5f, 1f);
            slotRect.anchorMax = new Vector2(0.5f, 1f);
            slotRect.pivot = new Vector2(0.5f, 1f);
            slotRect.sizeDelta = new Vector2(240f, height);
            slotRect.anchoredPosition = anchoredPosition;

            var rankGo = new GameObject("Rank", typeof(RectTransform), typeof(Text));
            rankGo.transform.SetParent(slotGo.transform, false);
            var rankText = rankGo.GetComponent<Text>();
            rankText.font = _font;
            rankText.fontSize = 20;
            rankText.fontStyle = FontStyle.Bold;
            rankText.alignment = TextAnchor.MiddleCenter;
            rankText.color = fallbackColor;
            rankText.text = rank switch { 1 => "1ST", 2 => "2ND", _ => "3RD" };
            var rankRect = (RectTransform)rankGo.transform;
            rankRect.anchorMin = new Vector2(0.5f, 1f);
            rankRect.anchorMax = new Vector2(0.5f, 1f);
            rankRect.pivot = new Vector2(0.5f, 1f);
            rankRect.sizeDelta = new Vector2(240f, 24f);
            rankRect.anchoredPosition = Vector2.zero;

            var glowGo = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(slotGo.transform, false);
            var glowRect = (RectTransform)glowGo.transform;
            glowRect.anchorMin = new Vector2(0.5f, 1f);
            glowRect.anchorMax = new Vector2(0.5f, 1f);
            glowRect.pivot = new Vector2(0.5f, 1f);
            glowRect.sizeDelta = new Vector2(82f, 82f);
            glowRect.anchoredPosition = new Vector2(0f, -24f);
            var glowImage = glowGo.GetComponent<Image>();
            glowImage.sprite = RuntimeUiSprites.Circle;
            glowImage.color = new Color(fallbackColor.r, fallbackColor.g, fallbackColor.b, 0.35f);

            if (sprite != null)
            {
                var medalGo = new GameObject("Medal", typeof(RectTransform), typeof(Image));
                medalGo.transform.SetParent(slotGo.transform, false);
                var medalRect = (RectTransform)medalGo.transform;
                medalRect.anchorMin = new Vector2(0.5f, 1f);
                medalRect.anchorMax = new Vector2(0.5f, 1f);
                medalRect.pivot = new Vector2(0.5f, 1f);
                medalRect.sizeDelta = new Vector2(80f, 94f);
                medalRect.anchoredPosition = new Vector2(0f, -6f);
                var medalImage = medalGo.GetComponent<Image>();
                medalImage.sprite = sprite;
                medalImage.color = Color.white;
                medalImage.preserveAspect = true;
            }
            else
            {
                var medalMaskGo = new GameObject("MedalMask", typeof(RectTransform), typeof(Image), typeof(Mask));
                medalMaskGo.transform.SetParent(slotGo.transform, false);
                var medalMaskRect = (RectTransform)medalMaskGo.transform;
                medalMaskRect.anchorMin = new Vector2(0.5f, 1f);
                medalMaskRect.anchorMax = new Vector2(0.5f, 1f);
                medalMaskRect.pivot = new Vector2(0.5f, 1f);
                medalMaskRect.sizeDelta = new Vector2(70f, 70f);
                medalMaskRect.anchoredPosition = new Vector2(0f, -20f);
                var medalImage = medalMaskGo.GetComponent<Image>();
                medalMaskGo.GetComponent<Mask>().showMaskGraphic = true;
                medalImage.sprite = RuntimeUiSprites.Circle;
                medalImage.color = fallbackColor;

                var glimmerGo = new GameObject("Glimmer", typeof(RectTransform), typeof(Image));
                glimmerGo.transform.SetParent(medalMaskGo.transform, false);
                var glimmerRect = (RectTransform)glimmerGo.transform;
                glimmerRect.anchorMin = new Vector2(0.5f, 0.5f);
                glimmerRect.anchorMax = new Vector2(0.5f, 0.5f);
                glimmerRect.sizeDelta = new Vector2(16f, 160f);
                glimmerRect.localRotation = Quaternion.Euler(0f, 0f, 28f);
                glimmerGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.4f);
            }

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(slotGo.transform, false);
            var nameText = nameGo.GetComponent<Text>();
            nameText.font = _font;
            nameText.fontSize = 19;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.text = "-";
            var nameRect = (RectTransform)nameGo.transform;
            nameRect.anchorMin = new Vector2(0.5f, 1f);
            nameRect.anchorMax = new Vector2(0.5f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.sizeDelta = new Vector2(240f, 22f);
            nameRect.anchoredPosition = new Vector2(0f, -108f);

            var valueGo = new GameObject("Value", typeof(RectTransform), typeof(Text));
            valueGo.transform.SetParent(slotGo.transform, false);
            var valueText = valueGo.GetComponent<Text>();
            valueText.font = _font;
            valueText.fontSize = 17;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.color = AccentColor;
            valueText.text = "";
            var valueRect = (RectTransform)valueGo.transform;
            valueRect.anchorMin = new Vector2(0.5f, 1f);
            valueRect.anchorMax = new Vector2(0.5f, 1f);
            valueRect.pivot = new Vector2(0.5f, 1f);
            valueRect.sizeDelta = new Vector2(240f, 20f);
            valueRect.anchoredPosition = new Vector2(0f, -130f);

            _podiumEntries.Add(slotGo);
        }

        private void BuildTable(Transform parent)
        {
            var viewportGo = new GameObject("TableViewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(parent, false);
            var viewportRect = (RectTransform)viewportGo.transform;
            viewportRect.anchorMin = new Vector2(0.5f, 0f);
            viewportRect.anchorMax = new Vector2(0.5f, 1f);
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.sizeDelta = new Vector2(860f, 0f);
            viewportRect.offsetMin = new Vector2(-430f, 92f);
            viewportRect.offsetMax = new Vector2(430f, -360f);
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var scrollGo = new GameObject("TableScroll", typeof(RectTransform));
            var scrollRect = scrollGo.AddComponent<Player.UI.Progression.SmoothStationScrollRect>();
            scrollGo.transform.SetParent(viewportGo.transform, false);
            var scrollRectTransform = (RectTransform)scrollGo.transform;
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;

            var contentGo = new GameObject("TableContent", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRect = (RectTransform)contentGo.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.spacing = 1f;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            _tableContent = contentGo.transform;

            var statusGo = new GameObject("Status", typeof(RectTransform), typeof(Text));
            statusGo.transform.SetParent(viewportGo.transform, false);
            _statusText = statusGo.GetComponent<Text>();
            _statusText.font = _font;
            _statusText.fontSize = 18;
            _statusText.alignment = TextAnchor.MiddleCenter;
            _statusText.color = new Color(1f, 1f, 1f, 0.7f);
            var statusRect = (RectTransform)statusGo.transform;
            statusRect.anchorMin = Vector2.zero;
            statusRect.anchorMax = Vector2.one;
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            _statusText.gameObject.SetActive(false);
        }

        private void BuildPagination(Transform parent)
        {
            _prevButton = CreateArrowButton(parent, "PrevPage", "<", new Vector2(-420f, 32f));
            _nextButton = CreateArrowButton(parent, "NextPage", ">", new Vector2(420f, 32f));
            _prevButton.onClick.AddListener(() => ChangePage(-1));
            _nextButton.onClick.AddListener(() => ChangePage(1));

            var pageLabelGo = new GameObject("PageLabel", typeof(RectTransform), typeof(Text));
            pageLabelGo.transform.SetParent(parent, false);
            _pageLabel = pageLabelGo.GetComponent<Text>();
            _pageLabel.font = _font;
            _pageLabel.fontSize = 16;
            _pageLabel.alignment = TextAnchor.MiddleCenter;
            _pageLabel.color = Color.white;
            var pageLabelRect = (RectTransform)pageLabelGo.transform;
            pageLabelRect.anchorMin = new Vector2(0.5f, 0f);
            pageLabelRect.anchorMax = new Vector2(0.5f, 0f);
            pageLabelRect.pivot = new Vector2(0.5f, 0f);
            pageLabelRect.sizeDelta = new Vector2(200f, 32f);
            pageLabelRect.anchoredPosition = new Vector2(0f, 24f);
        }

        private Button CreateArrowButton(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(56f, 44f);
            rect.anchoredPosition = anchoredPosition;
            var arrowImage = go.GetComponent<Image>();
            if (buttonSprite != null)
            {
                arrowImage.sprite = buttonSprite;
                arrowImage.type = Image.Type.Sliced;
            }
            arrowImage.color = new Color(0.15f, 0.18f, 0.24f, 0.95f);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = _font;
            text.fontSize = 22;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var button = go.GetComponent<Button>();
            button.targetGraphic = arrowImage;
            return button;
        }

        // ---------------------------------------------------------------- Behaviour

        private void SwitchTab(string leaderboardId)
        {
            if (_activeLeaderboardId == leaderboardId) return;
            _activeLeaderboardId = leaderboardId;
            _page = 0;
            RefreshTabVisuals();
            StartCoroutine(LoadPageRoutine());
        }

        private void RefreshTabVisuals()
        {
            bool waveActive = _activeLeaderboardId == LeaderboardIds.FurthestWave;
            ApplyTabState(_waveTabButton, _waveTabLabel, waveActive);
            ApplyTabState(_scoreTabButton, _scoreTabLabel, !waveActive);

            _podiumRoot.gameObject.SetActive(_page == 0);
        }

        // Selected state drops the button-kit sprite entirely for a flat solid fill - tinting the
        // sprite's own baked-in shading never reads as a clean, uniformly readable color, which
        // is why selected-tab text was unreadable before.
        private void ApplyTabState(Button button, Text label, bool selected)
        {
            if (selected)
            {
                button.image.sprite = null;
                button.image.type = Image.Type.Simple;
                button.image.color = SelectedTabColor;
                label.color = new Color(0.05f, 0.1f, 0.2f, 1f);
            }
            else
            {
                button.image.sprite = buttonSprite;
                button.image.type = buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
                button.image.color = UnselectedTabColor;
                label.color = Color.white;
            }
        }

        private void ChangePage(int delta)
        {
            int nextPage = Mathf.Max(0, _page + delta);
            if (nextPage == _page) return;
            _page = nextPage;
            _podiumRoot.gameObject.SetActive(_page == 0);
            StartCoroutine(LoadPageRoutine());
        }

        private IEnumerator LoadPageRoutine()
        {
            int token = ++_requestToken;
            SetStatus("Loading...");
            ClearTable();
            SetPodiumPlaceholder();
            _prevButton.interactable = false;
            _nextButton.interactable = false;

            var task = LeaderboardsClient.GetTopAsync(_activeLeaderboardId, _page * RowsPerPage);
            while (!task.IsCompleted) yield return null;
            if (token != _requestToken) yield break;

            if (task.IsFaulted || task.Result == null)
            {
                SetStatus("Unable to load leaderboard. Check your connection and try again.");
                _prevButton.interactable = _page > 0;
                yield break;
            }

            IReadOnlyList<LeaderboardEntry> results = task.Result.Results;
            _pageLabel.text = $"PAGE {_page + 1}";
            _prevButton.interactable = _page > 0;
            _nextButton.interactable = results != null && results.Count >= RowsPerPage;

            if (results == null || results.Count == 0)
            {
                SetStatus(_page == 0 ? "No scores yet - be the first!" : "No more entries.");
                yield break;
            }

            SetStatus(null);
            PopulatePage(results);
        }

        private void PopulatePage(IReadOnlyList<LeaderboardEntry> results)
        {
            bool isWaveTab = _activeLeaderboardId == LeaderboardIds.FurthestWave;
            int index = 0;

            if (_page == 0)
            {
                for (; index < results.Count && index < 3; index++)
                {
                    ApplyPodiumEntry(index, results[index], isWaveTab);
                }
            }

            for (; index < results.Count; index++)
            {
                CreateTableRow(results[index], isWaveTab, index % 2 == 0);
            }
        }

        private void ApplyPodiumEntry(int slotIndex, LeaderboardEntry entry, bool isWaveTab)
        {
            Transform slot = _podiumEntries[slotIndex].transform;
            Text nameText = slot.Find("Name").GetComponent<Text>();
            Text valueText = slot.Find("Value").GetComponent<Text>();
            nameText.text = string.IsNullOrEmpty(entry.PlayerName) ? "ANONYMOUS" : entry.PlayerName.ToUpperInvariant();
            valueText.text = isWaveTab
                ? $"WAVE {Mathf.RoundToInt((float)entry.Score)}"
                : $"{Mathf.RoundToInt((float)entry.Score)} PTS";
        }

        private void SetPodiumPlaceholder()
        {
            foreach (GameObject slot in _podiumEntries)
            {
                slot.transform.Find("Name").GetComponent<Text>().text = "-";
                slot.transform.Find("Value").GetComponent<Text>().text = "";
            }
        }

        private void CreateTableRow(LeaderboardEntry entry, bool isWaveTab, bool alternate)
        {
            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rowGo.transform.SetParent(_tableContent, false);
            rowGo.GetComponent<LayoutElement>().preferredHeight = RowHeight;
            var rowImage = rowGo.GetComponent<Image>();
            if (buttonSprite != null)
            {
                // Same button-kit sprite as the tabs/pagination, tinted low-alpha, so rows read
                // as a UI component rather than a bare translucent rectangle.
                rowImage.sprite = buttonSprite;
                rowImage.type = Image.Type.Sliced;
                rowImage.color = alternate ? new Color(1f, 1f, 1f, 0.18f) : new Color(1f, 1f, 1f, 0.06f);
            }
            else
            {
                rowImage.color = alternate ? RowColorA : RowColorB;
            }
            _pooledRows.Add(rowGo);

            Text rankText = CreateRowLabel(rowGo.transform, $"#{entry.Rank + 1}", TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0.12f, 1f));
            rankText.color = new Color(1f, 1f, 1f, 0.6f);
            rankText.fontSize = 15;

            Text nameText = CreateRowLabel(rowGo.transform,
                string.IsNullOrEmpty(entry.PlayerName) ? "Anonymous" : entry.PlayerName,
                TextAnchor.MiddleLeft, new Vector2(0.12f, 0f), new Vector2(0.55f, 1f));
            nameText.color = Color.white;
            nameText.fontSize = 15;

            // Left-aligned starting right after the name column (rather than right-aligned
            // against the row's far edge) so the value grows away from the edge instead of
            // toward it - moved further left overall per repeated clipping reports.
            Text valueText = CreateRowLabel(rowGo.transform,
                isWaveTab ? $"Wave {Mathf.RoundToInt((float)entry.Score)}" : $"{Mathf.RoundToInt((float)entry.Score)} pts",
                TextAnchor.MiddleLeft, new Vector2(0.58f, 0f), new Vector2(0.85f, 1f));
            valueText.color = AccentColor;
            valueText.fontSize = 15;
        }

        private Text CreateRowLabel(Transform parent, string content, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = 16;
            text.alignment = anchor;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(10f, 0f);
            rect.offsetMax = new Vector2(-10f, 0f);
            return text;
        }

        private void ClearTable()
        {
            foreach (GameObject row in _pooledRows)
            {
                if (row != null) Destroy(row);
            }
            _pooledRows.Clear();
        }

        private void SetStatus(string message)
        {
            if (_statusText == null) return;
            bool show = !string.IsNullOrEmpty(message);
            _statusText.gameObject.SetActive(show);
            if (show) _statusText.text = message;
        }

        private void AnimateGlimmer()
        {
            float t = Mathf.PingPong(Time.unscaledTime * 0.6f, 1f);
            foreach (GameObject slot in _podiumEntries)
            {
                Transform glimmer = slot.transform.Find("MedalMask/Glimmer");
                if (glimmer == null) continue;
                var rect = (RectTransform)glimmer;
                rect.anchoredPosition = new Vector2(Mathf.Lerp(-90f, 90f, t), 0f);
            }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using Player.UI;
using Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Leaderboard
{
    /// <summary>Self-built modal leaderboard popup: two tabs (Furthest Wave / Highest Score), a
    /// top-3 podium with medal art and a glimmer sweep, and a scrollable/paginated table below
    /// (50 entries per page, matching LeaderboardsClient.TopPageSize) capped with prev/next arrow
    /// buttons rather than infinite scroll. Instantiated lazily by
    /// MainMenuLeaderboardEntryPoint - there is exactly one of these per main menu session.
    /// Assign goldMedalSprite/silverMedalSprite/bronzeMedalSprite in the Inspector once the
    /// downloaded medal pack is imported as Sprites; until then the podium falls back to flat
    /// colored circles so the feature works without that manual import step.</summary>
    [DisallowMultipleComponent]
    public sealed class LeaderboardPopupController : MonoBehaviour
    {
        [Header("Optional medal art - falls back to flat colored circles if unassigned")]
        [SerializeField] private Sprite goldMedalSprite;
        [SerializeField] private Sprite silverMedalSprite;
        [SerializeField] private Sprite bronzeMedalSprite;

        private const int RowsPerPage = LeaderboardsClient.TopPageSize;
        private static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.1f, 0.97f);
        private static readonly Color RowColorA = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color RowColorB = new Color(1f, 1f, 1f, 0.0f);
        private static readonly Color AccentColor = new Color(1f, 0.85f, 0.25f, 1f);

        private Font _font;
        private GameObject _root;
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

        /// Must be called before the first Open() (i.e. right after AddComponent) - the podium is
        /// built lazily on first Open() and reads these fields at that point.
        public void ConfigureMedals(Sprite gold, Sprite silver, Sprite bronze)
        {
            goldMedalSprite = gold;
            silverMedalSprite = silver;
            bronzeMedalSprite = bronze;
        }

        public void Open()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_root == null) Build();
            _root.SetActive(true);
            _page = 0;
            _activeLeaderboardId = LeaderboardIds.FurthestWave;
            RefreshTabVisuals();
            StartCoroutine(LoadPageRoutine());
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void Update()
        {
            if (_root == null || !_root.activeSelf) return;
            AnimateGlimmer();
        }

        // ---------------------------------------------------------------- Build

        private void Build()
        {
            var rootRect = gameObject.GetComponent<RectTransform>();
            if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            _root = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(transform, false);
            var backdropRect = (RectTransform)_root.transform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_root.transform, false);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(900f, 780f);
            panelRect.anchoredPosition = Vector2.zero;
            var panelImage = panelGo.GetComponent<Image>();
            panelImage.sprite = RuntimeUiSprites.RoundedRect;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = PanelColor;

            // Thin accent border - a second rounded-rect one step behind and larger than the
            // panel, matching the console's cyan-outline look instead of a hard flat edge.
            var borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(_root.transform, false);
            borderGo.transform.SetSiblingIndex(panelGo.transform.GetSiblingIndex());
            var borderRect = (RectTransform)borderGo.transform;
            borderRect.anchorMin = new Vector2(0.5f, 0.5f);
            borderRect.anchorMax = new Vector2(0.5f, 0.5f);
            borderRect.sizeDelta = new Vector2(906f, 786f);
            borderRect.anchoredPosition = Vector2.zero;
            var borderImage = borderGo.GetComponent<Image>();
            borderImage.sprite = RuntimeUiSprites.RoundedRect;
            borderImage.type = Image.Type.Sliced;
            borderImage.color = new Color(0.35f, 0.75f, 0.95f, 0.5f);

            BuildTitleAndClose(panelGo.transform);
            BuildTabs(panelGo.transform);
            BuildPodium(panelGo.transform);
            BuildTable(panelGo.transform);
            BuildPagination(panelGo.transform);

            UiSfxWirer.WireAll(_root);
        }

        private void BuildTitleAndClose(Transform parent)
        {
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(parent, false);
            var title = titleGo.GetComponent<Text>();
            title.font = _font;
            title.fontSize = 26;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            title.text = "LEADERBOARD";
            var titleRect = (RectTransform)titleGo.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -20f);
            titleRect.sizeDelta = new Vector2(-160f, 40f);

            var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(parent, false);
            var closeRect = (RectTransform)closeGo.transform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(48f, 48f);
            closeRect.anchoredPosition = new Vector2(-16f, -16f);
            var closeImage = closeGo.GetComponent<Image>();
            closeImage.sprite = RuntimeUiSprites.RoundedRect;
            closeImage.type = Image.Type.Sliced;
            closeImage.color = new Color(0.6f, 0.2f, 0.2f, 0.9f);
            closeGo.GetComponent<Button>().onClick.AddListener(Close);

            var closeLabelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            closeLabelGo.transform.SetParent(closeGo.transform, false);
            var closeLabel = closeLabelGo.GetComponent<Text>();
            closeLabel.font = _font;
            closeLabel.fontSize = 22;
            closeLabel.fontStyle = FontStyle.Bold;
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeLabel.color = Color.white;
            closeLabel.text = "X";
            var closeLabelRect = (RectTransform)closeLabelGo.transform;
            closeLabelRect.anchorMin = Vector2.zero;
            closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.offsetMin = Vector2.zero;
            closeLabelRect.offsetMax = Vector2.zero;
        }

        private void BuildTabs(Transform parent)
        {
            _waveTabButton = CreateTabButton(parent, "WaveTab", "FURTHEST WAVE", new Vector2(-160f, -68f), out _waveTabLabel);
            _scoreTabButton = CreateTabButton(parent, "ScoreTab", "HIGHEST SCORE", new Vector2(160f, -68f), out _scoreTabLabel);

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
            rect.sizeDelta = new Vector2(280f, 44f);
            rect.anchoredPosition = anchoredPosition;
            var tabImage = go.GetComponent<Image>();
            tabImage.sprite = RuntimeUiSprites.RoundedRect;
            tabImage.type = Image.Type.Sliced;
            tabImage.color = new Color(0.15f, 0.18f, 0.24f, 0.95f);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            labelText = textGo.GetComponent<Text>();
            labelText.font = _font;
            labelText.fontSize = 16;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

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
            rect.sizeDelta = new Vector2(820f, 270f);
            rect.anchoredPosition = new Vector2(0f, -108f);
            _podiumRoot = podiumGo.transform;

            CreatePodiumSlot(1, new Vector2(0f, 0f), 260f, new Color(0.85f, 0.68f, 0.15f, 1f), goldMedalSprite);
            CreatePodiumSlot(2, new Vector2(-260f, -22f), 234f, new Color(0.6f, 0.62f, 0.66f, 1f), silverMedalSprite);
            CreatePodiumSlot(3, new Vector2(260f, -36f), 220f, new Color(0.65f, 0.42f, 0.24f, 1f), bronzeMedalSprite);
        }

        private void CreatePodiumSlot(int rank, Vector2 anchoredPosition, float height, Color fallbackColor, Sprite sprite)
        {
            var slotGo = new GameObject($"PodiumSlot{rank}", typeof(RectTransform));
            slotGo.transform.SetParent(_podiumRoot, false);
            var slotRect = (RectTransform)slotGo.transform;
            slotRect.anchorMin = new Vector2(0.5f, 1f);
            slotRect.anchorMax = new Vector2(0.5f, 1f);
            slotRect.pivot = new Vector2(0.5f, 1f);
            slotRect.sizeDelta = new Vector2(220f, height);
            slotRect.anchoredPosition = anchoredPosition;

            // Rank badge sits above the medal (1ST / 2ND / 3RD) so the podium reads at a glance.
            var rankGo = new GameObject("Rank", typeof(RectTransform), typeof(Text));
            rankGo.transform.SetParent(slotGo.transform, false);
            var rankText = rankGo.GetComponent<Text>();
            rankText.font = _font;
            rankText.fontSize = 18;
            rankText.fontStyle = FontStyle.Bold;
            rankText.alignment = TextAnchor.MiddleCenter;
            rankText.color = fallbackColor;
            rankText.text = rank switch { 1 => "1ST", 2 => "2ND", _ => "3RD" };
            var rankRect = (RectTransform)rankGo.transform;
            rankRect.anchorMin = new Vector2(0.5f, 1f);
            rankRect.anchorMax = new Vector2(0.5f, 1f);
            rankRect.pivot = new Vector2(0.5f, 1f);
            rankRect.sizeDelta = new Vector2(220f, 24f);
            rankRect.anchoredPosition = new Vector2(0f, 0f);

            // A soft glow ring behind the medal, slightly larger and dimmer than the medal itself.
            var glowGo = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(slotGo.transform, false);
            var glowRect = (RectTransform)glowGo.transform;
            glowRect.anchorMin = new Vector2(0.5f, 1f);
            glowRect.anchorMax = new Vector2(0.5f, 1f);
            glowRect.pivot = new Vector2(0.5f, 1f);
            glowRect.sizeDelta = new Vector2(114f, 114f);
            glowRect.anchoredPosition = new Vector2(0f, -34f);
            var glowImage = glowGo.GetComponent<Image>();
            glowImage.sprite = RuntimeUiSprites.Circle;
            glowImage.color = new Color(fallbackColor.r, fallbackColor.g, fallbackColor.b, 0.35f);

            if (sprite != null)
            {
                // Real medal art (ribbon + medallion) is not circular - show it whole with its
                // natural aspect ratio rather than square-cropping it into a masked disc, which
                // was clipping the glimmer sweep against the ribbon's own shape and reading as a
                // stray white smear. No glimmer overlay on real art; the source art already has
                // its own shading/detail.
                var medalGo = new GameObject("Medal", typeof(RectTransform), typeof(Image));
                medalGo.transform.SetParent(slotGo.transform, false);
                var medalRect = (RectTransform)medalGo.transform;
                medalRect.anchorMin = new Vector2(0.5f, 1f);
                medalRect.anchorMax = new Vector2(0.5f, 1f);
                medalRect.pivot = new Vector2(0.5f, 1f);
                medalRect.sizeDelta = new Vector2(112f, 132f);
                medalRect.anchoredPosition = new Vector2(0f, -8f);
                var medalImage = medalGo.GetComponent<Image>();
                medalImage.sprite = sprite;
                medalImage.color = Color.white;
                medalImage.preserveAspect = true;
            }
            else
            {
                // Fallback flat-color disc (no medal art assigned yet) - circular Mask clips the
                // glimmer sweep child to the actual circle shape rather than its square bounds.
                var medalMaskGo = new GameObject("MedalMask", typeof(RectTransform), typeof(Image), typeof(Mask));
                medalMaskGo.transform.SetParent(slotGo.transform, false);
                var medalMaskRect = (RectTransform)medalMaskGo.transform;
                medalMaskRect.anchorMin = new Vector2(0.5f, 1f);
                medalMaskRect.anchorMax = new Vector2(0.5f, 1f);
                medalMaskRect.pivot = new Vector2(0.5f, 1f);
                medalMaskRect.sizeDelta = new Vector2(96f, 96f);
                medalMaskRect.anchoredPosition = new Vector2(0f, -28f);
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
                var glimmerImage = glimmerGo.GetComponent<Image>();
                glimmerImage.color = new Color(1f, 1f, 1f, 0.4f);
            }

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(slotGo.transform, false);
            var nameText = nameGo.GetComponent<Text>();
            nameText.font = _font;
            nameText.fontSize = 16;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.text = "-";
            var nameRect = (RectTransform)nameGo.transform;
            nameRect.anchorMin = new Vector2(0.5f, 1f);
            nameRect.anchorMax = new Vector2(0.5f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.sizeDelta = new Vector2(220f, 26f);
            nameRect.anchoredPosition = new Vector2(0f, -152f);

            var valueGo = new GameObject("Value", typeof(RectTransform), typeof(Text));
            valueGo.transform.SetParent(slotGo.transform, false);
            var valueText = valueGo.GetComponent<Text>();
            valueText.font = _font;
            valueText.fontSize = 15;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.color = AccentColor;
            valueText.text = "";
            var valueRect = (RectTransform)valueGo.transform;
            valueRect.anchorMin = new Vector2(0.5f, 1f);
            valueRect.anchorMax = new Vector2(0.5f, 1f);
            valueRect.pivot = new Vector2(0.5f, 1f);
            valueRect.sizeDelta = new Vector2(220f, 22f);
            valueRect.anchoredPosition = new Vector2(0f, -178f);

            // Base block beneath the medal, height scaled by rank - the podium "step". Its own
            // height (not the whole slot height) drives the visible tier difference between ranks.
            var stepGo = new GameObject("Step", typeof(RectTransform), typeof(Image));
            stepGo.transform.SetParent(slotGo.transform, false);
            var stepRect = (RectTransform)stepGo.transform;
            stepRect.anchorMin = new Vector2(0.5f, 1f);
            stepRect.anchorMax = new Vector2(0.5f, 1f);
            stepRect.pivot = new Vector2(0.5f, 1f);
            stepRect.sizeDelta = new Vector2(176f, Mathf.Max(24f, height - 198f));
            stepRect.anchoredPosition = new Vector2(0f, -198f);
            var stepImage = stepGo.GetComponent<Image>();
            stepImage.sprite = RuntimeUiSprites.RoundedRect;
            stepImage.type = Image.Type.Sliced;
            stepImage.color = new Color(fallbackColor.r, fallbackColor.g, fallbackColor.b, 0.22f);
            // Sits behind the medal/name/value siblings drawn above it, not on top of them.
            stepGo.transform.SetAsFirstSibling();

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
            viewportRect.sizeDelta = new Vector2(820f, 0f);
            viewportRect.offsetMin = new Vector2(-410f, 92f);
            viewportRect.offsetMax = new Vector2(410f, -400f);
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
            layout.spacing = 2f;

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
            _prevButton = CreateArrowButton(parent, "PrevPage", "<", new Vector2(-380f, 32f));
            _nextButton = CreateArrowButton(parent, "NextPage", ">", new Vector2(380f, 32f));
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
            arrowImage.sprite = RuntimeUiSprites.RoundedRect;
            arrowImage.type = Image.Type.Sliced;
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
            _waveTabButton.image.color = waveActive
                ? AccentColor
                : new Color(0.15f, 0.18f, 0.24f, 0.95f);
            _waveTabLabel.color = waveActive ? Color.black : Color.white;

            _scoreTabButton.image.color = !waveActive
                ? AccentColor
                : new Color(0.15f, 0.18f, 0.24f, 0.95f);
            _scoreTabLabel.color = !waveActive ? Color.black : Color.white;

            _podiumRoot.gameObject.SetActive(_page == 0);
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
            if (token != _requestToken) yield break; // superseded by a newer request

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
            rowGo.GetComponent<Image>().color = alternate ? RowColorA : RowColorB;
            rowGo.GetComponent<LayoutElement>().preferredHeight = 34f;
            _pooledRows.Add(rowGo);

            Text rankText = CreateRowLabel(rowGo.transform, $"#{entry.Rank + 1}", TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0.15f, 1f));
            rankText.color = new Color(1f, 1f, 1f, 0.6f);

            Text nameText = CreateRowLabel(rowGo.transform,
                string.IsNullOrEmpty(entry.PlayerName) ? "Anonymous" : entry.PlayerName,
                TextAnchor.MiddleLeft, new Vector2(0.15f, 0f), new Vector2(0.65f, 1f));
            nameText.color = Color.white;

            Text valueText = CreateRowLabel(rowGo.transform,
                isWaveTab ? $"Wave {Mathf.RoundToInt((float)entry.Score)}" : $"{Mathf.RoundToInt((float)entry.Score)} pts",
                TextAnchor.MiddleRight, new Vector2(0.65f, 0f), new Vector2(1f, 1f));
            valueText.color = AccentColor;
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
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(12f, 0f);
            rect.offsetMax = new Vector2(-12f, 0f);
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

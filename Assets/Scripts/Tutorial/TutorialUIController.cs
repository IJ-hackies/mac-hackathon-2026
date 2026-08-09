using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tutorial
{
    /// Builds and drives every piece of tutorial UI at runtime: a top breadcrumb of every stage
    /// (dimmed/current/completed), a fading stage banner, the bottom key-prompt row (highlights
    /// and pulses each key as it's pressed/completed), the attack-counter text, the three
    /// item-pickup icons, and a modal info popup (dim full-screen backdrop, X close button,
    /// PopupClosed event) for TutorialZone messages - TutorialManager suspends player movement/
    /// look for the popup's lifetime, this component only presents it. There is no completion
    /// panel - reaching the Exit Zone returns to MainMenu immediately (see TutorialManager), no
    /// confirmation needed.
    /// Self-building (unlike the HUD scripts under Player/UI, which are built by an editor tool
    /// and only bound at runtime) since this is a standalone subsystem with no existing HUD
    /// pattern to match, and keeping the whole presentation in one file is simplest here.
    public class TutorialUIController : MonoBehaviour
    {
        [Tooltip("Optional - the same sliced Space Expansion UI panel sprite the health/ammo HUD " +
                 "bars use (Assets/Art/Textures/UI/Health/SpaceExpansion_BarTrack_Grey.png). Set " +
                 "by Tools/Tutorial/Polish Tutorial UI; falls back to a flat color panel if unset.")]
        [SerializeField] private Sprite panelSprite;
        [Tooltip("Optional - the same utility font the rest of the HUD uses " +
                 "(Assets/Art/Fonts/UI/KenneyFutureNarrow.ttf). Falls back to Unity's default font.")]
        [SerializeField] private Font hudFont;

        private Canvas _canvas;
        private Text _titleText;
        private Text _instructionText;
        private RectTransform _keyRow;
        private readonly List<Image> _keyBackgrounds = new List<Image>();
        private readonly List<Text> _keyLabels = new List<Text>();
        private Text _counterText;
        private RectTransform _itemRow;
        private readonly Dictionary<TutorialPickupWatcher.Kind, Image> _itemIcons =
            new Dictionary<TutorialPickupWatcher.Kind, Image>();
        private GameObject _infoBackdrop;
        private GameObject _infoPanel;
        private Text _infoTitleText;
        private Text _infoText;
        private CanvasGroup _bannerGroup;
        private readonly List<Image> _progressDots = new List<Image>();
        private Coroutine _bannerFadeRoutine;

        private static readonly Color IdleColor = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color CompleteColor = new Color(0.25f, 0.9f, 0.4f, 0.9f);
        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color AccentColor = new Color(0.3f, 0.65f, 1f, 1f);
        private static readonly Color DotUpcomingColor = new Color(1f, 1f, 1f, 0.2f);
        private static readonly Color DotCurrentColor = new Color(1f, 1f, 1f, 0.95f);

        private static readonly string[] StageNames =
        {
            "Movement", "Wave", "Jump", "Dash", "Light Attack", "Reload", "Heavy Attack",
            "Power-Ups", "Overview", "Complete",
        };

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            BuildProgressRow();
            BuildBanner();
            BuildKeyRow();
            BuildItemRow();
            BuildInfoPanel();
        }

        // ---- Progress breadcrumb ----

        private void BuildProgressRow()
        {
            var row = CreatePanel("ProgressRow", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(StageNames.Length * 46f, 24f));
            row.GetComponent<Image>().enabled = false;

            float spacing = 46f;
            float startX = -(StageNames.Length - 1) * spacing * 0.5f;
            for (int i = 0; i < StageNames.Length; i++)
            {
                var dot = new GameObject("Dot" + i, typeof(RectTransform), typeof(Image));
                dot.transform.SetParent(row.transform, false);
                var rect = (RectTransform)dot.transform;
                rect.sizeDelta = new Vector2(14f, 14f);
                rect.anchoredPosition = new Vector2(startX + i * spacing, 0f);
                var image = dot.GetComponent<Image>();
                image.color = DotUpcomingColor;
                _progressDots.Add(image);
            }
        }

        public void SetProgress(int stageIndex)
        {
            for (int i = 0; i < _progressDots.Count; i++)
            {
                _progressDots[i].color = i < stageIndex ? CompleteColor : i == stageIndex ? DotCurrentColor : DotUpcomingColor;
                _progressDots[i].rectTransform.localScale = i == stageIndex ? Vector3.one * 1.35f : Vector3.one;
            }
        }

        // ---- Banner ----

        private void BuildBanner()
        {
            var panel = CreatePanel("Banner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -120f), new Vector2(900f, 130f));
            _bannerGroup = panel.AddComponent<CanvasGroup>();

            var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(panel.transform, false);
            var accentRect = (RectTransform)accent.transform;
            accentRect.anchorMin = new Vector2(0.5f, 1f);
            accentRect.anchorMax = new Vector2(0.5f, 1f);
            accentRect.anchoredPosition = new Vector2(0f, 0f);
            accentRect.sizeDelta = new Vector2(64f, 3f);
            accent.GetComponent<Image>().color = AccentColor;

            _titleText = CreateText(panel.transform, "Title", 30, FontStyle.Bold, TextAnchor.UpperCenter);
            SetStretch(_titleText.rectTransform, new Vector2(20f, 58f), new Vector2(-20f, -12f));

            _instructionText = CreateText(panel.transform, "Instruction", 20, FontStyle.Normal, TextAnchor.UpperCenter);
            SetStretch(_instructionText.rectTransform, new Vector2(20f, 8f), new Vector2(-20f, -58f));
            // Some stage instructions run long (e.g. the Power-Ups callouts) - shrink to fit
            // rather than overflowing the banner instead of hand-tuning a font size per message.
            _instructionText.resizeTextForBestFit = true;
            _instructionText.resizeTextMinSize = 12;
            _instructionText.resizeTextMaxSize = 20;
        }

        // ---- Key prompt row ----

        private void BuildKeyRow()
        {
            var row = CreatePanel("KeyRow", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 110f), new Vector2(520f, 70f));
            row.GetComponent<Image>().enabled = false;
            _keyRow = row.GetComponent<RectTransform>();

            const float slotSpacing = 100f;
            for (int i = 0; i < 4; i++)
            {
                var slot = new GameObject("Key" + i, typeof(RectTransform), typeof(Image));
                slot.transform.SetParent(_keyRow, false);
                var rect = (RectTransform)slot.transform;
                rect.sizeDelta = new Vector2(80f, 64f);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2((i - 1.5f) * slotSpacing, 10f);
                var bg = slot.GetComponent<Image>();
                bg.color = IdleColor;

                var label = CreateText(slot.transform, "Label", 18, FontStyle.Bold, TextAnchor.MiddleCenter);
                SetStretch(label.rectTransform, Vector2.zero, Vector2.zero);

                slot.SetActive(false);
                _keyBackgrounds.Add(bg);
                _keyLabels.Add(label);
            }

            _counterText = CreateText(_keyRow, "Counter", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            var counterRect = _counterText.rectTransform;
            counterRect.anchorMin = new Vector2(0.5f, 0f);
            counterRect.anchorMax = new Vector2(0.5f, 0f);
            counterRect.anchoredPosition = new Vector2(0f, -30f);
            counterRect.sizeDelta = new Vector2(200f, 30f);
            _counterText.gameObject.SetActive(false);
        }

        // ---- Item pickup row ----

        private void BuildItemRow()
        {
            var row = CreatePanel("ItemRow", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 110f), new Vector2(420f, 70f));
            row.GetComponent<Image>().enabled = false;
            row.SetActive(false);
            _itemRow = row.GetComponent<RectTransform>();

            AddItemIcon(TutorialPickupWatcher.Kind.Health, "HEALTH", new Color(0.9f, 0.25f, 0.25f), 0);
            AddItemIcon(TutorialPickupWatcher.Kind.Ammo, "AMMO", new Color(0.3f, 0.6f, 1f), 1);
            AddItemIcon(TutorialPickupWatcher.Kind.Thunder, "ULTIMATE", new Color(0.95f, 0.85f, 0.2f), 2);
        }

        private void AddItemIcon(TutorialPickupWatcher.Kind kind, string label, Color tint, int index)
        {
            var slot = new GameObject(kind + "Icon", typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(_itemRow, false);
            var rect = (RectTransform)slot.transform;
            rect.sizeDelta = new Vector2(120f, 64f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2((index - 1f) * 140f, 0f);
            var bg = slot.GetComponent<Image>();
            bg.color = new Color(tint.r, tint.g, tint.b, 0.25f);

            var text = CreateText(slot.transform, "Label", 14, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.text = label;
            SetStretch(text.rectTransform, Vector2.zero, Vector2.zero);

            _itemIcons[kind] = bg;
        }

        // ---- Info popup (Overview stage) ----

        /// Fired when the player clicks the popup's close (X) button. TutorialManager owns
        /// suspending/restoring player movement/look around the popup's lifetime - this
        /// component only presents it.
        public event System.Action PopupClosed;

        private void BuildInfoPanel()
        {
            // Full-screen dim behind the popup, same idea (and same flat PanelColor) as every
            // other panel in this UI, just covering the whole canvas and raycast-blocking so
            // clicks can't reach anything behind it while the popup is up.
            _infoBackdrop = new GameObject("InfoBackdrop", typeof(RectTransform), typeof(Image));
            _infoBackdrop.transform.SetParent(transform, false);
            SetStretch((RectTransform)_infoBackdrop.transform, Vector2.zero, Vector2.zero);
            _infoBackdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            _infoPanel = CreatePanel("InfoPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 260f));
            _infoPanel.transform.SetParent(_infoBackdrop.transform, false);
            _infoPanel.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.1f, 0.96f);

            var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(_infoPanel.transform, false);
            var accentRect = (RectTransform)accent.transform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.offsetMin = new Vector2(0f, 0f);
            accentRect.offsetMax = new Vector2(6f, 0f);
            accent.GetComponent<Image>().color = AccentColor;

            _infoTitleText = CreateText(_infoPanel.transform, "Title", 22, FontStyle.Bold, TextAnchor.UpperLeft);
            var titleRect = _infoTitleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.anchoredPosition = new Vector2(30f, -22f);
            titleRect.sizeDelta = new Vector2(-100f, 34f);

            _infoText = CreateText(_infoPanel.transform, "Message", 20, FontStyle.Normal, TextAnchor.UpperLeft);
            SetStretch(_infoText.rectTransform, new Vector2(30f, 24f), new Vector2(-30f, -70f));
            _infoText.resizeTextForBestFit = true;
            _infoText.resizeTextMinSize = 14;
            _infoText.resizeTextMaxSize = 20;

            BuildInfoCloseButton();

            _infoBackdrop.SetActive(false);
        }

        // Small square button, top-right corner of the popup - same accent color as every other
        // interactive element in this UI (the banner accent bar, the completion button used to).
        private void BuildInfoCloseButton()
        {
            var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(_infoPanel.transform, false);
            var closeRect = (RectTransform)closeGo.transform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-26f, -22f);
            closeRect.sizeDelta = new Vector2(36f, 36f);
            closeGo.GetComponent<Image>().color = new Color(0.85f, 0.25f, 0.25f, 0.9f);

            var label = CreateText(closeGo.transform, "X", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            label.text = "X";
            SetStretch(label.rectTransform, Vector2.zero, Vector2.zero);

            closeGo.GetComponent<Button>().onClick.AddListener(() =>
            {
                HideInfo();
                PopupClosed?.Invoke();
            });
        }

        // ---- Public API used by TutorialManager ----

        public void ShowStage(string title, string instruction)
        {
            _titleText.text = title;
            _instructionText.text = instruction;

            if (_bannerFadeRoutine != null) StopCoroutine(_bannerFadeRoutine);
            _bannerFadeRoutine = StartCoroutine(FadeBanner());
        }

        public void SetProgress(TutorialStage stage) => SetProgress((int)stage);

        private IEnumerator FadeBanner()
        {
            float duration = 0.35f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _bannerGroup.alpha = t;
                yield return null;
            }
            _bannerGroup.alpha = 1f;
        }

        /// Rebuilds the key-prompt row for the current stage. Pass 1-4 short labels (e.g. "W",
        /// "SPACE", "LMB"); any unused slots hide themselves.
        public void SetKeyPrompts(params string[] labels)
        {
            for (int i = 0; i < _keyBackgrounds.Count; i++)
            {
                bool active = i < labels.Length;
                _keyBackgrounds[i].gameObject.SetActive(active);
                if (active)
                {
                    _keyLabels[i].text = labels[i];
                    _keyBackgrounds[i].color = IdleColor;
                }
            }

            _itemRow.gameObject.SetActive(false);
            _counterText.gameObject.SetActive(false);
        }

        public void SetKeyComplete(int index, bool complete)
        {
            if (index < 0 || index >= _keyBackgrounds.Count) return;
            _keyBackgrounds[index].color = complete ? CompleteColor : IdleColor;
            if (complete) StartCoroutine(Pulse(_keyBackgrounds[index].rectTransform));
        }

        /// A quick scale-up-and-settle so a just-completed key/counter/item reads as "that one
        /// just landed" rather than only a static color swap.
        private static IEnumerator Pulse(RectTransform rect)
        {
            const float duration = 0.25f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rect.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        public void SetCounter(int current, int target)
        {
            _counterText.gameObject.SetActive(true);
            _counterText.text = $"{Mathf.Min(current, target)} / {target}";
        }

        public void ShowItemRow()
        {
            for (int i = 0; i < _keyBackgrounds.Count; i++) _keyBackgrounds[i].gameObject.SetActive(false);
            _counterText.gameObject.SetActive(false);
            _itemRow.gameObject.SetActive(true);
        }

        public void SetItemCollected(TutorialPickupWatcher.Kind kind)
        {
            if (_itemIcons.TryGetValue(kind, out var image))
            {
                image.color = CompleteColor;
                StartCoroutine(Pulse(image.rectTransform));
            }
        }

        public void ShowInfo(string message)
        {
            _infoTitleText.text = "INFO";
            _infoText.text = message;
            _infoBackdrop.SetActive(true);
        }

        public void HideInfo()
        {
            _infoBackdrop.SetActive(false);
        }

        // ---- UI construction helpers ----

        private GameObject CreatePanel(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            if (panelSprite != null)
            {
                image.sprite = panelSprite;
                image.type = Image.Type.Sliced;
            }
            image.color = PanelColor;
            return go;
        }

        private Text CreateText(Transform parent, string name, int fontSize, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = hudFont != null ? hudFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}

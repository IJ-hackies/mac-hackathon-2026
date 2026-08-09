using Gameplay.Waves;
using Player.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Score
{
    /// <summary>Personal-best readout shown while the pause console is open. Rather than editing
    /// the sealed SettingsMenuController, this polls its public IsOpen state and shows/hides its
    /// own self-built overlay in sync - keeps the pause console's own open/close/time-scale
    /// ownership untouched. Add anywhere on the PlayerRig prefab.
    ///
    /// Shows best score/wave only - the live current run score is already visible in the
    /// always-on corner HUD (ScoreHud), so repeating it here was redundant.</summary>
    [DisallowMultipleComponent]
    public sealed class PauseScorePanel : MonoBehaviour
    {
        [SerializeField] private SettingsMenuController settingsMenu;
        [SerializeField] private Canvas hudCanvas;

        private const string PositionKey = "ui.pauseScorePanel.position";

        private GameObject _panel;
        private Text _text;
        private bool _wasOpen;

        private void Awake()
        {
            if (settingsMenu == null) settingsMenu = GetComponentInChildren<SettingsMenuController>(true);
            if (hudCanvas == null) hudCanvas = GetComponentInChildren<Canvas>(true);

            BuildPanel();
        }

        private void Update()
        {
            bool isOpen = settingsMenu != null && settingsMenu.IsOpen;
            if (isOpen == _wasOpen) return;

            _wasOpen = isOpen;
            if (_panel == null) return;

            _panel.SetActive(isOpen);
            if (isOpen) Refresh();
        }

        private void Refresh()
        {
            if (_text == null) return;
            _text.text = $"PERSONAL BEST   {LocalScoreRecord.BestScore} PTS   //   WAVE {LocalScoreRecord.BestWaveReached}";
        }

        private void BuildPanel()
        {
            Transform parent = hudCanvas != null ? hudCanvas.transform : transform;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _panel = new GameObject("PauseScorePanel", typeof(RectTransform));
            _panel.transform.SetParent(parent, false);
            var rect = (RectTransform)_panel.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(460f, 48f);
            // Sits just above the settings console panel (which is itself centered, roughly
            // 580-660 tall) rather than floating disconnected near the top of the screen.
            rect.anchoredPosition = new Vector2(0f, 330f);

            // Own overridden-sorting Canvas so this always draws above the settings console
            // regardless of sibling order within the shared HUD Canvas.
            var panelCanvas = _panel.AddComponent<Canvas>();
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder = 50;
            _panel.AddComponent<GraphicRaycaster>();

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(_panel.transform, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.sprite = RuntimeUiSprites.RoundedRect;
            fillImage.type = Image.Type.Sliced;
            fillImage.color = new Color(0.07f, 0.09f, 0.13f, 0.95f);

            // Dragging was used once to position this, then turned off - apply whatever position
            // was saved from that session and leave it fixed there.
            if (PlayerPrefs.HasKey(PositionKey + ".x"))
            {
                rect.anchoredPosition = new Vector2(
                    PlayerPrefs.GetFloat(PositionKey + ".x", rect.anchoredPosition.x),
                    PlayerPrefs.GetFloat(PositionKey + ".y", rect.anchoredPosition.y));
            }

            var borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(_panel.transform, false);
            borderGo.transform.SetAsFirstSibling();
            var borderRect = (RectTransform)borderGo.transform;
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-3f, -3f);
            borderRect.offsetMax = new Vector2(3f, 3f);
            var borderImage = borderGo.GetComponent<Image>();
            borderImage.sprite = RuntimeUiSprites.RoundedRect;
            borderImage.type = Image.Type.Sliced;
            borderImage.color = new Color(0.35f, 0.75f, 0.95f, 0.7f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(_panel.transform, false);
            _text = textGo.GetComponent<Text>();
            _text.font = font;
            _text.fontSize = 17;
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color = new Color(1f, 0.85f, 0.25f, 1f);
            // Otherwise the text (topmost sibling) intercepts the drag before it reaches Fill.
            _text.raycastTarget = false;
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);

            _panel.SetActive(false);
        }
    }
}

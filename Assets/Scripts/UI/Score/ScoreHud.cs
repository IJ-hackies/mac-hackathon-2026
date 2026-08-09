using Gameplay.Waves;
using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Score
{
    /// <summary>Self-built HUD row (same ad hoc runtime-UI convention as SettingsMenuController's
    /// confirm dialog - flat Image panels, no external sprite dependency) showing the live run
    /// score directly beneath ProgressionGoldHud. Add this component anywhere on the PlayerRig
    /// prefab; it resolves the gold HUD and WaveDirector itself, no wiring required.</summary>
    [DisallowMultipleComponent]
    public sealed class ScoreHud : MonoBehaviour
    {
        [SerializeField] private WaveDirector waveDirector;
        [SerializeField] private RectTransform goldHudPanel;
        [SerializeField] private string prefix = "SCORE ";

        private Text _valueText;
        private RectTransform _panelRect;

        private void Awake()
        {
            if (waveDirector == null) waveDirector = Object.FindFirstObjectByType<WaveDirector>();
            if (goldHudPanel == null)
            {
                var goldHud = Object.FindFirstObjectByType<Player.UI.Progression.ProgressionGoldHud>(
                    FindObjectsInactive.Include);
                if (goldHud != null) goldHudPanel = goldHud.GetComponent<RectTransform>();
            }

            BuildPanel();
        }

        private void OnEnable()
        {
            if (waveDirector != null)
            {
                waveDirector.GoldChanged += HandleStateChanged;
                waveDirector.WaveStarted += HandleWaveStarted;
                waveDirector.WaveCompleted += HandleWaveCompleted;
                waveDirector.RunEnded += HandleRunEnded;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (waveDirector != null)
            {
                waveDirector.GoldChanged -= HandleStateChanged;
                waveDirector.WaveStarted -= HandleWaveStarted;
                waveDirector.WaveCompleted -= HandleWaveCompleted;
                waveDirector.RunEnded -= HandleRunEnded;
            }
        }

        private void HandleStateChanged(int gold, int goldEarned) => Refresh();
        private void HandleWaveStarted(int wave, WaveKind kind) => Refresh();
        private void HandleWaveCompleted(int wave, WaveKind kind) => Refresh();
        private void HandleRunEnded(WaveRunResult result) => Refresh();

        private void Refresh()
        {
            if (_valueText == null || waveDirector == null) return;
            int score = ScoreRules.ComputeScore(waveDirector.Kills, waveDirector.GoldEarned, waveDirector.CurrentWave);
            _valueText.text = $"{prefix}{score}";
        }

        private void BuildPanel()
        {
            Transform parent = goldHudPanel != null ? goldHudPanel.parent : transform;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var panelGo = new GameObject("ScoreHud", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(parent, false);
            _panelRect = (RectTransform)panelGo.transform;

            if (goldHudPanel != null)
            {
                // Mirror the gold HUD's own anchoring/width, stacked directly beneath it.
                _panelRect.anchorMin = goldHudPanel.anchorMin;
                _panelRect.anchorMax = goldHudPanel.anchorMax;
                _panelRect.pivot = goldHudPanel.pivot;
                _panelRect.sizeDelta = goldHudPanel.sizeDelta;
                _panelRect.anchoredPosition =
                    goldHudPanel.anchoredPosition + new Vector2(0f, -(goldHudPanel.sizeDelta.y + 8f));
            }
            else
            {
                _panelRect.anchorMin = new Vector2(1f, 1f);
                _panelRect.anchorMax = new Vector2(1f, 1f);
                _panelRect.pivot = new Vector2(1f, 1f);
                _panelRect.sizeDelta = new Vector2(220f, 44f);
                _panelRect.anchoredPosition = new Vector2(-24f, -92f);
            }

            // Copy the gold HUD's own Image (sprite/type/material) instead of a flat color box,
            // so this reads as the same HUD element rather than a bare rectangle.
            var panelImage = panelGo.GetComponent<Image>();
            Image goldImage = goldHudPanel != null ? goldHudPanel.GetComponent<Image>() : null;
            if (goldImage != null && goldImage.sprite != null)
            {
                panelImage.sprite = goldImage.sprite;
                panelImage.type = goldImage.type;
                panelImage.material = goldImage.material;
                panelImage.color = goldImage.color;
            }
            else
            {
                panelImage.color = new Color(0.05f, 0.07f, 0.1f, 0.75f);
            }

            var textGo = new GameObject("Value", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(panelGo.transform, false);
            _valueText = textGo.GetComponent<Text>();
            _valueText.font = font;
            _valueText.fontSize = 20;
            _valueText.fontStyle = FontStyle.Bold;
            _valueText.alignment = TextAnchor.MiddleCenter;
            _valueText.color = new Color(1f, 0.85f, 0.25f, 1f);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 2f);
            textRect.offsetMax = new Vector2(-8f, -2f);
        }
    }
}

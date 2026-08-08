using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    /// Top-left "time remaining" readout, hidden unless PlayerUltimate.IsActive. Same track/fill
    /// sliced-sprite bar and smoothed anchorMax fill approach as HealthHudUI/AmmoHudUI, so it
    /// reads as the same bar system rather than a separate flat-color style.
    public class UltimateHudUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Image fill;
        [SerializeField] private Text timeText;
        [SerializeField] private PlayerUltimate ultimate;
        [SerializeField, Min(0.01f)] private float fillSpeed = 6f;

        private float _target = 1f;
        private float _displayed = 1f;
        private bool _hasInitialValue;

        public void SetWidgets(GameObject panel, Image fillImage, Text timeTextLabel)
        {
            panelRoot = panel;
            fill = fillImage;
            timeText = timeTextLabel;
        }

        public void Bind(PlayerUltimate target)
        {
            ultimate = target;
        }

        private void OnEnable()
        {
            if (ultimate == null)
            {
                var player = FindFirstObjectByType<global::Player.PlayerController>();
                if (player != null) ultimate = player.GetComponent<PlayerUltimate>();
            }

            if (ultimate != null)
            {
                ultimate.UltimateActivated += UpdateDisplay;
                ultimate.UltimateEnded += UpdateDisplay;
            }

            UpdateDisplay();
        }

        private void OnDisable()
        {
            if (ultimate != null)
            {
                ultimate.UltimateActivated -= UpdateDisplay;
                ultimate.UltimateEnded -= UpdateDisplay;
            }
        }

        private void Update()
        {
            if (ultimate == null || !ultimate.IsActive) return;

            UpdateTarget();
            _displayed = Mathf.MoveTowards(_displayed, _target, fillSpeed * Time.unscaledDeltaTime);
            ApplyFraction(_displayed);
            if (timeText != null) timeText.text = "ULTIMATE " + Mathf.CeilToInt(ultimate.TimeRemaining) + "s";
        }

        private void UpdateTarget()
        {
            _target = ultimate.Duration > 0f ? Mathf.Clamp01(ultimate.TimeRemaining / ultimate.Duration) : 0f;
        }

        private void UpdateDisplay()
        {
            bool active = ultimate != null && ultimate.IsActive;
            if (panelRoot != null) panelRoot.SetActive(active);
            if (!active) return;

            UpdateTarget();
            _displayed = _target;
            _hasInitialValue = true;
            ApplyFraction(_displayed);
            if (timeText != null) timeText.text = "ULTIMATE " + Mathf.CeilToInt(ultimate.TimeRemaining) + "s";
        }

        private void ApplyFraction(float fraction)
        {
            if (fill == null) return;

            RectTransform rect = fill.rectTransform;
            Vector2 anchorMax = rect.anchorMax;
            anchorMax.x = Mathf.Clamp01(fraction);
            rect.anchorMax = anchorMax;
            fill.enabled = _hasInitialValue && fraction > 0.001f;
        }
    }
}

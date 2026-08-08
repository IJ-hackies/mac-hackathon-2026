using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    /// Top-left "time remaining" readout, hidden unless PlayerUltimate.IsActive. Same generated-
    /// UI-rect approach as HealthHudUI/AmmoHudUI.
    public class UltimateHudUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Image fill;
        [SerializeField] private Text timeText;
        [SerializeField] private PlayerUltimate ultimate;

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
            if (ultimate != null && ultimate.IsActive) UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            bool active = ultimate != null && ultimate.IsActive;
            if (panelRoot != null) panelRoot.SetActive(active);
            if (!active) return;

            float fraction = ultimate.Duration > 0f ? Mathf.Clamp01(ultimate.TimeRemaining / ultimate.Duration) : 0f;
            if (fill != null) fill.fillAmount = fraction;
            if (timeText != null) timeText.text = "ULTIMATE " + Mathf.CeilToInt(ultimate.TimeRemaining) + "s";
        }
    }
}

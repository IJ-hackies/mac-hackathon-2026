using Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    /// Segmented "energy cell" readout, built entirely from generated UI rects the same way
    /// EmoteWheelUI/CrosshairUI are - no external art. Segments light up left-to-right by health
    /// fraction and shift from full-color toward lowColor as health drops.
    public class HealthHudUI : MonoBehaviour
    {
        [SerializeField] private Image[] segments;
        [SerializeField] private Text percentText;
        [SerializeField] private Health health;
        [SerializeField] private Color fullColor = new Color(0.3f, 0.9f, 1f);
        [SerializeField] private Color lowColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField] private Color emptySegmentColor = new Color(1f, 1f, 1f, 0.12f);

        public void SetSegments(Image[] segmentImages)
        {
            segments = segmentImages;
        }

        public void SetPercentText(Text text)
        {
            percentText = text;
        }

        // Only stores the reference here - PlayerSceneSetup calls this at edit time while
        // building the scene, and a delegate subscribed there wouldn't survive the domain
        // reload on entering Play mode. The actual event subscription happens in OnEnable
        // (runs at real runtime), same pattern as EnemyBase/PlayerDeathHandler's Health.Died.
        public void Bind(Health target)
        {
            health = target;
        }

        private void OnEnable()
        {
            if (health == null)
            {
                global::Player.PlayerController player = FindFirstObjectByType<global::Player.PlayerController>();
                if (player != null)
                {
                    health = player.GetComponent<Health>();
                }
            }

            if (health == null) return;
            health.HealthChanged += UpdateDisplay;
            UpdateDisplay(health.CurrentHealth, health.MaxHealth);
        }

        private void OnDisable()
        {
            if (health != null) health.HealthChanged -= UpdateDisplay;
        }

        private void UpdateDisplay(float current, float max)
        {
            float fraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            int litCount = Mathf.CeilToInt(fraction * segments.Length);
            Color activeColor = Color.Lerp(lowColor, fullColor, fraction);

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] != null)
                {
                    segments[i].color = i < litCount ? activeColor : emptySegmentColor;
                }
            }

            if (percentText != null)
            {
                percentText.text = Mathf.RoundToInt(fraction * 100f) + "%";
            }
        }
    }
}

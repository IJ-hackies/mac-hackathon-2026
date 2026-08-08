using Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    /// <summary>
    /// Minimal player-health readout: one red bar with the current HP centered inside it.
    /// </summary>
    public class HealthHudUI : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Text healthText;
        [SerializeField] private Health health;
        [SerializeField, Min(0.01f)] private float fillSpeed = 5.5f;

        private float _targetFraction = 1f;
        private float _displayedFraction = 1f;
        private bool _hasInitialValue;

        public void Configure(Image fill, Text valueText)
        {
            fillImage = fill;
            healthText = valueText;
        }

        // Editor setup only stores the reference. Runtime event subscriptions belong in
        // OnEnable so they survive the Play-mode domain reload.
        public void Bind(Health target)
        {
            health = target;
        }

        private void OnEnable()
        {
            if (health == null)
            {
                global::Player.PlayerController player =
                    FindFirstObjectByType<global::Player.PlayerController>();
                if (player != null)
                {
                    health = player.GetComponent<Health>();
                }
            }

            if (health == null)
            {
                return;
            }

            health.HealthChanged += HandleHealthChanged;
            SetImmediate(health.CurrentHealth, health.MaxHealth);
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.HealthChanged -= HandleHealthChanged;
            }
        }

        private void Update()
        {
            if (!_hasInitialValue)
            {
                return;
            }

            _displayedFraction = Mathf.MoveTowards(
                _displayedFraction,
                _targetFraction,
                fillSpeed * Time.unscaledDeltaTime);
            SetBarFraction(_displayedFraction);
        }

        private void HandleHealthChanged(float current, float max)
        {
            _targetFraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            SetHealthText(current);
        }

        private void SetImmediate(float current, float max)
        {
            _targetFraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            _displayedFraction = _targetFraction;
            _hasInitialValue = true;
            SetBarFraction(_displayedFraction);
            SetHealthText(current);
        }

        private void SetBarFraction(float fraction)
        {
            if (fillImage == null)
            {
                return;
            }

            RectTransform rect = fillImage.rectTransform;
            Vector2 anchorMax = rect.anchorMax;
            anchorMax.x = Mathf.Clamp01(fraction);
            rect.anchorMax = anchorMax;
            fillImage.enabled = fraction > 0.001f;
        }

        private void SetHealthText(float current)
        {
            if (healthText != null)
            {
                healthText.text = Mathf.Max(0, Mathf.RoundToInt(current)).ToString();
            }
        }
    }
}

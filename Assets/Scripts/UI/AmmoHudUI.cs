using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    /// <summary>
    /// Minimal player-ammo readout: one blue magazine bar with loaded / reserve rounds centered
    /// inside it. The fill still represents only the loaded magazine so reload cadence remains
    /// readable while the numeric value exposes the complete ammo state.
    /// </summary>
    public class AmmoHudUI : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Text ammoText;
        [SerializeField] private global::Player.PlayerAmmo playerAmmo;
        [SerializeField, Min(0.01f)] private float fillSpeed = 5.5f;

        private float _targetFraction = 1f;
        private float _displayedFraction = 1f;
        private bool _hasInitialValue;

        public void Configure(Image fill, Text valueText)
        {
            fillImage = fill;
            ammoText = valueText;
        }

        // Editor setup only stores the reference. Runtime event subscriptions belong in
        // OnEnable so they survive the Play-mode domain reload.
        public void Bind(global::Player.PlayerAmmo target)
        {
            playerAmmo = target;
        }

        private void OnEnable()
        {
            if (playerAmmo == null)
            {
                global::Player.PlayerController player =
                    FindFirstObjectByType<global::Player.PlayerController>();
                if (player != null)
                {
                    playerAmmo = player.GetComponent<global::Player.PlayerAmmo>();
                }
            }

            if (playerAmmo == null) return;
            playerAmmo.AmmoChanged += UpdateAmmoDisplay;
            UpdateAmmoDisplay(playerAmmo.CurrentMagazine, playerAmmo.CurrentStorage);
        }

        private void OnDisable()
        {
            if (playerAmmo != null)
            {
                playerAmmo.AmmoChanged -= UpdateAmmoDisplay;
            }
        }

        private void Update()
        {
            if (!_hasInitialValue) return;

            _displayedFraction = Mathf.MoveTowards(
                _displayedFraction,
                _targetFraction,
                fillSpeed * Time.unscaledDeltaTime);
            SetBarFraction(_displayedFraction);
        }

        private void UpdateAmmoDisplay(int magazine, int storage)
        {
            _targetFraction = playerAmmo != null && playerAmmo.InfiniteAmmo
                ? 1f
                : playerAmmo != null && playerAmmo.MagazineSize > 0
                    ? Mathf.Clamp01((float)magazine / playerAmmo.MagazineSize)
                    : 0f;

            if (!_hasInitialValue)
            {
                _displayedFraction = _targetFraction;
                _hasInitialValue = true;
                SetBarFraction(_displayedFraction);
            }

            if (ammoText != null)
            {
                ammoText.text = playerAmmo != null && playerAmmo.InfiniteAmmo
                    ? "\u221e"
                    : $"{Mathf.Max(0, magazine)} / {Mathf.Max(0, storage)}";
            }
        }

        private void SetBarFraction(float fraction)
        {
            if (fillImage == null) return;

            RectTransform rect = fillImage.rectTransform;
            Vector2 anchorMax = rect.anchorMax;
            anchorMax.x = Mathf.Clamp01(fraction);
            rect.anchorMax = anchorMax;
            fillImage.enabled = fraction > 0.001f;
        }
    }
}

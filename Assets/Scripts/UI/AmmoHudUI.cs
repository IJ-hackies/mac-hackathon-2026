using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    /// Bottom-right "magazine / storage" ammo readout plus a "RELOADING" indicator, built the
    /// same procedural-UI-rect way as HealthHudUI/CrosshairUI/EmoteWheelUI - no external art.
    /// Bind() (called by the scene-setup editor tool at edit time) only stores the PlayerAmmo
    /// reference; the actual event subscription happens in OnEnable, same pattern as
    /// HealthHudUI - an edit-time delegate subscription wouldn't survive the Play-mode domain
    /// reload.
    public class AmmoHudUI : MonoBehaviour
    {
        [SerializeField] private Text ammoText;
        [SerializeField] private Text reloadingText;
        [SerializeField] private global::Player.PlayerAmmo playerAmmo;

        public void SetAmmoText(Text text)
        {
            ammoText = text;
        }

        public void SetReloadingText(Text text)
        {
            reloadingText = text;
        }

        public void Bind(global::Player.PlayerAmmo target)
        {
            playerAmmo = target;
        }

        private void OnEnable()
        {
            if (playerAmmo == null)
            {
                global::Player.PlayerController player = FindFirstObjectByType<global::Player.PlayerController>();
                if (player != null)
                {
                    playerAmmo = player.GetComponent<global::Player.PlayerAmmo>();
                }
            }

            if (playerAmmo == null) return;
            playerAmmo.AmmoChanged += UpdateAmmoDisplay;
            playerAmmo.ReloadStarted += OnReloadStarted;
            playerAmmo.ReloadFinished += OnReloadFinished;
            UpdateAmmoDisplay(playerAmmo.CurrentMagazine, playerAmmo.CurrentStorage);
            SetReloadingVisible(playerAmmo.IsReloading);
        }

        private void OnDisable()
        {
            if (playerAmmo == null) return;
            playerAmmo.AmmoChanged -= UpdateAmmoDisplay;
            playerAmmo.ReloadStarted -= OnReloadStarted;
            playerAmmo.ReloadFinished -= OnReloadFinished;
        }

        private void UpdateAmmoDisplay(int magazine, int storage)
        {
            if (ammoText == null) return;
            ammoText.text = playerAmmo != null && playerAmmo.InfiniteAmmo ? "∞" : $"{magazine} / {storage}";
        }

        private void OnReloadStarted() => SetReloadingVisible(true);
        private void OnReloadFinished() => SetReloadingVisible(false);

        private void SetReloadingVisible(bool visible)
        {
            if (reloadingText != null) reloadingText.enabled = visible;
        }
    }
}

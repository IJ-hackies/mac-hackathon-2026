using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    /// Bottom-left ability readout - two slots, same track/fill sliced-sprite bar and smoothed
    /// anchorMax fill approach as HealthHudUI/AmmoHudUI (rather than a flat Image.Type.Filled
    /// bar) so all four HUD bars read as one visual system. Slot A shows PlayerDash's cooldown
    /// (normal mode) or PlayerShield's energy fraction (Ultimate mode, re-labeled live); slot B
    /// shows PlayerCombat's secondary-attack cooldown, which already covers both the base (4s)
    /// and Ultimate (7s) cooldown durations since both are just "cooldown remaining" from
    /// PlayerCombat's own perspective.
    public class AbilityHudUI : MonoBehaviour
    {
        [SerializeField] private Image slotAFill;
        [SerializeField] private Text slotALabel;
        [SerializeField] private Image slotBFill;
        [SerializeField] private Text slotBLabel;
        [SerializeField, Min(0.01f)] private float fillSpeed = 6f;

        [SerializeField] private PlayerDash dash;
        [SerializeField] private PlayerShield shield;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerUltimate ultimate;

        private float _targetA = 1f;
        private float _displayedA = 1f;
        private float _targetB = 1f;
        private float _displayedB = 1f;
        private bool _hasInitialValue;

        public void SetWidgets(Image slotAFillImage, Text slotALabelText, Image slotBFillImage, Text slotBLabelText)
        {
            slotAFill = slotAFillImage;
            slotALabel = slotALabelText;
            slotBFill = slotBFillImage;
            slotBLabel = slotBLabelText;
        }

        // See HealthHudUI.Bind - stores only the references at edit time; subscriptions happen
        // in OnEnable so they survive the Play-mode domain reload.
        public void Bind(PlayerDash dashRef, PlayerShield shieldRef, PlayerCombat combatRef, PlayerUltimate ultimateRef)
        {
            dash = dashRef;
            shield = shieldRef;
            combat = combatRef;
            ultimate = ultimateRef;
        }

        private void OnEnable()
        {
            if (dash == null || shield == null || combat == null || ultimate == null)
            {
                var player = FindFirstObjectByType<global::Player.PlayerController>();
                if (player != null)
                {
                    if (dash == null) dash = player.GetComponent<PlayerDash>();
                    if (shield == null) shield = player.GetComponent<PlayerShield>();
                    if (combat == null) combat = player.GetComponent<PlayerCombat>();
                    if (ultimate == null) ultimate = player.GetComponent<PlayerUltimate>();
                }
            }

            if (dash != null) dash.CooldownChanged += UpdateTargets;
            if (shield != null) shield.EnergyChanged += UpdateTargets;
            if (combat != null) combat.SecondaryCooldownChanged += UpdateTargets;
            if (ultimate != null)
            {
                ultimate.UltimateActivated += UpdateTargets;
                ultimate.UltimateEnded += UpdateTargets;
            }

            UpdateTargets();
            _displayedA = _targetA;
            _displayedB = _targetB;
            _hasInitialValue = true;
            ApplyFraction(slotAFill, _displayedA);
            ApplyFraction(slotBFill, _displayedB);
        }

        private void OnDisable()
        {
            if (dash != null) dash.CooldownChanged -= UpdateTargets;
            if (shield != null) shield.EnergyChanged -= UpdateTargets;
            if (combat != null) combat.SecondaryCooldownChanged -= UpdateTargets;
            if (ultimate != null)
            {
                ultimate.UltimateActivated -= UpdateTargets;
                ultimate.UltimateEnded -= UpdateTargets;
            }
        }

        private void Update()
        {
            // Cooldowns/energy tick down continuously, not just on discrete change events -
            // cheap enough (a handful of float reads) to just refresh every frame.
            UpdateTargets();

            if (!_hasInitialValue) return;

            _displayedA = Mathf.MoveTowards(_displayedA, _targetA, fillSpeed * Time.unscaledDeltaTime);
            _displayedB = Mathf.MoveTowards(_displayedB, _targetB, fillSpeed * Time.unscaledDeltaTime);
            ApplyFraction(slotAFill, _displayedA);
            ApplyFraction(slotBFill, _displayedB);
        }

        private void UpdateTargets()
        {
            bool ultimateActive = ultimate != null && ultimate.IsActive;

            if (ultimateActive && shield != null)
            {
                if (slotALabel != null) slotALabel.text = "SHIELD";
                _targetA = shield.EnergyFraction;
            }
            else if (dash != null)
            {
                if (slotALabel != null) slotALabel.text = "DASH";
                _targetA = dash.CooldownDuration > 0f
                    ? 1f - Mathf.Clamp01(dash.CooldownRemaining / dash.CooldownDuration)
                    : 1f;
            }

            if (combat != null)
            {
                if (slotBLabel != null) slotBLabel.text = ultimateActive ? "LIGHTNING" : "BEAM";
                float duration = combat.SecondaryCooldownDuration;
                _targetB = duration > 0f ? 1f - Mathf.Clamp01(combat.SecondaryCooldownRemaining / duration) : 1f;
            }
        }

        private static void ApplyFraction(Image fillImage, float fraction)
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

using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    /// Bottom-left ability readout - two slots, same generated-UI-rect approach as HealthHudUI/
    /// AmmoHudUI. Slot A shows PlayerDash's cooldown (normal mode) or PlayerShield's energy
    /// fraction (Ultimate mode, re-labeled live); slot B shows PlayerCombat's secondary-attack
    /// cooldown, which already covers both the base (4s) and Ultimate (7s) cooldown durations
    /// since both are just "cooldown remaining" from PlayerCombat's own perspective.
    public class AbilityHudUI : MonoBehaviour
    {
        [SerializeField] private Image slotAFill;
        [SerializeField] private Text slotALabel;
        [SerializeField] private Image slotBFill;
        [SerializeField] private Text slotBLabel;

        [SerializeField] private PlayerDash dash;
        [SerializeField] private PlayerShield shield;
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerUltimate ultimate;

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

            if (dash != null) dash.CooldownChanged += UpdateDisplay;
            if (shield != null) shield.EnergyChanged += UpdateDisplay;
            if (combat != null) combat.SecondaryCooldownChanged += UpdateDisplay;
            if (ultimate != null)
            {
                ultimate.UltimateActivated += UpdateDisplay;
                ultimate.UltimateEnded += UpdateDisplay;
            }

            UpdateDisplay();
        }

        private void OnDisable()
        {
            if (dash != null) dash.CooldownChanged -= UpdateDisplay;
            if (shield != null) shield.EnergyChanged -= UpdateDisplay;
            if (combat != null) combat.SecondaryCooldownChanged -= UpdateDisplay;
            if (ultimate != null)
            {
                ultimate.UltimateActivated -= UpdateDisplay;
                ultimate.UltimateEnded -= UpdateDisplay;
            }
        }

        private void Update()
        {
            // Cooldowns/energy tick down continuously, not just on discrete change events -
            // cheap enough (a handful of float reads) to just refresh every frame.
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            bool ultimateActive = ultimate != null && ultimate.IsActive;

            if (ultimateActive && shield != null)
            {
                if (slotALabel != null) slotALabel.text = "SHIELD";
                if (slotAFill != null) slotAFill.fillAmount = shield.EnergyFraction;
            }
            else if (dash != null)
            {
                if (slotALabel != null) slotALabel.text = "DASH";
                float fraction = dash.CooldownDuration > 0f
                    ? 1f - Mathf.Clamp01(dash.CooldownRemaining / dash.CooldownDuration)
                    : 1f;
                if (slotAFill != null) slotAFill.fillAmount = fraction;
            }

            if (combat != null)
            {
                if (slotBLabel != null) slotBLabel.text = ultimateActive ? "LIGHTNING" : "BEAM";
                float duration = combat.SecondaryCooldownDuration;
                float fraction = duration > 0f ? 1f - Mathf.Clamp01(combat.SecondaryCooldownRemaining / duration) : 1f;
                if (slotBFill != null) slotBFill.fillAmount = fraction;
            }
        }
    }
}

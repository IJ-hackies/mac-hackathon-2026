using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Progression
{
    /// <summary>Shared button status treatment for the teal, violet, and orange station cards.</summary>
    [DisallowMultipleComponent]
    public sealed class ProgressionPurchaseButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        [SerializeField] private Text price;
        [SerializeField] private Image accent;
        [SerializeField] private Color enabledAccent = new Color(0.25f, 0.86f, 0.88f);
        [SerializeField] private Color disabledAccent = new Color(0.32f, 0.36f, 0.42f);

        public Button Button => button;

        public void Configure(Button targetButton, Text labelText, Text priceText)
        {
            button = targetButton;
            label = labelText;
            price = priceText;
        }

        public void SetState(string status, string priceValue, bool interactable, bool insufficient = false)
        {
            if (button != null) button.interactable = interactable;
            if (label != null) label.text = status;
            if (price != null) price.text = priceValue;
            if (accent != null) accent.color = interactable ? enabledAccent : disabledAccent;
            if (label != null) label.color = insufficient ? new Color(1f, 0.56f, 0.4f) : Color.white;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Progression
{
    [DisallowMultipleComponent]
    public sealed class SpecialShopStationScreen : MonoBehaviour
    {
        [SerializeField] private ProgressionDataAdapter progression;
        [SerializeField] private ProgressionPurchaseButton holdToFireButton;
        [SerializeField] private Text goldText;
        [SerializeField] private int holdToFireCost = 500;

        private void Awake()
        {
            if (holdToFireButton != null && holdToFireButton.Button != null)
                holdToFireButton.Button.onClick.AddListener(PurchaseHoldToFire);
        }
        private void OnEnable()
        {
            if (progression == null) progression = GetComponentInParent<ProgressionDataAdapter>();
            if (progression != null) progression.Refreshed += Refresh;
            Refresh();
        }
        private void OnDisable()
        {
            if (progression != null) progression.Refreshed -= Refresh;
        }
        public void Bind(MonoBehaviour source)
        {
            if (progression == null) progression = GetComponentInParent<ProgressionDataAdapter>();
            if (progression != null) progression.Bind(source);
        }
        public void PurchaseHoldToFire()
        {
            progression?.TryPurchaseSpecial(ProgressionSpecialSkill.HoldToFire);
            progression?.RefreshNow();
        }
        public void Refresh()
        {
            int gold = progression != null ? progression.Gold : 0;
            if (goldText != null) goldText.text = "G " + gold;
            if (holdToFireButton == null) return;
            bool owned = progression != null && progression.OwnsSpecial(ProgressionSpecialSkill.HoldToFire);
            bool canBuy = progression != null && progression.CanPurchaseSpecial(ProgressionSpecialSkill.HoldToFire);
            bool insufficient = !owned && gold < holdToFireCost;
            holdToFireButton.SetState(owned ? "OWNED" : "UNLOCK", owned ? string.Empty : holdToFireCost + " G",
                !owned && canBuy && progression != null && progression.HasSource, insufficient);
        }
    }
}

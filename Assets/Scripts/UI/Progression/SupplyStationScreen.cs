using Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Progression
{
    [DisallowMultipleComponent]
    public sealed class SupplyStationScreen : MonoBehaviour
    {
        [SerializeField] private ProgressionDataAdapter progression;
        [SerializeField] private ProgressionPurchaseButton healthPackButton;
        [SerializeField] private ProgressionPurchaseButton largeHealthPackButton;
        [SerializeField] private ProgressionPurchaseButton ammoPackButton;
        [SerializeField] private Text goldText;
        [SerializeField] private int healthPackCost = 50;
        [SerializeField] private int largeHealthPackCost = 100;
        [SerializeField] private int ammoPackCost = 100;
        [SerializeField] private Health health;
        [SerializeField] private global::Player.PlayerAmmo ammo;

        private void Awake()
        {
            if (healthPackButton != null && healthPackButton.Button != null)
                healthPackButton.Button.onClick.AddListener(BuyHealth);
            if (largeHealthPackButton != null && largeHealthPackButton.Button != null)
                largeHealthPackButton.Button.onClick.AddListener(BuyLargeHealth);
            if (ammoPackButton != null && ammoPackButton.Button != null)
                ammoPackButton.Button.onClick.AddListener(BuyAmmo);
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

        public void BuyHealth() => Buy(ProgressionSupply.HealthPack);
        public void BuyLargeHealth() => Buy(ProgressionSupply.LargeHealthPack);
        public void BuyAmmo() => Buy(ProgressionSupply.AmmoPack);

        private void Buy(ProgressionSupply supply)
        {
            if (progression != null) progression.TryPurchaseSupply(supply);
            progression?.RefreshNow();
        }

        public void Refresh()
        {
            if (health == null) health = FindFirstObjectByType<global::Player.PlayerController>()?.GetComponent<Health>();
            if (ammo == null) ammo = FindFirstObjectByType<global::Player.PlayerAmmo>();
            int gold = progression != null ? progression.Gold : 0;
            if (goldText != null) goldText.text = "G " + gold;
            SetSupply(healthPackButton, ProgressionSupply.HealthPack, healthPackCost, gold,
                health != null && !health.IsDead && health.CurrentHealth < health.MaxHealth);
            SetSupply(largeHealthPackButton, ProgressionSupply.LargeHealthPack, largeHealthPackCost, gold,
                health != null && !health.IsDead && health.CurrentHealth < health.MaxHealth);
            SetSupply(ammoPackButton, ProgressionSupply.AmmoPack, ammoPackCost, gold,
                ammo != null && !ammo.IsFull);
        }

        private void SetSupply(ProgressionPurchaseButton card, ProgressionSupply supply, int cost, int gold, bool useful)
        {
            if (card == null) return;
            bool affordable = gold >= cost;
            card.SetState(useful ? "BUY" : "FULL", useful && affordable ? cost + " G" :
                useful ? cost + " G" : string.Empty, useful && affordable && progression != null && progression.HasSource,
                useful && !affordable);
        }
    }
}

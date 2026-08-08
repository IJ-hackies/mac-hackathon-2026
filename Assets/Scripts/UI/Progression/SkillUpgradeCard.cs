using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Progression
{
    [DisallowMultipleComponent]
    public sealed class SkillUpgradeCard : MonoBehaviour
    {
        [SerializeField] private ProgressionStat stat;
        [SerializeField] private Text title;
        [SerializeField] private Text description;
        [SerializeField] private Text currentValue;
        [SerializeField] private Text nextValue;
        [SerializeField] private Text levelPips;
        [SerializeField] private ProgressionPurchaseButton purchaseButton;
        [SerializeField] private int cost = 100;

        public ProgressionStat Stat => stat;

        private void Awake()
        {
            if (purchaseButton != null && purchaseButton.Button != null)
                purchaseButton.Button.onClick.AddListener(Purchase);
        }

        public void Refresh(ProgressionDataAdapter progression)
        {
            if (progression == null) return;
            int level = progression.GetLevel(stat);
            int max = progression.MaxLevel;
            float current = progression.GetPurchasedValue(stat);
            if (title != null) title.text = DisplayName(stat);
            if (description != null) description.text = Description(stat);
            if (currentValue != null) currentValue.text = "NOW  " + Format(stat, current);
            if (nextValue != null)
            {
                nextValue.text = level >= max ? "MAX LEVEL" : "NEXT  " + Format(stat, PreviewNext(stat, level));
            }
            if (levelPips != null) levelPips.text = BuildPips(level, max);

            bool belowMax = level < max;
            bool canUpgrade = progression.CanUpgrade(stat);
            bool insufficient = belowMax && progression.HasSource && progression.Gold < cost;
            if (purchaseButton != null)
            {
                purchaseButton.SetState(level >= max ? "MAX" : "UPGRADE", level >= max ? string.Empty : cost + " G",
                    canUpgrade && !insufficient && progression.HasSource, insufficient);
            }
        }

        private void Purchase()
        {
            ProgressionDataAdapter progression = GetComponentInParent<ProgressionDataAdapter>();
            if (progression == null) return;
            progression.TryUpgrade(stat);
            progression.RefreshNow();
        }

        private static string BuildPips(int level, int max)
        {
            level = Mathf.Clamp(level, 0, max);
            string pips = "";
            for (int index = 0; index < max; index++) pips += index < level ? "[x]" : "[ ]";
            return "LV " + level + "  " + pips;
        }

        private static string DisplayName(ProgressionStat value)
        {
            switch (value)
            {
                case ProgressionStat.MaxHealth: return "MAX HP";
                case ProgressionStat.MovementSpeed: return "MOVEMENT SPEED";
                case ProgressionStat.FireRate: return "FIRE RATE";
                case ProgressionStat.ShootingDamage: return "SHOOTING DAMAGE";
                case ProgressionStat.MeleeDamage: return "MELEE DAMAGE";
                case ProgressionStat.MaxAmmo: return "MAX AMMO";
                default: return "DEFENSE";
            }
        }

        private static string Description(ProgressionStat value)
        {
            switch (value)
            {
                case ProgressionStat.MaxHealth: return "+10 HP per level";
                case ProgressionStat.MovementSpeed: return "+3% base speed per level";
                case ProgressionStat.FireRate: return "+5% base fire rate per level";
                case ProgressionStat.ShootingDamage: return "+10% damage per level";
                case ProgressionStat.MeleeDamage: return "+10% damage per level";
                case ProgressionStat.Defense: return "+4% damage reduction per level";
                default: return "+2 loaded rounds per level";
            }
        }

        private static float PreviewNext(ProgressionStat value, int level)
        {
            int next = Mathf.Clamp(level + 1, 1, 10);
            switch (value)
            {
                case ProgressionStat.MaxHealth: return 100f + (next - 1) * 10f;
                case ProgressionStat.MovementSpeed: return 1f + (next - 1) * .03f;
                case ProgressionStat.FireRate: return 1f + (next - 1) * .05f;
                case ProgressionStat.ShootingDamage: return 15f * (1f + (next - 1) * .1f);
                case ProgressionStat.MeleeDamage: return 20f * (1f + (next - 1) * .1f);
                case ProgressionStat.Defense: return (next - 1) * .04f;
                default: return 12 + (next - 1) * 2;
            }
        }

        private static string Format(ProgressionStat value, float number)
        {
            switch (value)
            {
                case ProgressionStat.MovementSpeed:
                case ProgressionStat.FireRate: return Mathf.RoundToInt(number * 100f) + "%";
                case ProgressionStat.Defense: return Mathf.RoundToInt(number * 100f) + "%";
                default: return number % 1f < .01f ? Mathf.RoundToInt(number).ToString() : number.ToString("0.0");
            }
        }
    }
}

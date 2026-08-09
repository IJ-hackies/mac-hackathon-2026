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
            int cost = progression.GetUpgradeCost(stat);
            if (title != null) title.text = DisplayName(stat);
            if (description != null) description.text = Description(stat);
            if (currentValue != null) currentValue.text = "NOW  " + FormatAtLevel(progression, stat, level);
            if (nextValue != null)
            {
                nextValue.text = level >= max ? "MAX LEVEL" :
                    "NEXT  " + FormatAtLevel(progression, stat, level + 1);
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
                case ProgressionStat.MaxHealth: return "+10 HP, then +5 more each level";
                case ProgressionStat.MovementSpeed: return "+3%, then +2 points each level";
                case ProgressionStat.FireRate: return "+5%, then +2 points each level";
                case ProgressionStat.ShootingDamage: return "+2 damage, then +2 each level";
                case ProgressionStat.MeleeDamage: return "+3 damage, then +3 each level";
                case ProgressionStat.Defense: return "+2%, then +1 point each level";
                default: return "Magazine +2/+3/...  Reserve +5/+10/...";
            }
        }

        private static string FormatAtLevel(ProgressionDataAdapter progression, ProgressionStat value, int level)
        {
            level = Mathf.Clamp(level, 1, progression.MaxLevel);
            if (value == ProgressionStat.MaxAmmo)
            {
                int magazine = Mathf.RoundToInt(progression.GetValueAtLevel(value, level));
                int reserve = progression.GetReserveCapacityAtLevel(level);
                return "MAG " + magazine + " / RES " + reserve;
            }

            return Format(value, progression.GetValueAtLevel(value, level));
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

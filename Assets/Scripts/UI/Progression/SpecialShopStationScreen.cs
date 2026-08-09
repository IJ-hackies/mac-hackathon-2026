using System;
using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Progression
{
    /// <summary>Drives the data-defined, independent one-time special-skill catalog.</summary>
    [DisallowMultipleComponent]
    public sealed class SpecialShopStationScreen : MonoBehaviour
    {
        [SerializeField] private ProgressionDataAdapter progression;
        [SerializeField] private ProgressionPurchaseButton[] skillButtons;
        [SerializeField] private Text goldText;

        private void Awake()
        {
            if (skillButtons == null) return;
            ProgressionSpecialSkillDefinition[] definitions = CopyDefinitions();
            for (int index = 0; index < skillButtons.Length && index < definitions.Length; index++)
            {
                ProgressionPurchaseButton purchaseButton = skillButtons[index];
                if (purchaseButton == null || purchaseButton.Button == null) continue;
                ProgressionSpecialSkill skill = definitions[index].Skill;
                purchaseButton.Button.onClick.AddListener(() => Purchase(skill));
            }
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

        public void Purchase(ProgressionSpecialSkill skill)
        {
            progression?.TryPurchaseSpecial(skill);
            progression?.RefreshNow();
        }

        public void Refresh()
        {
            int gold = progression != null ? progression.Gold : 0;
            if (goldText != null) goldText.text = "G " + gold;
            if (skillButtons == null) return;

            ProgressionSpecialSkillDefinition[] definitions = CopyDefinitions();
            for (int index = 0; index < skillButtons.Length && index < definitions.Length; index++)
            {
                ProgressionPurchaseButton purchaseButton = skillButtons[index];
                if (purchaseButton == null) continue;

                ProgressionSpecialSkillDefinition definition = definitions[index];
                bool owned = progression != null && progression.OwnsSpecial(definition.Skill);
                bool canBuy = progression != null && progression.CanPurchaseSpecial(definition.Skill);
                bool insufficient = !owned && gold < definition.Cost;
                purchaseButton.SetState(owned ? "OWNED" : "UNLOCK",
                    owned ? string.Empty : definition.Cost + " G",
                    !owned && canBuy && progression != null && progression.HasSource, insufficient);
            }
        }

        // IReadOnlyList is intentionally not serialized. The small copy avoids exposing mutable
        // catalog state and lets this MonoBehaviour stay friendly to older Unity C# profiles.
        private static ProgressionSpecialSkillDefinition[] CopyDefinitions()
        {
            var source = ProgressionSpecialSkillCatalog.All;
            var result = new ProgressionSpecialSkillDefinition[source.Count];
            for (int index = 0; index < source.Count; index++) result[index] = source[index];
            Array.Sort(result, CompareByCost);
            return result;
        }

        private static int CompareByCost(ProgressionSpecialSkillDefinition left,
            ProgressionSpecialSkillDefinition right)
        {
            int costOrder = left.Cost.CompareTo(right.Cost);
            if (costOrder != 0) return costOrder;

            // Enum order mirrors the authoritative catalog and gives equal-cost skills a stable,
            // intentional order without changing the catalog used by progression and the overview.
            return left.Skill.CompareTo(right.Skill);
        }
    }
}

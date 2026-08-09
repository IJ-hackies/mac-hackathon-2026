using Combat;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Player.UI.Progression
{
    /// <summary>Read-only non-pausing Tab overlay. It intentionally contains stable purchased stats.</summary>
    [DefaultExecutionOrder(850)]
    [DisallowMultipleComponent]
    public sealed class RunStatsOverview : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private ProgressionDataAdapter progression;
        [SerializeField] private Text[] statRows;
        [SerializeField] private Text healthRow;
        [SerializeField] private Text ammoRow;
        [SerializeField] private Text skillsRow;
        [SerializeField] private Health health;
        [SerializeField] private global::Player.PlayerAmmo ammo;
        [SerializeField] private Gameplay.Interaction.StationMenuController stationMenu;
        [SerializeField] private global::Player.UI.SettingsMenuController settingsMenu;

        private static readonly ProgressionStat[] Stats =
        {
            ProgressionStat.MaxHealth, ProgressionStat.MovementSpeed, ProgressionStat.FireRate,
            ProgressionStat.ShootingDamage, ProgressionStat.MeleeDamage, ProgressionStat.Defense,
            ProgressionStat.MaxAmmo
        };

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            ResolveReferences();
        }

        private void Update()
        {
            bool shouldShow = Keyboard.current != null && Keyboard.current.tabKey.isPressed &&
                              (stationMenu == null || !stationMenu.IsOpen) &&
                              (settingsMenu == null || !settingsMenu.IsOpen);
            if (root != null && root.activeSelf != shouldShow) root.SetActive(shouldShow);
            if (shouldShow) Refresh();
        }

        public void Bind(MonoBehaviour source)
        {
            if (progression == null) progression = GetComponent<ProgressionDataAdapter>();
            if (progression != null) progression.Bind(source);
        }

        public void SetVisible(bool visible)
        {
            bool allowed = visible &&
                           (stationMenu == null || !stationMenu.IsOpen) &&
                           (settingsMenu == null || !settingsMenu.IsOpen);
            if (root != null) root.SetActive(allowed);
            if (allowed) Refresh();
        }

        public void Refresh()
        {
            ResolveReferences();
            if (progression != null) progression.RefreshNow();
            for (int index = 0; index < Stats.Length && statRows != null && index < statRows.Length; index++)
            {
                if (statRows[index] == null) continue;
                ProgressionStat stat = Stats[index];
                int level = progression != null ? progression.GetLevel(stat) : 1;
                float value = progression != null ? progression.GetPurchasedValue(stat) : 0f;
                statRows[index].text = stat == ProgressionStat.MaxAmmo
                    ? Label(stat) + "  LV " + level + "  MAG " + Mathf.RoundToInt(value) + " / RES " +
                      (progression != null ? progression.GetReserveCapacityAtLevel(level) : 0)
                    : Label(stat) + "  LV " + level + "  " + Format(stat, value);
            }
            if (healthRow != null && health != null)
            {
                healthRow.text = "HP  " + Mathf.CeilToInt(health.CurrentHealth) + " / " + Mathf.CeilToInt(health.MaxHealth);
            }
            if (ammoRow != null && ammo != null)
            {
                ammoRow.text = ammo.InfiniteAmmo ? "AMMO  INF" :
                    "AMMO  " + ammo.CurrentMagazine + " / " + ammo.CurrentStorage;
            }
            if (skillsRow != null)
            {
                var ownedNames = new List<string>();
                if (progression != null)
                {
                    foreach (ProgressionSpecialSkillDefinition definition in ProgressionSpecialSkillCatalog.All)
                    {
                        if (progression.OwnsSpecial(definition.Skill)) ownedNames.Add(definition.Title);
                    }
                }
                skillsRow.text = ownedNames.Count == 0
                    ? "OWNED SKILLS  NONE"
                    : "OWNED SKILLS  " + string.Join(", ", ownedNames);
            }
        }

        private void ResolveReferences()
        {
            if (progression == null) progression = GetComponent<ProgressionDataAdapter>();
            if (health == null) health = FindFirstObjectByType<global::Player.PlayerController>()?.GetComponent<Health>();
            if (ammo == null) ammo = FindFirstObjectByType<global::Player.PlayerAmmo>();
            if (stationMenu == null) stationMenu = FindFirstObjectByType<Gameplay.Interaction.StationMenuController>();
            if (settingsMenu == null) settingsMenu = FindFirstObjectByType<global::Player.UI.SettingsMenuController>();
        }

        private static string Label(ProgressionStat stat)
        {
            switch (stat)
            {
                case ProgressionStat.MaxHealth: return "MAX HP";
                case ProgressionStat.MovementSpeed: return "MOVE";
                case ProgressionStat.FireRate: return "FIRE RATE";
                case ProgressionStat.ShootingDamage: return "SHOT DMG";
                case ProgressionStat.MeleeDamage: return "MELEE DMG";
                case ProgressionStat.MaxAmmo: return "MAX AMMO";
                default: return "DEFENSE";
            }
        }

        private static string Format(ProgressionStat stat, float value)
        {
            return stat == ProgressionStat.MovementSpeed || stat == ProgressionStat.FireRate ||
                   stat == ProgressionStat.Defense
                ? Mathf.RoundToInt(value * 100f) + "%"
                : value % 1f < .01f ? Mathf.RoundToInt(value).ToString() : value.ToString("0.0");
        }
    }
}

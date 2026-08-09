using System;
using System.Collections.Generic;

namespace Player.UI.Progression
{
    public enum ProgressionSupply
    {
        HealthPack,
        LargeHealthPack,
        AmmoPack
    }

    public enum ProgressionStat
    {
        MaxHealth,
        MovementSpeed,
        FireRate,
        ShootingDamage,
        MeleeDamage,
        Defense,
        MaxAmmo
    }

    public enum ProgressionSpecialSkill
    {
        HoldToFire,
        BulletBounce,
        Fortune,
        FortuneII,
        MedKit,
        AmmoKit,
        Ultimate,
        Quickdraw,
        Vampire,
        ExplosiveBullets,
        Headshot,
        Minigun,
        Secret,
    }

    /// <summary>Presentation and economy metadata for a one-time, run-only special skill.</summary>
    public sealed class ProgressionSpecialSkillDefinition
    {
        public ProgressionSpecialSkillDefinition(ProgressionSpecialSkill skill, string title, int cost,
            string flavor, string effect, bool hideEffect = false)
        {
            Skill = skill;
            Title = title;
            Cost = cost;
            Flavor = flavor;
            Effect = effect;
            HideEffect = hideEffect;
        }

        public ProgressionSpecialSkill Skill { get; }
        public string Title { get; }
        public int Cost { get; }
        public string Flavor { get; }
        public string Effect { get; }
        public bool HideEffect { get; }
    }

    /// <summary>Single authoritative catalog. Keep all specials here rather than scattering UI constants.</summary>
    public static class ProgressionSpecialSkillCatalog
    {
        private static readonly ProgressionSpecialSkillDefinition[] Definitions =
        {
            new ProgressionSpecialSkillDefinition(ProgressionSpecialSkill.HoldToFire, "HOLD TO FIRE", 50,
                "hold to fire", "Fire the ordinary pistol continuously while Attack is held."),
            new ProgressionSpecialSkillDefinition(ProgressionSpecialSkill.BulletBounce, "bullet bounce", 750,
                "skill issue", "Ordinary pistol rounds bounce to up to 3 enemies total."),
            new ProgressionSpecialSkillDefinition(ProgressionSpecialSkill.Fortune, "fortune", 500,
                "2007 bitcoin", "Regular enemies outside arenas award 15% more gold."),
            new ProgressionSpecialSkillDefinition(ProgressionSpecialSkill.FortuneII, "fortune II", 500,
                "2012 dropshipping", "All arena-earned gold awards 15% more gold."),
            new ProgressionSpecialSkillDefinition(ProgressionSpecialSkill.MedKit, "med kit", 400,
                "nursing school she said.", "Regular waves scatter about 15 pickups; each restores 50 HP."),
            new ProgressionSpecialSkillDefinition(ProgressionSpecialSkill.AmmoKit, "ammo kit", 600,
                "its meta trust", "Regular waves scatter about 10 pickups; each adds two magazines to reserve."),
            new ProgressionSpecialSkillDefinition(ProgressionSpecialSkill.Ultimate, "ultimate!", 800,
                "the best feature in the game", "Arena fights spawn one Thunder pickup for a 20-second Mech Ultimate."),
            new ProgressionSpecialSkillDefinition(ProgressionSpecialSkill.Quickdraw, "quickdraw", 1200,
                "it's hiiigghh noon", "Set reload time to 0.1 seconds."),
            new ProgressionSpecialSkillDefinition(ProgressionSpecialSkill.Vampire, "vampire", 2000,
                "sucky sucky", "Heal for 2% of actual damage dealt by player attacks."),
            new ProgressionSpecialSkillDefinition(ProgressionSpecialSkill.ExplosiveBullets, "explosive bullets", 750,
                "bom bom bakudan!", "Ordinary pistol impacts deal 50% splash damage in a 3-unit radius."),
            new ProgressionSpecialSkillDefinition(ProgressionSpecialSkill.Headshot, "headshot!", 800,
                "FOUR!", "Every fourth ordinary pistol round deals double damage."),
            new ProgressionSpecialSkillDefinition(ProgressionSpecialSkill.Minigun, "minigun", 4000,
                "pew pew haha", "Pistol: +30 mag, +200 reserve, -20 damage, and 2x fire rate."),
            new ProgressionSpecialSkillDefinition(ProgressionSpecialSkill.Secret, "???", 15000,
                "how'd you get here?", string.Empty, true),
        };

        public static IReadOnlyList<ProgressionSpecialSkillDefinition> All => Definitions;

        public static ProgressionSpecialSkillDefinition Get(ProgressionSpecialSkill skill)
        {
            foreach (ProgressionSpecialSkillDefinition definition in Definitions)
            {
                if (definition.Skill == skill) return definition;
            }
            return null;
        }
    }

    /// <summary>
    /// Small presentation-facing API for the run progression model. It deliberately exposes
    /// purchased values only: temporary base, shield, or Ultimate modifiers remain gameplay
    /// concerns and must not make the Tab overview lie about an upgrade's permanent value.
    /// </summary>
    public interface IProgressionUiSource
    {
        int Gold { get; }
        event Action Changed;

        bool CanPurchaseSupply(ProgressionSupply supply);
        bool TryPurchaseSupply(ProgressionSupply supply);

        int GetLevel(ProgressionStat stat);
        int MaxLevel { get; }
        bool CanUpgrade(ProgressionStat stat);
        bool TryUpgrade(ProgressionStat stat);
        int GetUpgradeCost(ProgressionStat stat);
        float GetPurchasedValue(ProgressionStat stat);
        float GetValueAtLevel(ProgressionStat stat, int level);
        int GetReserveCapacityAtLevel(int level);

        bool OwnsSpecial(ProgressionSpecialSkill skill);
        bool CanPurchaseSpecial(ProgressionSpecialSkill skill);
        bool TryPurchaseSpecial(ProgressionSpecialSkill skill);
    }
}

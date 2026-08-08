using System;

namespace Player.UI.Progression
{
    public enum ProgressionSupply
    {
        HealthPack,
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
        HoldToFire
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
        float GetPurchasedValue(ProgressionStat stat);

        bool OwnsSpecial(ProgressionSpecialSkill skill);
        bool CanPurchaseSpecial(ProgressionSpecialSkill skill);
        bool TryPurchaseSpecial(ProgressionSpecialSkill skill);
    }
}

using System;
using System.Collections.Generic;
using Combat;
using Player;
using UnityEngine;

namespace Player.UI.Progression
{
    public enum PurchaseResult
    {
        Success,
        InsufficientGold,
        MaxLevel,
        Full,
        AlreadyOwned,
        MissingTarget,
    }

    /// <summary>
    /// Owns the non-persistent single-run economy, archive levels, and special-skill ownership.
    /// Special ownership deliberately uses a set and the catalog in ProgressionUiContracts so a
    /// new one-time skill does not require adding a matching boolean field or UI code path.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class PlayerProgression : MonoBehaviour, IProgressionUiSource
    {
        public const int StartingGold = 300;
        public const int FirstStatUpgradeCost = 50;
        public const int SupplyHealthCost = 50;
        public const int SupplyLargeHealthCost = 100;
        public const int SupplyAmmoCost = 100;
        public const int HoldToFireCost = 50;
        public const int InitialLevel = 1;
        public const int MaxLevelValue = 10;

        private const float SecretStatMultiplier = 3f;
        private const float DefenseReductionCap = .90f;
        private const int MinigunMagazineBonus = 30;
        private const int MinigunReserveBonus = 200;
        private const float QuickdrawReloadTime = .1f;

        private readonly Dictionary<ProgressionStat, int> _levels = new Dictionary<ProgressionStat, int>();
        private readonly HashSet<ProgressionSpecialSkill> _ownedSpecials = new HashSet<ProgressionSpecialSkill>();
        private readonly object _secretStatModifierSource = new object();
        private readonly object _secretAmmoModifierSource = new object();

        private Health _health;
        private PlayerAmmo _ammo;
        private PlayerCombat _combat;
        private PlayerController _controller;
        private float _baseMaxHealth;
        private int _baseMagazineSize;
        private int _baseReserveCapacity;
        private float _baseReloadTime;
        private bool _initialized;

        public int Gold { get; private set; }
        public int MaxLevel => MaxLevelValue;
        public bool HoldToFireOwned => OwnsSpecial(ProgressionSpecialSkill.HoldToFire);
        public PurchaseResult LastPurchaseResult { get; private set; } = PurchaseResult.Success;
        public event Action<int> GoldChanged;
        public event Action<ProgressionStat, int> StatChanged;
        public event Action<bool> HoldToFireChanged;
        public event Action<ProgressionSpecialSkill, bool> SpecialOwnershipChanged;
        public event Action<PurchaseResult> PurchaseAttempted;
        public event Action Changed;

        private void Awake()
        {
            ResolveDependencies();
            CaptureBaseValues();
            BeginNewRun();
        }

        public int GetLevel(ProgressionStat stat) =>
            _levels.TryGetValue(stat, out int level) ? level : InitialLevel;

        /// <summary>Archive value before run-special modifiers; intended for level-card previews.</summary>
        public float GetValueAtLevel(ProgressionStat stat, int level)
        {
            int upgrades = Mathf.Clamp(level, InitialLevel, MaxLevelValue) - InitialLevel;
            switch (stat)
            {
                case ProgressionStat.MaxHealth: return _baseMaxHealth + SumArithmetic(20f, 5f, upgrades);
                case ProgressionStat.MovementSpeed: return 1f + SumArithmetic(.03f, .02f, upgrades);
                case ProgressionStat.FireRate: return 1f + SumArithmetic(.05f, .02f, upgrades);
                case ProgressionStat.ShootingDamage: return (_combat != null ? _combat.BaseRangedDamage : 20f) + SumArithmetic(4f, 1f, upgrades);
                case ProgressionStat.MeleeDamage: return (_combat != null ? _combat.BaseMeleeDamage : 20f) + SumArithmetic(3f, 3f, upgrades);
                case ProgressionStat.Defense: return SumArithmetic(.02f, .01f, upgrades);
                case ProgressionStat.MaxAmmo: return _baseMagazineSize + SumArithmetic(2f, 1f, upgrades);
                default: return 0f;
            }
        }

        /// <summary>Run overview value after permanent special-skill modifiers.</summary>
        public float GetPurchasedValue(ProgressionStat stat)
        {
            int level = GetLevel(stat);
            float value = GetValueAtLevel(stat, level);
            if (stat == ProgressionStat.ShootingDamage && _combat != null)
            {
                // The pistol has Minigun's raw -20 adjustment while other ranged attacks do not.
                return _combat.EffectivePistolDamage;
            }
            if (stat == ProgressionStat.MaxAmmo)
            {
                value += OwnsSpecial(ProgressionSpecialSkill.Minigun) ? MinigunMagazineBonus : 0;
            }
            else if (stat == ProgressionStat.ShootingDamage)
            {
                if (OwnsSpecial(ProgressionSpecialSkill.Minigun))
                    return Mathf.Max(1f, (value - 20f) * CurrentSecretMultiplier);
            }
            else if (stat == ProgressionStat.FireRate)
            {
                if (OwnsSpecial(ProgressionSpecialSkill.Minigun)) value *= 2f;
            }
            else if (stat == ProgressionStat.Defense)
            {
                return Mathf.Min(DefenseReductionCap, value * CurrentSecretMultiplier);
            }

            return value * CurrentSecretMultiplier;
        }

        public int GetReserveCapacityAtLevel(int level)
        {
            int capacity = GetArchiveReserveCapacityAtLevel(level);
            if (OwnsSpecial(ProgressionSpecialSkill.Minigun)) capacity += MinigunReserveBonus;
            return Mathf.RoundToInt(capacity * CurrentSecretMultiplier);
        }

        public int GetUpgradeCost(ProgressionStat stat)
        {
            int nextLevel = GetLevel(stat) + 1;
            if (nextLevel > MaxLevelValue) return 0;
            return nextLevel <= 6
                ? FirstStatUpgradeCost << (nextLevel - 2)
                : 800 + (nextLevel - 6) * 100;
        }

        public bool CanUpgrade(ProgressionStat stat)
        {
            ResolveDependencies();
            return GetLevel(stat) < MaxLevelValue && Gold >= GetUpgradeCost(stat) && HasTargetFor(stat);
        }

        public bool TryUpgrade(ProgressionStat stat) => TryUpgradeDetailed(stat) == PurchaseResult.Success;

        public PurchaseResult TryUpgradeDetailed(ProgressionStat stat)
        {
            ResolveDependencies();
            if (GetLevel(stat) >= MaxLevelValue) return SetResult(PurchaseResult.MaxLevel);
            int cost = GetUpgradeCost(stat);
            if (Gold < cost) return SetResult(PurchaseResult.InsufficientGold);
            if (!HasTargetFor(stat)) return SetResult(PurchaseResult.MissingTarget);

            Gold -= cost;
            _levels[stat] = GetLevel(stat) + 1;
            ApplyStat(stat, true);
            GoldChanged?.Invoke(Gold);
            StatChanged?.Invoke(stat, GetLevel(stat));
            Changed?.Invoke();
            return SetResult(PurchaseResult.Success);
        }

        public bool CanPurchaseSupply(ProgressionSupply supply)
        {
            ResolveDependencies();
            if (supply == ProgressionSupply.HealthPack || supply == ProgressionSupply.LargeHealthPack)
            {
                int cost = supply == ProgressionSupply.HealthPack ? SupplyHealthCost : SupplyLargeHealthCost;
                return _health != null && !_health.IsDead && _health.CurrentHealth < _health.MaxHealth && Gold >= cost;
            }
            return supply == ProgressionSupply.AmmoPack && _ammo != null && !_ammo.IsFull && Gold >= SupplyAmmoCost;
        }

        public bool TryPurchaseSupply(ProgressionSupply supply) =>
            TryPurchaseSupplyDetailed(supply) == PurchaseResult.Success;

        public PurchaseResult TryPurchaseSupplyDetailed(ProgressionSupply supply)
        {
            ResolveDependencies();
            if (supply == ProgressionSupply.HealthPack || supply == ProgressionSupply.LargeHealthPack)
            {
                if (_health == null) return SetResult(PurchaseResult.MissingTarget);
                if (_health.IsDead || _health.CurrentHealth >= _health.MaxHealth) return SetResult(PurchaseResult.Full);
                int cost = supply == ProgressionSupply.HealthPack ? SupplyHealthCost : SupplyLargeHealthCost;
                if (Gold < cost) return SetResult(PurchaseResult.InsufficientGold);
                Gold -= cost;
                _health.Heal(supply == ProgressionSupply.HealthPack ? 50f : 150f);
            }
            else if (supply == ProgressionSupply.AmmoPack)
            {
                if (_ammo == null) return SetResult(PurchaseResult.MissingTarget);
                if (_ammo.IsFull) return SetResult(PurchaseResult.Full);
                if (Gold < SupplyAmmoCost) return SetResult(PurchaseResult.InsufficientGold);
                Gold -= SupplyAmmoCost;
                _ammo.RefillFull();
            }
            else return SetResult(PurchaseResult.MissingTarget);

            GoldChanged?.Invoke(Gold);
            Changed?.Invoke();
            return SetResult(PurchaseResult.Success);
        }

        public bool OwnsSpecial(ProgressionSpecialSkill skill) => _ownedSpecials.Contains(skill);

        public bool CanPurchaseSpecial(ProgressionSpecialSkill skill)
        {
            ProgressionSpecialSkillDefinition definition = ProgressionSpecialSkillCatalog.Get(skill);
            return definition != null && !OwnsSpecial(skill) && Gold >= definition.Cost;
        }

        public bool TryPurchaseSpecial(ProgressionSpecialSkill skill) =>
            TryPurchaseSpecialDetailed(skill) == PurchaseResult.Success;

        public PurchaseResult TryPurchaseSpecialDetailed(ProgressionSpecialSkill skill)
        {
            ResolveDependencies();
            ProgressionSpecialSkillDefinition definition = ProgressionSpecialSkillCatalog.Get(skill);
            if (definition == null) return SetResult(PurchaseResult.MissingTarget);
            if (OwnsSpecial(skill)) return SetResult(PurchaseResult.AlreadyOwned);
            if (Gold < definition.Cost) return SetResult(PurchaseResult.InsufficientGold);

            Gold -= definition.Cost;
            _ownedSpecials.Add(skill);
            ApplySpecial(skill, true);
            GoldChanged?.Invoke(Gold);
            SpecialOwnershipChanged?.Invoke(skill, true);
            if (skill == ProgressionSpecialSkill.HoldToFire) HoldToFireChanged?.Invoke(true);
            Changed?.Invoke();
            return SetResult(PurchaseResult.Success);
        }

        /// <summary>Explicit reset hook for a restart/new-run flow. Nothing is saved between runs.</summary>
        public void BeginNewRun()
        {
            ResolveDependencies();
            CaptureBaseValues();
            Gold = StartingGold;
            var previouslyOwned = new List<ProgressionSpecialSkill>(_ownedSpecials);
            _ownedSpecials.Clear();
            LastPurchaseResult = PurchaseResult.Success;
            foreach (ProgressionStat stat in (ProgressionStat[])Enum.GetValues(typeof(ProgressionStat)))
                _levels[stat] = InitialLevel;

            ResetExternalSpecialState();
            if (_health != null)
            {
                _health.SetMaxHealth(_baseMaxHealth);
                _health.FullyHeal();
            }
            if (_ammo != null)
            {
                _ammo.SetCapacities(_baseMagazineSize, _baseReserveCapacity);
                _ammo.SetReloadTime(_baseReloadTime);
                _ammo.RefillFull();
            }

            ApplyAllStats(false);
            _initialized = true;
            GoldChanged?.Invoke(Gold);
            foreach (ProgressionSpecialSkill skill in previouslyOwned) SpecialOwnershipChanged?.Invoke(skill, false);
            HoldToFireChanged?.Invoke(false);
            Changed?.Invoke();
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            GoldChanged?.Invoke(Gold);
            Changed?.Invoke();
        }

        private float CurrentSecretMultiplier => OwnsSpecial(ProgressionSpecialSkill.Secret) ? SecretStatMultiplier : 1f;

        private void ResolveDependencies()
        {
            if (_health == null) _health = GetComponent<Health>() ?? GetComponentInChildren<Health>(true);
            if (_ammo == null) _ammo = GetComponent<PlayerAmmo>() ?? GetComponentInChildren<PlayerAmmo>(true);
            if (_combat == null) _combat = GetComponent<PlayerCombat>() ?? GetComponentInChildren<PlayerCombat>(true);
            if (_controller == null) _controller = GetComponent<PlayerController>() ?? GetComponentInChildren<PlayerController>(true);
        }

        private void CaptureBaseValues()
        {
            if (_initialized) return;
            if (_health != null) _baseMaxHealth = _health.MaxHealth;
            if (_ammo != null)
            {
                _baseMagazineSize = _ammo.MagazineSize;
                _baseReserveCapacity = _ammo.MaxStorage;
                _baseReloadTime = _ammo.ReloadTime;
            }
        }

        private bool HasTargetFor(ProgressionStat stat)
        {
            switch (stat)
            {
                case ProgressionStat.MaxHealth:
                case ProgressionStat.Defense: return _health != null;
                case ProgressionStat.MovementSpeed: return _controller != null;
                case ProgressionStat.FireRate:
                case ProgressionStat.ShootingDamage:
                case ProgressionStat.MeleeDamage: return _combat != null;
                case ProgressionStat.MaxAmmo: return _ammo != null;
                default: return false;
            }
        }

        private void ApplyAllStats(bool grantNewCapacity)
        {
            foreach (ProgressionStat stat in (ProgressionStat[])Enum.GetValues(typeof(ProgressionStat)))
                ApplyStat(stat, grantNewCapacity);
        }

        private void ApplyStat(ProgressionStat stat, bool grantNewCapacity)
        {
            switch (stat)
            {
                case ProgressionStat.MaxHealth:
                    _health?.SetMaxHealth(GetPurchasedValue(stat), grantNewCapacity);
                    break;
                case ProgressionStat.MovementSpeed:
                    _controller?.SetMovementSpeedModifier(this, GetPurchasedValue(stat));
                    break;
                case ProgressionStat.FireRate:
                    _combat?.SetFireRateModifier(this, GetValueAtLevel(stat, GetLevel(stat)));
                    _combat?.SetFireRateModifier(_secretStatModifierSource, CurrentSecretMultiplier);
                    break;
                case ProgressionStat.ShootingDamage:
                    _combat?.SetRangedDamageBonus(this, GetValueAtLevel(stat, GetLevel(stat)) - _combat.BaseRangedDamage);
                    _combat?.SetRangedDamageModifier(_secretStatModifierSource, CurrentSecretMultiplier);
                    break;
                case ProgressionStat.MeleeDamage:
                    _combat?.SetMeleeDamageBonus(this, GetValueAtLevel(stat, GetLevel(stat)) - _combat.BaseMeleeDamage);
                    _combat?.SetMeleeDamageModifier(_secretStatModifierSource, CurrentSecretMultiplier);
                    break;
                case ProgressionStat.Defense:
                    _health?.SetIncomingDamageModifier(this, 1f - GetPurchasedValue(stat));
                    break;
                case ProgressionStat.MaxAmmo:
                    ApplyAmmoCapacity(grantNewCapacity);
                    break;
            }
        }

        private void ApplySpecial(ProgressionSpecialSkill skill, bool grantNewCapacity)
        {
            switch (skill)
            {
                case ProgressionSpecialSkill.HoldToFire:
                    _combat?.SetHoldToFireUnlocked(true);
                    break;
                case ProgressionSpecialSkill.BulletBounce:
                    _combat?.SetBulletBounceEnabled(true);
                    break;
                case ProgressionSpecialSkill.Quickdraw:
                    _ammo?.SetReloadTime(QuickdrawReloadTime);
                    break;
                case ProgressionSpecialSkill.Vampire:
                    _combat?.SetVampireEnabled(true);
                    break;
                case ProgressionSpecialSkill.ExplosiveBullets:
                    _combat?.SetExplosiveBulletsEnabled(true);
                    break;
                case ProgressionSpecialSkill.Headshot:
                    _combat?.SetHeadshotEnabled(true);
                    break;
                case ProgressionSpecialSkill.Minigun:
                    _combat?.SetMinigunEnabled(true);
                    _ammo?.SetMinigunCapacityEnabled(true);
                    ApplyAmmoCapacity(grantNewCapacity);
                    break;
                case ProgressionSpecialSkill.Secret:
                    ApplyAllStats(grantNewCapacity);
                    break;
            }
        }

        private void ResetExternalSpecialState()
        {
            _combat?.ResetOrdinaryPistolRoundCount();
            _combat?.SetHoldToFireUnlocked(false);
            _combat?.SetBulletBounceEnabled(false);
            _combat?.SetVampireEnabled(false);
            _combat?.SetExplosiveBulletsEnabled(false);
            _combat?.SetHeadshotEnabled(false);
            _combat?.SetMinigunEnabled(false);
            _combat?.SetRangedDamageModifier(_secretStatModifierSource, 1f);
            _combat?.SetMeleeDamageModifier(_secretStatModifierSource, 1f);
            _combat?.SetFireRateModifier(_secretStatModifierSource, 1f);
            _ammo?.SetMinigunCapacityEnabled(false);
            _ammo?.RemoveCapacityMultiplier(_secretAmmoModifierSource);
        }

        private int GetArchiveReserveCapacityAtLevel(int level)
        {
            int upgrades = Mathf.Clamp(level, InitialLevel, MaxLevelValue) - InitialLevel;
            return _baseReserveCapacity + Mathf.RoundToInt(SumArithmetic(5f, 5f, upgrades));
        }

        /// <summary>Archive capacities remain the base layer; special layers compose after it.</summary>
        private void ApplyAmmoCapacity(bool grantNewCapacity)
        {
            if (_ammo == null) return;

            int archiveMagazine = Mathf.RoundToInt(GetValueAtLevel(ProgressionStat.MaxAmmo,
                GetLevel(ProgressionStat.MaxAmmo)));
            int archiveReserve = GetArchiveReserveCapacityAtLevel(GetLevel(ProgressionStat.MaxAmmo));
            _ammo.SetCapacities(archiveMagazine, archiveReserve, grantNewCapacity);

            if (OwnsSpecial(ProgressionSpecialSkill.Secret))
            {
                _ammo.SetCapacityMultiplier(_secretAmmoModifierSource, SecretStatMultiplier, grantNewCapacity);
            }
            else
            {
                _ammo.RemoveCapacityMultiplier(_secretAmmoModifierSource);
            }
        }

        private static float SumArithmetic(float firstTerm, float increasePerPurchase, int purchases)
        {
            if (purchases <= 0) return 0f;
            return purchases * (2f * firstTerm + (purchases - 1) * increasePerPurchase) * .5f;
        }

        private PurchaseResult SetResult(PurchaseResult result)
        {
            LastPurchaseResult = result;
            PurchaseAttempted?.Invoke(result);
            return result;
        }
    }
}

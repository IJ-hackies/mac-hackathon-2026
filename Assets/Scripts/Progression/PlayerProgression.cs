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
    /// Owns the intentionally non-persistent, single-run economy. Attach it to the player root;
    /// a fresh scene/player begins a fresh run with the approved starting state.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class PlayerProgression : MonoBehaviour, IProgressionUiSource
    {
        public const int StartingGold = 10000;
        public const int StatUpgradeCost = 100;
        public const int SupplyHealthCost = 50;
        public const int SupplyAmmoCost = 100;
        public const int HoldToFireCost = 500;
        public const int InitialLevel = 1;
        public const int MaxLevelValue = 10;

        private readonly Dictionary<ProgressionStat, int> _levels = new Dictionary<ProgressionStat, int>();
        private Health _health;
        private PlayerAmmo _ammo;
        private PlayerCombat _combat;
        private PlayerController _controller;
        private float _baseMaxHealth;
        private int _baseMagazineSize;
        private bool _initialized;
        private bool _holdToFireOwned;

        public int Gold { get; private set; }
        public int MaxLevel => MaxLevelValue;
        public bool HoldToFireOwned => _holdToFireOwned;
        public PurchaseResult LastPurchaseResult { get; private set; } = PurchaseResult.Success;
        public event Action<int> GoldChanged;
        public event Action<ProgressionStat, int> StatChanged;
        public event Action<bool> HoldToFireChanged;
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

        public bool CanUpgrade(ProgressionStat stat)
        {
            ResolveDependencies();
            return GetLevel(stat) < MaxLevelValue && Gold >= StatUpgradeCost && HasTargetFor(stat);
        }

        public float GetPurchasedValue(ProgressionStat stat)
        {
            int upgrades = GetLevel(stat) - InitialLevel;
            switch (stat)
            {
                case ProgressionStat.MaxHealth: return _baseMaxHealth + upgrades * 10f;
                case ProgressionStat.MovementSpeed: return 1f + upgrades * .03f;
                case ProgressionStat.FireRate: return 1f + upgrades * .05f;
                case ProgressionStat.ShootingDamage: return (_combat != null ? _combat.BaseRangedDamage : 15f) * (1f + upgrades * .1f);
                case ProgressionStat.MeleeDamage: return (_combat != null ? _combat.BaseMeleeDamage : 20f) * (1f + upgrades * .1f);
                case ProgressionStat.Defense: return upgrades * .04f;
                case ProgressionStat.MaxAmmo: return _baseMagazineSize + upgrades * 2;
                default: return 0f;
            }
        }

        public bool TryUpgrade(ProgressionStat stat) => TryUpgradeDetailed(stat) == PurchaseResult.Success;

        public PurchaseResult TryUpgradeDetailed(ProgressionStat stat)
        {
            ResolveDependencies();
            if (GetLevel(stat) >= MaxLevelValue) return SetResult(PurchaseResult.MaxLevel);
            if (Gold < StatUpgradeCost) return SetResult(PurchaseResult.InsufficientGold);
            if (!HasTargetFor(stat)) return SetResult(PurchaseResult.MissingTarget);

            Gold -= StatUpgradeCost;
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
            if (supply == ProgressionSupply.HealthPack)
                return _health != null && !_health.IsDead && _health.CurrentHealth < _health.MaxHealth && Gold >= SupplyHealthCost;
            return _ammo != null && !_ammo.IsFull && Gold >= SupplyAmmoCost;
        }

        public bool TryPurchaseSupply(ProgressionSupply supply) =>
            TryPurchaseSupplyDetailed(supply) == PurchaseResult.Success;

        public PurchaseResult TryPurchaseSupplyDetailed(ProgressionSupply supply)
        {
            ResolveDependencies();
            if (supply == ProgressionSupply.HealthPack)
            {
                if (_health == null) return SetResult(PurchaseResult.MissingTarget);
                if (_health.IsDead || _health.CurrentHealth >= _health.MaxHealth) return SetResult(PurchaseResult.Full);
                if (Gold < SupplyHealthCost) return SetResult(PurchaseResult.InsufficientGold);
                Gold -= SupplyHealthCost;
                _health.FullyHeal();
            }
            else
            {
                if (_ammo == null) return SetResult(PurchaseResult.MissingTarget);
                if (_ammo.IsFull) return SetResult(PurchaseResult.Full);
                if (Gold < SupplyAmmoCost) return SetResult(PurchaseResult.InsufficientGold);
                Gold -= SupplyAmmoCost;
                _ammo.RefillFull();
            }

            GoldChanged?.Invoke(Gold);
            Changed?.Invoke();
            return SetResult(PurchaseResult.Success);
        }

        public bool OwnsSpecial(ProgressionSpecialSkill skill) => skill == ProgressionSpecialSkill.HoldToFire && _holdToFireOwned;

        public bool CanPurchaseSpecial(ProgressionSpecialSkill skill)
        {
            ResolveDependencies();
            return skill == ProgressionSpecialSkill.HoldToFire && !_holdToFireOwned && _combat != null && Gold >= HoldToFireCost;
        }

        public bool TryPurchaseSpecial(ProgressionSpecialSkill skill) =>
            TryPurchaseSpecialDetailed(skill) == PurchaseResult.Success;

        public PurchaseResult TryPurchaseSpecialDetailed(ProgressionSpecialSkill skill)
        {
            ResolveDependencies();
            if (skill != ProgressionSpecialSkill.HoldToFire || _combat == null) return SetResult(PurchaseResult.MissingTarget);
            if (_holdToFireOwned) return SetResult(PurchaseResult.AlreadyOwned);
            if (Gold < HoldToFireCost) return SetResult(PurchaseResult.InsufficientGold);

            Gold -= HoldToFireCost;
            _holdToFireOwned = true;
            _combat.SetHoldToFireUnlocked(true);
            GoldChanged?.Invoke(Gold);
            HoldToFireChanged?.Invoke(true);
            Changed?.Invoke();
            return SetResult(PurchaseResult.Success);
        }

        /// <summary>Explicit reset hook for a restart/new-run flow. Nothing is saved between runs.</summary>
        public void BeginNewRun()
        {
            ResolveDependencies();
            CaptureBaseValues();
            Gold = StartingGold;
            _holdToFireOwned = false;
            LastPurchaseResult = PurchaseResult.Success;
            foreach (ProgressionStat stat in (ProgressionStat[])Enum.GetValues(typeof(ProgressionStat)))
                _levels[stat] = InitialLevel;

            if (_health != null)
            {
                _health.SetMaxHealth(_baseMaxHealth);
                _health.FullyHeal();
            }
            if (_ammo != null)
            {
                _ammo.SetMagazineSize(_baseMagazineSize);
                _ammo.RefillFull();
            }

            ApplyAllStats(false);
            _combat?.SetHoldToFireUnlocked(false);
            _initialized = true;
            GoldChanged?.Invoke(Gold);
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
            if (_ammo != null) _baseMagazineSize = _ammo.MagazineSize;
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
            int upgrades = GetLevel(stat) - InitialLevel;
            switch (stat)
            {
                case ProgressionStat.MaxHealth:
                    _health?.SetMaxHealth(_baseMaxHealth + upgrades * 10f, grantNewCapacity);
                    break;
                case ProgressionStat.MovementSpeed:
                    _controller?.SetMovementSpeedModifier(this, 1f + upgrades * .03f);
                    break;
                case ProgressionStat.FireRate:
                    _combat?.SetFireRateModifier(this, 1f + upgrades * .05f);
                    break;
                case ProgressionStat.ShootingDamage:
                    _combat?.SetRangedDamageModifier(this, 1f + upgrades * .1f);
                    break;
                case ProgressionStat.MeleeDamage:
                    _combat?.SetMeleeDamageModifier(this, 1f + upgrades * .1f);
                    break;
                case ProgressionStat.Defense:
                    _health?.SetIncomingDamageModifier(this, 1f - upgrades * .04f);
                    break;
                case ProgressionStat.MaxAmmo:
                    _ammo?.SetMagazineSize(_baseMagazineSize + upgrades * 2, grantNewCapacity);
                    break;
            }
        }

        private PurchaseResult SetResult(PurchaseResult result)
        {
            LastPurchaseResult = result;
            PurchaseAttempted?.Invoke(result);
            return result;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Player
{
    /// First-pass ammo/reload system: a magazine drawn from a storage reserve. PlayerCombat
    /// calls TryConsumeRound() once per shot beat instead of firing unconditionally; running
    /// dry auto-starts a reload (if storage remains), and the Reload input can also trigger one
    /// manually. Values are placeholder tuning, meant to be retuned in the Inspector alongside
    /// future reload-speed/magazine-size upgrades rather than hardcoded balance.
    public class PlayerAmmo : MonoBehaviour
    {
        [SerializeField] private int magazineSize = 15;
        [SerializeField] private int maxStorage = 120;
        [SerializeField] private float reloadTime = 1.5f;

        public int MagazineSize => magazineSize;
        public int MaxStorage => maxStorage;
        public float ReloadTime => reloadTime;
        public int CurrentMagazine { get; private set; }
        public int CurrentStorage { get; private set; }
        public bool IsReloading { get; private set; }
        // Set by Player.PlayerUltimate while the Mech's left-click (electric bolts) is active -
        // "constant shooting, no reload." TryConsumeRound/StartReload short-circuit to true/no-op
        // rather than every caller needing its own infinite-ammo branch.
        public bool InfiniteAmmo { get; private set; }
        public bool IsFull => CurrentMagazine >= magazineSize && CurrentStorage >= maxStorage;

        public event Action<int, int> AmmoChanged;
        public event Action ReloadStarted;
        public event Action ReloadFinished;

        private Coroutine _reloadRoutine;
        private int _baseMagazineSize;
        private int _baseMaxStorage;
        private bool _baseCapacitiesInitialized;
        private readonly Dictionary<object, CapacityBonus> _capacityBonuses = new Dictionary<object, CapacityBonus>();
        private readonly Dictionary<object, float> _capacityMultipliers = new Dictionary<object, float>();
        private static readonly object MinigunCapacitySource = new object();

        private readonly struct CapacityBonus
        {
            public readonly int Magazine;
            public readonly int Storage;

            public CapacityBonus(int magazine, int storage)
            {
                Magazine = magazine;
                Storage = storage;
            }
        }

        private void Awake()
        {
            EnsureBaseCapacities();
            CurrentMagazine = magazineSize;
            CurrentStorage = maxStorage;
        }

        private void OnEnable()
        {
            AmmoChanged?.Invoke(CurrentMagazine, CurrentStorage);
        }

        /// Called once per shot. Returns false (no round fired) when reloading or the magazine
        /// is empty; an empty magazine with storage remaining auto-starts a reload.
        public bool TryConsumeRound()
        {
            if (InfiniteAmmo) return true;
            if (IsReloading) return false;

            if (CurrentMagazine <= 0)
            {
                if (CurrentStorage > 0) StartReload();
                return false;
            }

            CurrentMagazine--;
            AmmoChanged?.Invoke(CurrentMagazine, CurrentStorage);
            return true;
        }

        public void StartReload()
        {
            if (InfiniteAmmo || IsReloading || CurrentMagazine >= magazineSize || CurrentStorage <= 0) return;
            _reloadRoutine = StartCoroutine(ReloadRoutine());
        }

        /// Called by PlayerUltimate on activate/end. Cancels an in-progress reload when turning
        /// infinite ammo on (there's nothing left to wait for), and re-broadcasts the current
        /// magazine/storage so AmmoHudUI can immediately swap to/from its "∞" display.
        public void SetInfiniteAmmo(bool infinite)
        {
            InfiniteAmmo = infinite;
            if (infinite && _reloadRoutine != null)
            {
                StopCoroutine(_reloadRoutine);
                _reloadRoutine = null;
                IsReloading = false;
                ReloadFinished?.Invoke();
            }

            AmmoChanged?.Invoke(CurrentMagazine, CurrentStorage);
        }

        private IEnumerator ReloadRoutine()
        {
            IsReloading = true;
            ReloadStarted?.Invoke();

            yield return new WaitForSeconds(reloadTime);

            int needed = magazineSize - CurrentMagazine;
            int drawn = Mathf.Min(needed, CurrentStorage);
            CurrentMagazine += drawn;
            CurrentStorage -= drawn;

            IsReloading = false;
            _reloadRoutine = null;
            AmmoChanged?.Invoke(CurrentMagazine, CurrentStorage);
            ReloadFinished?.Invoke();
        }

        /// Used by AmmoPickup - fully tops up both the magazine and the storage reserve.
        public void RefillFull()
        {
            if (_reloadRoutine != null)
            {
                StopCoroutine(_reloadRoutine);
                _reloadRoutine = null;
                IsReloading = false;
                ReloadFinished?.Invoke();
            }

            CurrentMagazine = magazineSize;
            CurrentStorage = maxStorage;
            AmmoChanged?.Invoke(CurrentMagazine, CurrentStorage);
        }

        /// <summary>
        /// Updates the archive/base capacities. Special capacity bonuses remain layered on top,
        /// so subsequent archive upgrades cannot erase Minigun's extra magazine and reserve.
        /// </summary>
        public void SetCapacities(int newMagazineSize, int newMaxStorage, bool grantCapacityDifference = false)
        {
            EnsureBaseCapacities();
            newMagazineSize = Mathf.Max(0, newMagazineSize);
            newMaxStorage = Mathf.Max(0, newMaxStorage);

            int previousMagazineSize = magazineSize;
            int previousMaxStorage = maxStorage;
            _baseMagazineSize = newMagazineSize;
            _baseMaxStorage = newMaxStorage;
            ResolveCapacities();

            if (grantCapacityDifference)
            {
                CurrentMagazine = Mathf.Min(magazineSize,
                    CurrentMagazine + Mathf.Max(0, magazineSize - previousMagazineSize));
                CurrentStorage = Mathf.Min(maxStorage,
                    CurrentStorage + Mathf.Max(0, maxStorage - previousMaxStorage));
            }
            else
            {
                CurrentMagazine = Mathf.Min(CurrentMagazine, magazineSize);
                CurrentStorage = Mathf.Min(CurrentStorage, maxStorage);
            }

            AmmoChanged?.Invoke(CurrentMagazine, CurrentStorage);
        }

        /// <summary>Changes magazine capacity while retaining the existing reserve capacity.</summary>
        public void SetMagazineSize(int value, bool addCapacityDifferenceToMagazine = false)
        {
            EnsureBaseCapacities();
            SetCapacities(value, _baseMaxStorage, addCapacityDifferenceToMagazine);
        }

        /// <summary>Changes reserve capacity while retaining the existing magazine capacity.</summary>
        public void SetMaxStorage(int value, bool addCapacityDifferenceToStorage = false)
        {
            EnsureBaseCapacities();
            SetCapacities(_baseMagazineSize, value, addCapacityDifferenceToStorage);
        }

        /// <summary>
        /// Layers a run-scoped capacity bonus over the archive/base values. Positive capacity
        /// changes can immediately grant the added rounds; removal only clamps current ammo.
        /// </summary>
        public void SetCapacityBonus(object source, int magazineBonus, int storageBonus,
            bool grantCapacityDifference = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            EnsureBaseCapacities();

            int previousMagazineSize = magazineSize;
            int previousMaxStorage = maxStorage;
            _capacityBonuses[source] = new CapacityBonus(magazineBonus, storageBonus);
            ResolveCapacities();
            ApplyCapacityChange(previousMagazineSize, previousMaxStorage, grantCapacityDifference);
        }

        public void RemoveCapacityBonus(object source)
        {
            if (source == null || !_capacityBonuses.Remove(source)) return;

            int previousMagazineSize = magazineSize;
            int previousMaxStorage = maxStorage;
            ResolveCapacities();
            ApplyCapacityChange(previousMagazineSize, previousMaxStorage, false);
        }

        /// <summary>
        /// Multiplies the final capacity after all raw bonuses. This lets a later secret skill
        /// correctly turn Minigun's <c>archive + bonus</c> capacities into <c>(archive + bonus) x 3</c>.
        /// </summary>
        public void SetCapacityMultiplier(object source, float multiplier, bool grantCapacityDifference = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f)
                throw new ArgumentOutOfRangeException(nameof(multiplier));

            EnsureBaseCapacities();
            int previousMagazineSize = magazineSize;
            int previousMaxStorage = maxStorage;
            _capacityMultipliers[source] = multiplier;
            ResolveCapacities();
            ApplyCapacityChange(previousMagazineSize, previousMaxStorage, grantCapacityDifference);
        }

        public void RemoveCapacityMultiplier(object source)
        {
            if (source == null || !_capacityMultipliers.Remove(source)) return;

            int previousMagazineSize = magazineSize;
            int previousMaxStorage = maxStorage;
            ResolveCapacities();
            ApplyCapacityChange(previousMagazineSize, previousMaxStorage, false);
        }

        /// <summary>Applies Minigun's +30 magazine / +200 reserve bonus without touching upgrades.</summary>
        public void SetMinigunCapacityEnabled(bool enabled)
        {
            if (enabled) SetCapacityBonus(MinigunCapacitySource, 30, 200, true);
            else RemoveCapacityBonus(MinigunCapacitySource);
        }

        /// <summary>Sets the reload duration used by future reloads. Quickdraw passes 0.1 seconds.</summary>
        public void SetReloadTime(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Reload time must be finite and greater than zero.");
            reloadTime = value;
        }

        private void EnsureBaseCapacities()
        {
            if (_baseCapacitiesInitialized) return;
            _baseMagazineSize = magazineSize;
            _baseMaxStorage = maxStorage;
            _baseCapacitiesInitialized = true;
        }

        private void ResolveCapacities()
        {
            int magazineBonus = 0;
            int storageBonus = 0;
            foreach (CapacityBonus bonus in _capacityBonuses.Values)
            {
                magazineBonus += bonus.Magazine;
                storageBonus += bonus.Storage;
            }

            float multiplier = 1f;
            foreach (float value in _capacityMultipliers.Values) multiplier *= value;

            magazineSize = Mathf.Max(0, Mathf.RoundToInt((_baseMagazineSize + magazineBonus) * multiplier));
            maxStorage = Mathf.Max(0, Mathf.RoundToInt((_baseMaxStorage + storageBonus) * multiplier));
        }

        private void ApplyCapacityChange(int previousMagazineSize, int previousMaxStorage,
            bool grantCapacityDifference)
        {
            if (grantCapacityDifference)
            {
                CurrentMagazine = Mathf.Min(magazineSize,
                    CurrentMagazine + Mathf.Max(0, magazineSize - previousMagazineSize));
                CurrentStorage = Mathf.Min(maxStorage,
                    CurrentStorage + Mathf.Max(0, maxStorage - previousMaxStorage));
            }
            else
            {
                CurrentMagazine = Mathf.Min(CurrentMagazine, magazineSize);
                CurrentStorage = Mathf.Min(CurrentStorage, maxStorage);
            }

            AmmoChanged?.Invoke(CurrentMagazine, CurrentStorage);
        }
    }
}

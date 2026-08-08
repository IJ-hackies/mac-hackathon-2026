using System;
using System.Collections;
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
        [SerializeField] private int magazineSize = 12;
        [SerializeField] private int maxStorage = 90;
        [SerializeField] private float reloadTime = 1.5f;

        public int MagazineSize => magazineSize;
        public int MaxStorage => maxStorage;
        public int CurrentMagazine { get; private set; }
        public int CurrentStorage { get; private set; }
        public bool IsReloading { get; private set; }
        // Set by Player.PlayerUltimate while the Mech's left-click (electric bolts) is active -
        // "constant shooting, no reload." TryConsumeRound/StartReload short-circuit to true/no-op
        // rather than every caller needing its own infinite-ammo branch.
        public bool InfiniteAmmo { get; private set; }

        public event Action<int, int> AmmoChanged;
        public event Action ReloadStarted;
        public event Action ReloadFinished;

        private Coroutine _reloadRoutine;

        private void Awake()
        {
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
    }
}

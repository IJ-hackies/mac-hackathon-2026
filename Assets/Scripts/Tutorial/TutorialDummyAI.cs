using Combat;
using Enemies;
using Player;
using UnityEngine;

namespace Tutorial
{
    /// The large-enemy practice dummy: always faces the player, never attacks, and never dies -
    /// Health is given a huge ceiling and fully healed after every hit lands, rather than zeroing
    /// incoming damage (which would also silently swallow Health.Hit and break the counting
    /// below). DamageType alone can't tell a light (pistol, LMB/Attack) hit from a heavy
    /// (secondary, RMB/Attack2) hit apart - both tag Combat.DamageType.Ranged (see
    /// PlayerCombat.FireProjectile/DamageIfStillNear) - so this correlates Health.Hit against
    /// PlayerCombat's ShotFired/SecondaryFired timestamps instead.
    public class TutorialDummyAI : EnemyBase
    {
        private const float HitAttributionWindow = 0.6f;
        private const float InvincibleMaxHealth = 1_000_000f;

        [SerializeField] private PlayerCombat playerCombat;

        private float _lastShotTime = -999f;
        private float _lastSecondaryTime = -999f;

        public event System.Action LightHitLanded;
        public event System.Action HeavyHitLanded;

        public void Configure(PlayerCombat combat)
        {
            playerCombat = combat;
        }

        protected override void Awake()
        {
            base.Awake();
            if (playerCombat == null) playerCombat = FindFirstObjectByType<PlayerCombat>();
            health.SetMaxHealth(InvincibleMaxHealth, addToCurrentHealth: true);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            health.Hit += HandleTutorialHit;
            if (playerCombat != null)
            {
                playerCombat.ShotFired += OnShotFired;
                playerCombat.SecondaryFired += OnSecondaryFired;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            health.Hit -= HandleTutorialHit;
            if (playerCombat != null)
            {
                playerCombat.ShotFired -= OnShotFired;
                playerCombat.SecondaryFired -= OnSecondaryFired;
            }
        }

        private void Update()
        {
            FacePlayer();
        }

        private void OnShotFired() => _lastShotTime = Time.time;
        private void OnSecondaryFired() => _lastSecondaryTime = Time.time;

        private void HandleTutorialHit(DamageType damageType)
        {
            health.FullyHeal();

            bool heavy = _lastSecondaryTime >= _lastShotTime &&
                         Time.time - _lastSecondaryTime <= HitAttributionWindow;
            bool light = !heavy && Time.time - _lastShotTime <= HitAttributionWindow;

            if (heavy) HeavyHitLanded?.Invoke();
            else if (light) LightHitLanded?.Invoke();
        }
    }
}

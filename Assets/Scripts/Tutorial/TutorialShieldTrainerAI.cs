using System;
using System.Collections;
using Combat;
using Enemies;
using Player;
using UnityEngine;

namespace Tutorial
{
    /// A stationary flying enemy for the Power-Ups room: it turns to face the player every frame
    /// and, on a fixed interval, fires a real travelling projectile at them (the same generic
    /// Enemies.BossProjectile the flying enemy/bosses use) after a short telegraph delay. The
    /// player is expected to be holding Shield (Shift, once Thunder has activated Ultimate) when
    /// it lands. Shield being active at the moment of impact reports the hit as mitigated via
    /// DamageMitigated, which TutorialManager sums toward the stage's requirement - the blue
    /// "damage blocked" popup itself is Player.PlayerShield's own permanent, game-wide feedback
    /// (via Combat.Health.MitigatedDamage), not duplicated here. If Shield is not active, the
    /// projectile deals its normal real damage and this immediately heals it back, so the player
    /// sees an authentic hit but this trainer can never actually kill them - the same "never
    /// lethal" guarantee TutorialDummyAI gives the combat stage, applied to the player's side
    /// instead of the target's.
    public class TutorialShieldTrainerAI : MonoBehaviour
    {
        [SerializeField] private Transform firePoint;
        [SerializeField] private float attackInterval = 2.5f;
        [SerializeField] private float chargeDelay = 0.5f;
        [SerializeField] private float damagePerAttack = 10f;
        [SerializeField] private float projectileSpeed = 25f;
        [SerializeField] private GameObject projectileVisualPrefab;
        [SerializeField] private GameObject impactEffectPrefab;
        [SerializeField] private Quaternion projectileVisualRotationOffset = Quaternion.Euler(0f, 90f, 0f);
        [SerializeField] private PlayerShield playerShield;
        [SerializeField] private Health playerHealth;

        private Transform _player;
        private float _nextAttackTime;

        public event Action<float> DamageMitigated;

        public void Configure(PlayerShield shield, Health health)
        {
            playerShield = shield;
            playerHealth = health;
        }

        private void Awake()
        {
            var controller = FindFirstObjectByType<PlayerController>();
            if (controller != null)
            {
                _player = controller.transform;
                if (playerShield == null) playerShield = controller.GetComponent<PlayerShield>();
                if (playerHealth == null) playerHealth = controller.GetComponent<Health>();
            }
            if (firePoint == null) firePoint = transform;
        }

        private void OnEnable()
        {
            _nextAttackTime = Time.time + attackInterval;
        }

        private void Update()
        {
            FacePlayer();

            if (Time.time < _nextAttackTime) return;
            _nextAttackTime = Time.time + attackInterval;
            StartCoroutine(AttackRoutine());
        }

        private void FacePlayer()
        {
            if (_player == null) return;

            Vector3 toPlayer = _player.position - transform.position;
            if (toPlayer.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        }

        private IEnumerator AttackRoutine()
        {
            yield return new WaitForSeconds(chargeDelay);
            if (_player == null) yield break;

            Vector3 direction = (_player.position + Vector3.up - firePoint.position).normalized;
            var visuals = new BossProjectileVisuals
            {
                ImportedVisualPrefab = projectileVisualPrefab,
                ExtraRotationOffset = projectileVisualRotationOffset,
                ImpactEffectPrefab = impactEffectPrefab,
                ImpactEffectScale = 1f,
            };

            BossProjectile.Create(firePoint.position, direction, null, projectileSpeed, damagePerAttack,
                false, 5f, ~0, Color.yellow, 0.3f, ProjectileVisualStyle.Bolt,
                visuals: visuals, onHit: _ => HandleHit());
        }

        private void HandleHit()
        {
            if (playerShield != null && playerShield.IsActive)
            {
                // PlayerShield's own Health.MitigatedDamage subscription already pops the blue
                // "damage blocked" number - this call is purely for the tutorial's own tally.
                DamageMitigated?.Invoke(damagePerAttack);
                return;
            }

            if (playerHealth != null && !playerHealth.IsDead)
            {
                playerHealth.Heal(damagePerAttack);
            }
        }
    }
}

using System.Collections;
using UnityEngine;

namespace Enemies
{
    /// Ranged flyer: wanders randomly while drifting toward the player, capped at
    /// maxDistanceFromPlayer. Attack fires a travelling Projectiles_green_shuriken shot
    /// (Enemies.BossProjectile) straight at the player with no warning telegraph - "it should
    /// just shoot as normal" - and at 1.8x its earlier fire rate (attackCooldown divided by 1.8).
    /// The tracking-telegraph-then-freeze dodge window and the older instant-hitscan laser beam
    /// have both been removed; this is now a plain fire-and-forget ranged attack.
    public class EnemyFlyingAI : EnemyBase
    {
        [Header("Movement")]
        [SerializeField] private float hoverHeight = 2f;
        [SerializeField] private float bobAmplitude = 0.25f;
        [SerializeField] private float bobSpeed = 1.5f;
        [SerializeField] private float wanderSpeed = 1.2f;
        [SerializeField] private float approachSpeed = 1.8f;
        [SerializeField] private float maxDistanceFromPlayer = 14f;
        [SerializeField] private float wanderRadius = 4f;
        [SerializeField] private float wanderInterval = 3f;

        [Header("Attack")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private float attackRange = 16f;
        [Tooltip("Raised x1.8 from the original 4s - shorter cooldown = higher fire rate.")]
        [SerializeField] private float attackCooldown = 4f / 1.8f;
        [Tooltip("Brief windup before the shot actually leaves - keeps the Attack animation's " +
                 "own windup readable without a separate dodge-window telegraph.")]
        [SerializeField] private float chargeStartDelay = 0.3f;
        [SerializeField] private float laserDamage = 15f;

        [Header("Shuriken Projectile")]
        [SerializeField] private GameObject shurikenVisualPrefab;
        [SerializeField] private float shurikenVisualScale = 0.4f;
        // Same Lana Studio Range_attack authored-forward-is-+Y fix as the other projectile
        // callers (BossAstronautAI/BossMechAI/PlayerCombat) - see their comments.
        [SerializeField] private Quaternion shurikenRotationOffset = Quaternion.Euler(0f, 90f, 0f);
        [SerializeField] private GameObject shurikenImpactEffectPrefab;
        [SerializeField] private float shurikenImpactEffectScale = 1f;
        [SerializeField] private float shurikenSpeed = 14f;
        [SerializeField] private float shurikenLifetime = 4f;
        [SerializeField] private float shurikenHitRadius = 0.2f;

        private static readonly int AttackParam = Animator.StringToHash("Attack");
        private static readonly int SpeedParam = Animator.StringToHash("Speed");

        private Vector3 _wanderOffset;
        private float _nextWanderTime;
        private float _lastAttackTime = -999f;
        private bool _isAttacking;
        private float _bobSeed;

        protected override void Awake()
        {
            base.Awake();
            _bobSeed = Random.value * 100f;

            if (firePoint == null) firePoint = transform;
        }

        private void Update()
        {
            if (isDead || isFrozen) return;

            if (_isAttacking)
            {
                animator.SetFloat(SpeedParam, 0f, 0.1f, Time.deltaTime);
                return;
            }

            FacePlayer();
            float speed = UpdateMovement();
            animator.SetFloat(SpeedParam, Mathf.Clamp01(speed), 0.1f, Time.deltaTime);

            if (Time.time - _lastAttackTime >= attackCooldown && DistanceToPlayer() <= attackRange)
            {
                StartCoroutine(AttackRoutine());
            }
        }

        // See EnemyBase.SetFrozen - the AttackRoutine coroutine gets killed mid-flight by
        // StopAllCoroutines() just before this runs, so _isAttacking needs resetting here or
        // Update() would stay stuck on the early-return branch forever.
        protected override void OnFrozen()
        {
            _isAttacking = false;
        }

        private float UpdateMovement()
        {
            if (Time.time >= _nextWanderTime)
            {
                _nextWanderTime = Time.time + wanderInterval;
                Vector2 randomCircle = Random.insideUnitCircle;
                _wanderOffset = new Vector3(randomCircle.x, 0f, randomCircle.y) * wanderRadius;
            }

            Vector3 flatToPlayer = player.position - transform.position;
            flatToPlayer.y = 0f;
            float distance = flatToPlayer.magnitude;

            Vector3 desiredDirection;
            float speed;
            if (distance > maxDistanceFromPlayer)
            {
                desiredDirection = flatToPlayer.normalized;
                speed = approachSpeed;
            }
            else
            {
                Vector3 blended = flatToPlayer.normalized * 0.3f + _wanderOffset.normalized * 0.7f;
                desiredDirection = blended.sqrMagnitude > 0.0001f ? blended.normalized : Vector3.zero;
                speed = wanderSpeed;
            }

            Vector3 nextPosition = transform.position + desiredDirection * speed * SpeedMultiplier * Time.deltaTime;
            nextPosition.y = hoverHeight + Mathf.Sin((Time.time + _bobSeed) * bobSpeed) * bobAmplitude;
            transform.position = nextPosition;

            return speed / approachSpeed;
        }

        private IEnumerator AttackRoutine()
        {
            _isAttacking = true;
            animator.SetTrigger(AttackParam);

            yield return new WaitForSeconds(chargeStartDelay);

            if (isDead || player == null)
            {
                _isAttacking = false;
                yield break;
            }

            Vector3 origin = firePoint.position;
            Vector3 direction = (player.position - origin).normalized;

            int playerLayer = LayerMask.NameToLayer("Player");
            int mask = playerLayer >= 0 ? 1 << playerLayer : ~0;

            var visuals = new BossProjectileVisuals
            {
                ImportedVisualPrefab = shurikenVisualPrefab,
                ImportedVisualScale = shurikenVisualScale,
                ExtraRotationOffset = shurikenRotationOffset,
                ImpactEffectPrefab = shurikenImpactEffectPrefab,
                ImpactEffectScale = shurikenImpactEffectScale,
            };

            BossProjectile.Create(origin, direction, player, shurikenSpeed, laserDamage,
                false, shurikenLifetime, mask, Color.white, shurikenHitRadius, ProjectileVisualStyle.Bolt,
                visuals: visuals);

            _isAttacking = false;
            _lastAttackTime = Time.time;
        }
    }
}

using System.Collections;
using UnityEngine;

namespace Enemies
{
    /// Ranged flyer: wanders randomly while drifting toward the player, capped at
    /// maxDistanceFromPlayer. Headbutt attack charges a tracking telegraph line (updated to
    /// the player's current position every frame), then freezes it for a dodge window before
    /// firing an instant hitscan laser along the frozen line - only the frozen line matters for
    /// the hit check, so moving out of it during the pause avoids the shot entirely.
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
        [SerializeField] private LineRenderer telegraphLine;
        [SerializeField] private float attackRange = 16f;
        [SerializeField] private float attackCooldown = 4f;
        [SerializeField] private float chargeStartDelay = 0.5f;
        [SerializeField] private float trackDuration = 0.7f;
        [SerializeField] private float pauseDuration = 0.4f;
        [SerializeField] private float beamDuration = 0.15f;
        [SerializeField] private float laserDamage = 15f;
        [SerializeField] private float laserRange = 60f;
        [SerializeField] private Color telegraphColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        [SerializeField] private Color beamColor = new Color(1f, 0.15f, 0.1f, 1f);

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

            if (telegraphLine == null)
            {
                var lineGo = new GameObject("Telegraph");
                lineGo.transform.SetParent(transform, false);
                telegraphLine = lineGo.AddComponent<LineRenderer>();
            }

            telegraphLine.positionCount = 2;
            telegraphLine.material = new Material(Shader.Find("Sprites/Default"));
            telegraphLine.textureMode = LineTextureMode.Stretch;
            telegraphLine.useWorldSpace = true;
            telegraphLine.enabled = false;
        }

        private void Update()
        {
            if (isDead) return;

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

            Vector3 nextPosition = transform.position + desiredDirection * speed * Time.deltaTime;
            nextPosition.y = hoverHeight + Mathf.Sin((Time.time + _bobSeed) * bobSpeed) * bobAmplitude;
            transform.position = nextPosition;

            return speed / approachSpeed;
        }

        private IEnumerator AttackRoutine()
        {
            _isAttacking = true;
            animator.SetTrigger(AttackParam);

            yield return new WaitForSeconds(chargeStartDelay);

            telegraphLine.enabled = true;
            telegraphLine.startColor = telegraphColor;
            telegraphLine.endColor = telegraphColor;
            telegraphLine.startWidth = 0.04f;
            telegraphLine.endWidth = 0.04f;

            Vector3 frozenDirection = firePoint.forward;
            float trackEnd = Time.time + trackDuration;
            while (Time.time < trackEnd)
            {
                if (player != null)
                {
                    frozenDirection = (player.position - firePoint.position).normalized;
                }
                telegraphLine.SetPosition(0, firePoint.position);
                telegraphLine.SetPosition(1, firePoint.position + frozenDirection * laserRange);
                yield return null;
            }

            // Freeze: the line stops tracking here, giving the player a fixed path to dodge.
            Vector3 frozenOrigin = firePoint.position;
            telegraphLine.SetPosition(0, frozenOrigin);
            telegraphLine.SetPosition(1, frozenOrigin + frozenDirection * laserRange);

            yield return new WaitForSeconds(pauseDuration);

            telegraphLine.startColor = beamColor;
            telegraphLine.endColor = beamColor;
            telegraphLine.startWidth = 0.12f;
            telegraphLine.endWidth = 0.12f;

            int playerLayer = LayerMask.NameToLayer("Player");
            int mask = playerLayer >= 0 ? 1 << playerLayer : ~0;
            if (Physics.Raycast(frozenOrigin, frozenDirection, out RaycastHit hitInfo, laserRange, mask,
                    QueryTriggerInteraction.Ignore) && playerHealth != null)
            {
                playerHealth.ApplyDamage(laserDamage, hitInfo.point, gameObject);
            }

            yield return new WaitForSeconds(beamDuration);
            telegraphLine.enabled = false;

            _isAttacking = false;
            _lastAttackTime = Time.time;
        }
    }
}

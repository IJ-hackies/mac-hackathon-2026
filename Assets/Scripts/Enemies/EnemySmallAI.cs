using System.Collections;
using UnityEngine;

namespace Enemies
{
    /// Melee flyer: always closes distance to the player, stops once in range, and punches on
    /// a cooldown. Damage only lands if the player is still in range at the swing's midpoint.
    public class EnemySmallAI : EnemyBase
    {
        [Header("Movement")]
        [SerializeField] private float hoverHeight = 1.4f;
        [SerializeField] private float bobAmplitude = 0.15f;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float approachSpeed = 3.2f;

        [Header("Attack")]
        [SerializeField] private float attackRange = 1.6f;
        [SerializeField] private float attackCooldown = 1.4f;
        [SerializeField] private float hitDelay = 0.35f;
        [SerializeField] private float recoveryAfterHit = 0.3f;
        [SerializeField] private float punchDamage = 8f;

        private static readonly int AttackParam = Animator.StringToHash("Attack");
        private static readonly int SpeedParam = Animator.StringToHash("Speed");

        private float _lastAttackTime = -999f;
        private bool _isAttacking;
        private float _bobSeed;

        protected override void Awake()
        {
            base.Awake();
            _bobSeed = Random.value * 100f;
        }

        private void Update()
        {
            if (isDead || isFrozen) return;

            FacePlayer();

            float distance = DistanceToPlayer();

            if (_isAttacking)
            {
                animator.SetFloat(SpeedParam, 0f, 0.1f, Time.deltaTime);
                return;
            }

            if (distance > attackRange)
            {
                MoveTowardPlayer();
                animator.SetFloat(SpeedParam, 1f, 0.1f, Time.deltaTime);
            }
            else
            {
                animator.SetFloat(SpeedParam, 0f, 0.1f, Time.deltaTime);
                if (Time.time - _lastAttackTime >= attackCooldown)
                {
                    StartCoroutine(AttackRoutine());
                }
            }
        }

        // See EnemyBase.SetFrozen's comment on why this reset is needed.
        protected override void OnFrozen()
        {
            _isAttacking = false;
        }

        private void MoveTowardPlayer()
        {
            Vector3 flat = player.position - transform.position;
            flat.y = 0f;
            Vector3 direction = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.zero;

            Vector3 nextPosition = transform.position + direction * approachSpeed * SpeedMultiplier * Time.deltaTime;
            nextPosition.y = hoverHeight + Mathf.Sin((Time.time + _bobSeed) * bobSpeed) * bobAmplitude;
            transform.position = nextPosition;
        }

        private IEnumerator AttackRoutine()
        {
            _isAttacking = true;
            animator.SetTrigger(AttackParam);

            yield return new WaitForSeconds(hitDelay);

            if (DistanceToPlayer() <= attackRange && playerHealth != null)
            {
                playerHealth.ApplyDamage(punchDamage, player.position, gameObject);
                SpawnMeleeHitVfx(player.position + Vector3.up);
                // Reuses the same melee-impact cue the player's own melee used to play (now
                // removed from the player's swing per request - it fires here instead, on the
                // enemy's hit actually connecting).
                Audio.AudioManager.Instance.PlaySfx(Audio.SfxId.PlayerMelee, player.position);
            }

            yield return new WaitForSeconds(recoveryAfterHit);

            _isAttacking = false;
            _lastAttackTime = Time.time;
        }
    }
}

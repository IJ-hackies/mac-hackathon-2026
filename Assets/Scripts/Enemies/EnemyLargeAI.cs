using System.Collections;
using UnityEngine;

namespace Enemies
{
    /// Grounded melee swarmer: always closes on the player (vampire-survivors style), walking
    /// at range and running once close, then alternates Punch, Punch, Weapon (Weapon deals
    /// more damage). Damage only lands if the player is still in range at the swing's midpoint.
    [RequireComponent(typeof(CharacterController))]
    public class EnemyLargeAI : EnemyBase
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 1.6f;
        [SerializeField] private float runSpeed = 3.6f;
        [SerializeField] private float runDistanceThreshold = 6f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Attack")]
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackCooldown = 1.6f;
        [SerializeField] private float punchHitDelay = 0.4f;
        [SerializeField] private float punchRecovery = 0.3f;
        [SerializeField] private float punchDamage = 10f;
        [SerializeField] private float weaponHitDelay = 0.55f;
        [SerializeField] private float weaponRecovery = 0.45f;
        [SerializeField] private float weaponDamage = 22f;

        private static readonly int PunchParam = Animator.StringToHash("Punch");
        private static readonly int WeaponParam = Animator.StringToHash("Weapon");
        private static readonly int SpeedParam = Animator.StringToHash("Speed");

        private CharacterController _controller;
        private Vector3 _verticalVelocity;
        private float _lastAttackTime = -999f;
        private bool _isAttacking;
        private int _attackStep;

        protected override void Awake()
        {
            base.Awake();
            _controller = GetComponent<CharacterController>();
        }

        // Defensive: if the killing blow lands while Punch/Weapon is still mid-swing, that
        // trigger may still be armed (not yet consumed by its own transition) the same frame
        // Death fires. Clearing it here, plus freezing Speed to 0, guarantees Death can't lose
        // priority and the model can't show a moving pose once dead (Update stops calling
        // SetFloat once isDead is set, so Speed would otherwise stay frozen at its last value).
        protected override void HandleDeath()
        {
            animator.ResetTrigger(PunchParam);
            animator.ResetTrigger(WeaponParam);
            animator.SetFloat(SpeedParam, 0f);
            base.HandleDeath();
        }

        private void Update()
        {
            if (isDead || isFrozen) return;

            FacePlayer();
            ApplyGravity();

            float distance = DistanceToPlayer();
            float normalizedSpeed = 0f;

            if (!_isAttacking)
            {
                if (distance > attackRange)
                {
                    float speed = distance <= runDistanceThreshold ? runSpeed : walkSpeed;
                    normalizedSpeed = speed / runSpeed;

                    Vector3 flat = player.position - transform.position;
                    flat.y = 0f;
                    Vector3 direction = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.zero;
                    Vector3 horizontal = direction * speed * SpeedMultiplier;

                    _controller.Move((horizontal + _verticalVelocity) * Time.deltaTime);
                }
                else
                {
                    _controller.Move(_verticalVelocity * Time.deltaTime);
                    if (Time.time - _lastAttackTime >= attackCooldown)
                    {
                        StartCoroutine(AttackRoutine());
                    }
                }
            }
            else
            {
                _controller.Move(_verticalVelocity * Time.deltaTime);
            }

            animator.SetFloat(SpeedParam, Mathf.Clamp01(normalizedSpeed), 0.1f, Time.deltaTime);
        }

        // See EnemyBase.SetFrozen's comment on why this reset is needed.
        protected override void OnFrozen()
        {
            _isAttacking = false;
        }

        private void ApplyGravity()
        {
            if (_controller.isGrounded && _verticalVelocity.y < 0f)
            {
                _verticalVelocity.y = groundedStickForce;
            }
            else
            {
                _verticalVelocity.y += gravity * Time.deltaTime;
            }
        }

        private IEnumerator AttackRoutine()
        {
            _isAttacking = true;
            bool useWeapon = _attackStep >= 2;
            animator.SetTrigger(useWeapon ? WeaponParam : PunchParam);

            yield return new WaitForSeconds(useWeapon ? weaponHitDelay : punchHitDelay);

            if (DistanceToPlayer() <= attackRange && playerHealth != null)
            {
                playerHealth.ApplyDamage(useWeapon ? weaponDamage : punchDamage, player.position, gameObject);
                SpawnMeleeHitVfx(player.position + Vector3.up);
            }

            yield return new WaitForSeconds(useWeapon ? weaponRecovery : punchRecovery);

            _attackStep = useWeapon ? 0 : _attackStep + 1;
            _isAttacking = false;
            _lastAttackTime = Time.time;
        }
    }
}

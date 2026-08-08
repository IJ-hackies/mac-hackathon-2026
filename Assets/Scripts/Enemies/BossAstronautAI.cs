using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Enemies
{
    /// Boss Stage 1: a player-like fighter built on the same rig/clip set as the player
    /// (Idle_Gun/Walk_Gun/Run_Gun locomotion, Run_Gun_Shoot ranged burst on an Arms override
    /// layer, Punch/Weapon melee, Wave/No one-shots). Circle-strafes the player at
    /// preferredRange and picks a weighted-random attack (deliberately unpredictable - the
    /// user's spec explicitly wants this stage "random like a boss fight", in contrast to
    /// BossMechAI's fixed round-robin).
    [RequireComponent(typeof(CharacterController))]
    public class BossAstronautAI : EnemyBase
    {
        private enum AttackKind { Ranged, Punch, Weapon }

        [Header("Movement")]
        [SerializeField] private float preferredRange = 6f;
        [SerializeField] private float orbitDegreesPerSecond = 20f;
        [SerializeField] private float walkSpeed = 2f;
        [SerializeField] private float runSpeed = 4.5f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Ranged Attack")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private float rangedMaxRange = 14f;
        [SerializeField] private float rangedBurstDuration = 2f;
        [SerializeField] private float rangedTickInterval = 0.4f;
        [SerializeField] private float rangedDamagePerTick = 6f;
        [Tooltip("Travel speed of the visible bolt spawned each tick - a slow enough, visible " +
                 "projectile (not an instant hitscan raycast) so the player actually has time to " +
                 "see and dodge it.")]
        [SerializeField] private float rangedProjectileSpeed = 16f;
        [SerializeField] private float rangedProjectileLifetime = 3f;
        [SerializeField] private Color rangedProjectileColor = new Color(1f, 0.25f, 0.2f);
        [Tooltip("Imported bolt visual (Lana Studio's Projectiles_light) - replaces the " +
                 "procedural streak look. Null falls back to the procedural bolt.")]
        [SerializeField] private GameObject rangedProjectileVisualPrefab;
        [SerializeField] private float rangedProjectileVisualScale = 0.4f;
        // Lana Studio's Range_attack prefabs (Projectiles_light here) are authored travelling
        // along local +Y, not +Z - BossProjectile's LookRotation only aligns +Z to the travel
        // direction, so without this the beam rendered as a near-vertical column regardless of
        // aim. (90,0,0) rotates authored +Y onto +Z.
        [SerializeField] private Quaternion rangedProjectileRotationOffset = Quaternion.Euler(0f, 90f, 0f);
        [Tooltip("Imported impact burst (Lana Studio's Hit_light) played where the bolt connects.")]
        [SerializeField] private GameObject rangedImpactEffectPrefab;
        [SerializeField] private float rangedImpactEffectScale = 1f;

        [Header("Melee Attacks")]
        [SerializeField] private float meleeRange = 2.2f;
        [SerializeField] private float punchHitDelay = 0.35f;
        [SerializeField] private float punchRecovery = 0.35f;
        [SerializeField] private float punchDamage = 10f;
        [SerializeField] private float weaponHitDelay = 0.6f;
        [SerializeField] private float weaponRecovery = 0.5f;
        [SerializeField] private float weaponDamage = 26f;

        [Header("Death")]
        [Tooltip("How long the Death animation gets to play before the model is removed. Unlike " +
                 "the three basic enemies, this boss does NOT dissolve on death - it just plays " +
                 "Death and disappears, since BossFightController's cutscene takes over the " +
                 "visual spectacle immediately after (\"die without dissolving and then become " +
                 "the mech\").")]
        [SerializeField] private float deathHoldBeforeDestroy = 1.2f;

        [Header("Boss Pacing")]
        [SerializeField] private float attackCooldown = 1.4f;
        [Tooltip("How long a health-threshold Stagger (\"recollecting itself\") suppresses " +
                 "starting a *new* attack. Movement/facing keep running underneath it, matching " +
                 "the project-wide \"hit reactions never block gameplay\" invariant - only the " +
                 "next attack decision waits.")]
        [SerializeField] private float staggerAttackSuppression = 1.1f;

        private static readonly float[] StaggerThresholds = { 0.75f, 0.5f, 0.25f };
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int FiringParam = Animator.StringToHash("Firing");
        private static readonly int PunchParam = Animator.StringToHash("Punch");
        private static readonly int WeaponParam = Animator.StringToHash("Weapon");
        private static readonly int TauntParam = Animator.StringToHash("Taunt");
        private static readonly int StaggerParam = Animator.StringToHash("Stagger");

        private CharacterController _controller;
        private Vector3 _verticalVelocity;
        private float _orbitAngle;
        private float _lastAttackTime = -999f;
        private float _attackFreeUntil;
        private bool _isAttacking;
        private AttackKind? _lastAttack;
        private int _armsLayerIndex = -1;
        private readonly HashSet<int> _crossedThresholds = new();

        protected override void Awake()
        {
            base.Awake();
            _controller = GetComponent<CharacterController>();
            _orbitAngle = Random.Range(0f, 360f);
            if (animator != null) _armsLayerIndex = animator.GetLayerIndex("Arms");
            if (muzzle == null) muzzle = transform;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            health.HealthChanged += OnHealthChanged;
            // Hit-react is suppressed entirely (not just rate-limited/during attacks) - even a
            // throttled flinch kept visibly cutting into Punch/Weapon/the ranged burst often
            // enough that the boss read as "stuck, can't attack." The health-threshold Stagger
            // (No animation) already covers "the boss visibly reacting to damage" per spec.
            health.SuppressHitReact = true;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            health.HealthChanged -= OnHealthChanged;
        }

        // Deliberately does NOT call base.HandleDeath() - EnemyBase's dissolve-and-fall effect is
        // skipped here per spec ("die without dissolving and then become the mech"). Death still
        // plays, but the model is simply removed afterward instead of crumbling away; the
        // transformation cutscene (BossFightController, listening to the same Health.Died) takes
        // over the visual spectacle from here.
        protected override void HandleDeath()
        {
            animator.ResetTrigger(PunchParam);
            animator.ResetTrigger(WeaponParam);
            animator.SetBool(FiringParam, false);
            if (_armsLayerIndex >= 0) animator.SetLayerWeight(_armsLayerIndex, 0f);
            animator.SetFloat(SpeedParam, 0f);
            health.SuppressHitReact = false;

            isDead = true;
            StopAllCoroutines();
            StartCoroutine(DestroyAfterDeathAnimation());
        }

        private IEnumerator DestroyAfterDeathAnimation()
        {
            yield return new WaitForSeconds(deathHoldBeforeDestroy);
            Destroy(gameObject);
        }

        // "Every time we deal twenty five percent of its damage... the No animation will play as
        // if it's shaking its head and recollecting itself." Thresholds only ever fire once each
        // since health is monotonically decreasing.
        private void OnHealthChanged(float current, float max)
        {
            if (max <= 0f) return;
            float fraction = current / max;

            for (int i = 0; i < StaggerThresholds.Length; i++)
            {
                if (_crossedThresholds.Contains(i)) continue;
                if (fraction <= StaggerThresholds[i])
                {
                    _crossedThresholds.Add(i);
                    animator.SetTrigger(StaggerParam);
                    _attackFreeUntil = Time.time + staggerAttackSuppression;
                }
            }
        }

        private void Update()
        {
            if (isDead) return;

            FacePlayer();
            ApplyGravity();

            if (_isAttacking)
            {
                _controller.Move(_verticalVelocity * Time.deltaTime);
                return;
            }

            float distance = DistanceToPlayer();
            Orbit(distance);

            if (Time.time >= _lastAttackTime + attackCooldown && Time.time >= _attackFreeUntil)
            {
                AttackKind? chosen = ChooseAttack(distance);
                if (chosen.HasValue)
                {
                    StartCoroutine(AttackRoutine(chosen.Value));
                }
            }
        }

        private void Orbit(float distance)
        {
            _orbitAngle += orbitDegreesPerSecond * Time.deltaTime;
            Vector3 offset = Quaternion.Euler(0f, _orbitAngle, 0f) * Vector3.forward * preferredRange;
            Vector3 targetPoint = player.position + offset;

            Vector3 toTarget = targetPoint - transform.position;
            toTarget.y = 0f;

            float distanceToTarget = toTarget.magnitude;
            float normalizedSpeed = 0f;

            if (distanceToTarget > 0.5f)
            {
                bool sprint = distanceToTarget > preferredRange * 0.75f || distance > preferredRange * 1.5f;
                float speed = sprint ? runSpeed : walkSpeed;
                normalizedSpeed = speed / runSpeed;

                Vector3 direction = toTarget.normalized;
                _controller.Move((direction * speed + _verticalVelocity) * Time.deltaTime);
            }
            else
            {
                _controller.Move(_verticalVelocity * Time.deltaTime);
            }

            animator.SetFloat(SpeedParam, Mathf.Clamp01(normalizedSpeed), 0.1f, Time.deltaTime);
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

        // Weighted random with no-immediate-repeat: melee only enters the pool once in range, so
        // "come up in melee once in a while" naturally falls out of proximity plus the weighting
        // below rather than a separate timer. Explicitly random (not a fixed rotation) per spec,
        // unlike BossMechAI's predictable round-robin.
        private AttackKind? ChooseAttack(float distance)
        {
            var candidates = new List<(AttackKind kind, float weight)>();

            if (distance <= rangedMaxRange)
            {
                candidates.Add((AttackKind.Ranged, 0.5f));
            }

            if (distance <= meleeRange)
            {
                candidates.Add((AttackKind.Punch, 0.3f));
                candidates.Add((AttackKind.Weapon, 0.2f));
            }

            if (candidates.Count == 0) return null;
            if (candidates.Count > 1 && _lastAttack.HasValue)
            {
                candidates.RemoveAll(c => c.kind == _lastAttack.Value && candidates.Count > 1);
            }

            float totalWeight = 0f;
            foreach (var candidate in candidates) totalWeight += candidate.weight;

            float roll = Random.Range(0f, totalWeight);
            foreach (var candidate in candidates)
            {
                if (roll <= candidate.weight) return candidate.kind;
                roll -= candidate.weight;
            }

            return candidates[candidates.Count - 1].kind;
        }

        private IEnumerator AttackRoutine(AttackKind kind)
        {
            _isAttacking = true;
            _lastAttack = kind;

            switch (kind)
            {
                case AttackKind.Ranged:
                    yield return RangedBurst();
                    break;
                case AttackKind.Punch:
                    yield return MeleeSwing(PunchParam, punchHitDelay, punchRecovery, punchDamage, false);
                    break;
                case AttackKind.Weapon:
                    yield return MeleeSwing(WeaponParam, weaponHitDelay, weaponRecovery, weaponDamage, true);
                    break;
            }

            _isAttacking = false;
            _lastAttackTime = Time.time;
        }

        // Arms layer is kept at weight 0 the rest of the time (see AC_BossAstronaut.controller)
        // so Punch/Weapon/Taunt/Stagger's full-body base-layer poses aren't fought by the
        // upper-body-masked Shoot pose - only switched on for the burst, same reasoning as
        // PlayerCombat's Arms layer.
        //
        // Fires visible, travel-time BossProjectile bolts (like BossMechAI's bullets) rather than
        // an instant hitscan raycast - a hitscan tick is invisible and unavoidable by definition,
        // which is exactly what read as "can't see them / can't avoid them."
        private IEnumerator RangedBurst()
        {
            if (_armsLayerIndex >= 0) animator.SetLayerWeight(_armsLayerIndex, 1f);
            animator.SetBool(FiringParam, true);

            float elapsed = 0f;
            while (elapsed < rangedBurstDuration)
            {
                if (playerHealth != null && !playerHealth.IsDead)
                {
                    SpawnBolt();
                }

                yield return new WaitForSeconds(rangedTickInterval);
                elapsed += rangedTickInterval;
            }

            animator.SetBool(FiringParam, false);
            if (_armsLayerIndex >= 0) animator.SetLayerWeight(_armsLayerIndex, 0f);
        }

        private void SpawnBolt()
        {
            Vector3 origin = muzzle.position;
            Vector3 direction = (player.position + Vector3.up - origin).normalized;

            int playerLayer = LayerMask.NameToLayer("Player");
            int mask = playerLayer >= 0 ? 1 << playerLayer : ~0;

            var visuals = new BossProjectileVisuals
            {
                ImportedVisualPrefab = rangedProjectileVisualPrefab,
                ImportedVisualScale = rangedProjectileVisualScale,
                ExtraRotationOffset = rangedProjectileRotationOffset,
                ImpactEffectPrefab = rangedImpactEffectPrefab,
                ImpactEffectScale = rangedImpactEffectScale,
            };

            BossProjectile.Create(origin, direction, player, rangedProjectileSpeed, rangedDamagePerTick,
                false, rangedProjectileLifetime, mask, rangedProjectileColor, 0.15f, ProjectileVisualStyle.Bolt,
                visuals: visuals);
        }

        // Weapon is this boss's highest-damage move - a confirmed hit fires Taunt (Wave)
        // immediately after, matching "whenever it deals significant damage to us by its highest
        // damage move (weapon), it should taunt us."
        private IEnumerator MeleeSwing(int triggerParam, float hitDelay, float recovery, float damage, bool tauntOnHit)
        {
            animator.SetTrigger(triggerParam);
            yield return new WaitForSeconds(hitDelay);

            bool connected = false;
            if (DistanceToPlayer() <= meleeRange + 0.5f && playerHealth != null && !playerHealth.IsDead)
            {
                playerHealth.ApplyDamage(damage, player.position, gameObject, Combat.DamageType.Melee);
                connected = true;
            }

            if (connected && tauntOnHit)
            {
                animator.SetTrigger(TauntParam);
            }

            yield return new WaitForSeconds(recovery);
        }
    }
}

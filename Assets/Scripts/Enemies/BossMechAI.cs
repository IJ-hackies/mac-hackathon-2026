using System.Collections;
using Player;
using UnityEngine;

namespace Enemies
{
    /// Boss Stage 2: the mech form. Wanders within [minRange, maxRange] of the player (movement
    /// direction is independent of the player's position - only the distance band matters, per
    /// spec) while facing/aiming at the player, and works through a *fixed* round-robin attack
    /// rotation (Dance -> Shoot_Small -> Jump -> Shoot_Big -> repeat) - deliberately predictable,
    /// the mirror image of BossAstronautAI's weighted-random pool, so "the player can understand
    /// and counter" per spec.
    [RequireComponent(typeof(CharacterController))]
    public class BossMechAI : EnemyBase
    {
        private enum AttackKind { Dance, ShootSmall, Jump, ShootBig }

        [Header("Movement")]
        // Tightened from 5/12 - "move less by being in range with the player more": a narrower
        // band keeps it from wandering off into mid-range no-man's-land between attacks.
        [SerializeField] private float minRange = 3f;
        [SerializeField] private float maxRange = 7f;
        [SerializeField] private float wanderSpeed = 2.5f;
        [Tooltip("Used specifically when the player has drifted beyond maxRange, instead of " +
                 "wanderSpeed - faster than the player's own sprint speed (6.5) so a player who " +
                 "just runs away and never attacks can't keep the mech at arm's length forever.")]
        [SerializeField] private float chaseSpeed = 7.5f;
        [SerializeField] private float wanderInterval = 4f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Attack Pacing")]
        [SerializeField] private float attackEngageRange = 16f;
        // Shortened from 1.2 - "attacking in general needs to be more frequent."
        [SerializeField] private float postAttackGap = 0.35f;

        [Header("Dance - Homing Fireballs")]
        [Tooltip("Fireballs spawn at a random point on a ring around the mech at this radius " +
                 "(scaled by the mech's own lossyScale), rather than always from one fixed muzzle " +
                 "- reads as the mech surrounding itself with fire while dancing.")]
        [SerializeField] private float fireballSpawnRadius = 2.5f;
        [SerializeField] private float fireballSpawnHeight = 1.5f;
        [SerializeField] private float danceDuration = 3.2f;
        [SerializeField] private float fireballInterval = 0.5f;
        [SerializeField] private float fireballSpeed = 7f;
        [SerializeField] private float fireballDamage = 8f;
        [SerializeField] private float fireballLifetime = 2.5f;
        [Tooltip("Beyond this distance from the player, a fireball flies straight instead of " +
                 "steering - moving far enough away is a valid way to dodge it.")]
        [SerializeField] private float fireballHomingRange = 9f;
        [SerializeField] private float fireballTurnDegreesPerSecond = 70f;
        [SerializeField] private Color fireballColor = new Color(1f, 0.45f, 0.1f);
        [Tooltip("How many fireballs launch from each portal, one shortly after another.")]
        [SerializeField] private int fireballsPerPortal = 2;
        [SerializeField] private float fireballLaunchStagger = 0.1f;

        [Header("Shoot Small - Fast Bullet")]
        [Tooltip("Gun muzzle points, one per arm cannon - bullets alternate left/right per shot " +
                 "in the burst, like an actual machine gun, instead of all firing from one point. " +
                 "Both default to the mech's own root transform if left unassigned.")]
        [SerializeField] private Transform firePointLeft;
        [SerializeField] private Transform firePointRight;
        [SerializeField] private float shootSmallDelay = 0.4f;
        [Tooltip("Bullets fire as a burst (this many, one bulletBurstInterval apart) each time " +
                 "the round-robin lands on Shoot_Small - firerate (burstInterval) stays the same, " +
                 "this just controls how long the mech keeps firing each time it's its turn.")]
        [SerializeField] private int bulletBurstCount = 55;
        [SerializeField] private float bulletBurstInterval = 0.08f;
        [SerializeField] private float bulletSpeed = 22f;
        [SerializeField] private float bulletDamage = 9f;
        [SerializeField] private float bulletLifetime = 4f;
        [SerializeField] private Color bulletColor = new Color(0.3f, 0.9f, 1f);

        [Header("Shoot Big - Slow Missile")]
        [SerializeField] private float shootBigDelay = 0.6f;
        [Tooltip("Missiles fire as a burst too, just fewer/slower-spaced than bullets given their " +
                 "higher damage.")]
        [SerializeField] private int missileBurstCount = 3;
        [SerializeField] private float missileBurstInterval = 0.35f;
        [SerializeField] private float missileSpeed = 9f;
        [SerializeField] private float missileDamage = 24f;
        [SerializeField] private float missileLifetime = 6f;
        [SerializeField] private Color missileColor = new Color(1f, 0.15f, 0.1f);

        [Header("Jump - Ground Slam")]
        [SerializeField] private float jumpAscendDuration = 0.6f;
        [SerializeField] private float jumpApexHeight = 3f;
        [SerializeField] private float jumpLandingHold = 0.5f;
        [SerializeField] private float playerStaggerDuration = 1.4f;
        [SerializeField] private float shakeDuration = 0.5f;
        [SerializeField] private float shakeMagnitude = 0.35f;

        [Header("Visual Assets (Gabriel Aguiar's Free Quick Effects Vol.1 - optional)")]
        [Tooltip("vfx_Projectile_01 - the fast bullet's own visual. Null falls back to the " +
                 "procedural streak.")]
        [SerializeField] private GameObject bulletVisualPrefab;
        [SerializeField] private float bulletVisualScale = 0.7f;
        [Tooltip("Same family, scaled up - \"a slightly bigger projectile compared to the " +
                 "bullet\" rather than a dedicated missile model/mesh.")]
        [SerializeField] private GameObject bigProjectileVisualPrefab;
        [SerializeField] private float bigProjectileVisualScale = 1f;
        [Tooltip("Explosion/impact burst played at the hit point when a bullet/big projectile/ " +
                 "fireball connects. Null = no impact effect.")]
        [SerializeField] private GameObject impactEffectPrefab;
        [SerializeField] private float impactEffectScale = 1f;
        [Tooltip("A short burst (e.g. vfx_Flames_01) played at the spawn ring point before each " +
                 "fireball actually launches - the \"conjuring\" windup - then destroyed early/" +
                 "cut short rather than played to completion.")]
        [SerializeField] private GameObject fireballConjureEffectPrefab;
        [SerializeField] private float fireballConjureDuration = 0.4f;
        [SerializeField] private float fireballConjureEffectScale = 1.3f;
        [Tooltip("The fireball's own in-flight visual (e.g. vfx_Flamethrower_01/vfx_Flames_01) " +
                 "instead of the procedural glow+embers. Null falls back to procedural.")]
        [SerializeField] private GameObject fireballLoopEffectPrefab;
        [SerializeField] private float fireballLoopEffectScale = 1.1f;

        private static readonly AttackKind[] AttackOrder =
        {
            AttackKind.Dance, AttackKind.ShootSmall, AttackKind.Jump, AttackKind.ShootBig,
        };

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int DanceParam = Animator.StringToHash("Dance");
        private static readonly int ShootSmallParam = Animator.StringToHash("ShootSmall");
        private static readonly int ShootBigParam = Animator.StringToHash("ShootBig");
        private static readonly int JumpParam = Animator.StringToHash("Jump");

        private CharacterController _controller;
        private Vector3 _verticalVelocity;
        private PlayerController _playerController;
        private ThirdPersonCameraController _cameraController;

        private Vector3 _wanderDirection = Vector3.forward;
        private float _nextWanderTime;
        private int _attackIndex;
        private float _attackReadyTime;
        private bool _isAttacking;
        // JumpSlam hand-animates height directly via CharacterController.Move - the normal
        // gravity/verticalVelocity handling below would otherwise fight that arc (Update() still
        // runs every frame regardless of the coroutine, so without this it would apply falling
        // velocity on top of the coroutine's own ascend/descend motion).
        private bool _manualHeightControl;

        protected override void Awake()
        {
            base.Awake();
            _controller = GetComponent<CharacterController>();
            if (firePointLeft == null) firePointLeft = transform;
            if (firePointRight == null) firePointRight = transform;

            _playerController = FindFirstObjectByType<PlayerController>();
            _cameraController = FindFirstObjectByType<ThirdPersonCameraController>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            // Hit-react (both Health's generic trigger and this boss's own HitRecieve_1/2, which
            // used to fire off Health.Hit here) is suppressed entirely - even gated to "only
            // between attacks," a hit landing right as an attack was about to start still cut it
            // off often enough that the mech read as "stuck, can't attack." Damage still applies
            // normally; there's just no reaction animation anymore.
            health.SuppressHitReact = true;
        }

        protected override void HandleDeath()
        {
            animator.SetBool(DanceParam, false);
            animator.SetFloat(SpeedParam, 0f);
            base.HandleDeath();
        }

        private void Update()
        {
            if (isDead) return;

            FacePlayer();

            if (_manualHeightControl) return;

            ApplyGravity();

            if (_isAttacking)
            {
                _controller.Move(_verticalVelocity * Time.deltaTime);
                return;
            }

            Wander();
            TryAdvanceAttack();
        }

        // Movement is independent of the player's position beyond keeping distance in
        // [minRange, maxRange] - "this can be in any direction and not necessarily towards the
        // player, but keeping within range." A fresh random direction is picked periodically;
        // outside the band it's biased back toward/away from the player instead.
        private void Wander()
        {
            float distance = DistanceToPlayer();

            if (Time.time >= _nextWanderTime)
            {
                _nextWanderTime = Time.time + wanderInterval;
                Vector2 randomCircle = Random.insideUnitCircle;
                _wanderDirection = new Vector3(randomCircle.x, 0f, randomCircle.y).normalized;
            }

            Vector3 flatToPlayer = player != null ? player.position - transform.position : Vector3.zero;
            flatToPlayer.y = 0f;

            Vector3 direction = _wanderDirection;
            float speed = wanderSpeed;
            if (distance < minRange && flatToPlayer.sqrMagnitude > 0.01f)
            {
                direction = -flatToPlayer.normalized;
            }
            else if (distance > maxRange && flatToPlayer.sqrMagnitude > 0.01f)
            {
                // Faster catch-up speed specifically here - a player who just runs away and never
                // attacks shouldn't be able to keep the mech stuck at wanderSpeed forever.
                direction = flatToPlayer.normalized;
                speed = chaseSpeed;
            }

            _controller.Move((direction * speed + _verticalVelocity) * Time.deltaTime);
            animator.SetFloat(SpeedParam, 1f, 0.1f, Time.deltaTime);
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

        // Fixed round-robin, gated on cooldown + engage range. Out-of-range simply waits without
        // advancing the sequence, so the same rotation resumes once back in range rather than
        // skipping an attack.
        private void TryAdvanceAttack()
        {
            if (Time.time < _attackReadyTime) return;
            if (DistanceToPlayer() > attackEngageRange) return;

            StartCoroutine(AttackRoutine(AttackOrder[_attackIndex]));
            _attackIndex = (_attackIndex + 1) % AttackOrder.Length;
        }

        private IEnumerator AttackRoutine(AttackKind kind)
        {
            _isAttacking = true;
            animator.SetFloat(SpeedParam, 0f);

            switch (kind)
            {
                case AttackKind.Dance:
                    yield return DanceAttack();
                    break;
                case AttackKind.ShootSmall:
                    yield return ShootAttack(ShootSmallParam, shootSmallDelay, bulletSpeed, bulletDamage,
                        bulletLifetime, bulletColor, bulletBurstCount, bulletBurstInterval, ProjectileVisualStyle.Bullet);
                    break;
                case AttackKind.ShootBig:
                    yield return ShootAttack(ShootBigParam, shootBigDelay, missileSpeed, missileDamage,
                        missileLifetime, missileColor, missileBurstCount, missileBurstInterval, ProjectileVisualStyle.BigProjectile);
                    break;
                case AttackKind.Jump:
                    yield return JumpSlam();
                    break;
            }

            yield return new WaitForSeconds(postAttackGap);
            _isAttacking = false;
            _attackReadyTime = Time.time;
        }

        private IEnumerator DanceAttack()
        {
            animator.SetBool(DanceParam, true);

            float elapsed = 0f;
            while (elapsed < danceDuration)
            {
                // Fire-and-forget per tick (not yield-blocking DanceAttack's own loop) so the
                // conjure windup on one fireball overlaps the next tick instead of stalling the
                // whole stream - "dancing while these fireballs are spawning and shooting."
                StartCoroutine(ConjureAndFireFromRing());
                yield return new WaitForSeconds(fireballInterval);
                elapsed += fireballInterval;
            }

            animator.SetBool(DanceParam, false);
        }

        // Random point on a ring around the mech, biased to the hemisphere already facing the
        // player (+-80 degrees of the player direction) rather than a full 360 degree pick - a
        // spawn point on the far side would otherwise need to curve its whole travel path around
        // (and visually through) the mech's own body before it could ever head toward the player,
        // which is what read as the fireballs "moving weirdly." Plays a brief conjure burst at the
        // ring point before actually launching the fireball, per spec.
        private IEnumerator ConjureAndFireFromRing()
        {
            if (player == null) yield break;

            Vector3 towardPlayer = player.position - transform.position;
            towardPlayer.y = 0f;
            Vector3 forward = towardPlayer.sqrMagnitude > 0.01f ? towardPlayer.normalized : transform.forward;

            float angle = Random.Range(-80f, 80f);
            Vector3 ringDirection = Quaternion.Euler(0f, angle, 0f) * forward;

            float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            Vector3 origin = transform.position
                + ringDirection * (fireballSpawnRadius * scale)
                + Vector3.up * (fireballSpawnHeight * transform.lossyScale.y);

            if (fireballConjureEffectPrefab != null)
            {
                // Oriented to face the player (its "through" axis pointed down the line the
                // fireball is about to travel) so the portal reads as a disc the fireball emerges
                // from, not a ring lying flat/edge-on to the direction of travel.
                Vector3 towardPlayerAim = (player.position + Vector3.up - origin).normalized;
                var conjure = Instantiate(fireballConjureEffectPrefab, origin,
                    Quaternion.LookRotation(towardPlayerAim, Vector3.up));
                Vfx.ImportedVfxUtility.FixUrpMaterials(conjure);
                Vfx.ImportedVfxUtility.ForceHierarchyParticleScaling(conjure);
                conjure.transform.localScale = Vector3.one * fireballConjureEffectScale;
                Destroy(conjure, fireballConjureDuration + 1f);
            }

            // Windup beat always happens (even without a conjure effect assigned) so the "conjure
            // then launch" rhythm/timing reads the same regardless of which visual is wired.
            yield return new WaitForSeconds(fireballConjureDuration);
            if (isDead) yield break;

            // Multiple fireballs out of the same portal, a beat apart rather than perfectly
            // simultaneous - reads as a rapid volley from one conjured portal instead of a single
            // shot.
            for (int i = 0; i < fireballsPerPortal; i++)
            {
                if (isDead || player == null) yield break;
                LaunchFireball(origin);
                if (i < fireballsPerPortal - 1) yield return new WaitForSeconds(fireballLaunchStagger);
            }
        }

        private void LaunchFireball(Vector3 origin)
        {
            var visuals = new BossProjectileVisuals
            {
                ImpactEffectPrefab = impactEffectPrefab,
                ImpactEffectScale = impactEffectScale,
                ImportedVisualPrefab = fireballLoopEffectPrefab,
                ImportedVisualScale = fireballLoopEffectScale,
                ForceLoopParticles = true,
            };

            BossProjectile.Create(origin, (player.position + Vector3.up - origin).normalized, player,
                fireballSpeed, fireballDamage, true, fireballLifetime,
                LayerMask.NameToLayer("Player") >= 0 ? 1 << LayerMask.NameToLayer("Player") : ~0,
                fireballColor, fireballLoopEffectPrefab != null ? fireballLoopEffectScale : 0.55f,
                ProjectileVisualStyle.Fireball, fireballTurnDegreesPerSecond, fireballHomingRange, visuals);
        }

        // Fires burstCount shots, burstInterval apart, off a single animator trigger/clip play -
        // "shooting needs to be a lot more frequent" without also re-triggering (and restarting)
        // the Shoot_Small/Shoot_Big clip once per shot. Alternates firePointLeft/firePointRight
        // per shot - "come out of the mech guns like machine guns" instead of every shot spawning
        // from one point (which, left unassigned, was the mech's own root - reading as "the
        // bottom centre").
        private IEnumerator ShootAttack(int triggerParam, float delay, float speed, float damage, float lifetime,
            Color color, int burstCount, float burstInterval, ProjectileVisualStyle style)
        {
            animator.SetTrigger(triggerParam);
            yield return new WaitForSeconds(delay);

            float visualRadius = style == ProjectileVisualStyle.BigProjectile ? 0.4f : 0.3f;
            for (int i = 0; i < burstCount; i++)
            {
                Transform origin = i % 2 == 0 ? firePointLeft : firePointRight;
                SpawnProjectile(origin.position, speed, damage, lifetime, false, color, visualRadius, style, 90f, 10f);
                if (i < burstCount - 1) yield return new WaitForSeconds(burstInterval);
            }
        }

        private void SpawnProjectile(Vector3 origin, float speed, float damage, float lifetime, bool homing,
            Color color, float visualRadius, ProjectileVisualStyle style, float homingTurnDegreesPerSecond, float homingRange)
        {
            if (player == null) return;

            Vector3 direction = (player.position + Vector3.up - origin).normalized;
            int playerLayer = LayerMask.NameToLayer("Player");
            int mask = playerLayer >= 0 ? 1 << playerLayer : ~0;

            var visuals = new BossProjectileVisuals
            {
                ImpactEffectPrefab = impactEffectPrefab,
                ImpactEffectScale = impactEffectScale,
            };
            switch (style)
            {
                case ProjectileVisualStyle.BigProjectile:
                    visuals.ImportedVisualPrefab = bigProjectileVisualPrefab;
                    visuals.ImportedVisualScale = bigProjectileVisualScale;
                    visuals.ImpactEffectScale *= 2f; // bigger projectile hits should read as a bigger blast
                    break;
                case ProjectileVisualStyle.Bullet:
                    visuals.ImportedVisualPrefab = bulletVisualPrefab;
                    visuals.ImportedVisualScale = bulletVisualScale;
                    break;
            }

            BossProjectile.Create(origin, direction, player, speed, damage, homing, lifetime, mask,
                color, visualRadius, style, homingTurnDegreesPerSecond, homingRange, visuals);
        }

        // Lets BossFightController play the exact same ground-slam (arc, camera shake, player
        // stagger) as the reveal beat of the Stage 1 -> Stage 2 cutscene, without needing this
        // component's own Update()/attack-rotation loop enabled yet. Callable while this behaviour
        // is still disabled - Awake already set up _controller/_playerController/_cameraController
        // regardless, and the coroutine only touches those, not anything gated by OnEnable/Update.
        public IEnumerator PlayIntroSlam()
        {
            yield return JumpSlam();
        }

        // Ascends over jumpAscendDuration, then drops back down - on touching ground again, fires
        // the camera shake and unconditionally staggers the player into the Duck animation with
        // movement locked ("the player will have the duck animation played...and the player
        // cannot move until the duck animation is done").
        private IEnumerator JumpSlam()
        {
            animator.SetTrigger(JumpParam);
            _manualHeightControl = true;
            _verticalVelocity = Vector3.zero;

            float heightOffset = 0f;
            float elapsed = 0f;
            while (elapsed < jumpAscendDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / jumpAscendDuration;
                float targetHeight = Mathf.Sin(t * Mathf.PI * 0.5f) * jumpApexHeight;
                _controller.Move(new Vector3(0f, targetHeight - heightOffset, 0f));
                heightOffset = targetHeight;
                yield return null;
            }

            elapsed = 0f;
            float descendDuration = jumpAscendDuration * 0.8f;
            float apex = heightOffset;
            while (elapsed < descendDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / descendDuration;
                float targetHeight = Mathf.Lerp(apex, 0f, t * t);
                _controller.Move(new Vector3(0f, targetHeight - heightOffset, 0f));
                heightOffset = targetHeight;
                yield return null;
            }

            if (heightOffset != 0f)
            {
                _controller.Move(new Vector3(0f, -heightOffset, 0f));
            }

            _manualHeightControl = false;

            if (_cameraController != null)
            {
                _cameraController.Shake(shakeDuration, shakeMagnitude);
            }

            // Unconditional now - the impactRadius distance gate was making this feel like it
            // "never" staggered (the mech's own attack-range logic already keeps it roughly within
            // wander/engage range, but a player who moved during the ~1-1.5s jump arc could easily
            // end up just outside whatever radius was picked, especially since the mech itself
            // isn't required to be near the player to begin with - see the class doc). Per spec
            // this is meant to always land as a stagger on landing, not a proximity check.
            if (_playerController != null)
            {
                _playerController.Stagger(playerStaggerDuration);
            }

            yield return new WaitForSeconds(jumpLandingHold);
        }
    }
}

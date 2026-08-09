using System.Collections;
using Audio;
using Combat;
using Player;
using UnityEngine;
using Vfx;

namespace Enemies
{
    /// Boss Stage 2: the mech form. Wanders within [minRange, maxRange] of the player (movement
    /// direction is independent of the player's position - only the distance band matters, per
    /// spec) while facing/aiming at the player, and works through a *fixed* round-robin attack
    /// rotation (TopDownBeam -> ShootSmall -> Jump -> ShootBig -> TopDownRocket -> ShootSmall ->
    /// Jump -> ShootBig -> repeat) - deliberately predictable, the mirror image of
    /// BossAstronautAI's weighted-random pool, so "the player can understand and counter" per
    /// spec. The two top-down ground-target attacks (Lana Studio's top_down_beam_line_blue /
    /// top_down_rocket_circle_red) replaced the old Dance homing-fireball attack entirely.
    [RequireComponent(typeof(CharacterController))]
    public class BossMechAI : EnemyBase
    {
        private enum AttackKind { TopDownBeam, ShootSmall, Jump, ShootBig, TopDownRocket }

        [Header("Movement")]
        // Tightened from 5/12 - "move less by being in range with the player more": a narrower
        // band keeps it from wandering off into mid-range no-man's-land between attacks.
        [SerializeField] private float minRange = 3f;
        [SerializeField] private float maxRange = 7f;
        [SerializeField] private float wanderSpeed = 2.5f;
        [Tooltip("Used specifically when the player has drifted beyond maxRange, instead of " +
                 "wanderSpeed so a player who " +
                 "just runs away and never attacks can't keep the mech at arm's length forever.")]
        [SerializeField] private float chaseSpeed = 9f;
        [SerializeField] private float wanderInterval = 4f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Attack Pacing")]
        [SerializeField] private float attackEngageRange = 16f;
        // Shortened from 1.2 - "attacking in general needs to be more frequent."
        [SerializeField] private float postAttackGap = 0.35f;

        [Header("Top-Down Ground-Target Attacks")]
        [Tooltip("Ground-targets the player's position at the moment the attack starts, shows " +
                 "the imported prefab's own telegraph (shot_controller) for this long before the " +
                 "hit visual (hit_controller) and damage land - extended well past the pack's " +
                 "authored controller length so the windup reads as a fair dodge window.")]
        [SerializeField] private float topDownTelegraphDelay = 2f;
        [SerializeField] private float topDownDamage = 18f;
        [SerializeField] private float topDownHitRadius = 2.2f;
        [Tooltip("How long the hit visual is left alive after damage lands before being cleaned " +
                 "up, on top of whatever its own particle systems' authored duration is.")]
        [SerializeField] private float topDownVfxLingerAfterHit = 1.5f;
        [Tooltip("Lana Studio's top_down_beam_line_blue.prefab.")]
        [SerializeField] private GameObject topDownBeamPrefab;
        [Tooltip("Lana Studio's top_down_rocket_circle_red.prefab.")]
        [SerializeField] private GameObject topDownRocketPrefab;

        [Header("Shoot Small - Fast Light Bolts")]
        [Tooltip("Gun muzzle points, one per arm cannon - bullets alternate left/right per shot " +
                 "in the burst, like an actual machine gun, instead of all firing from one point. " +
                 "Both default to the mech's own root transform if left unassigned.")]
        [SerializeField] private Transform firePointLeft;
        [SerializeField] private Transform firePointRight;
        [SerializeField] private float shootSmallDelay = 0.4f;
        [Tooltip("Bullets fire as a burst (this many, one bulletBurstInterval apart) each time " +
                 "the round-robin lands on Shoot_Small - reskinned to Projectiles_light: a higher " +
                 "fire rate (short burstInterval) and a longer burst (high burstCount) than the " +
                 "Shoot_Big/magic burst below, per spec (\"faster... higher fire rate and a " +
                 "longer shooting\").")]
        [SerializeField] private int bulletBurstCount = 80;
        [SerializeField] private float bulletBurstInterval = 0.05f;
        [SerializeField] private float bulletSpeed = 26f;
        [SerializeField] private float bulletDamage = 3f;
        [SerializeField] private float bulletLifetime = 4f;
        [SerializeField] private Color bulletColor = new Color(1f, 0.95f, 0.6f);

        [Header("Shoot Big - Slow Magic Bolts")]
        [SerializeField] private float shootBigDelay = 0.6f;
        [Tooltip("Reskinned to Projectiles_magic: a lower fire rate (long burstInterval) and " +
                 "higher per-shot damage than Shoot_Small/light, but still a long burst overall " +
                 "(burstCount raised) per spec (\"lower fire rate, deal more damage, but also " +
                 "shoot for quite long\").")]
        [SerializeField] private int missileBurstCount = 9;
        [SerializeField] private float missileBurstInterval = 0.5f;
        [SerializeField] private float missileSpeed = 11f;
        [SerializeField] private float missileDamage = 16f;
        [SerializeField] private float missileLifetime = 6f;
        [SerializeField] private Color missileColor = new Color(0.7f, 0.3f, 1f);

        [Header("Jump - Ground Slam")]
        [SerializeField] private float jumpAscendDuration = 0.6f;
        [SerializeField] private float jumpApexHeight = 3f;
        [SerializeField] private float jumpLandingHold = 0.5f;
        [SerializeField] private float playerStaggerDuration = 1.4f;
        [SerializeField] private float shakeDuration = 0.5f;
        [SerializeField] private float shakeMagnitude = 0.35f;

        [Header("Visual Assets (Lana Studio's Casual RPG VFX - optional)")]
        [Tooltip("Projectiles_light - Shoot_Small's own visual. Null falls back to the " +
                 "procedural streak.")]
        [SerializeField] private GameObject bulletVisualPrefab;
        [SerializeField] private float bulletVisualScale = 0.5f;
        // Lana Studio's Range_attack prefabs are authored travelling along local +Y, not +Z -
        // see BossAstronautAI.rangedProjectileRotationOffset for the full explanation.
        [SerializeField] private Quaternion bulletVisualRotationOffset = Quaternion.Euler(0f, 90f, 0f);
        [Tooltip("Projectiles_magic - Shoot_Big's own visual.")]
        [SerializeField] private GameObject bigProjectileVisualPrefab;
        [SerializeField] private float bigProjectileVisualScale = 0.6f;
        [SerializeField] private Quaternion bigProjectileVisualRotationOffset = Quaternion.Euler(0f, 90f, 0f);
        [Tooltip("Impact burst played at the hit point when a bullet/big projectile connects " +
                 "(Hit_light for Shoot_Small, a separate hit is applied for Shoot_Big below via " +
                 "ImpactEffectScale doubling - see SpawnProjectile). Null = no impact effect.")]
        [SerializeField] private GameObject impactEffectPrefab;
        [SerializeField] private float impactEffectScale = 1f;
        [SerializeField] private GameObject bigProjectileImpactEffectPrefab;

        // Fixed/predictable, per spec - the two top-down variants alternate by recurring twice in
        // this array rather than by any runtime coin-flip, so the whole 8-step cycle is still a
        // deterministic rotation like the rest of the mech's attacks.
        private static readonly AttackKind[] AttackOrder =
        {
            AttackKind.TopDownBeam, AttackKind.ShootSmall, AttackKind.Jump, AttackKind.ShootBig,
            AttackKind.TopDownRocket, AttackKind.ShootSmall, AttackKind.Jump, AttackKind.ShootBig,
        };

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int TopDownBeamParam = Animator.StringToHash("TopDownBeam");
        private static readonly int TopDownRocketParam = Animator.StringToHash("TopDownRocket");
        private static readonly int ShootSmallParam = Animator.StringToHash("ShootSmall");
        private static readonly int ShootBigParam = Animator.StringToHash("ShootBig");
        private static readonly int JumpParam = Animator.StringToHash("Jump");

        private CharacterController _controller;
        private float _verticalVelocity;
        private PlayerController _playerController;
        private ThirdPersonCameraController _cameraController;

        [Header("Audio - Leg Steps")]
        [SerializeField] private float legStepInterval = 0.5f;
        [SerializeField] private float legStepMinSpeed = 0.3f;

        private Vector3 _wanderDirection = Vector3.forward;
        private float _nextWanderTime;
        private int _attackIndex;
        private float _attackReadyTime;
        private bool _isAttacking;
        private float _nextLegStepTime;
        private bool _legStepAlternate;
        // JumpSlam hand-animates height directly via CharacterController.Move - the normal
        // gravity/verticalVelocity handling below would otherwise fight that arc (Update() still
        // runs every frame regardless of the coroutine, so without this it would apply falling
        // velocity on top of the coroutine's own ascend/descend motion).
        private bool _manualHeightControl;

        protected override bool StartsAggroed => true;

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

        // Reuses PlayerHitAudio/PlayerAnimatorRelay's Mech* cue ("the same conceptual metal impact"
        // per spec) rather than a separate boss-only sfx id.
        protected override void HandleHit(DamageType damageType)
        {
            AudioManager.Instance.PlaySfx(SfxId.MechHitReact, transform.position);
        }

        protected override void HandleDeath()
        {
            animator.SetFloat(SpeedParam, 0f);

            // Sequencing only, per spec - a full "spasm the mech" animation/VFX sequence is out of
            // scope for this audio task and is left as a follow-up.
            AudioManager.Instance.PlaySfx(SfxId.Boss2DeathImpact, transform.position);

            BossFightController.BossFightActive = false;
            var musicManager = MusicManager.Instance;
            if (musicManager != null)
            {
                musicManager.PlayMusic(musicManager.baseMusic);
            }

            // base.HandleDeath() calls StopAllCoroutines() - start the delayed explosion AFTER it,
            // not before, or it would be killed before its 0.4s wait elapses.
            base.HandleDeath();
            StartCoroutine(PlayDelayedDeathExplosion());
        }

        private IEnumerator PlayDelayedDeathExplosion()
        {
            yield return new WaitForSeconds(0.4f);
            AudioManager.Instance.PlaySfx(SfxId.Boss2DeathExplosion, transform.position);
        }

        private void Update()
        {
            if (IsAiLifecycleSuspended) return;

            if (!CanRunAi())
            {
                if (!_manualHeightControl) MoveGrounded(_controller, Vector3.zero, ref _verticalVelocity, gravity, groundedStickForce);
                return;
            }

            if (_manualHeightControl)
            {
                FacePlayer();
                return;
            }

            if (_isAttacking)
            {
                FacePlayer();
                MoveGrounded(_controller, Vector3.zero, ref _verticalVelocity, gravity, groundedStickForce);
                return;
            }

            Wander();
            TryAdvanceAttack();
        }

        protected override void OnFrozen()
        {
            _isAttacking = false;
            _manualHeightControl = false;
        }

        // Movement is independent of the player's position beyond keeping distance in
        // [minRange, maxRange] - "this can be in any direction and not necessarily towards the
        // player, but keeping within range." A fresh random direction is picked periodically;
        // outside the band it's biased back toward/away from the player instead.
        private void Wander()
        {
            float distance = DistanceToPlayer();
            float effectiveMinRange = WorldDistance(minRange);
            float effectiveMaxRange = WorldDistance(maxRange);

            if (Time.time >= _nextWanderTime)
            {
                _nextWanderTime = Time.time + wanderInterval;
                Vector2 randomCircle = Random.insideUnitCircle;
                _wanderDirection = BuildTangentDirection(SurfaceUp(transform.position), randomCircle);
            }

            Vector3 flatToPlayer = TangentTowardsPlayer();

            Vector3 direction = _wanderDirection;
            float speed = wanderSpeed;
            if (distance < effectiveMinRange && flatToPlayer.sqrMagnitude > 0.01f)
            {
                direction = -flatToPlayer.normalized;
            }
            else if (distance > effectiveMaxRange && flatToPlayer.sqrMagnitude > 0.01f)
            {
                // Faster catch-up speed specifically here - a player who just runs away and never
                // attacks shouldn't be able to keep the mech stuck at wanderSpeed forever.
                direction = flatToPlayer.normalized;
                speed = chaseSpeed;
            }

            Vector3 posBeforeMove = transform.position;
            Vector3 movementDirection = MoveGrounded(
                _controller, direction * speed,
                ref _verticalVelocity, gravity, groundedStickForce);
            if (movementDirection.sqrMagnitude > 0.0001f) FaceMovement(movementDirection);
            else FacePlayer();
            animator.SetFloat(SpeedParam, movementDirection.sqrMagnitude > 0.0001f ? 1f : 0f,
                0.1f, Time.deltaTime);

            // Measured against actual displacement this frame, not the intended wander
            // direction/speed - Wander() runs (and issues a Move call) essentially every frame
            // it's not attacking, so gating on intent alone made leg-step audio play even while
            // the mech was effectively holding position (e.g. pinned against the band boundary or
            // blocked by a collision) and reads as "moving" sound with nothing visibly moving.
            float horizontalSpeed = (transform.position - posBeforeMove).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            UpdateLegSteps(horizontalSpeed);
        }

        // No existing step event to hook into (unlike PlayerAnimatorRelay, which drives off
        // NormalizedSpeed) - a simple fixed-cadence timer while actually displacing, same interval
        // approach as the player's footsteps.
        private void UpdateLegSteps(float horizontalSpeed)
        {
            if (horizontalSpeed < legStepMinSpeed)
            {
                // Re-arm immediately so a step plays right away once it starts moving again,
                // instead of waiting out whatever fraction of the interval was left when it stopped.
                _nextLegStepTime = Time.time;
                return;
            }

            if (Time.time < _nextLegStepTime) return;
            _nextLegStepTime = Time.time + legStepInterval;

            SfxId stepId = _legStepAlternate ? SfxId.BossLegStepB : SfxId.BossLegStepA;
            _legStepAlternate = !_legStepAlternate;
            AudioManager.Instance.PlaySfx(stepId, transform.position);
        }

        // Fixed round-robin, gated on cooldown + engage range. Out-of-range simply waits without
        // advancing the sequence, so the same rotation resumes once back in range rather than
        // skipping an attack.
        private void TryAdvanceAttack()
        {
            if (Time.time < _attackReadyTime) return;
            if (DistanceToPlayer() > WorldDistance(attackEngageRange)) return;

            StartCoroutine(AttackRoutine(AttackOrder[_attackIndex]));
            _attackIndex = (_attackIndex + 1) % AttackOrder.Length;
        }

        private IEnumerator AttackRoutine(AttackKind kind)
        {
            _isAttacking = true;
            animator.SetFloat(SpeedParam, 0f);

            switch (kind)
            {
                case AttackKind.TopDownBeam:
                    yield return TopDownAttack(TopDownBeamParam, topDownBeamPrefab);
                    break;
                case AttackKind.TopDownRocket:
                    yield return TopDownAttack(TopDownRocketParam, topDownRocketPrefab);
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

            yield return new WaitForSeconds(AttackInterval(postAttackGap));
            _isAttacking = false;
            _attackReadyTime = Time.time;
        }

        // Ground-targets the player's position at cast time, shows the imported prefab's own
        // telegraph (its shot_controller child - a ground marker/ring/dot, per the pack's
        // convention) for topDownTelegraphDelay, then reveals the hit visual (hit_controller) and
        // applies damage if the player is still standing in the marked area. Neither controller
        // has a baked-in delay (both are active with startDelay 0 in the source prefab), so the
        // telegraph -> impact sequencing is entirely driven here, not by the prefab itself.
        private IEnumerator TopDownAttack(int triggerParam, GameObject vfxPrefab)
        {
            animator.SetTrigger(triggerParam);

            if (player == null) yield break;

            Vector3 groundPoint = player.position;
            if (TryGetSurfacePoint(groundPoint, out Vector3 resolvedGroundPoint, out _)) groundPoint = resolvedGroundPoint;

            // SFX moved from cast-time to hit-confirm - it should only be heard when the attack
            // actually connects, not on every telegraph/cast regardless of whether the player
            // dodged out of the marked area.
            SfxId hitSfx = triggerParam == TopDownBeamParam ? SfxId.Boss2TopDownV1 : SfxId.Boss2TopDownV2;

            // groundPoint (boss's own y, used for the hit-check distance below) stays untouched -
            // this is only for where the VFX visually spawns, so a jumping/airborne player doesn't
            // leave the ground-impact effect floating at their feet mid-air.
            Vector3 vfxPoint = IsPlanetSurfaceRuntime ? groundPoint : TopDownGroundEffect.GroundedPoint(groundPoint);

            yield return TopDownGroundEffect.Play(vfxPrefab, vfxPoint, topDownTelegraphDelay,
                topDownVfxLingerAfterHit, () =>
                {
                    if (isDead) return;
                    if (Vector3.Distance(player.position, groundPoint) <= topDownHitRadius &&
                        playerHealth != null && !playerHealth.IsDead)
                    {
                        playerHealth.ApplyDamage(topDownDamage * DamageMultiplier, groundPoint, gameObject, DamageType.Generic);
                        AudioManager.Instance.PlaySfx(hitSfx, groundPoint);
                    }
                });
        }

        // Fires burstCount shots, burstInterval apart, off a single animator trigger/clip play -
        // "shooting needs to be a lot more frequent" without also re-triggering (and restarting)
        // the Shoot_Small/Shoot_Big clip once per shot. Alternates firePointLeft/firePointRight
        // per shot - "come out of the mech guns like machine guns" instead of every shot spawning
        // from one point (which, left unassigned, was the mech's own root - reading as "the
        // bottom centre").
        // Shoot_Small (fast/frequent/long burst) reads as the "forward machine-gun-style burst" -
        // bracketed with the manual half-clip-loop Boss2ShootPrimaryLoop cue for its whole burst,
        // per spec ("machine-gun loop + ricochet" pairing). Shoot_Big (slow/heavy/long-range) gets
        // just the per-impact Boss2Ricochet cue on each hit, no sustained loop - there's no
        // dedicated sfx id for it in the spec's table, and its lower fire rate doesn't read as a
        // sustained "loop" the way Shoot_Small's 80-bullet burst does.
        private IEnumerator ShootAttack(int triggerParam, float delay, float speed, float damage, float lifetime,
            Color color, int burstCount, float burstInterval, ProjectileVisualStyle style)
        {
            animator.SetTrigger(triggerParam);
            yield return new WaitForSeconds(AttackInterval(delay));

            bool isMachineGunBurst = style == ProjectileVisualStyle.Bullet;
            AudioHandle loopHandle = AudioHandle.Invalid;
            if (isMachineGunBurst)
            {
                loopHandle = AudioManager.Instance.PlayLoop(SfxId.Boss2ShootPrimaryLoop, transform);
            }

            float visualRadius = style == ProjectileVisualStyle.BigProjectile ? 0.4f : 0.3f;
            for (int i = 0; i < burstCount; i++)
            {
                Transform origin = i % 2 == 0 ? firePointLeft : firePointRight;
                SpawnProjectile(origin.position, speed, damage, lifetime, false, color, visualRadius, style, 90f, 10f);
                if (i < burstCount - 1) yield return new WaitForSeconds(AttackInterval(burstInterval));
            }

            if (loopHandle.IsValid) AudioManager.Instance.StopLoop(loopHandle);
        }

        private void SpawnProjectile(Vector3 origin, float speed, float damage, float lifetime, bool homing,
            Color color, float visualRadius, ProjectileVisualStyle style, float homingTurnDegreesPerSecond, float homingRange)
        {
            if (player == null) return;

            Vector3 direction = (player.position + SurfaceUp(player.position) - origin).normalized;
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
                    visuals.ExtraRotationOffset = bigProjectileVisualRotationOffset;
                    if (bigProjectileImpactEffectPrefab != null) visuals.ImpactEffectPrefab = bigProjectileImpactEffectPrefab;
                    visuals.ImpactEffectScale *= 2f; // bigger projectile hits should read as a bigger blast
                    break;
                case ProjectileVisualStyle.Bullet:
                    visuals.ImportedVisualPrefab = bulletVisualPrefab;
                    visuals.ImportedVisualScale = bulletVisualScale;
                    visuals.ExtraRotationOffset = bulletVisualRotationOffset;
                    break;
            }

            BossProjectile.Create(origin, direction, player, speed * ProjectileSpeedMultiplier, damage * DamageMultiplier, homing, lifetime, mask,
                color, visualRadius, style, homingTurnDegreesPerSecond, homingRange, visuals,
                onHit: _ => AudioManager.Instance.PlaySfx(SfxId.Boss2Ricochet, origin));
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
            _verticalVelocity = 0f;

            float heightOffset = 0f;
            float elapsed = 0f;
            while (elapsed < jumpAscendDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / jumpAscendDuration;
                float targetHeight = Mathf.Sin(t * Mathf.PI * 0.5f) * jumpApexHeight;
                _controller.Move(SurfaceUp(transform.position) * (targetHeight - heightOffset));
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
                _controller.Move(SurfaceUp(transform.position) * (targetHeight - heightOffset));
                heightOffset = targetHeight;
                yield return null;
            }

            if (heightOffset != 0f)
            {
                _controller.Move(-SurfaceUp(transform.position) * heightOffset);
            }

            _manualHeightControl = false;

            AudioManager.Instance.PlaySfx(SfxId.Boss2JumpSlam, transform.position);

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

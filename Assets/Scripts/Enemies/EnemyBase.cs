using System.Collections;
using System.Collections.Generic;
using Audio;
using Combat;
using UnityEngine;
using Vfx;

namespace Enemies
{
    /// <summary>Wave-owned multipliers applied to an enemy's explicitly supported combat stats.</summary>
    public readonly struct EnemyWaveScaling
    {
        public static readonly EnemyWaveScaling Identity = new EnemyWaveScaling(1f, 1f, 1f, 1f, 1f);

        public readonly float Health;
        public readonly float Damage;
        public readonly float Movement;
        public readonly float AttackRate;
        public readonly float ProjectileSpeed;

        public EnemyWaveScaling(float health, float damage, float movement, float attackRate, float projectileSpeed)
        {
            Health = Mathf.Max(0f, health);
            Damage = Mathf.Max(0f, damage);
            Movement = Mathf.Max(0f, movement);
            AttackRate = Mathf.Max(0.0001f, attackRate);
            ProjectileSpeed = Mathf.Max(0f, projectileSpeed);
        }
    }

    public enum EnemyDespawnReason
    {
        Forced,
        Retreat,
    }

    [RequireComponent(typeof(Health))]
    public abstract class EnemyBase : MonoBehaviour
    {
        [Header("Enemy Base")]
        [SerializeField] protected Animator animator;
        [SerializeField] private float faceRotationDegreesPerSecond = 220f;

        [Header("Wave Runtime")]
        [Tooltip("Ordinary wave enemies remain passive until the player enters this radius. Bosses override this to start aggroed.")]
        [SerializeField, Min(0f)] private float detectionRadius = 20f;
        [Tooltip("The actual crater/planet collider. Leaving this empty auto-discovers the active object named Planet Ground; no such object keeps the flat sandbox behaviour.")]
        [SerializeField] private Collider planetGround;
        [SerializeField] private Transform planetCenter;
        [SerializeField, Min(0.01f)] private float surfaceProbeDistance = 96f;
        [SerializeField, Min(0f)] private float groundedSnapDistance = 0.18f;

        [Header("Navigation")]
        [Tooltip("Solid world layers probed ahead of movement. The terrain collider is explicitly ignored; rocks, walls, and other solid obstacles are not.")]
        [SerializeField] private LayerMask navigationObstacleMask = ~0;
        [SerializeField, Min(0.1f)] private float navigationProbeDistance = 2.5f;
        [Tooltip("Small world-space buffer added around the actor's real body radius when checking rocks and walls.")]
        [SerializeField, Min(0f)] private float navigationClearanceMargin = 0.25f;
        [SerializeField, Min(0.05f)] private float hoverProbeRadius = 0.45f;
        [SerializeField, Min(0.1f)] private float stuckRecoveryDelay = 0.65f;
        [SerializeField, Min(0.1f)] private float stuckDetourDuration = 1.1f;

        [Header("Death")]
        [Tooltip("How long the Death animation gets to play, undisturbed, before the dissolve " +
                 "starts eating the model away.")]
        [SerializeField] private float deathAnimationHold = 1.2f;
        [SerializeField] private float dissolveDuration = 1f;
        [SerializeField] private float deathFallGravity = 20f;
        [Tooltip("Maximum time after the animation hold that a corpse may spend falling before " +
                 "cleanup proceeds. Prevents malformed or unreachable surface queries from " +
                 "blocking dissolve/destruction forever.")]
        [SerializeField, Min(0.1f)] private float maximumDeathFallDuration = 3f;

        [Header("Melee Hit VFX")]
        [Tooltip("Imported slash effect (e.g. Lana Studio's Slash_stone_once) played on the " +
                 "player where this enemy's melee attack connects.")]
        [SerializeField] private GameObject meleeHitVfxPrefab;
        [SerializeField] private float meleeHitVfxScale = 1f;

        [Header("Slow Debuff (e.g. Ultimate electric bolts)")]
        [Tooltip("Looping fog VFX (e.g. Lana Studio's Fog/Fog_electric) worn for the duration " +
                 "of an active slow, so the debuff is visible, not just a numeric multiplier.")]
        [SerializeField] private GameObject slowVfxPrefab;
        [SerializeField] private float slowVfxScale = 1f;

        private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        private static Shader _dissolveShader;

        protected Health health;
        protected Transform player;
        protected Health playerHealth;
        protected bool isDead;
        // Gates AI movement/attacks the same way isDead already does (subclasses add
        // `|| isFrozen` next to their existing `if (isDead) return;` in Update) - used by
        // BossFightController to guarantee no basic enemy can attack the player during the
        // Stage 1 -> Stage 2 cutscene, regardless of whether it happens to be active in the scene.
        protected bool isFrozen;
        protected bool isRetreating;

        private float _baseMaxHealth;
        private EnemyWaveScaling _waveScaling = EnemyWaveScaling.Identity;
        private float _worldDistanceMultiplier = 1f;
        private bool _hasAggro;
        private bool _removalReported;
        private readonly RaycastHit[] _navigationHits = new RaycastHit[12];
        private int _detourSide;
        private float _detourUntil;
        private float _stuckFor;
        private Vector3 _escapeDirection;
        private float _escapeUntil;

        // 1 = no slow. AI movement scripts (EnemyFlyingAI/EnemySmallAI/EnemyLargeAI) multiply
        // their own speed fields by this at their existing move call sites - simpler than a full
        // keyed-modifier dictionary (PlayerController's SetMovementSpeedModifier) since there's
        // only one debuff source today (the Ultimate's electric bolts).
        public float SpeedMultiplier { get; private set; } = 1f;
        private Coroutine _slowRoutine;
        private GameObject _slowVfxInstance;

        /// <summary>Raised only for a health-driven kill, so reward systems never see a recycle as a kill.</summary>
        public event System.Action<EnemyBase> Killed;
        /// <summary>Raised only when a director explicitly removes this enemy without death rewards.</summary>
        public event System.Action<EnemyBase, EnemyDespawnReason> Despawned;

        public bool IsDead => isDead;
        public bool IsAggroed => _hasAggro;
        public bool IsPlanetSurfaceRuntime => planetGround != null;
        public float DetectionRadius => detectionRadius;
        public EnemyWaveScaling WaveScaling => _waveScaling;
        protected float DamageMultiplier => _waveScaling.Damage;
        protected float MovementMultiplier => _waveScaling.Movement;
        protected float AttackRateMultiplier => _waveScaling.AttackRate;
        protected float ProjectileSpeedMultiplier => _waveScaling.ProjectileSpeed;
        protected virtual bool StartsAggroed => false;
        protected bool IsAiLifecycleSuspended => isDead || isFrozen || isRetreating;

        protected virtual void Awake()
        {
            health = GetComponent<Health>();
            _baseMaxHealth = health.MaxHealth;
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (planetGround == null || !planetGround.enabled || !planetGround.gameObject.activeInHierarchy)
            {
                GameObject planet = GameObject.Find("Planet Ground");
                if (planet != null)
                {
                    planetCenter = planet.transform;
                    // Planet Ground deliberately retains a disabled reference SphereCollider on
                    // its root. GetComponentInChildren<Collider>() returns that first match,
                    // not the active crater mesh used by gameplay. Only the active MeshCollider
                    // represents the authored lunar surface.
                    planetGround = FindActivePlanetMeshCollider(planet);
                }
            }
            if (planetGround != null && planetCenter == null)
            {
                GameObject planet = GameObject.Find("Planet Ground");
                planetCenter = planet != null ? planet.transform : planetGround.transform;
            }

            var playerController = FindFirstObjectByType<Player.PlayerController>();
            if (playerController != null)
            {
                player = playerController.transform;
                playerHealth = playerController.GetComponent<Health>();
            }

            _hasAggro = StartsAggroed;
        }

        /// <summary>Finds the authored active crater surface, excluding disabled reference colliders.</summary>
        public static MeshCollider FindActivePlanetMeshCollider(GameObject planet)
        {
            if (planet == null) return null;
            MeshCollider[] candidates = planet.GetComponentsInChildren<MeshCollider>(true);
            for (int index = 0; index < candidates.Length; index++)
            {
                MeshCollider candidate = candidates[index];
                if (candidate != null && candidate.enabled && candidate.gameObject.activeInHierarchy) return candidate;
            }
            return null;
        }

        protected virtual void OnEnable()
        {
            health.Died += HandleDeath;
            health.Hit += HandleHit;
        }

        protected virtual void OnDisable()
        {
            health.Died -= HandleDeath;
            health.Hit -= HandleHit;
        }

        // BossAstronautAI/BossMechAI override this to play their own Boss1HitReact/MechHitReact
        // cue instead - this base implementation covers the three basic enemy types.
        protected virtual void HandleHit(DamageType damageType)
        {
            AudioManager.Instance.PlaySfx(SfxId.EnemyHitReact, transform.position);
        }

        // Cancels any in-flight attack coroutine outright (not paused/resumed) rather than
        // leaving it to finish later - StopAllCoroutines() kills it mid-flight, which would leave
        // a subclass's own private "_isAttacking" flag stuck true forever (nothing left to ever
        // clear it) unless that subclass resets it here via the OnFrozen override below.
        public void SetFrozen(bool frozen)
        {
            if (isDead) return;

            isFrozen = frozen;
            if (frozen)
            {
                StopAllCoroutines();
                OnFrozen();
            }
        }

        /// Subclasses with their own private "is currently mid-attack" flag (EnemyFlyingAI/
        /// EnemySmallAI/EnemyLargeAI) override this to reset it - see SetFrozen's comment.
        protected virtual void OnFrozen()
        {
        }

        // Movement/attack scripts check isDead themselves rather than being disabled outright,
        // so the Death animator trigger (already fired by Health) plays out undisturbed instead
        // of being interrupted by this component going away mid-transition. StopAllCoroutines
        // matters here too: a subclass's in-flight attack coroutine (e.g. EnemyFlyingAI's
        // multi-second Headbutt telegraph) doesn't check isDead mid-sequence, so dying during an
        // attack could otherwise leave that coroutine still running/animating over the top of
        // the Death state - killing it here is what guarantees Death is the only thing playing
        // from this point on.
        protected virtual void HandleDeath()
        {
            isDead = true;
            AudioManager.Instance.PlaySfx(SfxId.EnemyDeath, transform.position);
            ReportKilled();
            StopAllCoroutines();
            DisableDeathCollision();
            StartCoroutine(DissolveAndDestroy());
        }

        private void DisableDeathCollision()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null) colliders[index].enabled = false;
            }
        }

        /// <summary>Applies the five wave multipliers. Movement/attack/projectile caps belong to the director that supplies them.</summary>
        public void ConfigureWaveScaling(EnemyWaveScaling scaling)
        {
            _waveScaling = scaling;
            if (health == null) health = GetComponent<Health>();
            if (health == null) return;
            if (_baseMaxHealth <= 0f) _baseMaxHealth = health.MaxHealth;
            health.SetMaxHealth(_baseMaxHealth * scaling.Health, true);
        }

        /// <summary>Overrides the shared passive detection distance for a spawn family or encounter.</summary>
        public void ConfigureDetectionRadius(float radius)
        {
            detectionRadius = Mathf.Max(0f, radius);
        }

        /// <summary>
        /// Scales authored movement/combat distance bands with an external wrapper scale. Boss
        /// stages keep their own authored local scales; the wave director supplies only its 3x
        /// wrapper multiplier so the flat sandbox remains unchanged.
        /// </summary>
        public void ConfigureWorldDistanceMultiplier(float multiplier)
        {
            _worldDistanceMultiplier = Mathf.Max(0.0001f, multiplier);
        }

        protected float WorldDistance(float authoredDistance)
        {
            return ScaleWorldDistance(authoredDistance, _worldDistanceMultiplier);
        }

        public static float ScaleWorldDistance(float authoredDistance, float multiplier)
        {
            return Mathf.Max(0f, authoredDistance) * Mathf.Max(0.0001f, multiplier);
        }

        /// <summary>Immediately enables sticky aggro; used by arena waves and both boss phases.</summary>
        public void ForceImmediateAggro()
        {
            _hasAggro = true;
        }

        /// <summary>Destroys this enemy without invoking death/reward listeners.</summary>
        public void RequestDespawnWithoutRewards()
        {
            ReportDespawn(EnemyDespawnReason.Forced);
            Destroy(gameObject);
        }

        /// <summary>Stops combat immediately, then removes the enemy without death rewards after the requested retreat window.</summary>
        public void RequestRetreatAndDespawn(float retreatDuration)
        {
            if (isDead || _removalReported) return;
            isRetreating = true;
            StopAllCoroutines();
            OnFrozen();
            StartCoroutine(RetreatAndDespawnRoutine(Mathf.Max(0f, retreatDuration)));
        }

        private IEnumerator RetreatAndDespawnRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            ReportDespawn(EnemyDespawnReason.Retreat);
            Destroy(gameObject);
        }

        protected void ReportKilled()
        {
            if (_removalReported) return;
            _removalReported = true;
            Killed?.Invoke(this);
        }

        private void ReportDespawn(EnemyDespawnReason reason)
        {
            if (_removalReported) return;
            _removalReported = true;
            Despawned?.Invoke(this, reason);
        }

        /// <summary>
        /// Returns true only for active aggro. Callers must return on
        /// <see cref="IsAiLifecycleSuspended"/> before treating false as passive behaviour;
        /// otherwise death/freeze/retreat would incorrectly run hover or controller maintenance.
        /// Aggro, once acquired, never clears.
        /// </summary>
        protected bool CanRunAi()
        {
            if (isDead || isFrozen || isRetreating) return false;
            if (_hasAggro) return true;
            if (player != null && DistanceToPlayer() <= detectionRadius) _hasAggro = true;
            return _hasAggro;
        }

        protected float AttackInterval(float unscaledInterval)
        {
            return unscaledInterval / AttackRateMultiplier;
        }

        // True per-pixel dissolve (Custom/EnemyDissolve, Assets/Art/Shaders/S_EnemyDissolve.shader):
        // clips pixels away against 3D noise as _DissolveAmount rises 0->1, with a glowing edge -
        // a "Thanos snap" crumble rather than a uniform scale-down. Swaps each renderer onto a
        // per-instance material clone (copying the original's texture/color) instead of touching
        // the shared M_Enemy*.mat asset, so other live instances of the same enemy type are
        // unaffected. deathAnimationHold runs first so the Death animation always gets
        // uninterrupted screen time before the dissolve starts.
        private IEnumerator DissolveAndDestroy()
        {
            yield return FallToGround();

            if (_dissolveShader == null) _dissolveShader = Shader.Find("Custom/EnemyDissolve");

            var dissolveMaterials = new List<Material>();
            if (_dissolveShader != null)
            {
                foreach (var meshRenderer in GetComponentsInChildren<Renderer>())
                {
                    var source = meshRenderer.sharedMaterial;
                    var dissolveMaterial = new Material(_dissolveShader);
                    if (source != null)
                    {
                        if (source.HasProperty("_BaseMap")) dissolveMaterial.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                        if (source.HasProperty("_BaseColor")) dissolveMaterial.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                    }
                    meshRenderer.material = dissolveMaterial;
                    dissolveMaterials.Add(dissolveMaterial);
                }
            }

            float elapsed = 0f;
            while (elapsed < dissolveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dissolveDuration);
                foreach (var dissolveMaterial in dissolveMaterials)
                {
                    dissolveMaterial.SetFloat(DissolveAmountId, t);
                }
                yield return null;
            }

            Destroy(gameObject);
        }

        // Flyers (EnemyFlyingAI/EnemySmallAI) freeze mid-air at whatever hover height they died
        // at, since their Update loops stop as soon as isDead is set - without this they'd
        // dissolve while still floating. Runs concurrently with the Death animation (gravity
        // fall, not a snap), and keeps going past deathAnimationHold if it hasn't landed yet, so
        // the dissolve never starts before the model is actually on the ground. Grounded enemies
        // (EnemyLargeAI) are already at y=0 and just wait out deathAnimationHold unaffected.
        private IEnumerator FallToGround()
        {
            float verticalVelocity = 0f;
            float elapsed = 0f;

            bool aboveSurface = IsAboveSurface();
            while (ShouldContinueDeathFall(
                       elapsed, deathAnimationHold, maximumDeathFallDuration, aboveSurface))
            {
                elapsed += Time.deltaTime;

                if (aboveSurface)
                {
                    verticalVelocity += deathFallGravity * Time.deltaTime;
                    Vector3 down = IsPlanetSurfaceRuntime ? -SurfaceUp(transform.position) : Vector3.down;
                    Vector3 candidate = transform.position + down * verticalVelocity * Time.deltaTime;
                    if (TryGetSurfacePoint(candidate, out Vector3 groundPoint, out Vector3 groundUp) &&
                        Vector3.Dot(candidate - groundPoint, groundUp) <= 0f)
                    {
                        transform.position = groundPoint;
                    }
                    else transform.position = candidate;
                }

                yield return null;
                aboveSurface = IsAboveSurface();
            }

            if (TryGetSurfacePoint(transform.position, out Vector3 finalGroundPoint, out _))
            {
                transform.position = finalGroundPoint;
            }
        }

        internal static bool ShouldContinueDeathFall(
            float elapsed, float animationHold, float maximumFallDuration, bool aboveSurface)
        {
            float hold = Mathf.Max(0f, animationHold);
            if (elapsed < hold) return true;
            return aboveSurface && elapsed < hold + Mathf.Max(0.1f, maximumFallDuration);
        }

        protected void FacePlayer()
        {
            if (player == null) return;
            FaceDirection(Vector3.ProjectOnPlane(player.position - transform.position, SurfaceUp(transform.position)));
        }

        /// <summary>
        /// Turns the body toward actual locomotion. Boss projectiles still use their explicit
        /// player-target direction, so this fixes forward-only walk animations without changing
        /// combat aiming.
        /// </summary>
        protected void FaceMovement(Vector3 direction)
        {
            FaceDirection(TangentDirection(direction));
        }

        private void FaceDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, SurfaceUp(transform.position));
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, faceRotationDegreesPerSecond * Time.deltaTime);
        }

        protected float DistanceToPlayer()
        {
            return player == null ? Mathf.Infinity : Vector3.Distance(transform.position, player.position);
        }

        protected Vector3 SurfaceUp(Vector3 position)
        {
            if (!IsPlanetSurfaceRuntime) return Vector3.up;
            Vector3 offset = position - planetCenter.position;
            return offset.sqrMagnitude > 0.0001f ? offset.normalized : planetCenter.up;
        }

        protected Vector3 TangentTowardsPlayer()
        {
            if (player == null) return Vector3.zero;
            return Vector3.ProjectOnPlane(player.position - transform.position, SurfaceUp(transform.position)).normalized;
        }

        protected Vector3 TangentDirection(Vector3 direction)
        {
            return Vector3.ProjectOnPlane(direction, SurfaceUp(transform.position)).normalized;
        }

        /// <summary>Builds an unbiased local tangent direction from a two-dimensional sample.</summary>
        public static Vector3 BuildTangentDirection(Vector3 surfaceUp, Vector2 sample)
        {
            Vector3 up = surfaceUp.sqrMagnitude > 0.0001f ? surfaceUp.normalized : Vector3.up;
            Vector3 reference = Mathf.Abs(Vector3.Dot(up, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
            Vector3 tangentRight = Vector3.Cross(reference, up).normalized;
            Vector3 tangentForward = Vector3.Cross(up, tangentRight).normalized;
            Vector3 result = tangentRight * sample.x + tangentForward * sample.y;
            return result.sqrMagnitude > 0.0001f ? result.normalized : tangentForward;
        }

        /// <summary>Returns a deterministic tangent detour on the requested side of desired movement.</summary>
        public static Vector3 BuildDetourDirection(Vector3 desiredDirection, Vector3 surfaceUp, int side)
        {
            Vector3 up = surfaceUp.sqrMagnitude > 0.0001f ? surfaceUp.normalized : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(desiredDirection, up);
            if (forward.sqrMagnitude < 0.0001f) return Vector3.zero;
            forward.Normalize();
            Vector3 lateral = Vector3.Cross(up, forward) * (side >= 0 ? 1f : -1f);
            return (forward * 0.55f + lateral * 0.85f).normalized;
        }

        /// <summary>Prefers the deterministic side unless only the other probe is clear.</summary>
        public static int ChooseDetourSide(bool leftClear, bool rightClear, int preferredSide)
        {
            int preferred = preferredSide >= 0 ? 1 : -1;
            if (leftClear && !rightClear) return 1;
            if (rightClear && !leftClear) return -1;
            return preferred;
        }

        /// <summary>
        /// Requests a short outward steering override. Wave protected-area handling uses this
        /// instead of rewinding transforms into the same blocked route every frame.
        /// </summary>
        public void RequestNavigationRecovery(Vector3 escapeDirection, float duration = 1.25f)
        {
            Vector3 tangentEscape = TangentDirection(escapeDirection);
            if (tangentEscape.sqrMagnitude < 0.0001f) return;
            _escapeDirection = tangentEscape;
            _escapeUntil = Mathf.Max(_escapeUntil, Time.time + Mathf.Max(0.05f, duration));
            _detourSide = Vector3.Dot(Vector3.Cross(SurfaceUp(transform.position), transform.forward), tangentEscape) >= 0f ? 1 : -1;
        }

        protected Vector3 MoveHover(Vector3 tangentDirection, float speed, float hoverHeight)
        {
            // The hover transform is already at its requested radial height; probe from that
            // body position rather than another hoverHeight above it so low rocks are seen.
            Vector3 scale = transform.lossyScale;
            float worldProbeRadius = hoverProbeRadius * Mathf.Max(
                Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)) + navigationClearanceMargin;
            Vector3 direction = ResolveNavigationDirection(tangentDirection, transform.position, worldProbeRadius);
            if (!IsPlanetSurfaceRuntime)
            {
                Vector3 beforeMove = transform.position;
                Vector3 next = transform.position + direction * speed * MovementMultiplier * SpeedMultiplier * Time.deltaTime;
                next.y = hoverHeight;
                transform.position = next;
                TrackNavigationProgress(direction, beforeMove, transform.position, speed);
                return ActualTangentDisplacement(beforeMove, transform.position, Vector3.up);
            }

            Vector3 before = transform.position;
            Vector3 candidate = transform.position + direction * speed * MovementMultiplier * SpeedMultiplier * Time.deltaTime;
            if (TryGetSurfacePoint(candidate, out Vector3 groundPoint, out Vector3 up))
            {
                transform.position = groundPoint + up * hoverHeight;
            }
            else transform.position = candidate;
            TrackNavigationProgress(direction, before, transform.position, speed);
            return ActualTangentDisplacement(before, transform.position, SurfaceUp(before));
        }

        protected Vector3 MoveGrounded(CharacterController controller, Vector3 tangentVelocity, ref float verticalVelocity,
            float gravity, float stickForce)
        {
            if (controller == null) return Vector3.zero;
            float speed = tangentVelocity.magnitude;
            float radius = Mathf.Max(0.05f, controller.radius * Mathf.Max(
                Mathf.Abs(controller.transform.lossyScale.x), Mathf.Abs(controller.transform.lossyScale.z))) +
                           navigationClearanceMargin;
            Vector3 direction = ResolveNavigationDirection(
                tangentVelocity, ControllerLowerSphereCenter(controller), radius);
            Vector3 before = transform.position;
            if (!IsPlanetSurfaceRuntime)
            {
                if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = stickForce;
                else verticalVelocity += gravity * Time.deltaTime;
                controller.Move((direction * speed * MovementMultiplier * SpeedMultiplier + Vector3.up * verticalVelocity) * Time.deltaTime);
                TrackNavigationProgress(direction, before, transform.position, speed);
                return ActualTangentDisplacement(before, transform.position, Vector3.up);
            }

            Vector3 up = SurfaceUp(transform.position);
            if (TryGetSurfacePoint(transform.position, out Vector3 currentGround, out Vector3 currentUp) &&
                IsControllerNearSurface(controller, currentGround, currentUp) && verticalVelocity <= 0f)
            {
                verticalVelocity = stickForce;
            }
            else verticalVelocity -= Mathf.Abs(gravity) * Time.deltaTime;

            controller.Move((direction * speed * MovementMultiplier * SpeedMultiplier + up * verticalVelocity) * Time.deltaTime);
            if (TryGetSurfacePoint(transform.position, out Vector3 groundPoint, out Vector3 groundUp) &&
                IsControllerNearSurface(controller, groundPoint, groundUp) && verticalVelocity <= 0f)
            {
                SnapControllerRootToSurface(controller, groundPoint, groundUp);
                verticalVelocity = stickForce;
            }
            TrackNavigationProgress(direction, before, transform.position, speed);
            return ActualTangentDisplacement(before, transform.position, SurfaceUp(before));
        }

        private bool IsControllerNearSurface(CharacterController controller, Vector3 groundPoint, Vector3 groundUp)
        {
            Vector3 up = groundUp.sqrMagnitude > 0.0001f ? groundUp.normalized : Vector3.up;
            float rootHeight = Vector3.Dot(transform.position - groundPoint, up);
            float worldSkinWidth = controller.transform.TransformVector(Vector3.up * controller.skinWidth).magnitude;
            return rootHeight <= ControllerRootClearance(controller, transform.position, up) +
                                 groundedSnapDistance + worldSkinWidth;
        }

        private void SnapControllerRootToSurface(CharacterController controller, Vector3 groundPoint, Vector3 groundUp)
        {
            Vector3 up = groundUp.sqrMagnitude > 0.0001f ? groundUp.normalized : Vector3.up;
            float rootHeight = Vector3.Dot(transform.position - groundPoint, up);
            float requiredClearance = ControllerRootClearance(controller, transform.position, up);
            transform.position += up * (requiredClearance - rootHeight);
        }

        /// <summary>
        /// Returns the lift the actor root needs to keep the controller's authored lower support
        /// point outside the ground. Some imported walkers have capsule bottoms below their root;
        /// snapping those roots directly onto the terrain embeds the capsule and collision-locks
        /// tangent motion. Controllers whose bottom is above the root retain the authored root as
        /// the visual ground contact (important for Barbara's mech stage).
        /// </summary>
        public static float ControllerRootClearance(CharacterController controller, Vector3 actorRoot, Vector3 surfaceUp)
        {
            if (controller == null) return 0f;
            Vector3 up = surfaceUp.sqrMagnitude > 0.0001f ? surfaceUp.normalized : Vector3.up;
            float halfHeight = Mathf.Max(controller.radius, controller.height * 0.5f);
            Vector3 lowerSupport = controller.transform.TransformPoint(
                controller.center - Vector3.up * halfHeight);
            return Mathf.Max(0f, -Vector3.Dot(lowerSupport - actorRoot, up));
        }

        /// <summary>World center of the controller's lower capsule sphere, used to probe low props.</summary>
        public static Vector3 ControllerLowerSphereCenter(CharacterController controller)
        {
            if (controller == null) return Vector3.zero;
            float halfHeight = Mathf.Max(controller.radius, controller.height * 0.5f);
            float centerOffset = Mathf.Max(0f, halfHeight - controller.radius);
            return controller.transform.TransformPoint(
                controller.center - Vector3.up * centerOffset);
        }

        /// <summary>
        /// Returns the direction an actor visibly translated after navigation, collision sliding,
        /// and radial grounding. Facing the pre-navigation request causes forward-only walk clips
        /// to moonwalk whenever obstacle recovery steers to the side.
        /// </summary>
        public static Vector3 ActualTangentDisplacement(Vector3 before, Vector3 after, Vector3 surfaceUp)
        {
            Vector3 up = surfaceUp.sqrMagnitude > 0.0001f ? surfaceUp.normalized : Vector3.up;
            Vector3 displacement = Vector3.ProjectOnPlane(after - before, up);
            return displacement.sqrMagnitude > 0.000001f ? displacement.normalized : Vector3.zero;
        }

        private Vector3 ResolveNavigationDirection(Vector3 desiredDirection, Vector3 probeOrigin, float probeRadius)
        {
            Vector3 up = SurfaceUp(transform.position);
            Vector3 desired = Vector3.ProjectOnPlane(desiredDirection, up);
            if (desired.sqrMagnitude < 0.0001f) return Vector3.zero;
            desired.Normalize();

            if (Time.time < _escapeUntil)
            {
                desired = (desired * 0.35f + _escapeDirection * 1.15f).normalized;
            }

            bool directBlocked = ProbeObstacle(desired, probeOrigin, probeRadius);
            if (!directBlocked)
            {
                _detourUntil = 0f;
                return desired;
            }

            if (Time.time < _detourUntil)
            {
                Vector3 activeDetour = BuildDetourDirection(desired, up, _detourSide);
                if (!ProbeObstacle(activeDetour, probeOrigin, probeRadius)) return activeDetour;
            }

            Vector3 left = BuildDetourDirection(desired, up, 1);
            Vector3 right = BuildDetourDirection(desired, up, -1);
            bool leftClear = !ProbeObstacle(left, probeOrigin, probeRadius);
            bool rightClear = !ProbeObstacle(right, probeOrigin, probeRadius);
            _detourSide = ChooseDetourSide(leftClear, rightClear, _detourSide == 0 ? 1 : _detourSide);
            _detourUntil = Time.time + stuckDetourDuration;
            return BuildDetourDirection(desired, up, _detourSide);
        }

        private bool ProbeObstacle(Vector3 direction, Vector3 origin, float radius)
        {
            if (direction.sqrMagnitude < 0.0001f) return false;
            int hitCount = Physics.SphereCastNonAlloc(
                origin, radius, direction, _navigationHits, navigationProbeDistance,
                navigationObstacleMask, QueryTriggerInteraction.Ignore);
            for (int index = 0; index < hitCount; index++)
            {
                Collider hitCollider = _navigationHits[index].collider;
                if (!IsStaticNavigationObstacle(hitCollider, planetGround, transform, player)) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Static terrain avoidance excludes the destination and dynamic enemies. Their physical
        /// controllers still slide/collide normally, while rocks and walls continue to detour.
        /// </summary>
        public static bool IsStaticNavigationObstacle(
            Collider candidate, Collider ground, Transform actor, Transform target)
        {
            if (candidate == null || candidate == ground) return false;
            Transform hit = candidate.transform;
            if (actor != null && (hit == actor || hit.IsChildOf(actor))) return false;
            if (target != null && (hit == target || hit.IsChildOf(target))) return false;
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            return enemyLayer < 0 || candidate.gameObject.layer != enemyLayer;
        }

        private void TrackNavigationProgress(Vector3 direction, Vector3 before, Vector3 after, float requestedSpeed)
        {
            if (requestedSpeed <= 0.01f || direction.sqrMagnitude < 0.0001f)
            {
                _stuckFor = 0f;
                return;
            }

            Vector3 displacement = Vector3.ProjectOnPlane(after - before, SurfaceUp(before));
            float forwardProgress = Vector3.Dot(displacement, direction.normalized);
            if (forwardProgress > requestedSpeed * Time.deltaTime * 0.12f)
            {
                _stuckFor = 0f;
                return;
            }

            _stuckFor += Time.deltaTime;
            if (_stuckFor < stuckRecoveryDelay) return;

            _stuckFor = 0f;
            _detourSide = _detourSide == 0 ? 1 : -_detourSide;
            _detourUntil = Time.time + stuckDetourDuration;
        }

        protected void MaintainPassiveHover(float hoverHeight)
        {
            if (!IsPlanetSurfaceRuntime)
            {
                Vector3 position = transform.position;
                position.y = hoverHeight;
                transform.position = position;
                return;
            }
            if (TryGetSurfacePoint(transform.position, out Vector3 groundPoint, out Vector3 up))
            {
                transform.position = groundPoint + up * hoverHeight;
            }
        }

        protected bool TryGetSurfacePoint(Vector3 nearPosition, out Vector3 point, out Vector3 up)
        {
            up = SurfaceUp(nearPosition);
            if (!IsPlanetSurfaceRuntime)
            {
                point = new Vector3(nearPosition.x, 0f, nearPosition.z);
                return true;
            }

            Vector3 origin = nearPosition + up * surfaceProbeDistance;
            Ray ray = new Ray(origin, -up);
            if (planetGround.Raycast(ray, out RaycastHit hit, surfaceProbeDistance * 2f))
            {
                point = hit.point;
                return true;
            }

            point = nearPosition;
            return false;
        }

        private bool IsAboveSurface()
        {
            if (!TryGetSurfacePoint(transform.position, out Vector3 point, out Vector3 up)) return false;
            return IsPlanetSurfaceRuntime
                ? Vector3.Dot(transform.position - point, up) > 0.001f
                : transform.position.y > 0.001f;
        }

        // Shared by every melee-capable subclass (EnemySmallAI/EnemyLargeAI) so the imported-
        // prefab instantiate/fix/destroy boilerplate isn't duplicated in each attack routine.
        // point should be an upward-offset hit point (e.g. player.position + Vector3.up), not the
        // bare feet-level position ApplyDamage is called with, so the effect reads as landing on
        // the body.
        protected void SpawnMeleeHitVfx(Vector3 point)
        {
            if (meleeHitVfxPrefab == null) return;

            var effect = Instantiate(meleeHitVfxPrefab, point, Quaternion.identity);
            effect.transform.localScale = Vector3.one * meleeHitVfxScale;
            ImportedVfxUtility.FixUrpMaterials(effect);
            ImportedVfxUtility.ForceHierarchyParticleScaling(effect);
            Destroy(effect, 2f);
        }

        // Refreshes duration on reapply rather than stacking - a sustained burst of slowing
        // shots should just keep the debuff topped up, not compound it below the intended floor.
        public void ApplySlow(float multiplier, float duration)
        {
            if (isDead) return;

            SpeedMultiplier = Mathf.Clamp01(multiplier);
            if (_slowRoutine != null) StopCoroutine(_slowRoutine);
            _slowRoutine = StartCoroutine(SlowRoutine(duration));

            if (_slowVfxInstance == null && slowVfxPrefab != null)
            {
                _slowVfxInstance = Instantiate(slowVfxPrefab, transform);
                _slowVfxInstance.transform.localPosition = Vector3.zero;
                _slowVfxInstance.transform.localScale = Vector3.one * slowVfxScale;
                ImportedVfxUtility.FixUrpMaterials(_slowVfxInstance);
                ImportedVfxUtility.ForceHierarchyParticleScaling(_slowVfxInstance);
            }
        }

        private IEnumerator SlowRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            SpeedMultiplier = 1f;
            _slowRoutine = null;
            if (_slowVfxInstance != null)
            {
                Destroy(_slowVfxInstance);
                _slowVfxInstance = null;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Player
{
    [RequireComponent(typeof(RadialCapsuleMotor))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 6.5f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float rotationDegreesPerSecond = 540f;

        [Header("Planet")]
        [Tooltip("Center used for radial gravity and orientation. Falls back to a scene object named " +
                 "'Planet Ground', then world up for flat test scenes.")]
        [SerializeField] private Transform planetCenter;
        [Tooltip("Maximum tilt from center-radial gravity toward the sampled terrain normal. " +
                 "This keeps near-vertical terrain from becoming an excessive sideways pull.")]
        [SerializeField, Range(0f, 89f)] private float maxSurfaceGravityAngle = 35f;
        [Tooltip("How quickly gravity follows changes in the sampled terrain normal. The body " +
                 "and camera remain aligned to stable center-radial up.")]
        [SerializeField, Min(0f)] private float surfaceGravityDegreesPerSecond = 120f;
        [SerializeField, Min(0f)] private float radialAlignmentDegreesPerSecond = 360f;

        [Header("Jump / Gravity")]
        [SerializeField] private float jumpHeight = 1.4f;
        [Tooltip("Positive acceleration toward the authored planet surface.")]
        [FormerlySerializedAs("gravity")]
        [SerializeField, Min(0.01f)] private float gravityAcceleration = 6.5f;
        [SerializeField, Min(0.01f)] private float terminalFallSpeed = 30f;
        [Tooltip("Positive speed toward the surface used to maintain contact with uneven ground.")]
        [FormerlySerializedAs("groundedStickForce")]
        [SerializeField, Min(0f)] private float groundedStickSpeed = 2f;

        [Header("Grounding")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("How far below the controller's feet to search for walkable ground.")]
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.35f;
        [SerializeField, Min(0f)] private float groundProbeStartOffset = 0.1f;
        [SerializeField, Range(0f, 89f)] private float maxGroundAngle = 45f;
        [Tooltip("Radius of the broad foot probe used to preserve grounding across slopes and steps. " +
                 "Only the center ray is allowed to steer surface gravity.")]
        [SerializeField, Range(0.1f, 1f)] private float groundProbeRadiusScale = 0.85f;

        [Header("Spawn Grounding")]
        [SerializeField] private bool snapToGroundOnStart = true;
        [Tooltip("Moves the spawn ray's origin outward before casting back toward the center.")]
        [SerializeField, Min(0f)] private float spawnRaycastOutwardOffset = 10f;
        [SerializeField, Min(0.1f)] private float spawnRaycastDistance = 100f;
        [SerializeField, Min(0f)] private float spawnSurfaceOffset = 0.02f;

        [Header("Camera")]
        [Tooltip("Forward from this transform is projected onto the local planet tangent. Defaults to Camera.main.")]
        [SerializeField] private Transform cameraReference;

        [Header("Stagger")]
        [SerializeField] private Animator animator;

        private static readonly int StaggerParam = Animator.StringToHash("Stagger");
        private const float DirectionEpsilon = 0.0001f;
        private const int HitBufferSize = 16;

        private Rigidbody _body;
        private CapsuleCollider _capsule;
        private RadialCapsuleMotor _motor;
        private InputSystem_Actions _actions;
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[HitBufferSize];
        private readonly Dictionary<object, float> _movementSpeedModifiers =
            new Dictionary<object, float>();
        private float _radialSpeed;
        private Vector3 _surfaceUp = Vector3.up;
        private bool _surfaceUpInitialized;
        private bool _jumpQueued;
        private bool _searchedForPlanetGround;
        private float _currentSpeed;
        private float _staggerLockedUntil = -1f;
        private float _duckClipLength = -1f;

        public float NormalizedSpeed { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool JumpTriggeredThisFrame { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public float GroundClearance { get; private set; }
        public bool IsStaggered => Time.time < _staggerLockedUntil;
        public float CurrentHorizontalSpeed => _currentSpeed;
        public float MovementSpeedMultiplier { get; private set; } = 1f;
        public float EffectiveWalkSpeed => Mathf.Max(0f, walkSpeed) * MovementSpeedMultiplier;
        public float EffectiveSprintSpeed => Mathf.Max(0f, sprintSpeed) * MovementSpeedMultiplier;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();
            _motor = GetComponent<RadialCapsuleMotor>();
            _actions = new InputSystem_Actions();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            // Looked up once so Stagger() can guarantee the lock covers the whole clip
            // ("the entire duck animation must play") regardless of whatever duration a caller
            // (e.g. BossMechAI's ground-slam) happens to pass in.
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (clip != null && clip.name.IndexOf("Duck", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _duckClipLength = clip.length;
                        break;
                    }
                }
            }
        }

        private void OnEnable()
        {
            _actions.Player.Enable();
            _actions.Player.Jump.performed += OnJumpPerformed;
        }

        private void OnDisable()
        {
            _actions.Player.Jump.performed -= OnJumpPerformed;
            _actions.Player.Disable();
        }

        private void Start()
        {
            if (cameraReference == null && Camera.main != null)
            {
                cameraReference = Camera.main.transform;
            }

            Vector3 spawnCastUp = TryResolvePlanetCenter()
                ? GetRadialUpDirection()
                : Vector3.up;
            AlignUpImmediately(spawnCastUp);

            if (snapToGroundOnStart && !SnapToGround(spawnCastUp))
            {
                Debug.LogWarning(
                    $"{nameof(PlayerController)} on '{name}' could not find ground below its spawn point.",
                    this);
            }

            Vector3 radialUp = GetPlanetUpDirection();
            AlignUpImmediately(radialUp);
            Vector3 localUp = transform.up.normalized;
            IsGrounded = ProbeGround(
                localUp,
                false,
                out Vector3 supportUp,
                out float supportClearance,
                out bool hasDirectSupport);
            UpdateGroundSupport(IsGrounded, supportUp, supportClearance);
            SetSurfaceUpImmediately(IsGrounded && hasDirectSupport
                ? GetSurfaceUpDirection(radialUp, supportUp)
                : radialUp);
            if (IsGrounded)
            {
                _radialSpeed = -Mathf.Abs(groundedStickSpeed);
            }
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            _jumpQueued = true;
        }

        public void SetMovementSpeedModifier(object source, float multiplier)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(multiplier),
                    multiplier,
                    "Movement speed multipliers must be finite and non-negative.");
            }

            _movementSpeedModifiers[source] = multiplier;
            RecalculateMovementSpeedMultiplier();
        }

        public void RemoveMovementSpeedModifier(object source)
        {
            if (source != null && _movementSpeedModifiers.Remove(source))
            {
                RecalculateMovementSpeedMultiplier();
            }
        }

        private void RecalculateMovementSpeedMultiplier()
        {
            float previousMultiplier = MovementSpeedMultiplier;
            float resolvedMultiplier = 1f;
            foreach (float multiplier in _movementSpeedModifiers.Values)
            {
                resolvedMultiplier *= multiplier;
            }

            MovementSpeedMultiplier = resolvedMultiplier;
            if (resolvedMultiplier < previousMultiplier)
            {
                _currentSpeed = previousMultiplier > 0f
                    ? _currentSpeed * (resolvedMultiplier / previousMultiplier)
                    : 0f;
            }
        }

        // Used by boss attacks (e.g. BossMechAI's ground-slam landing) to force the Duck
        // animation and lock movement/jump input for its duration - "player cannot move until
        // the duck animation is done." Doesn't disable this component outright (that would also
        // stop gravity/camera-facing from running), just gates input for the locked window below.
        public void Stagger(float duration)
        {
            float lockedDuration = _duckClipLength > 0f ? Mathf.Max(duration, _duckClipLength) : duration;
            _staggerLockedUntil = Time.time + lockedDuration;
            _jumpQueued = false;
            if (animator != null) animator.SetTrigger(StaggerParam);
        }

        private void FixedUpdate()
        {
            Vector3 radialUp = GetPlanetUpDirection();
            Quaternion bodyRotation = GetPlanetAlignedRotation(_body.rotation, radialUp);
            Vector3 localUp = bodyRotation * Vector3.up;

            Vector3 supportUp = localUp;
            float supportClearance = 0f;
            bool hasDirectSupport = false;
            IsGrounded = _radialSpeed <= 0f &&
                         ProbeGround(
                             localUp,
                             _motor.HadBottomContact,
                             out supportUp,
                             out supportClearance,
                             out hasDirectSupport);
            if (IsGrounded && hasDirectSupport)
            {
                UpdateSurfaceUp(GetSurfaceUpDirection(radialUp, supportUp));
            }
            else if (IsGrounded)
            {
                // Broad support bridges a stair lip, but must never preserve a previous
                // surface-normal pull when the center of the player is unsupported.
                SetSurfaceUpImmediately(radialUp);
            }
            else if (_radialSpeed <= 0f)
            {
                // Walking off an edge or entering a hole must immediately release surface
                // adhesion. Radial down then carries the player toward the floor, not a wall.
                SetSurfaceUpImmediately(radialUp);
            }
            else
            {
                // Preserve a slope-normal jump at takeoff, then smoothly return its trajectory
                // to radial while the player is still rising.
                UpdateSurfaceUp(radialUp);
            }

            bool staggered = IsStaggered;
            Vector2 moveInput = staggered ? Vector2.zero : _actions.Player.Move.ReadValue<Vector2>();
            bool sprinting = !staggered && _actions.Player.Sprint.IsPressed();
            if (staggered) _jumpQueued = false;

            if (cameraReference == null && Camera.main != null)
            {
                cameraReference = Camera.main.transform;
            }

            bodyRotation = GetCameraFacingRotation(bodyRotation, localUp);

            // Facing is locked to the camera, so movement remains camera-relative and supports
            // forward/backpedal/strafe without turning the body away from the aim direction.
            Vector3 moveDirection = Vector3.ProjectOnPlane(
                CameraRelativeDirection(moveInput, localUp),
                _surfaceUp);
            if (moveDirection.sqrMagnitude >= DirectionEpsilon)
            {
                moveDirection.Normalize();
            }

            float maxSpeed = sprinting ? EffectiveSprintSpeed : EffectiveWalkSpeed;
            float targetHorizontalSpeed = maxSpeed * Mathf.Clamp01(moveInput.magnitude);
            _currentSpeed = Mathf.MoveTowards(
                _currentSpeed,
                targetHorizontalSpeed,
                acceleration * Time.fixedDeltaTime);

            Vector3 tangentMotion = moveDirection * _currentSpeed;
            NormalizedSpeed = Mathf.Clamp01(
                _currentSpeed / Mathf.Max(0.01f, EffectiveSprintSpeed));

            ApplyGravityAndJump();
            if (!IsGrounded && _radialSpeed <= 0f)
            {
                // The apex can be crossed during gravity integration. Switch before Move so
                // even the first falling frame heads toward the hole floor rather than a wall.
                SetSurfaceUpImmediately(radialUp);
            }

            Vector3 gravityMotion = _surfaceUp * _radialSpeed;
            Vector3 displacement = (tangentMotion + gravityMotion) * Time.fixedDeltaTime;
            _motor.Move(displacement, bodyRotation, localUp, IsGrounded);
            UpdateGroundSupport(IsGrounded, supportUp, supportClearance);
            if (IsGrounded && _radialSpeed < 0f)
            {
                _radialSpeed = -Mathf.Abs(groundedStickSpeed);
                if (!hasDirectSupport)
                {
                    SetSurfaceUpImmediately(GetPlanetUpDirection());
                }
            }
            else if (_radialSpeed <= 0f)
            {
                SetSurfaceUpImmediately(GetPlanetUpDirection());
            }
        }

        private void LateUpdate()
        {
            // Animation consumes this render-frame latch in Update. Resetting it in FixedUpdate
            // can lose a jump when two physics ticks occur before the next rendered frame.
            JumpTriggeredThisFrame = false;
        }

        private void ApplyGravityAndJump()
        {
            if (IsGrounded)
            {
                _radialSpeed = -Mathf.Abs(groundedStickSpeed);

                if (_jumpQueued)
                {
                    float gravity = Mathf.Max(0.01f, Mathf.Abs(gravityAcceleration));
                    _radialSpeed = Mathf.Sqrt(2f * gravity * Mathf.Max(0f, jumpHeight));
                    JumpTriggeredThisFrame = true;
                    IsGrounded = false;
                }
            }
            else
            {
                float fallSpeed = Mathf.Max(0.01f, Mathf.Abs(terminalFallSpeed));
                _radialSpeed = Mathf.Max(
                    _radialSpeed - Mathf.Max(0.01f, Mathf.Abs(gravityAcceleration)) *
                    Time.fixedDeltaTime,
                    -fallSpeed);
            }

            _jumpQueued = false;
        }

        private Vector3 CameraRelativeDirection(Vector2 input, Vector3 radialUp)
        {
            if (input.sqrMagnitude < DirectionEpsilon)
            {
                return Vector3.zero;
            }

            Vector3 referenceForward = cameraReference != null
                ? cameraReference.forward
                : transform.forward;
            Vector3 forward = Vector3.ProjectOnPlane(referenceForward, radialUp);

            if (forward.sqrMagnitude < DirectionEpsilon && cameraReference != null)
            {
                forward = Vector3.ProjectOnPlane(cameraReference.up, radialUp);
            }

            if (forward.sqrMagnitude < DirectionEpsilon)
            {
                forward = GetFallbackTangent(radialUp);
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(radialUp, forward).normalized;
            return (forward * input.y + right * input.x).normalized;
        }

        private Quaternion GetPlanetAlignedRotation(Quaternion currentRotation, Vector3 targetUp)
        {
            Quaternion upAlignedRotation =
                Quaternion.FromToRotation(currentRotation * Vector3.up, targetUp) * currentRotation;
            return Quaternion.RotateTowards(
                currentRotation,
                upAlignedRotation,
                Mathf.Max(0f, radialAlignmentDegreesPerSecond) * Time.fixedDeltaTime);
        }

        // Character yaw always tracks the camera's yaw projected onto the planet tangent,
        // whether or not there is movement input. The returned rotation is consumed by the
        // radial motor in the same fixed physics step as translation.
        private Quaternion GetCameraFacingRotation(Quaternion currentRotation, Vector3 localUp)
        {
            if (cameraReference == null)
            {
                return currentRotation;
            }

            Vector3 cameraForward = Vector3.ProjectOnPlane(cameraReference.forward, localUp);
            if (cameraForward.sqrMagnitude < DirectionEpsilon)
            {
                return currentRotation;
            }

            Quaternion targetRotation = Quaternion.LookRotation(cameraForward.normalized, localUp);
            return Quaternion.RotateTowards(
                currentRotation,
                targetRotation,
                Mathf.Max(0f, rotationDegreesPerSecond) * Time.fixedDeltaTime);
        }

        private void AlignUpImmediately(Vector3 radialUp)
        {
            Quaternion alignedRotation =
                Quaternion.FromToRotation(transform.up, radialUp) * transform.rotation;
            transform.rotation = alignedRotation;
            if (_body != null)
            {
                _body.rotation = alignedRotation;
            }
        }

        private Vector3 GetPlanetUpDirection()
        {
            return TryResolvePlanetCenter()
                ? GetRadialUpDirection()
                : Vector3.up;
        }

        private Vector3 GetSurfaceUpDirection(Vector3 radialUp, Vector3 supportUp)
        {
            if (Vector3.Dot(supportUp, radialUp) <= 0f)
            {
                return radialUp;
            }

            // Terrain normals shape local gravity without allowing near-vertical features to
            // become an excessive sideways pull.
            float maxTiltRadians = Mathf.Clamp(maxSurfaceGravityAngle, 0f, 89f) * Mathf.Deg2Rad;
            return Vector3.RotateTowards(
                    radialUp,
                    supportUp.normalized,
                    maxTiltRadians,
                    0f)
                .normalized;
        }

        private void SetSurfaceUpImmediately(Vector3 surfaceUp)
        {
            _surfaceUp = surfaceUp.normalized;
            _surfaceUpInitialized = true;
        }

        private void UpdateSurfaceUp(Vector3 targetSurfaceUp)
        {
            if (!_surfaceUpInitialized)
            {
                SetSurfaceUpImmediately(targetSurfaceUp);
                return;
            }

            float maxRadiansDelta = Mathf.Max(0f, surfaceGravityDegreesPerSecond) *
                                    Mathf.Deg2Rad * Time.fixedDeltaTime;
            _surfaceUp = Vector3.RotateTowards(
                    _surfaceUp,
                    targetSurfaceUp,
                    maxRadiansDelta,
                    0f)
                .normalized;
        }

        private Vector3 GetRadialUpDirection()
        {
            Vector3 centerToPlayer = transform.position - planetCenter.position;
            return centerToPlayer.sqrMagnitude >= DirectionEpsilon
                ? centerToPlayer.normalized
                : Vector3.up;
        }

        private bool TryResolvePlanetCenter()
        {
            if (planetCenter != null)
            {
                return true;
            }

            if (_searchedForPlanetGround)
            {
                return false;
            }

            _searchedForPlanetGround = true;
            GameObject activePlanetGround = GameObject.Find("Planet Ground");
            if (activePlanetGround != null)
            {
                planetCenter = activePlanetGround.transform;
                return true;
            }

            Transform[] sceneTransforms = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (Transform candidate in sceneTransforms)
            {
                if (candidate.name == "Planet Ground")
                {
                    planetCenter = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool ProbeGround(
            Vector3 localUp,
            bool hasBottomContact,
            out Vector3 supportUp,
            out float supportClearance,
            out bool hasDirectSupport)
        {
            supportUp = localUp;
            supportClearance = 0f;
            hasDirectSupport = false;
            Vector3 bottomPoint = GetControllerBottomPoint();
            float startOffset = Mathf.Max(0f, groundProbeStartOffset);
            float castDistance = startOffset + Mathf.Max(0.01f, groundProbeDistance);

            // Only a centerline hit is allowed to steer surface gravity. A lateral hole wall
            // cannot intersect this ray, so losing the floor always releases wall adhesion.
            int centerHitCount = Physics.RaycastNonAlloc(
                bottomPoint + localUp * startOffset,
                -localUp,
                _hitBuffer,
                castDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

            if (TryGetNearestWalkableHit(centerHitCount, localUp, out RaycastHit centerHit))
            {
                supportUp = centerHit.normal.normalized;
                supportClearance = centerHit.distance - startOffset;
                hasDirectSupport = true;
                return true;
            }

            // A center ray can briefly miss at lips or between uneven triangles. A broad foot
            // cast preserves grounding, but the walkable-normal filter rejects side walls and
            // the fallback never steers surface gravity sideways.
            if (!hasBottomContact)
            {
                return false;
            }

            float probeRadius = Mathf.Max(
                0.01f,
                GetControllerWorldRadius() * Mathf.Clamp(groundProbeRadiusScale, 0.1f, 1f));
            Vector3 sphereOrigin = bottomPoint + localUp * (probeRadius + startOffset);
            int footHitCount = Physics.SphereCastNonAlloc(
                sphereOrigin,
                probeRadius,
                -localUp,
                _hitBuffer,
                castDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

            if (!TryGetNearestWalkableHit(footHitCount, localUp, out RaycastHit footHit))
            {
                return false;
            }

            supportClearance = footHit.distance - startOffset;
            return true;
        }

        private void UpdateGroundSupport(
            bool hasSupport,
            Vector3 supportUp,
            float supportClearance)
        {
            GroundNormal = hasSupport ? supportUp.normalized : GetPlanetUpDirection();
            GroundClearance = hasSupport ? supportClearance : 0f;
        }

        private bool TryGetNearestWalkableHit(
            int hitCount,
            Vector3 localUp,
            out RaycastHit nearestHit)
        {
            float minimumGroundDot = Mathf.Cos(
                Mathf.Clamp(maxGroundAngle, 0f, 89f) * Mathf.Deg2Rad);
            bool foundGround = false;
            float nearestDistance = float.PositiveInfinity;
            nearestHit = default;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                if (IsOwnCollider(hit.collider) ||
                    Vector3.Dot(hit.normal, localUp) < minimumGroundDot ||
                    hit.distance >= nearestDistance)
                {
                    continue;
                }

                foundGround = true;
                nearestDistance = hit.distance;
                nearestHit = hit;
            }

            return foundGround;
        }

        private bool SnapToGround(Vector3 radialUp)
        {
            float outwardOffset = Mathf.Max(0f, spawnRaycastOutwardOffset);
            Vector3 rayOrigin = transform.position + radialUp * outwardOffset;
            float rayDistance = Mathf.Max(0.1f, spawnRaycastDistance);
            int hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                -radialUp,
                _hitBuffer,
                rayDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

            bool foundGround = false;
            float nearestDistance = float.PositiveInfinity;
            RaycastHit nearestHit = default;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                if (IsOwnCollider(hit.collider) || Vector3.Dot(hit.normal, radialUp) <= 0f)
                {
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    foundGround = true;
                    nearestDistance = hit.distance;
                    nearestHit = hit;
                }
            }

            if (!foundGround)
            {
                return false;
            }

            Vector3 currentBottomPoint = GetControllerBottomPoint();
            Vector3 desiredBottomPoint = nearestHit.point + radialUp * Mathf.Max(0f, spawnSurfaceOffset);
            Vector3 snappedPosition = transform.position + desiredBottomPoint - currentBottomPoint;

            transform.position = snappedPosition;
            _body.position = snappedPosition;
            Physics.SyncTransforms();
            return true;
        }

        private Vector3 GetControllerBottomPoint()
        {
            return _motor.GetBottomPoint(transform.position, transform.rotation);
        }

        private float GetControllerWorldRadius()
        {
            Vector3 scale = transform.lossyScale;
            float lateralScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            return Mathf.Max(0.01f, _capsule.radius * lateralScale);
        }

        private bool IsOwnCollider(Collider candidate)
        {
            if (candidate == null)
            {
                return true;
            }

            Transform candidateTransform = candidate.transform;
            return candidate == _capsule ||
                   candidateTransform == transform ||
                   candidateTransform.IsChildOf(transform);
        }

        private static Vector3 GetFallbackTangent(Vector3 radialUp)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(Vector3.forward, radialUp);
            if (tangent.sqrMagnitude < DirectionEpsilon)
            {
                tangent = Vector3.ProjectOnPlane(Vector3.right, radialUp);
            }

            return tangent.normalized;
        }
    }
}

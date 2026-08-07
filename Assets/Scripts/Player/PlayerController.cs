using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Player
{
    [RequireComponent(typeof(CharacterController))]
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
        [SerializeField, Min(0f)] private float radialAlignmentDegreesPerSecond = 360f;

        [Header("Jump / Gravity")]
        [SerializeField] private float jumpHeight = 1.4f;
        [Tooltip("Positive acceleration toward the planet center.")]
        [FormerlySerializedAs("gravity")]
        [SerializeField, Min(0.01f)] private float gravityAcceleration = 6.5f;
        [Tooltip("Positive inward speed used to keep the controller in contact with uneven ground.")]
        [FormerlySerializedAs("groundedStickForce")]
        [SerializeField, Min(0f)] private float groundedStickSpeed = 2f;

        [Header("Grounding")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("How far below the controller's feet to search for walkable ground.")]
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.35f;
        [SerializeField, Min(0f)] private float groundProbeStartOffset = 0.1f;
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

        private const float DirectionEpsilon = 0.0001f;
        private const int HitBufferSize = 16;

        private CharacterController _controller;
        private InputSystem_Actions _actions;
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[HitBufferSize];
        private float _radialSpeed;
        private bool _jumpQueued;
        private bool _searchedForPlanetGround;
        private float _currentSpeed;

        public float NormalizedSpeed { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool JumpTriggeredThisFrame { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _actions = new InputSystem_Actions();
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

            Vector3 radialUp = GetUpDirection();
            AlignUpImmediately(radialUp);

            if (snapToGroundOnStart && !SnapToGround(radialUp))
            {
                Debug.LogWarning(
                    $"{nameof(PlayerController)} on '{name}' could not find ground below its spawn point.",
                    this);
            }

            radialUp = GetUpDirection();
            IsGrounded = ProbeGround(radialUp);
            if (IsGrounded)
            {
                _radialSpeed = -Mathf.Abs(groundedStickSpeed);
            }
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            _jumpQueued = true;
        }

        private void Update()
        {
            JumpTriggeredThisFrame = false;

            Vector3 radialUp = GetUpDirection();

            if (cameraReference == null && Camera.main != null)
            {
                cameraReference = Camera.main.transform;
            }

            Vector2 moveInput = _actions.Player.Move.ReadValue<Vector2>();
            bool sprinting = _actions.Player.Sprint.IsPressed();

            Vector3 moveDirection = CameraRelativeDirection(moveInput, radialUp);
            AlignToPlanet(radialUp, moveDirection);

            float maxSpeed = sprinting ? sprintSpeed : walkSpeed;
            float targetHorizontalSpeed = maxSpeed * Mathf.Clamp01(moveInput.magnitude);
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetHorizontalSpeed, acceleration * Time.deltaTime);

            Vector3 tangentMotion = moveDirection * _currentSpeed;
            NormalizedSpeed = Mathf.Clamp01(_currentSpeed / Mathf.Max(0.01f, sprintSpeed));

            IsGrounded = _radialSpeed <= 0f && ProbeGround(radialUp);
            ApplyGravityAndJump();

            Vector3 radialMotion = radialUp * _radialSpeed;
            Vector3 motion = (tangentMotion + radialMotion) * Time.deltaTime;
            _controller.Move(motion);

            radialUp = GetUpDirection();
            IsGrounded = _radialSpeed <= 0f && ProbeGround(radialUp);
            if (IsGrounded && _radialSpeed < 0f)
            {
                _radialSpeed = -Mathf.Abs(groundedStickSpeed);
            }
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
                _radialSpeed -= Mathf.Max(0.01f, Mathf.Abs(gravityAcceleration)) * Time.deltaTime;
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

        private void AlignToPlanet(Vector3 radialUp, Vector3 moveDirection)
        {
            Quaternion upAlignedRotation =
                Quaternion.FromToRotation(transform.up, radialUp) * transform.rotation;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                upAlignedRotation,
                Mathf.Max(0f, radialAlignmentDegreesPerSecond) * Time.deltaTime);

            if (moveDirection.sqrMagnitude < DirectionEpsilon)
            {
                return;
            }

            Vector3 currentForward = Vector3.ProjectOnPlane(transform.forward, radialUp);
            if (currentForward.sqrMagnitude < DirectionEpsilon)
            {
                currentForward = GetFallbackTangent(radialUp);
            }

            float angle = Vector3.SignedAngle(currentForward, moveDirection, radialUp);
            float turn = Mathf.Clamp(
                angle,
                -Mathf.Max(0f, rotationDegreesPerSecond) * Time.deltaTime,
                Mathf.Max(0f, rotationDegreesPerSecond) * Time.deltaTime);
            transform.rotation = Quaternion.AngleAxis(turn, radialUp) * transform.rotation;
        }

        private void AlignUpImmediately(Vector3 radialUp)
        {
            transform.rotation =
                Quaternion.FromToRotation(transform.up, radialUp) * transform.rotation;
        }

        private Vector3 GetUpDirection()
        {
            if (!TryResolvePlanetCenter())
            {
                return Vector3.up;
            }

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

            Transform[] sceneTransforms = Object.FindObjectsByType<Transform>(
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

        private bool ProbeGround(Vector3 radialUp)
        {
            float worldRadius = GetControllerWorldRadius();
            float probeRadius = Mathf.Max(0.01f, worldRadius * groundProbeRadiusScale);
            Vector3 bottomPoint = GetControllerBottomPoint(radialUp);
            float startOffset = Mathf.Max(0f, groundProbeStartOffset);
            Vector3 origin = bottomPoint + radialUp * (probeRadius + startOffset);
            float castDistance = startOffset + Mathf.Max(0.01f, groundProbeDistance);

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                probeRadius,
                -radialUp,
                _hitBuffer,
                castDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

            float minimumGroundDot = Mathf.Cos(_controller.slopeLimit * Mathf.Deg2Rad);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                if (IsOwnCollider(hit.collider))
                {
                    continue;
                }

                if (Vector3.Dot(hit.normal, radialUp) >= minimumGroundDot)
                {
                    return true;
                }
            }

            return false;
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

            Vector3 currentBottomPoint = GetControllerBottomPoint(radialUp);
            Vector3 desiredBottomPoint = nearestHit.point + radialUp * Mathf.Max(0f, spawnSurfaceOffset);
            Vector3 snappedPosition = transform.position + desiredBottomPoint - currentBottomPoint;

            bool controllerWasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.position = snappedPosition;
            _controller.enabled = controllerWasEnabled;
            Physics.SyncTransforms();
            return true;
        }

        private Vector3 GetControllerBottomPoint(Vector3 radialUp)
        {
            float worldHeight = Mathf.Max(
                GetControllerWorldRadius() * 2f,
                _controller.height * Mathf.Abs(transform.lossyScale.y));
            return transform.TransformPoint(_controller.center) - radialUp * (worldHeight * 0.5f);
        }

        private float GetControllerWorldRadius()
        {
            Vector3 scale = transform.lossyScale;
            float lateralScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            return Mathf.Max(0.01f, _controller.radius * lateralScale);
        }

        private bool IsOwnCollider(Collider candidate)
        {
            if (candidate == null)
            {
                return true;
            }

            Transform candidateTransform = candidate.transform;
            return candidate == _controller ||
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

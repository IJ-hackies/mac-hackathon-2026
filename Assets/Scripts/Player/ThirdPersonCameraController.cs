using UnityEngine;

namespace Player
{
    public class ThirdPersonCameraController : MonoBehaviour
    {
        public const float MinimumMouseSensitivity = 0.01f;
        public const float MaximumMouseSensitivity = 0.3f;

        [Header("Target")]
        [SerializeField] private Transform target;
        [Tooltip("Camera height above the player. This sits well above head height on purpose: " +
                 "if the camera were at head height it would always be looking almost level " +
                 "into the back of the character's head, keeping them pinned near screen center " +
                 "no matter how you pitch. Elevating the rig pushes the character into the lower " +
                 "part of the frame and opens up the space around the (fixed, center-screen) " +
                 "crosshair. The offset is target-local, so its Y axis follows the player's " +
                 "radial up direction around a spherical world.")]
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 3.2f, 0f);

        [Header("Orbit")]
        [SerializeField] private Transform cameraTransform;
        // Went 7 -> 11 chasing "zoom out more" before settling back at a standard over-the-
        // shoulder distance - past a point, more distance just fights aim (see _pitch's comment).
        [SerializeField] private float distance = 8f;
        [SerializeField] private float minDistance = 0.6f;
        [Tooltip("Degrees of orbit per pixel of raw mouse delta (the Look action has no " +
                 "scaling processor). Values above ~0.3 feel extremely twitchy.")]
        [SerializeField] private float mouseSensitivity = 0.08f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 60f;
        [SerializeField] private float followSmoothTime = 0.06f;

        [Header("Shoulder Offset")]
        [Tooltip("Lateral/vertical offset of the camera from dead-center behind the player. " +
                 "Without this the camera's forward ray runs straight through the player's own " +
                 "head (since it looks parallel to, not at, the pivot), so aiming into the " +
                 "distance means looking through yourself. A shoulder offset moves the " +
                 "character off-center so the crosshair sits in open space.")]
        [SerializeField] private float shoulderOffsetX = 1.4f;
        [SerializeField] private float shoulderOffsetY = 0.3f;

        [Header("Collision")]
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private LayerMask collisionMask = ~0;

        [Tooltip("While true, mouse Look input is ignored (e.g. the emote wheel is using the " +
                 "cursor). The camera still follows the target's position.")]
        public bool InputSuspended;

        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set => mouseSensitivity = Mathf.Clamp(
                value,
                MinimumMouseSensitivity,
                MaximumMouseSensitivity);
        }

        // Added on top of distance/minDistance while set - e.g. PlayerUltimate pulls the camera
        // back further while the mech visual (larger than the astronaut) is active, so it doesn't
        // fill the frame.
        private float _extraDistance;
        // Same idea, added on top of targetOffset.y - the Mech is taller than the astronaut, so
        // its pivot needs to sit higher too, on top of the shared base height.
        private float _extraHeight;

        public void SetExtraDistance(float extraDistance)
        {
            _extraDistance = Mathf.Max(0f, extraDistance);
        }

        public void SetExtraHeight(float extraHeight)
        {
            _extraHeight = extraHeight;
        }

        private Vector3 GetEffectiveTargetOffset()
        {
            return targetOffset + new Vector3(0f, _extraHeight, 0f);
        }

        private InputSystem_Actions _actions;
        // Went 15 -> 30 -> 45 chasing this as a band-aid while distance/targetOffset kept growing
        // from repeated "zoom out more" requests - a camera parked far and high needs a steeper
        // and steeper default pitch to hit anything close, and that only ever suits one specific
        // target distance. Reverted back down to a modest value now that distance/targetOffset
        // themselves came back down to a standard over-the-shoulder range (see their comments) -
        // the real fix was the rig geometry, not the default pitch.
        private float _pitch = 15f;
        private Vector3 _followVelocity;
        private Vector3 _shakeOffset;
        private float _shakeUntil = -1f;
        private float _shakeDuration;
        private float _shakeMagnitude;
        private Vector3 _orbitForward;
        private Vector3 _lastTargetUp;
        private bool _orbitInitialized;

        private void Awake()
        {
            EnsureRuntimeState();
        }

        private void EnsureRuntimeState()
        {
            if (_actions == null) _actions = PlayerInputBindings.CreateActions();
            if (cameraTransform == null)
            {
                cameraTransform = GetComponentInChildren<Camera>()?.transform;
            }
        }

        private void OnEnable()
        {
            EnsureRuntimeState();
            _actions.Player.Enable();
        }

        private void OnDisable()
        {
            _actions?.Player.Disable();
        }

        private void OnDestroy()
        {
            PlayerInputBindings.ReleaseActions(_actions);
            _actions = null;
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (target != null)
            {
                InitializeOrbit(target.up);
            }
        }

        public void SetTarget(Transform newTarget)
        {
            if (target == newTarget) return;

            target = newTarget;
            _orbitInitialized = false;
        }

        /// <summary>
        /// Sets the tangent direction and pitch the gameplay orbit will use when a cutscene hands
        /// the camera back. This is safe while the component is disabled.
        /// </summary>
        public void SetOrbit(Vector3 worldForward, float pitchDegrees)
        {
            if (target == null) return;

            Vector3 targetUp = target.up.normalized;
            _orbitForward = ProjectOntoTangent(worldForward, targetUp);
            _lastTargetUp = targetUp;
            _pitch = Mathf.Clamp(pitchDegrees, minPitch, maxPitch);
            _orbitInitialized = true;
        }

        /// <summary>
        /// Synchronizes both the orbit pivot and child Camera to the queried follow pose before
        /// re-enabling LateUpdate. Without this, a cutscene that moved the child Camera in world
        /// space could reveal the stale pivot for one frame during hand-back.
        /// </summary>
        public void SnapToFollowPose()
        {
            if (target == null || cameraTransform == null) return;

            Vector3 targetUp = target.up.normalized;
            if (!_orbitInitialized)
            {
                InitializeOrbit(targetUp);
            }

            GetDesiredFollowState(
                out Vector3 pivotPosition,
                out Quaternion followRotation,
                out float resolvedDistance);
            transform.SetPositionAndRotation(
                pivotPosition,
                followRotation);
            cameraTransform.localPosition = new Vector3(
                shoulderOffsetX,
                shoulderOffsetY,
                -resolvedDistance);
            cameraTransform.localRotation = Quaternion.identity;
            _followVelocity = Vector3.zero;
            _shakeOffset = Vector3.zero;
            _shakeUntil = -1f;
        }

        // Lets a cutscene (BossFightController's Stage 1 -> Stage 2 transition) blend Camera.main
        // smoothly back to the normal follow pose instead of snapping to it the instant this
        // component is re-enabled. Pure query - doesn't touch the orbit/pitch/transform, so it's safe
        // to call every frame while this component is still disabled (its own LateUpdate isn't
        // running to move anything out from under the blend). It resolves the same collision pull-in
        // as LateUpdate so the first gameplay frame cannot pop closer to the player.
        public void GetFollowPose(out Vector3 worldPosition, out Quaternion worldRotation)
        {
            if (target == null)
            {
                worldPosition = transform.position;
                worldRotation = transform.rotation;
                return;
            }

            GetDesiredFollowState(
                out Vector3 pivotPosition,
                out worldRotation,
                out float resolvedDistance);
            Vector3 shoulderLocalOffset = new Vector3(
                shoulderOffsetX,
                shoulderOffsetY,
                -resolvedDistance);
            worldPosition = pivotPosition + worldRotation * shoulderLocalOffset;
        }

        // Boss impact feedback (e.g. BossMechAI's ground-slam landing) - decaying random jitter
        // layered on top of the normal follow position each LateUpdate, rather than touching yaw/
        // pitch, so it reads as the ground/surroundings shaking without fighting player look input.
        public void Shake(float duration, float magnitude)
        {
            _shakeDuration = duration;
            _shakeMagnitude = magnitude;
            _shakeUntil = Time.time + duration;
        }

        private void LateUpdate()
        {
            if (target == null || cameraTransform == null) return;

            Vector3 targetUp = target.up.normalized;
            if (!_orbitInitialized)
            {
                InitializeOrbit(targetUp);
            }
            else
            {
                // Carry the horizontal orbit direction across the changing tangent plane using
                // the smallest rotation between consecutive radial-up vectors. In particular,
                // target yaw never enters this calculation, so turning the character to face its
                // movement direction cannot drag or snap the independently controlled camera.
                Quaternion tangentTransport = Quaternion.FromToRotation(_lastTargetUp, targetUp);
                _orbitForward = ProjectOntoTangent(tangentTransport * _orbitForward, targetUp);
                _lastTargetUp = targetUp;
            }

            if (!InputSuspended)
            {
                Vector2 look = _actions.Player.Look.ReadValue<Vector2>();
                _orbitForward = ProjectOntoTangent(
                    Quaternion.AngleAxis(look.x * mouseSensitivity, targetUp) * _orbitForward,
                    targetUp);
                _pitch = Mathf.Clamp(_pitch - look.y * mouseSensitivity, minPitch, maxPitch);
            }

            Vector3 desiredPosition = target.position + target.TransformDirection(GetEffectiveTargetOffset());
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _followVelocity, followSmoothTime);

            Quaternion tangentFrame = Quaternion.LookRotation(_orbitForward, targetUp);
            transform.rotation = tangentFrame * Quaternion.Euler(_pitch, 0f, 0f);

            if (Time.time < _shakeUntil)
            {
                float remaining = _shakeUntil - Time.time;
                float falloff = _shakeDuration > 0f ? Mathf.Clamp01(remaining / _shakeDuration) : 0f;
                _shakeOffset = Random.insideUnitSphere * (_shakeMagnitude * falloff);
                transform.position += _shakeOffset;
            }

            float clampedDistance = ResolveCollisionDistance(transform.position, transform.rotation);

            cameraTransform.localPosition = new Vector3(shoulderOffsetX, shoulderOffsetY, -clampedDistance);
            cameraTransform.localRotation = Quaternion.identity;
        }

        private void GetDesiredFollowState(
            out Vector3 pivotPosition,
            out Quaternion followRotation,
            out float resolvedDistance)
        {
            Vector3 targetUp = target.up.normalized;
            Vector3 orbitForward = _orbitInitialized
                ? ProjectOntoTangent(_orbitForward, targetUp)
                : ProjectOntoTangent(transform.forward, targetUp);
            Quaternion tangentFrame = Quaternion.LookRotation(orbitForward, targetUp);
            followRotation = tangentFrame * Quaternion.Euler(_pitch, 0f, 0f);
            pivotPosition = target.position + target.TransformDirection(GetEffectiveTargetOffset());
            resolvedDistance = ResolveCollisionDistance(pivotPosition, followRotation);
        }

        private float ResolveCollisionDistance(Vector3 pivotPosition, Quaternion pivotRotation)
        {
            float targetDistance = distance + _extraDistance;
            Vector3 shoulderLocalOffset = new Vector3(shoulderOffsetX, shoulderOffsetY, 0f);
            Vector3 castOrigin = pivotPosition + pivotRotation * shoulderLocalOffset;
            Vector3 castDirection = pivotRotation * Vector3.back;

            if (Physics.SphereCast(
                    castOrigin,
                    collisionRadius,
                    castDirection,
                    out RaycastHit hit,
                    targetDistance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                return Mathf.Clamp(hit.distance, minDistance, targetDistance);
            }

            return targetDistance;
        }

        private void InitializeOrbit(Vector3 targetUp)
        {
            targetUp.Normalize();

            Vector3 initialForward = Vector3.ProjectOnPlane(transform.forward, targetUp);
            if (initialForward.sqrMagnitude < 0.000001f)
            {
                initialForward = Vector3.ProjectOnPlane(target.forward, targetUp);
            }

            _orbitForward = ProjectOntoTangent(initialForward, targetUp);
            _lastTargetUp = targetUp;
            _orbitInitialized = true;
        }

        private static Vector3 ProjectOntoTangent(Vector3 direction, Vector3 up)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(direction, up);
            if (tangent.sqrMagnitude >= 0.000001f)
            {
                return tangent.normalized;
            }

            // Only reachable for a degenerate initial orientation. Pick a deterministic axis
            // that is not parallel to up so Quaternion.LookRotation always receives a basis.
            Vector3 fallback = Mathf.Abs(Vector3.Dot(up, Vector3.forward)) < 0.99f
                ? Vector3.forward
                : Vector3.right;
            return Vector3.ProjectOnPlane(fallback, up).normalized;
        }
    }
}

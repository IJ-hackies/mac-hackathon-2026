using UnityEngine;

namespace Player
{
    public class ThirdPersonCameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [Tooltip("Camera height above the player. This sits well above head height on purpose: " +
                 "if the camera were at head height it would always be looking almost level " +
                 "into the back of the character's head, keeping them pinned near screen center " +
                 "no matter how you pitch. Elevating the rig pushes the character into the lower " +
                 "part of the frame and opens up the space around the (fixed, center-screen) " +
                 "crosshair.")]
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 2.3f, 0f);

        [Header("Orbit")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float distance = 7f;
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

        private InputSystem_Actions _actions;
        private float _yaw;
        private float _pitch = 15f;
        private Vector3 _followVelocity;
        private Vector3 _shakeOffset;
        private float _shakeUntil = -1f;
        private float _shakeDuration;
        private float _shakeMagnitude;

        private void Awake()
        {
            _actions = new InputSystem_Actions();
            if (cameraTransform == null)
            {
                cameraTransform = GetComponentInChildren<Camera>()?.transform;
            }
        }

        private void OnEnable()
        {
            _actions.Player.Enable();
        }

        private void OnDisable()
        {
            _actions.Player.Disable();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _yaw = transform.eulerAngles.y;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        // Lets a cutscene (BossFightController's Stage 1 -> Stage 2 transition) blend Camera.main
        // smoothly back to the normal follow pose instead of snapping to it the instant this
        // component is re-enabled. Pure query - doesn't touch _yaw/_pitch/transform, so it's safe
        // to call every frame while this component is still disabled (its own LateUpdate isn't
        // running to move anything out from under the blend). Skips the collision SphereCast for
        // simplicity; the blend only needs to be close, not pixel-identical, since the real
        // LateUpdate takes over the instant the blend finishes.
        public void GetFollowPose(out Vector3 worldPosition, out Quaternion worldRotation)
        {
            worldRotation = Quaternion.Euler(_pitch, _yaw, 0f);

            if (target == null)
            {
                worldPosition = transform.position;
                return;
            }

            Vector3 pivotPosition = target.position + targetOffset;
            Vector3 shoulderLocalOffset = new Vector3(shoulderOffsetX, shoulderOffsetY, -distance);
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

            if (!InputSuspended)
            {
                Vector2 look = _actions.Player.Look.ReadValue<Vector2>();
                _yaw += look.x * mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch - look.y * mouseSensitivity, minPitch, maxPitch);
            }

            Vector3 desiredPosition = target.position + targetOffset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _followVelocity, followSmoothTime);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            if (Time.time < _shakeUntil)
            {
                float remaining = _shakeUntil - Time.time;
                float falloff = _shakeDuration > 0f ? Mathf.Clamp01(remaining / _shakeDuration) : 0f;
                _shakeOffset = Random.insideUnitSphere * (_shakeMagnitude * falloff);
                transform.position += _shakeOffset;
            }

            Vector3 shoulderLocalOffset = new Vector3(shoulderOffsetX, shoulderOffsetY, 0f);
            Vector3 castOrigin = transform.TransformPoint(shoulderLocalOffset);
            Vector3 castDirection = transform.rotation * Vector3.back;

            float clampedDistance = distance;
            if (Physics.SphereCast(castOrigin, collisionRadius, castDirection, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                clampedDistance = Mathf.Clamp(hit.distance, minDistance, distance);
            }

            cameraTransform.localPosition = new Vector3(shoulderOffsetX, shoulderOffsetY, -clampedDistance);
            cameraTransform.localRotation = Quaternion.identity;
        }
    }
}

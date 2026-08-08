using UnityEngine;

namespace Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerVisualGroundConformer : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Direct-child visual hierarchy to tilt and lift. If unassigned, it is resolved " +
                 "from the first child Animator.")]
        [SerializeField] private Transform visualRoot;

        [Header("Ground Conforming")]
        [Tooltip("Maximum visual tilt toward the walkable support normal. The physical capsule " +
                 "and camera remain radial.")]
        [SerializeField, Range(0f, 45f)] private float maxTiltAngle = 30f;
        [Tooltip("How quickly the astronaut model follows terrain tilt while grounded.")]
        [SerializeField, Min(0f)] private float groundedTiltResponse = 14f;
        [Tooltip("How quickly the model returns to radial alignment while airborne.")]
        [SerializeField, Min(0f)] private float airborneReturnResponse = 18f;
        [Tooltip("Desired visible clearance between the model's foot origin and the support surface.")]
        [SerializeField, Min(0f)] private float targetGroundClearance = 0.025f;
        [Tooltip("Maximum visual-only lift used to compensate for the capsule motor's contact gap.")]
        [SerializeField, Min(0f)] private float maxGroundLift = 0.12f;
        [Tooltip("How quickly the visual lift follows changes in measured ground clearance.")]
        [SerializeField, Min(0f)] private float liftResponse = 18f;

        private PlayerController _controller;
        private Vector3 _baselineLocalPosition;
        private Quaternion _baselineLocalRotation;
        private bool _baselineCaptured;
        private float _currentLift;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            ResolveVisualRoot();
            CaptureBaseline();
        }

        private void LateUpdate()
        {
            if (visualRoot == null || !_baselineCaptured)
            {
                return;
            }

            bool grounded = _controller != null && _controller.IsGrounded;
            Quaternion targetRotation = grounded
                ? GetGroundedLocalRotation(_controller.GroundNormal)
                : _baselineLocalRotation;
            float rotationResponse = grounded
                ? groundedTiltResponse
                : airborneReturnResponse;

            visualRoot.localRotation = Quaternion.Slerp(
                visualRoot.localRotation,
                targetRotation,
                DampFactor(rotationResponse));

            float targetLift = grounded
                ? Mathf.Clamp(
                    targetGroundClearance - _controller.GroundClearance,
                    0f,
                    Mathf.Max(0f, maxGroundLift))
                : 0f;
            _currentLift = Mathf.Lerp(_currentLift, targetLift, DampFactor(liftResponse));
            visualRoot.localPosition = _baselineLocalPosition + Vector3.up * _currentLift;
        }

        private void OnDisable()
        {
            if (visualRoot == null || !_baselineCaptured)
            {
                return;
            }

            visualRoot.localPosition = _baselineLocalPosition;
            visualRoot.localRotation = _baselineLocalRotation;
            _currentLift = 0f;
        }

        private void ResolveVisualRoot()
        {
            if (visualRoot != null)
            {
                return;
            }

            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.transform != transform)
            {
                Transform candidate = animator.transform;
                while (candidate.parent != null && candidate.parent != transform)
                {
                    candidate = candidate.parent;
                }

                if (candidate.parent == transform)
                {
                    visualRoot = candidate;
                }
            }
        }

        private void CaptureBaseline()
        {
            if (visualRoot == null || visualRoot.parent != transform)
            {
                Debug.LogWarning(
                    $"{nameof(PlayerVisualGroundConformer)} on '{name}' requires a direct child visual root.",
                    this);
                visualRoot = null;
                return;
            }

            _baselineLocalPosition = visualRoot.localPosition;
            _baselineLocalRotation = visualRoot.localRotation;
            _baselineCaptured = true;
        }

        private Quaternion GetGroundedLocalRotation(Vector3 supportNormal)
        {
            Vector3 localSupportUp = transform.InverseTransformDirection(supportNormal).normalized;
            Vector3 baselineUp = _baselineLocalRotation * Vector3.up;
            float maxTiltRadians = Mathf.Clamp(maxTiltAngle, 0f, 45f) * Mathf.Deg2Rad;
            Vector3 targetUp = Vector3.RotateTowards(
                baselineUp,
                localSupportUp,
                maxTiltRadians,
                0f);
            return Quaternion.FromToRotation(baselineUp, targetUp) * _baselineLocalRotation;
        }

        private float DampFactor(float response)
        {
            return 1f - Mathf.Exp(-Mathf.Max(0f, response) * Time.deltaTime);
        }
    }
}

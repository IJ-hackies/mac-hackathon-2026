using UnityEngine;
using UnityEngine.InputSystem;

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

        [Header("Jump / Gravity")]
        [SerializeField] private float jumpHeight = 1.4f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Camera")]
        [Tooltip("Yaw of this transform is used to make movement camera-relative. Defaults to Camera.main.")]
        [SerializeField] private Transform cameraReference;

        private CharacterController _controller;
        private InputSystem_Actions _actions;
        private Vector3 _verticalVelocity;
        private bool _jumpQueued;
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
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            _jumpQueued = true;
        }

        private void Update()
        {
            JumpTriggeredThisFrame = false;
            IsGrounded = _controller.isGrounded;

            Vector2 moveInput = _actions.Player.Move.ReadValue<Vector2>();
            bool sprinting = _actions.Player.Sprint.IsPressed();

            Vector3 moveDirection = CameraRelativeDirection(moveInput);

            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRotation, rotationDegreesPerSecond * Time.deltaTime);
            }

            float maxSpeed = sprinting ? sprintSpeed : walkSpeed;
            float targetHorizontalSpeed = maxSpeed * Mathf.Clamp01(moveInput.magnitude);
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetHorizontalSpeed, acceleration * Time.deltaTime);

            Vector3 horizontalMotion = moveDirection * _currentSpeed;
            NormalizedSpeed = Mathf.Clamp01(_currentSpeed / sprintSpeed);

            ApplyGravityAndJump();

            Vector3 motion = (horizontalMotion + _verticalVelocity) * Time.deltaTime;
            _controller.Move(motion);
        }

        private void ApplyGravityAndJump()
        {
            if (IsGrounded)
            {
                if (_verticalVelocity.y < 0f)
                {
                    _verticalVelocity.y = groundedStickForce;
                }

                if (_jumpQueued)
                {
                    _verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    JumpTriggeredThisFrame = true;
                }
            }
            else
            {
                _verticalVelocity.y += gravity * Time.deltaTime;
            }

            _jumpQueued = false;
        }

        private Vector3 CameraRelativeDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (cameraReference != null)
            {
                forward = cameraReference.forward;
                right = cameraReference.right;
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();
            }

            return (forward * input.y + right * input.x).normalized;
        }
    }
}

using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAnimatorRelay : MonoBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int GroundedParam = Animator.StringToHash("Grounded");
        private static readonly int JumpParam = Animator.StringToHash("Jump");

        [SerializeField] private Animator animator;
        [SerializeField] private float speedDampTime = 0.1f;

        private PlayerController _controller;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        private void Update()
        {
            if (animator == null) return;

            animator.SetFloat(SpeedParam, _controller.NormalizedSpeed, speedDampTime, Time.deltaTime);
            animator.SetBool(GroundedParam, _controller.IsGrounded);

            if (_controller.JumpTriggeredThisFrame)
            {
                animator.SetTrigger(JumpParam);
            }
        }
    }
}

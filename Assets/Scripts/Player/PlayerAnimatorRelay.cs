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

        // Called by PlayerUltimate when swapping the active visual (astronaut <-> mech) - both
        // models' AnimatorControllers share the same Speed/Grounded/Jump parameter names, so
        // driving whichever is currently active needs nothing else changed here.
        public void SetAnimator(Animator target)
        {
            animator = target;
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

using Audio;
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

        [SerializeField] private PlayerUltimate playerUltimate;

        private PlayerController _controller;
        private bool _wasGrounded;
        private bool _groundStateInitialized;
        private bool _hasJumpParam;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            if (playerUltimate == null) playerUltimate = GetComponent<PlayerUltimate>();
            _hasJumpParam = AnimatorHasParameter(animator, JumpParam);
            EnsureFootstepEventReceiver();
        }

        // Footsteps are triggered by "PlayFootstep" Animation Events authored directly on the
        // walk/run clips (see Assets/Editor/Player/PlayerFootstepEventsSetup.cs) so they land
        // exactly when each foot's contact frame plays, instead of a speed-scaled timer
        // approximating the cadence and drifting out of sync with the actual animation. Unity
        // delivers Animation Events via SendMessage on the Animator's own GameObject, not
        // upwards to this component's GameObject, so the receiver has to live there instead.
        private void EnsureFootstepEventReceiver()
        {
            if (animator == null) return;
            if (animator.GetComponent<PlayerFootstepAnimationEvents>() == null)
            {
                animator.gameObject.AddComponent<PlayerFootstepAnimationEvents>();
            }
        }

        private static bool AnimatorHasParameter(Animator target, int paramHash)
        {
            if (target == null) return false;
            var parameters = target.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == paramHash) return true;
            }
            return false;
        }

        // Called by PlayerUltimate when swapping the active visual (astronaut <-> mech). Both
        // models' AnimatorControllers share Speed/Grounded, but the mech controller has no Jump
        // trigger (it doesn't have a jump animation) - SetTrigger(JumpParam) on it logged
        // "Parameter 'Hash ...' does not exist" every jump, hence the HasJumpParam recheck here.
        public void SetAnimator(Animator target)
        {
            animator = target;
            _hasJumpParam = AnimatorHasParameter(animator, JumpParam);
            EnsureFootstepEventReceiver();
        }

        private void Update()
        {
            if (animator == null) return;

            animator.SetFloat(SpeedParam, _controller.NormalizedSpeed, speedDampTime, Time.deltaTime);
            animator.SetBool(GroundedParam, _controller.IsGrounded);

            if (_controller.JumpTriggeredThisFrame)
            {
                if (_hasJumpParam) animator.SetTrigger(JumpParam);
                AudioManager.Instance.PlaySfx(SfxId.PlayerJump, transform.position);
            }

            // Skip the very first frame's comparison - _wasGrounded defaults false and would
            // otherwise fire a false "landing" sound if the player simply starts out grounded.
            if (_groundStateInitialized && _controller.IsGrounded && !_wasGrounded)
            {
                AudioManager.Instance.PlaySfx(SfxId.PlayerLand, transform.position);
            }
            _wasGrounded = _controller.IsGrounded;
            _groundStateInitialized = true;
        }
    }
}

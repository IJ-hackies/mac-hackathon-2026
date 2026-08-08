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

        [Header("Footsteps")]
        [Tooltip("Step interval at a walking NormalizedSpeed (~0) - blends down toward " +
                 "footstepIntervalRun as speed rises toward 1.")]
        [SerializeField] private float footstepIntervalWalk = 0.35f;
        [SerializeField] private float footstepIntervalRun = 0.22f;

        [SerializeField] private PlayerUltimate playerUltimate;

        private PlayerController _controller;
        private float _nextFootstepTime;
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

            UpdateFootsteps();
        }

        private void UpdateFootsteps()
        {
            float normalizedSpeed = Mathf.Clamp01(_controller.NormalizedSpeed);
            bool moving = _controller.IsGrounded && normalizedSpeed > 0.05f;

            if (!moving)
            {
                // Re-arm immediately on the next step after stopping, rather than waiting out
                // whatever fraction of the previous interval was left.
                _nextFootstepTime = Time.time;
                return;
            }

            if (Time.time < _nextFootstepTime) return;

            float interval = Mathf.Lerp(footstepIntervalWalk, footstepIntervalRun, normalizedSpeed);
            _nextFootstepTime = Time.time + interval;

            // Player-controlled mech (ultimate) reuses the normal player footstep instead of the
            // hydraulic mech-leg cues - those are reserved for the boss's mech (BossMechAI).
            AudioManager.Instance.PlaySfx(SfxId.PlayerFootstep, transform.position);
        }
    }
}

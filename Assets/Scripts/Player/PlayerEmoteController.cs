using Audio;
using Player.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerEmoteController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerCombat playerCombat;
        [SerializeField] private PlayerUltimate playerUltimate;
        [SerializeField] private ThirdPersonCameraController cameraController;
        [SerializeField] private EmoteWheelUI wheelUi;
        [SerializeField] private CrosshairUI crosshairUi;

        [Header("Emotes (index order must match the wheel UI's slice order)")]
        [SerializeField] private AnimationClip waveClip;
        [SerializeField] private AnimationClip yesClip;
        [SerializeField] private AnimationClip noClip;

        [Header("Mech-only emote clips (own skeleton, own clip lengths)")]
        [SerializeField] private AnimationClip mechWaveClip;
        [SerializeField] private AnimationClip mechYesClip;
        [SerializeField] private AnimationClip mechNoClip;
        [Tooltip("Mech-only 4th wheel option ('Dance') - loops until interrupted (movement/" +
                 "attack/re-selecting), unlike Wave/Yes/No which play once and return to Idle.")]
        [SerializeField] private AnimationClip danceClip;

        private static readonly string[] BaseLabels = { "Wave", "Yes", "No" };
        private static readonly string[] MechLabels = { "Wave", "Yes", "No", "Dance" };
        private const int DanceIndex = 3;

        [Tooltip("The wheel is driven by the same mouse-delta Look input the camera uses, " +
                 "accumulated into a virtual joystick direction while the wheel is held open " +
                 "(the cursor itself never unlocks/moves). Higher = less mouse travel needed " +
                 "to reach full deflection.")]
        [SerializeField] private float wheelSensitivity = 0.012f;

        private static readonly int EmotingParam = Animator.StringToHash("Emoting");
        private static readonly int EmoteIndexParam = Animator.StringToHash("EmoteIndex");
        private static readonly int PlayEmoteParam = Animator.StringToHash("PlayEmote");

        private InputSystem_Actions _actions;
        private AnimationClip[] _baseClips;
        private AnimationClip[] _mechClips;
        private Vector2 _wheelDirection;
        private bool _wheelOpen;
        private bool _isEmoting;
        private bool _ignoreGameplayInterrupts;
        private float _emoteEndTime;

        [Tooltip("Cadence for the boss-mech leg-step cues played while the mech Dance emote loops.")]
        [SerializeField] private float mechDanceStepInterval = 0.45f;
        private bool _mechDanceAudioActive;
        private float _nextMechDanceStepTime;
        private bool _mechDanceStepAlternate;

        public bool InputSuspended { get; private set; }

        private void Awake()
        {
            _actions = new InputSystem_Actions();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (playerController == null) playerController = GetComponent<PlayerController>();
            if (playerCombat == null) playerCombat = GetComponent<PlayerCombat>();
            if (playerUltimate == null) playerUltimate = GetComponent<PlayerUltimate>();
            _baseClips = new[] { waveClip, yesClip, noClip };
            _mechClips = new[] { mechWaveClip, mechYesClip, mechNoClip, danceClip };
        }

        // Wave/Yes/No/Dance actually play via the Animator state machine (EmoteIndex + PlayEmote,
        // built identically on both controllers) - this array only supplies clip.length for
        // _emoteEndTime timing, so it has to match whichever skeleton/clip set is actually active
        // or the "when does this finish" timer would use the wrong model's clip duration.
        private AnimationClip[] ActiveClips =>
            (playerUltimate != null && playerUltimate.IsActive) ? _mechClips : _baseClips;

        // Called by PlayerUltimate on activate/end, mirroring PlayerCombat.SetAnimator - both
        // controllers share the Emoting/EmoteIndex/PlayEmote param contract.
        public void SetAnimator(Animator target)
        {
            animator = target;
        }

        private void OnEnable()
        {
            _actions.Player.Enable();
            _actions.Player.EmoteWheel.started += OnWheelStarted;
            _actions.Player.EmoteWheel.canceled += OnWheelCanceled;
        }

        private void OnDisable()
        {
            _actions.Player.EmoteWheel.started -= OnWheelStarted;
            _actions.Player.EmoteWheel.canceled -= OnWheelCanceled;
            _actions.Player.Disable();
        }

        private void OnWheelStarted(InputAction.CallbackContext context)
        {
            if (InputSuspended) return;

            _wheelOpen = true;
            _wheelDirection = Vector2.zero;

            if (cameraController != null) cameraController.InputSuspended = true;
            if (crosshairUi != null) crosshairUi.SetVisible(false);
            if (wheelUi != null)
            {
                bool mech = playerUltimate != null && playerUltimate.IsActive;
                wheelUi.Configure(mech ? MechLabels : BaseLabels);
                wheelUi.Show();
            }
        }

        private void OnWheelCanceled(InputAction.CallbackContext context)
        {
            if (InputSuspended) return;

            _wheelOpen = false;

            if (cameraController != null) cameraController.InputSuspended = false;
            if (crosshairUi != null) crosshairUi.SetVisible(true);

            int hovered = wheelUi != null ? wheelUi.HoveredIndex : -1;
            if (wheelUi != null) wheelUi.Hide();

            if (hovered >= 0)
            {
                TriggerEmote(hovered);
            }
        }

        public void SetInputSuspended(bool suspended)
        {
            InputSuspended = suspended;
            if (!suspended || !_wheelOpen) return;

            _wheelOpen = false;
            _wheelDirection = Vector2.zero;
            if (cameraController != null) cameraController.InputSuspended = false;
            if (crosshairUi != null) crosshairUi.SetVisible(true);
            if (wheelUi != null) wheelUi.Hide();
        }

        /// <summary>
        /// Plays the existing Wave animation without opening the player-controlled emote wheel.
        /// Returns the authored clip length so a cinematic can hold the shot long enough.
        /// </summary>
        public float PlayCinematicWave()
        {
            _ignoreGameplayInterrupts = true;
            return TriggerEmote(0);
        }

        public void StopCinematicEmote()
        {
            _ignoreGameplayInterrupts = false;
            StopEmote();
        }

        private float TriggerEmote(int index)
        {
            AnimationClip[] clips = ActiveClips;
            if (clips == null || index >= clips.Length || clips[index] == null)
            {
                Debug.LogWarning($"PlayerEmoteController: no clip wired for emote index {index}; " +
                                  "check the waveClip/yesClip/noClip (or mech equivalents) " +
                                  "references (rerun Build Test Scene if the FBX's clip lookup " +
                                  "warned about a missing take).");
                return 0f;
            }

            _isEmoting = true;
            // Dance loops until interrupted (movement/attack/re-opening the wheel) rather than
            // finishing on its own - see Update()'s interrupt check, which already applies to
            // every emote; Dance just never reaches the natural "clip finished" branch since
            // there's no end time to reach.
            _emoteEndTime = index == DanceIndex ? float.PositiveInfinity : Time.time + clips[index].length;

            if (animator != null)
            {
                animator.SetInteger(EmoteIndexParam, index);
                animator.SetBool(EmotingParam, true);
                // A dedicated trigger (auto-consumed after one use) drives entry into the emote
                // state instead of the held "Emoting" bool. An AnyState transition gated purely
                // by a bool that stays true for the whole action can re-fire into itself every
                // frame and restart the clip (see PlayerSceneSetup's AC_Player.controller
                // comments) even with canTransitionToSelf disabled; Triggers don't have that
                // failure mode since Mecanim consumes them on use, matching how Melee/Fire work.
                animator.SetTrigger(PlayEmoteParam);
            }

            // Dance is mech-only (see MechLabels) - while it loops, play the same leg-step cues
            // the boss mech uses for movement, on their own cadence rather than tying it to actual
            // locomotion (the player isn't moving during Dance).
            bool isMechDance = index == DanceIndex && playerUltimate != null && playerUltimate.IsActive;
            _mechDanceAudioActive = isMechDance;
            if (isMechDance) _nextMechDanceStepTime = Time.time;

            return clips[index].length;
        }

        private void StopEmote()
        {
            _isEmoting = false;
            _mechDanceAudioActive = false;
            if (animator != null)
            {
                animator.SetBool(EmotingParam, false);
            }
        }

        private void Update()
        {
            if (_wheelOpen)
            {
                Vector2 look = _actions.Player.Look.ReadValue<Vector2>();
                _wheelDirection = Vector2.ClampMagnitude(_wheelDirection + look * wheelSensitivity, 1f);
                if (wheelUi != null) wheelUi.UpdateHover(_wheelDirection);
            }

            if (_isEmoting)
            {
                bool interrupted = !_ignoreGameplayInterrupts &&
                    ((playerController != null &&
                      (playerController.NormalizedSpeed > 0.05f ||
                       playerController.JumpTriggeredThisFrame)) ||
                     (playerCombat != null && playerCombat.IsAttacking));

                if (interrupted || Time.time >= _emoteEndTime)
                {
                    StopEmote();
                }
            }

            if (_mechDanceAudioActive && Time.time >= _nextMechDanceStepTime)
            {
                _nextMechDanceStepTime = Time.time + mechDanceStepInterval;
                SfxId step = _mechDanceStepAlternate ? SfxId.BossLegStepB : SfxId.BossLegStepA;
                _mechDanceStepAlternate = !_mechDanceStepAlternate;
                AudioManager.Instance.PlaySfx(step, transform.position);
            }
        }
    }
}

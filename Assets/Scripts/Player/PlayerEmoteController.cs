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
        [SerializeField] private ThirdPersonCameraController cameraController;
        [SerializeField] private EmoteWheelUI wheelUi;
        [SerializeField] private CrosshairUI crosshairUi;

        [Header("Emotes (index order must match the wheel UI's slice order)")]
        [SerializeField] private AnimationClip waveClip;
        [SerializeField] private AnimationClip yesClip;
        [SerializeField] private AnimationClip noClip;

        [Tooltip("The wheel is driven by the same mouse-delta Look input the camera uses, " +
                 "accumulated into a virtual joystick direction while the wheel is held open " +
                 "(the cursor itself never unlocks/moves). Higher = less mouse travel needed " +
                 "to reach full deflection.")]
        [SerializeField] private float wheelSensitivity = 0.012f;

        private static readonly int EmotingParam = Animator.StringToHash("Emoting");
        private static readonly int EmoteIndexParam = Animator.StringToHash("EmoteIndex");
        private static readonly int PlayEmoteParam = Animator.StringToHash("PlayEmote");

        private InputSystem_Actions _actions;
        private AnimationClip[] _clips;
        private Vector2 _wheelDirection;
        private bool _wheelOpen;
        private bool _isEmoting;
        private bool _ignoreGameplayInterrupts;
        private float _emoteEndTime;

        public bool InputSuspended { get; private set; }

        private void Awake()
        {
            _actions = new InputSystem_Actions();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (playerController == null) playerController = GetComponent<PlayerController>();
            if (playerCombat == null) playerCombat = GetComponent<PlayerCombat>();
            _clips = new[] { waveClip, yesClip, noClip };
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
            if (wheelUi != null) wheelUi.Show();
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
            if (_clips == null || index >= _clips.Length || _clips[index] == null)
            {
                Debug.LogWarning($"PlayerEmoteController: no clip wired for emote index {index}; " +
                                  "check the waveClip/yesClip/noClip references (rerun Build Test " +
                                  "Scene if the FBX's clip lookup warned about a missing take).");
                return 0f;
            }

            _isEmoting = true;
            _emoteEndTime = Time.time + _clips[index].length;

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

            return _clips[index].length;
        }

        private void StopEmote()
        {
            _isEmoting = false;
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
        }
    }
}

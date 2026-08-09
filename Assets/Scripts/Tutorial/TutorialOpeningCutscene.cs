using System.Collections;
using Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tutorial
{
    /// <summary>
    /// Tutorial.unity's opening shot: holds on a hand-framed pose looking at the planet/NAUT
    /// base, pans to a second hand-framed overview of the tutorial room, then pans to face the
    /// player, who waves - then control hands off to TutorialManager. The two hand-framed poses
    /// are captured directly from the Scene view via Tools/Tutorial/Capture Opening Shot From
    /// Scene View and Capture Area Overview From Scene View, rather than computed from planet
    /// geometry - deliberately simpler than Player.OpeningCutsceneController's spherical
    /// planet-to-player shot, since matching an exact hand-placed frame beats re-deriving it from
    /// a formula. See TutorialManager.Start(), which waits for BeginTutorial() instead of
    /// entering Movement itself whenever one of these is present in the scene.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class TutorialOpeningCutscene : MonoBehaviour
    {
        [Header("Hand-Framed Shots (Tools/Tutorial/Capture ... From Scene View)")]
        [Tooltip("Opening pose looking at the planet/NAUT base - world-space position.")]
        [SerializeField] private Vector3 openingShotPosition;
        [Tooltip("Opening pose looking at the planet/NAUT base - world-space Euler angles.")]
        [SerializeField] private Vector3 openingShotEulerAngles;
        [SerializeField, Range(25f, 70f)] private float openingFieldOfView = 45f;
        [Tooltip("Overview pose for the tutorial room's starting area - world-space position.")]
        [SerializeField] private Vector3 areaOverviewPosition;
        [Tooltip("Overview pose for the tutorial room's starting area - world-space Euler angles.")]
        [SerializeField] private Vector3 areaOverviewEulerAngles;
        [SerializeField, Range(25f, 70f)] private float areaFieldOfView = 50f;

        [Header("Optional Scene References (auto-resolved when empty)")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerCombat playerCombat;
        [SerializeField] private PlayerAnimatorRelay animatorRelay;
        [SerializeField] private PlayerEmoteController emoteController;
        [SerializeField] private ThirdPersonCameraController gameplayCameraController;
        [SerializeField] private Camera cinematicCamera;
        [SerializeField] private Animator playerAnimator;
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private TutorialManager manager;

        [Header("Playback")]
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool allowSkip = true;

        [Header("Shot Timing")]
        [SerializeField, Min(0.1f)] private float planetHoldDuration = 2.2f;
        [SerializeField, Min(0.1f)] private float panToAreaDuration = 2.4f;
        [SerializeField, Min(0.1f)] private float panToPlayerDuration = 2.0f;
        [SerializeField, Min(0.1f)] private float minimumWaveDuration = 2.1f;
        [SerializeField, Min(0.1f)] private float maximumWaveDuration = 3.5f;
        [SerializeField, Min(0.1f)] private float gameplayHandoffDuration = 1.2f;

        [Header("Player Shot Framing")]
        [Tooltip("Zoomed-out front-facing wave shot - distance from the player.")]
        [SerializeField, Min(1.5f)] private float playerFrontDistance = 5.5f;
        [SerializeField] private float playerFrontHeight = 1.6f;
        [SerializeField, Range(25f, 70f)] private float playerFieldOfView = 45f;
        [SerializeField, Range(-30f, 60f)] private float gameplayPitch = 15f;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int GroundedParam = Animator.StringToHash("Grounded");

        private bool _playing;
        private bool _completed;
        private bool _skipRequested;
        private bool _playerWasEnabled;
        private bool _combatWasEnabled;
        private bool _relayWasEnabled;
        private bool _cameraControllerWasEnabled;
        private bool _cameraInputWasSuspended;
        private bool _emoteInputWasSuspended;
        private bool _hudWasEnabled;
        private float _gameplayFieldOfView;

        // Async-loaded scenes (SceneTransitionController) can mark AsyncOperation.isDone before
        // every object's own Start() coroutine has actually run, so a single resolve attempt on
        // the very first frame can race PlayerRig/TutorialManager/etc. still finishing their own
        // setup. Retry across a couple seconds instead of giving up after one try.
        private const float ResolveRetryTimeout = 2f;

        private IEnumerator Start()
        {
            if (!playOnStart)
            {
                if (manager != null) manager.BeginTutorial();
                yield break;
            }

            float resolveElapsed = 0f;
            while (!ResolveReferences())
            {
                resolveElapsed += Time.unscaledDeltaTime;
                if (resolveElapsed >= ResolveRetryTimeout)
                {
                    if (manager != null) manager.BeginTutorial();
                    yield break;
                }

                yield return null;
            }

            try
            {
                BeginCutscene();
                if (allowSkip) Player.UI.CutsceneSkipPromptUI.Show();
                yield return PlaySequence();
            }
            finally
            {
                Player.UI.CutsceneSkipPromptUI.Hide();
                CompleteCutscene(_skipRequested);
            }
        }

        private void Update()
        {
            if (!_playing || !allowSkip || _skipRequested) return;

            bool keyboardSkip = Keyboard.current != null &&
                (Keyboard.current.escapeKey.wasPressedThisFrame ||
                 Keyboard.current.spaceKey.wasPressedThisFrame);
            _skipRequested = keyboardSkip;
        }

        private bool ResolveReferences()
        {
            if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
            if (playerController != null)
            {
                if (playerCombat == null) playerCombat = playerController.GetComponent<PlayerCombat>();
                if (animatorRelay == null) animatorRelay = playerController.GetComponent<PlayerAnimatorRelay>();
                if (emoteController == null) emoteController = playerController.GetComponent<PlayerEmoteController>();
                if (playerAnimator == null) playerAnimator = playerController.GetComponentInChildren<Animator>();
            }

            if (gameplayCameraController == null && playerController != null)
            {
                gameplayCameraController =
                    playerController.transform.root.GetComponentInChildren<ThirdPersonCameraController>(true);
            }
            if (gameplayCameraController != null && cinematicCamera == null)
            {
                cinematicCamera = gameplayCameraController.GetComponentInChildren<Camera>(true);
            }
            if (cinematicCamera == null) cinematicCamera = Camera.main;

            if (hudCanvas == null && playerController != null)
            {
                hudCanvas = playerController.transform.root.GetComponentInChildren<Canvas>(true);
            }
            if (manager == null) manager = FindFirstObjectByType<TutorialManager>();

            return playerController != null && playerCombat != null && animatorRelay != null &&
                   emoteController != null && gameplayCameraController != null &&
                   cinematicCamera != null && playerAnimator != null && hudCanvas != null &&
                   manager != null;
        }

        private void BeginCutscene()
        {
            _playing = true;
            _playerWasEnabled = playerController.enabled;
            _combatWasEnabled = playerCombat.enabled;
            _relayWasEnabled = animatorRelay.enabled;
            _cameraControllerWasEnabled = gameplayCameraController.enabled;
            _cameraInputWasSuspended = gameplayCameraController.InputSuspended;
            _emoteInputWasSuspended = emoteController.InputSuspended;
            _hudWasEnabled = hudCanvas.enabled;
            _gameplayFieldOfView = cinematicCamera.fieldOfView;

            playerController.enabled = false;
            playerCombat.enabled = false;
            animatorRelay.enabled = false;
            gameplayCameraController.enabled = false;
            emoteController.SetInputSuspended(true);
            hudCanvas.enabled = false;

            playerAnimator.SetFloat(SpeedParam, 0f);
            playerAnimator.SetBool(GroundedParam, true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private IEnumerator PlaySequence()
        {
            Quaternion openingRotation = Quaternion.Euler(openingShotEulerAngles);
            Quaternion areaRotation = Quaternion.Euler(areaOverviewEulerAngles);

            SetCameraPose(openingShotPosition, openingRotation, openingFieldOfView);
            yield return HoldOrSkip(planetHoldDuration);
            if (_skipRequested) yield break;

            yield return Animate(panToAreaDuration, t =>
            {
                SetCameraPose(
                    Vector3.Lerp(openingShotPosition, areaOverviewPosition, SmoothStep(t)),
                    Quaternion.Slerp(openingRotation, areaRotation, SmoothStep(t)),
                    Mathf.Lerp(openingFieldOfView, areaFieldOfView, t));
            });
            if (_skipRequested) yield break;

            Transform player = playerController.transform;
            Vector3 playerFocus = player.position + Vector3.up * 1.25f;
            Vector3 playerForward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            Vector3 frontPosition = playerFocus + playerForward * playerFrontDistance + Vector3.up * playerFrontHeight;
            Quaternion frontRotation = Quaternion.LookRotation(-playerForward, Vector3.up);

            Vector3 panStart = cinematicCamera.transform.position;
            Quaternion panStartRotation = cinematicCamera.transform.rotation;
            yield return Animate(panToPlayerDuration, t =>
            {
                SetCameraPose(
                    Vector3.Lerp(panStart, frontPosition, SmoothStep(t)),
                    Quaternion.Slerp(panStartRotation, frontRotation, SmoothStep(t)),
                    Mathf.Lerp(areaFieldOfView, playerFieldOfView, t));
            });
            if (_skipRequested) yield break;

            float waveClipLength = emoteController.PlayCinematicWave();
            float waveDuration = Mathf.Clamp(
                Mathf.Max(minimumWaveDuration, waveClipLength),
                minimumWaveDuration,
                Mathf.Max(minimumWaveDuration, maximumWaveDuration));
            yield return HoldOrSkip(waveDuration, () => SetCameraPose(frontPosition, frontRotation, playerFieldOfView));
            if (_skipRequested) yield break;

            emoteController.StopCinematicEmote();
            gameplayCameraController.SetOrbit(playerForward, gameplayPitch);
            gameplayCameraController.GetFollowPose(out Vector3 gameplayPosition, out Quaternion gameplayRotation);

            Vector3 handoffStart = cinematicCamera.transform.position;
            Quaternion handoffStartRotation = cinematicCamera.transform.rotation;
            yield return Animate(gameplayHandoffDuration, t =>
            {
                cinematicCamera.transform.SetPositionAndRotation(
                    Vector3.Lerp(handoffStart, gameplayPosition, SmoothStep(t)),
                    Quaternion.Slerp(handoffStartRotation, gameplayRotation, t));
                cinematicCamera.fieldOfView = Mathf.Lerp(playerFieldOfView, _gameplayFieldOfView, t);
            });
        }

        private IEnumerator Animate(float duration, System.Action<float> apply)
        {
            float safeDuration = Mathf.Max(0.0001f, duration);
            float elapsed = 0f;
            apply(0f);

            while (elapsed < safeDuration && !_skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                apply(Mathf.Clamp01(elapsed / safeDuration));
                yield return null;
            }

            if (!_skipRequested) apply(1f);
        }

        private IEnumerator HoldOrSkip(float duration, System.Action tick = null)
        {
            float elapsed = 0f;
            while (elapsed < duration && !_skipRequested)
            {
                tick?.Invoke();
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void CompleteCutscene(bool skipped)
        {
            if (_completed) return;
            _completed = true;
            _playing = false;

            emoteController.StopCinematicEmote();

            Vector3 playerForward = Vector3.ProjectOnPlane(playerController.transform.forward, Vector3.up).normalized;
            gameplayCameraController.SetOrbit(playerForward, gameplayPitch);
            gameplayCameraController.SnapToFollowPose();
            cinematicCamera.fieldOfView = _gameplayFieldOfView;

            emoteController.SetInputSuspended(_emoteInputWasSuspended);
            gameplayCameraController.InputSuspended = _cameraInputWasSuspended;
            gameplayCameraController.enabled = _cameraControllerWasEnabled;
            animatorRelay.enabled = _relayWasEnabled;
            playerCombat.enabled = _combatWasEnabled;
            playerController.enabled = _playerWasEnabled;
            hudCanvas.enabled = _hudWasEnabled;

            if (skipped)
            {
                Debug.Log("Tutorial opening cutscene skipped; gameplay camera and controls restored.", this);
            }

            manager.BeginTutorial();
        }

        private void SetCameraPose(Vector3 position, Quaternion rotation, float fieldOfView)
        {
            cinematicCamera.transform.SetPositionAndRotation(position, rotation);
            cinematicCamera.fieldOfView = fieldOfView;
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);
    }
}

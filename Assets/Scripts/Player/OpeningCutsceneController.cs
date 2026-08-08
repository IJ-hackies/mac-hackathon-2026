using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Player
{
    /// <summary>
    /// Plays the planet-to-player opening shot in the authored spherical world. The continuous
    /// wide opening follows the planet shell instead of cutting through it; close motion uses cubic Bezier
    /// paths in stable centre-radial frames. Each beat has its own editable velocity curve so the
    /// camera can accelerate, coast, and settle without relying on frame-dependent smoothing.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class OpeningCutsceneController : MonoBehaviour
    {
        [Header("Optional Scene References (auto-resolved when empty)")]
        [SerializeField] private Transform planetCenter;
        [SerializeField] private Transform landingBase;
        [SerializeField] private Transform baseCenter;
        [SerializeField] private Transform nautRockArt;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerCombat playerCombat;
        [SerializeField] private PlayerAnimatorRelay animatorRelay;
        [SerializeField] private PlayerEmoteController emoteController;
        [SerializeField] private ThirdPersonCameraController gameplayCameraController;
        [SerializeField] private Camera cinematicCamera;
        [SerializeField] private Animator playerAnimator;
        [SerializeField] private Canvas hudCanvas;

        [Header("Playback")]
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool allowSkip = true;
        [Tooltip("Escape or Space skips directly to the playable camera.")]
        [SerializeField] private bool logSkipHint = true;
        [Tooltip("Temporary URP shadow range used by the planet-wide opening shots.")]
        [SerializeField, Min(50f)] private float cutsceneShadowDistance = 500f;

        [Header("Shot Timing")]
        [SerializeField, Min(0.1f)] private float planetOrbitDuration = 6f;
        [SerializeField, Min(0f)] private float baseReadDuration = 1f;
        [SerializeField, Min(0f)] private float basePlayerPanDuration = 1.35f;
        [SerializeField, Min(0.1f)] private float playerDiveDuration = 2.35f;
        [SerializeField, Min(0.1f)] private float minimumWaveDuration = 2.1f;
        [SerializeField, Min(0.1f)] private float maximumWaveDuration = 3.5f;
        [SerializeField, Min(0.1f)] private float gameplayHandoffDuration = 1.9f;

        [Header("Framing")]
        [SerializeField, Min(1.5f)] private float planetOrbitRadiusMultiplier = 2.9f;
        [Tooltip("Opening position. Higher positive angles reveal more of the terminator side.")]
        [SerializeField, Range(-180f, 180f)] private float orbitStartAngle = 140f;
        [SerializeField, Range(30f, 80f)] private float wideFieldOfView = 54f;
        [Tooltip("Height above the NAUT rocks for the first, planet-wide overhead reveal.")]
        [SerializeField, Min(20f)] private float baseRevealHeight = 185f;
        [Tooltip("Height above the NAUT rocks after the reveal zoom completes.")]
        [SerializeField, Min(20f)] private float baseOverviewHeight = 100f;
        [SerializeField, Range(25f, 70f)] private float baseFieldOfView = 50f;
        [Tooltip("How far the overview camera drifts toward the player before the close dive.")]
        [SerializeField, Min(0f)] private float basePlayerPanDistance = 12f;
        [Tooltip("How much the overview aim leads from NAUT toward the player during the pan.")]
        [SerializeField, Range(0f, 0.5f)] private float basePlayerAimLead = 0.2f;
        [SerializeField, Min(2f)] private float playerFrontDistance = 6.2f;
        [SerializeField] private float playerFrontHeight = 1.15f;
        [SerializeField, Range(25f, 70f)] private float playerFieldOfView = 40f;
        [SerializeField, Range(-30f, 60f)] private float gameplayPitch = 15f;

        [Header("Keyframed Velocity")]
        [SerializeField] private AnimationCurve orbitVelocity = OpeningArcEase();
        [SerializeField] private AnimationCurve planetDollyVelocity = OpeningDollyEase();
        [SerializeField] private AnimationCurve planetAimVelocity = OpeningAimEase();
        [SerializeField] private AnimationCurve baseReadVelocity = NautZoomEase();
        [SerializeField] private AnimationCurve basePlayerPanVelocity = PlayerPanEase();
        [SerializeField] private AnimationCurve playerDiveVelocity = PlayerDiveEase();
        [SerializeField] private AnimationCurve waveCameraVelocity = WaveEase();
        [SerializeField] private AnimationCurve gameplayHandoffVelocity = HandoffEase();
        [SerializeField] private AnimationCurve aimVelocity = DefaultEase();
        [SerializeField, HideInInspector] private int velocityProfileVersion;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int GroundedParam = Animator.StringToHash("Grounded");
        private const int CurrentVelocityProfileVersion = 1;

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
        private UniversalRenderPipelineAsset _shadowPipelineAsset;
        private float _previousShadowDistance;
        private bool _shadowDistanceOverridden;

        private void Awake()
        {
            EnsureVelocityCurves();
        }

        private void OnValidate()
        {
            EnsureVelocityCurves();
        }

        private IEnumerator Start()
        {
            if (!playOnStart)
            {
                yield break;
            }

            if (!ResolveReferences())
            {
                Debug.LogWarning(
                    $"{nameof(OpeningCutsceneController)} could not resolve the planet, base, " +
                    "player, camera, and HUD references. The opening was skipped.",
                    this);
                yield break;
            }

            try
            {
                BeginCutscene();
                if (logSkipHint && allowSkip)
                {
                    Debug.Log("Opening cutscene: press Escape or Space to skip.", this);
                }

                yield return PlaySequence();
            }
            finally
            {
                if (!_completed)
                {
                    if (CanRestoreCutsceneState())
                    {
                        CompleteCutscene(_skipRequested);
                    }
                    else
                    {
                        _playing = false;
                        RestoreShadowDistance();
                    }
                }
            }
        }

        private void Update()
        {
            if (!_playing || !allowSkip || _skipRequested)
            {
                return;
            }

            bool keyboardSkip = Keyboard.current != null &&
                                (Keyboard.current.escapeKey.wasPressedThisFrame ||
                                 Keyboard.current.spaceKey.wasPressedThisFrame);
            _skipRequested = keyboardSkip;
        }

        private void OnDisable()
        {
            RestoreShadowDistance();

            if (_playing && !_completed)
            {
                _skipRequested = true;
                if (!CanRestoreCutsceneState())
                {
                    _playing = false;
                    RestoreShadowDistance();
                    return;
                }

                try
                {
                    CompleteCutscene(true);
                }
                catch (MissingReferenceException)
                {
                    // Scene/domain teardown can destroy child Transforms before this component.
                    _playing = false;
                    RestoreShadowDistance();
                }
            }
        }

        private void OnDestroy()
        {
            RestoreShadowDistance();
        }

        private bool CanRestoreCutsceneState()
        {
            return planetCenter != null && playerController != null && playerCombat != null &&
                   animatorRelay != null && emoteController != null &&
                   gameplayCameraController != null && cinematicCamera != null &&
                   hudCanvas != null;
        }

        private bool ResolveReferences()
        {
            Scene scene = gameObject.scene;
            if (planetCenter == null)
            {
                planetCenter = FindSceneRoot(scene, "Planet Ground")?.transform;
            }

            if (landingBase == null)
            {
                landingBase = FindSceneRoot(scene, "LandingBase")?.transform;
            }

            if (landingBase != null)
            {
                if (baseCenter == null) baseCenter = landingBase.Find("Layout/BaseCenter");
                if (nautRockArt == null) nautRockArt = landingBase.Find("Generated NAUT Rock Art");
            }

            if (playerController == null)
            {
                playerController = FindSingleSceneComponent<PlayerController>(scene);
            }
            if (playerController != null)
            {
                if (playerCombat == null) playerCombat = playerController.GetComponent<PlayerCombat>();
                if (animatorRelay == null) animatorRelay = playerController.GetComponent<PlayerAnimatorRelay>();
                if (emoteController == null) emoteController = playerController.GetComponent<PlayerEmoteController>();
                if (playerAnimator == null) playerAnimator = playerController.GetComponentInChildren<Animator>();
            }

            if (gameplayCameraController == null)
            {
                gameplayCameraController = playerController != null
                    ? playerController.transform.root
                        .GetComponentInChildren<ThirdPersonCameraController>(true)
                    : FindSingleSceneComponent<ThirdPersonCameraController>(scene);
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

            return planetCenter != null && landingBase != null && baseCenter != null &&
                   nautRockArt != null && playerController != null && playerCombat != null &&
                   animatorRelay != null && emoteController != null &&
                   gameplayCameraController != null && cinematicCamera != null &&
                   playerAnimator != null && hudCanvas != null;
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
            OverrideShadowDistance();

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
            Vector3 centre = planetCenter.position;
            Vector3 anchor = baseCenter.position;
            Vector3 baseUp = SafeDirection(anchor - centre, Vector3.up);
            Vector3 baseFocus = TryGetRendererBounds(nautRockArt, out Bounds wordBounds)
                ? wordBounds.center
                : anchor;
            Vector3 wordUp = SafeDirection(baseFocus - centre, baseUp);
            Vector3 wordHorizontal = DeriveWordHorizontal(nautRockArt, baseCenter, wordUp);
            Vector3 wordVertical = Vector3.Cross(wordHorizontal, wordUp).normalized;
            float planetRadius = Mathf.Max(1f, Vector3.Distance(anchor, centre));
            float orbitRadius = planetRadius * Mathf.Max(1.5f, planetOrbitRadiusMultiplier);
            Vector3 revealPosition = baseFocus + wordUp * baseRevealHeight;
            Vector3 revealDirection = SafeDirection(revealPosition - centre, wordUp);
            float revealRadius = Vector3.Distance(revealPosition, centre);

            Transform player = playerController.transform;
            Vector3 playerUp = SafeDirection(player.position - centre, player.up);
            Vector3 playerForward = ProjectTangent(player.forward, playerUp);
            Vector3 playerRight = Vector3.Cross(playerUp, playerForward).normalized;
            Vector3 playerFocus = player.position + playerUp * 1.25f;

            Vector3 orbitAxis = DeriveBestTangent(baseCenter, baseUp);
            Vector3 orbitStartDirection =
                (Quaternion.AngleAxis(orbitStartAngle, orbitAxis) * baseUp).normalized;

            yield return Animate(planetOrbitDuration, null, t =>
            {
                float angularProgress = Evaluate(orbitVelocity, t);
                float dollyProgress = Evaluate(planetDollyVelocity, t);
                float aimProgress = Evaluate(planetAimVelocity, t);
                Vector3 direction = Vector3.Slerp(
                    orbitStartDirection,
                    revealDirection,
                    angularProgress).normalized;
                float radius = Mathf.Lerp(orbitRadius, revealRadius, dollyProgress);
                Vector3 position = centre + direction * radius;
                Vector3 lookAt = Vector3.Lerp(centre, baseFocus, aimProgress);
                SetCameraPose(position, lookAt, wordVertical, wideFieldOfView);
            });
            if (_skipRequested) yield break;

            Vector3 overviewPosition = baseFocus + wordUp * baseOverviewHeight;
            yield return Animate(baseReadDuration, baseReadVelocity, t =>
            {
                SetCameraPose(
                    Vector3.Lerp(revealPosition, overviewPosition, t),
                    baseFocus,
                    wordVertical,
                    Mathf.Lerp(wideFieldOfView, baseFieldOfView, t));
            });
            if (_skipRequested) yield break;

            Vector3 playerward = ProjectTangent(playerFocus - baseFocus, wordUp);
            Vector3 playerPanPosition = overviewPosition + playerward * basePlayerPanDistance;
            Vector3 playerPanFocus = Vector3.Lerp(baseFocus, playerFocus, basePlayerAimLead);
            yield return Animate(basePlayerPanDuration, basePlayerPanVelocity, t =>
            {
                SetCameraPose(
                    Vector3.Lerp(overviewPosition, playerPanPosition, t),
                    Vector3.Lerp(baseFocus, playerPanFocus, t),
                    wordVertical,
                    baseFieldOfView);
            });
            if (_skipRequested) yield break;

            Vector3 frontPosition = playerFocus + playerForward * playerFrontDistance +
                                    playerUp * playerFrontHeight;
            Vector3 diveStart = cinematicCamera.transform.position;
            Vector3 diveControlA = diveStart - baseUp * (baseOverviewHeight * 0.38f) +
                                   wordVertical * 8f;
            Vector3 diveControlB = playerFocus + playerForward * (playerFrontDistance * 2.2f) +
                                   playerUp * 7f;

            yield return Animate(playerDiveDuration, playerDiveVelocity, t =>
            {
                Vector3 position = CubicBezier(
                    diveStart,
                    diveControlA,
                    diveControlB,
                    frontPosition,
                    t);
                Vector3 lookAt = Vector3.Lerp(playerPanFocus, playerFocus, Evaluate(aimVelocity, t));
                Vector3 screenUp = Vector3.Slerp(wordVertical, playerUp, t);
                SetCameraPose(
                    position,
                    lookAt,
                    screenUp,
                    Mathf.Lerp(baseFieldOfView, playerFieldOfView, t));
            });
            if (_skipRequested) yield break;

            float waveClipLength = emoteController.PlayCinematicWave();
            float waveDuration = Mathf.Clamp(
                Mathf.Max(minimumWaveDuration, waveClipLength),
                minimumWaveDuration,
                Mathf.Max(minimumWaveDuration, maximumWaveDuration));
            Vector3 waveStart = cinematicCamera.transform.position;
            Vector3 waveRelative = waveStart - playerFocus;
            Vector3 waveEnd = playerFocus +
                              Quaternion.AngleAxis(8f, playerUp) * waveRelative * 0.94f;

            yield return Animate(waveDuration, waveCameraVelocity, t =>
            {
                SetCameraPose(
                    Vector3.Lerp(waveStart, waveEnd, t),
                    playerFocus + playerUp * 0.1f,
                    playerUp,
                    playerFieldOfView);
            });
            if (_skipRequested) yield break;

            emoteController.StopCinematicEmote();
            gameplayCameraController.SetOrbit(playerForward, gameplayPitch);
            gameplayCameraController.GetFollowPose(
                out Vector3 gameplayPosition,
                out Quaternion gameplayRotation);

            Vector3 handoffStart = cinematicCamera.transform.position;
            Quaternion handoffStartRotation = cinematicCamera.transform.rotation;
            Vector3 handoffControlA = handoffStart + playerRight * 4.5f + playerUp * 0.75f;
            Vector3 handoffControlB = gameplayPosition + playerRight * 3.5f + playerUp * 1.4f;

            yield return Animate(gameplayHandoffDuration, gameplayHandoffVelocity, t =>
            {
                Vector3 position = CubicBezier(
                    handoffStart,
                    handoffControlA,
                    handoffControlB,
                    gameplayPosition,
                    t);
                cinematicCamera.transform.SetPositionAndRotation(
                    position,
                    Quaternion.Slerp(handoffStartRotation, gameplayRotation, t));
                cinematicCamera.fieldOfView = Mathf.Lerp(
                    playerFieldOfView,
                    _gameplayFieldOfView,
                    t);
            });
        }

        private IEnumerator Animate(float duration, AnimationCurve velocity, Action<float> apply)
        {
            float safeDuration = Mathf.Max(0.0001f, duration);
            float elapsed = 0f;
            apply(Evaluate(velocity, 0f));

            while (elapsed < safeDuration && !_skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / safeDuration);
                apply(Evaluate(velocity, normalizedTime));
                yield return null;
            }

            if (!_skipRequested)
            {
                apply(Evaluate(velocity, 1f));
            }
        }

        private void CompleteCutscene(bool skipped)
        {
            RestoreShadowDistance();

            if (_completed)
            {
                return;
            }

            _completed = true;
            emoteController.StopCinematicEmote();

            Vector3 playerUp = SafeDirection(
                playerController.transform.position - planetCenter.position,
                playerController.transform.up);
            Vector3 playerForward = ProjectTangent(playerController.transform.forward, playerUp);
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

            _playing = false;
            if (skipped)
            {
                Debug.Log("Opening cutscene skipped; gameplay camera and controls restored.", this);
            }
        }

        private void OverrideShadowDistance()
        {
            if (_shadowDistanceOverridden)
            {
                return;
            }

            UniversalRenderPipelineAsset pipelineAsset = UniversalRenderPipeline.asset;
            if (pipelineAsset == null)
            {
                return;
            }

            _shadowPipelineAsset = pipelineAsset;
            _previousShadowDistance = pipelineAsset.shadowDistance;
            pipelineAsset.shadowDistance = Mathf.Max(_previousShadowDistance, cutsceneShadowDistance);
            _shadowDistanceOverridden = true;
        }

        private void RestoreShadowDistance()
        {
            if (!_shadowDistanceOverridden)
            {
                return;
            }

            UniversalRenderPipelineAsset pipelineAsset = _shadowPipelineAsset;
            float previousShadowDistance = _previousShadowDistance;
            _shadowPipelineAsset = null;
            _shadowDistanceOverridden = false;

            if (pipelineAsset != null)
            {
                pipelineAsset.shadowDistance = previousShadowDistance;
            }
        }

        private void SetCameraPose(
            Vector3 position,
            Vector3 lookAt,
            Vector3 preferredScreenUp,
            float fieldOfView)
        {
            cinematicCamera.transform.SetPositionAndRotation(
                position,
                LookRotation(position, lookAt, preferredScreenUp));
            cinematicCamera.fieldOfView = fieldOfView;
        }

        private static Quaternion LookRotation(
            Vector3 position,
            Vector3 lookAt,
            Vector3 preferredScreenUp)
        {
            Vector3 forward = SafeDirection(lookAt - position, Vector3.forward);
            Vector3 screenUp = Vector3.ProjectOnPlane(preferredScreenUp, forward);
            if (screenUp.sqrMagnitude < 0.000001f)
            {
                Vector3 fallback = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < 0.98f
                    ? Vector3.up
                    : Vector3.forward;
                screenUp = Vector3.ProjectOnPlane(fallback, forward);
            }

            return Quaternion.LookRotation(forward, screenUp.normalized);
        }

        private static Vector3 DeriveBestTangent(Transform heading, Vector3 radialUp)
        {
            Vector3[] candidates =
            {
                heading.right,
                heading.up,
                heading.forward,
                Vector3.right,
                Vector3.forward
            };

            Vector3 best = Vector3.zero;
            float bestMagnitude = 0f;
            foreach (Vector3 candidate in candidates)
            {
                Vector3 tangent = Vector3.ProjectOnPlane(candidate, radialUp);
                if (tangent.sqrMagnitude > bestMagnitude)
                {
                    best = tangent;
                    bestMagnitude = tangent.sqrMagnitude;
                }
            }

            return bestMagnitude > 0.000001f
                ? best.normalized
                : ProjectTangent(Vector3.forward, radialUp);
        }

        private static Vector3 DeriveWordHorizontal(
            Transform wordRoot,
            Transform fallbackHeading,
            Vector3 radialUp)
        {
            Transform firstLetter = wordRoot.Find("N");
            Transform lastLetter = wordRoot.Find("T");
            if (firstLetter != null && lastLetter != null)
            {
                Vector3 firstCentre = TryGetRendererBounds(firstLetter, out Bounds firstBounds)
                    ? firstBounds.center
                    : firstLetter.position;
                Vector3 lastCentre = TryGetRendererBounds(lastLetter, out Bounds lastBounds)
                    ? lastBounds.center
                    : lastLetter.position;
                Vector3 acrossWord = Vector3.ProjectOnPlane(
                    lastCentre - firstCentre,
                    radialUp);
                if (acrossWord.sqrMagnitude >= 0.000001f)
                {
                    return acrossWord.normalized;
                }
            }

            return DeriveBestTangent(fallbackHeading, radialUp);
        }

        private static Vector3 ProjectTangent(Vector3 direction, Vector3 up)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(direction, up);
            if (tangent.sqrMagnitude >= 0.000001f)
            {
                return tangent.normalized;
            }

            Vector3 fallback = Mathf.Abs(Vector3.Dot(up, Vector3.forward)) < 0.98f
                ? Vector3.forward
                : Vector3.right;
            return Vector3.ProjectOnPlane(fallback, up).normalized;
        }

        private static Vector3 SafeDirection(Vector3 direction, Vector3 fallback)
        {
            return direction.sqrMagnitude >= 0.000001f
                ? direction.normalized
                : fallback.normalized;
        }

        private static Vector3 CubicBezier(
            Vector3 start,
            Vector3 controlA,
            Vector3 controlB,
            Vector3 end,
            float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * inverse * start +
                   3f * inverse * inverse * t * controlA +
                   3f * inverse * t * t * controlB +
                   t * t * t * end;
        }

        private static float Evaluate(AnimationCurve curve, float t)
        {
            return Mathf.Clamp01(curve != null ? curve.Evaluate(Mathf.Clamp01(t)) : t);
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }

        private static GameObject FindSceneRoot(Scene scene, string rootName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == rootName)
                {
                    return root;
                }
            }

            return null;
        }

        private static T FindSingleSceneComponent<T>(Scene scene) where T : Component
        {
            T match = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T[] candidates = root.GetComponentsInChildren<T>(true);
                foreach (T candidate in candidates)
                {
                    if (match != null)
                    {
                        return null;
                    }

                    match = candidate;
                }
            }

            return match;
        }

        private static AnimationCurve DefaultEase()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.42f, 0.24f, 1.4f, 1.4f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        private static AnimationCurve OpeningArcEase()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.2f, 0.055f, 0.35f, 0.35f),
                new Keyframe(0.56f, 0.43f, 1.15f, 1.15f),
                new Keyframe(0.84f, 0.86f, 1f, 1f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        private static AnimationCurve OpeningDollyEase()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.3f, 0.015f, 0.1f, 0.1f),
                new Keyframe(0.55f, 0.12f, 0.55f, 0.55f),
                new Keyframe(0.78f, 0.58f, 1.3f, 1.3f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        private static AnimationCurve OpeningAimEase()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.3f, 0.02f, 0.08f, 0.08f),
                new Keyframe(0.58f, 0.24f, 0.7f, 0.7f),
                new Keyframe(0.82f, 0.72f, 1.05f, 1.05f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        private static AnimationCurve NautZoomEase()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.25f, 0.08f, 0.35f, 0.35f),
                new Keyframe(0.6f, 0.52f, 1.25f, 1.25f),
                new Keyframe(0.84f, 0.9f, 0.7f, 0.7f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        private static AnimationCurve PlayerPanEase()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.32f, 0.1f, 0.45f, 0.45f),
                new Keyframe(0.67f, 0.62f, 1.25f, 1.25f),
                new Keyframe(0.88f, 0.93f, 0.45f, 0.45f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        private static AnimationCurve PlayerDiveEase()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.22f, 0.055f, 0.28f, 0.28f),
                new Keyframe(0.52f, 0.46f, 1.35f, 1.35f),
                new Keyframe(0.78f, 0.86f, 1.05f, 1.05f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        private static AnimationCurve WaveEase()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.3f, 0.075f, 0.3f, 0.3f),
                new Keyframe(0.64f, 0.55f, 1.05f, 1.05f),
                new Keyframe(0.86f, 0.91f, 0.48f, 0.48f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        private static AnimationCurve HandoffEase()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.24f, 0.06f, 0.3f, 0.3f),
                new Keyframe(0.55f, 0.46f, 1.25f, 1.25f),
                new Keyframe(0.8f, 0.89f, 0.85f, 0.85f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        private void EnsureVelocityCurves()
        {
            if (velocityProfileVersion < CurrentVelocityProfileVersion)
            {
                orbitVelocity = OpeningArcEase();
                planetDollyVelocity = OpeningDollyEase();
                planetAimVelocity = OpeningAimEase();
                baseReadVelocity = NautZoomEase();
                basePlayerPanVelocity = PlayerPanEase();
                playerDiveVelocity = PlayerDiveEase();
                waveCameraVelocity = WaveEase();
                gameplayHandoffVelocity = HandoffEase();
                aimVelocity = DefaultEase();
                velocityProfileVersion = CurrentVelocityProfileVersion;
                return;
            }

            if (orbitVelocity == null || orbitVelocity.length == 0)
                orbitVelocity = OpeningArcEase();
            if (planetDollyVelocity == null || planetDollyVelocity.length == 0)
                planetDollyVelocity = OpeningDollyEase();
            if (planetAimVelocity == null || planetAimVelocity.length == 0)
                planetAimVelocity = OpeningAimEase();
            if (baseReadVelocity == null || baseReadVelocity.length == 0)
                baseReadVelocity = NautZoomEase();
            if (basePlayerPanVelocity == null || basePlayerPanVelocity.length == 0)
                basePlayerPanVelocity = PlayerPanEase();
            if (playerDiveVelocity == null || playerDiveVelocity.length == 0)
                playerDiveVelocity = PlayerDiveEase();
            if (waveCameraVelocity == null || waveCameraVelocity.length == 0)
                waveCameraVelocity = WaveEase();
            if (gameplayHandoffVelocity == null || gameplayHandoffVelocity.length == 0)
                gameplayHandoffVelocity = HandoffEase();
            if (aimVelocity == null || aimVelocity.length == 0) aimVelocity = DefaultEase();
        }
    }
}

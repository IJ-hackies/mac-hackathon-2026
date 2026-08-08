using System.Collections;
using Audio;
using Combat;
using Player;
using UnityEngine;
using UnityEngine.UI;

namespace Enemies
{
    /// Orchestrates the Stage 1 -> Stage 2 boss transformation. Sits above both boss instances;
    /// the mech starts inert (scale 0, AI/collider disabled) and is only grown/activated once the
    /// astronaut's Health.Died fires, via a longer, four-phase scripted cutscene: player input and
    /// the normal camera rig are frozen for the whole thing, so it plays as one continuous camera
    /// move rather than a hard swap -
    ///   1. Linger: camera pushes in on the dissolving astronaut.
    ///   2. Pan: camera travels from the astronaut to the mech's feet (still 0-scale/invisible).
    ///   3. Grow: camera spirals multiple times around the mech, bottom to top, while it scales up
    ///      (0 -> targetScale with a slight overshoot) with screen flashes + a particle burst.
    ///   4. Reveal: the fully-grown mech performs its ground-slam (staggering the player, same as
    ///      the real attack), then the camera blends smoothly back to the normal follow view
    ///      before control is handed back.
    public class BossFightController : MonoBehaviour
    {
        // Checked by Audio.GameplayMusicController before switching away from bossMusic on an
        // area re-evaluation - true from the moment the Stage1->Stage2 cutscene starts, false once
        // the mech (Stage 2) actually dies (see BossMechAI.HandleDeath).
        public static bool BossFightActive { get; set; }

        [SerializeField] private Health astronautHealth;
        [SerializeField] private GameObject mechRoot;
        [SerializeField] private BossMechAI mechAi;
        [SerializeField] private Collider mechCollider;
        [SerializeField] private Vector3 mechTargetScale = new Vector3(4f, 4f, 4f);
        [Tooltip("Imported burst effect (e.g. Creepy Cat's Effect_02/Effect_07) played alongside " +
                 "the procedural burst/pillar at the start of the grow phase. Null = procedural only.")]
        [SerializeField] private GameObject importedBurstEffectPrefab;

        [Header("Phase 1 - Linger on Astronaut")]
        [SerializeField] private float astronautLingerDuration = 1.8f;
        [SerializeField] private float astronautLingerDistance = 2.5f;
        [SerializeField] private float astronautLingerHeight = 1.2f;

        [Header("Phase 2 - Pan to Mech")]
        [SerializeField] private float panToMechDuration = 1.5f;

        [Header("Phase 3 - Grow")]
        [SerializeField] private float growDuration = 6.5f;
        [Tooltip("Orbit angle 0 looks at the mech's face (SpawnModel's spawn rotation puts its " +
                 "forward down -Z, so a camera at -Z looking back at it is a front view); 180 " +
                 "looks at its back. Camera starts here (180 = back) and sweeps by " +
                 "cameraOrbitDegrees, matching \"circling from the back to the front.\"")]
        [SerializeField] private float cameraStartAngleDegrees = 180f;
        [Tooltip("Total degrees the camera sweeps while the mech grows. Must equal " +
                 "(360 - cameraStartAngleDegrees) plus a multiple of 360 so the sweep lands " +
                 "exactly back on the face (angle 0) by the end, however many extra loops happen " +
                 "along the way - e.g. start 180 + sweep 900 = 1080 = 3x360, ending at 0.")]
        [SerializeField] private float cameraOrbitDegrees = 900f;
        [Tooltip("Base camera distance, in world units per 1x of mech scale - multiplied by the " +
                 "mech's actual target scale below so the framing keeps up with however big the " +
                 "mech ends up being (it's spawned at 4x, towering over the astronaut's 2x - a " +
                 "fixed distance was way too close and mostly showed clipped-into geometry).")]
        [SerializeField] private float cameraDistance = 2.6f;
        // Matches BuildBossMech's fixed local CharacterController height (Assets/Editor/Enemies/
        // BossSceneSetup.cs) - used to approximate the mech's real world height so the spiral has
        // an actual "top of the mech" to end on instead of a guessed constant.
        private const float MechLocalHeight = 2.2f;

        [Header("Phase 4 - Reveal Jump")]
        [Tooltip("The normal ThirdPersonCameraController.Shake has no visible effect here (its " +
                 "LateUpdate isn't running while disabled for the cutscene), so this jitters " +
                 "Camera.main directly for roughly the duration of the mech's intro ground-slam.")]
        [SerializeField] private float introSlamCameraShakeDuration = 1.6f;
        [SerializeField] private float introSlamShakeMagnitude = 0.35f;

        [Header("Hand-back")]
        [Tooltip("Blends Camera.main from the cutscene pose to the normal follow pose before " +
                 "re-enabling ThirdPersonCameraController, instead of snapping/cutting to it.")]
        [SerializeField] private float cameraBlendBackDuration = 1.5f;

        [Header("Flash / Slow-mo")]
        [SerializeField] private int flashCount = 3;
        [SerializeField] private float flashInterval = 0.35f;
        [SerializeField] private float slowMotionScale = 0.35f;
        [SerializeField] private float slowMotionDuration = 0.6f;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int GroundedParam = Animator.StringToHash("Grounded");

        private PlayerController _playerController;
        private PlayerCombat _playerCombat;
        private PlayerAnimatorRelay _playerAnimatorRelay;
        private Animator _playerAnimator;
        private ThirdPersonCameraController _cameraController;
        private Camera _mainCamera;
        private Image _flashImage;

        private void Awake()
        {
            mechRoot.transform.localScale = Vector3.zero;
            if (mechAi != null) mechAi.enabled = false;
            if (mechCollider != null) mechCollider.enabled = false;

            _playerController = FindFirstObjectByType<PlayerController>();
            _playerCombat = FindFirstObjectByType<PlayerCombat>();
            _playerAnimatorRelay = FindFirstObjectByType<PlayerAnimatorRelay>();
            if (_playerController != null) _playerAnimator = _playerController.GetComponentInChildren<Animator>();
            _cameraController = FindFirstObjectByType<ThirdPersonCameraController>();
            _mainCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (astronautHealth != null) astronautHealth.Died += OnStage1Died;
        }

        private void OnDisable()
        {
            if (astronautHealth != null) astronautHealth.Died -= OnStage1Died;
        }

        private void OnStage1Died()
        {
            StartCoroutine(PlayTransitionCutscene());
        }

        private IEnumerator PlayTransitionCutscene()
        {
            BossFightActive = true;
            AudioHandle cutsceneLoopHandle = AudioManager.Instance.PlayLoop(SfxId.Boss1Cutscene);
            MusicManager.Instance.PlayMusic(MusicManager.Instance.bossMusic);

            if (_playerController != null) _playerController.enabled = false;
            if (_playerCombat != null) _playerCombat.enabled = false;
            if (_cameraController != null) _cameraController.enabled = false;

            // "Ensure everything is frozen during the cutscene" - covers any basic enemy
            // (EnemyFlyingAI/EnemySmallAI/EnemyLargeAI) that happens to be active in the scene
            // (they're SetActive(false) by default, but a test setup may have re-enabled them).
            // Excludes the mech - it's driven explicitly through this same coroutine (Phase 4's
            // PlayIntroSlam) - and the astronaut, which is already mid-death/being destroyed by
            // this point and has no Update loop left to gate anyway.
            var otherEnemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            foreach (var enemy in otherEnemies)
            {
                if (enemy == null || enemy == mechAi) continue;
                if (astronautHealth != null && enemy.gameObject == astronautHealth.gameObject) continue;
                enemy.SetFrozen(true);
            }

            // PlayerAnimatorRelay keeps driving Speed/Grounded off PlayerController every frame
            // regardless of PlayerController.enabled (disabling a component doesn't stop other
            // scripts from reading its public properties, and Relay isn't gated on that itself) -
            // so without also disabling it here, the Animator just freezes on whatever Speed value
            // PlayerController last reported (e.g. still "running") and keeps looping that
            // animation for the whole cutscene instead of settling to Idle.
            if (_playerAnimatorRelay != null) _playerAnimatorRelay.enabled = false;
            if (_playerAnimator != null)
            {
                _playerAnimator.SetFloat(SpeedParam, 0f);
                _playerAnimator.SetBool(GroundedParam, true);
            }

            EnsureFlashImage();

            Vector3 spawnPoint = mechRoot.transform.position;
            float scaleFactor = Mathf.Max(mechTargetScale.x, mechTargetScale.y, mechTargetScale.z);
            float scaledDistance = cameraDistance * scaleFactor;
            float mechWorldHeight = MechLocalHeight * mechTargetScale.y;
            float bottomHeight = mechWorldHeight * 0.08f;
            float topHeight = mechWorldHeight * 0.85f;

            // Captured once, before the astronaut's own EnemyBase dissolve-and-destroy coroutine
            // (running independently off Health.Died, same event this cutscene reacts to) has a
            // chance to finish and destroy the GameObject out from under us.
            Vector3 astronautPosition = astronautHealth != null ? astronautHealth.transform.position : spawnPoint;

            // Phase 1 - Linger: push in on the astronaut while it dissolves, instead of cutting
            // straight to the mech - gives the death itself a beat to read.
            Vector3 lingerCameraPos = astronautPosition + Vector3.up * astronautLingerHeight
                + Vector3.back * astronautLingerDistance;
            float lingerElapsed = 0f;
            while (lingerElapsed < astronautLingerDuration)
            {
                lingerElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(lingerElapsed / astronautLingerDuration);

                if (_mainCamera != null)
                {
                    // Slow push-in, not a static hold, so the linger still reads as camera motion.
                    Vector3 pos = Vector3.Lerp(
                        lingerCameraPos + Vector3.back * astronautLingerDistance * 0.5f, lingerCameraPos, t);
                    _mainCamera.transform.position = pos;
                    _mainCamera.transform.LookAt(astronautPosition + Vector3.up * astronautLingerHeight);
                }

                yield return null;
            }

            // Phase 2 - Pan: travel from the astronaut to the mech's spawn point (still invisible
            // at scale 0), landing exactly where the grow spiral's bottom-of-mech framing expects
            // (behind the mech - see cameraStartAngleDegrees).
            Vector3 spiralStartPos = spawnPoint + Vector3.up * bottomHeight
                + Quaternion.Euler(0f, cameraStartAngleDegrees, 0f) * new Vector3(0f, 0f, -scaledDistance);
            Vector3 panStartPos = _mainCamera != null ? _mainCamera.transform.position : lingerCameraPos;
            Quaternion panStartRot = _mainCamera != null ? _mainCamera.transform.rotation : Quaternion.identity;

            float panElapsed = 0f;
            while (panElapsed < panToMechDuration)
            {
                panElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(panElapsed / panToMechDuration);
                float eased = 1f - Mathf.Pow(1f - t, 2f);

                if (_mainCamera != null)
                {
                    Vector3 pos = Vector3.Lerp(panStartPos, spiralStartPos, eased);
                    Quaternion targetRot = Quaternion.LookRotation(
                        (spawnPoint + Vector3.up * bottomHeight - pos).normalized, Vector3.up);
                    _mainCamera.transform.position = pos;
                    _mainCamera.transform.rotation = Quaternion.Slerp(panStartRot, targetRot, eased);
                }

                yield return null;
            }

            // Phase 3 - Grow: spiral (potentially several full loops - cameraOrbitDegrees can
            // exceed 360) from the mech's feet up to its head while it scales up. Flash/particles
            // start here, not at the top of the cutscene, so they land on the actual reveal
            // instead of firing off-screen while the camera is still lingering on the astronaut.
            StartCoroutine(FlashRoutine());
            SpawnBurstParticles();

            float elapsed = 0f;
            bool slowed = false;

            while (elapsed < growDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / growDuration);

                if (_mainCamera != null)
                {
                    float angle = cameraStartAngleDegrees + cameraOrbitDegrees * t;
                    float height = Mathf.Lerp(bottomHeight, topHeight, t);
                    Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, -scaledDistance);
                    _mainCamera.transform.position = spawnPoint + Vector3.up * height + offset;
                    _mainCamera.transform.LookAt(spawnPoint + Vector3.up * height);
                }

                mechRoot.transform.localScale = mechTargetScale * EaseOvershoot(t);

                if (!slowed && t > 0.35f)
                {
                    slowed = true;
                    StartCoroutine(SlowMotionPulse());
                }

                yield return null;
            }

            mechRoot.transform.localScale = mechTargetScale;

            // Phase 4 - Reveal: camera holds framed on the now-fully-grown mech's head while it plays
            // its ground-slam - the same attack the player will see again during the real fight,
            // introduced here as the "and it doing a jump causing the player to stagger" payoff.
            Vector3 impactCameraPos = spawnPoint + Vector3.up * topHeight
                + Quaternion.Euler(0f, cameraStartAngleDegrees + cameraOrbitDegrees, 0f) * new Vector3(0f, 0f, -scaledDistance);
            Vector3 impactLookAt = spawnPoint + Vector3.up * topHeight;

            if (cutsceneLoopHandle.IsValid) AudioManager.Instance.StopLoop(cutsceneLoopHandle);

            // Roar lands right as the camera shake/ground-slam reveal begins - the "shakes and
            // roars" beat of the stage1->stage2 transformation.
            AudioManager.Instance.PlaySfx(SfxId.Boss2Roar, mechRoot.transform.position);

            if (mechCollider != null) mechCollider.enabled = true;
            StartCoroutine(ShakeCameraAt(impactCameraPos, impactLookAt, introSlamCameraShakeDuration));
            if (mechAi != null)
            {
                yield return StartCoroutine(mechAi.PlayIntroSlam());
            }

            // Hand-back: blend Camera.main to wherever the normal follow camera would already be
            // (player hasn't moved - input's been locked this whole time) before re-enabling it,
            // so control returns as a smooth pan rather than a jarring cut.
            if (_cameraController != null && _mainCamera != null)
            {
                Vector3 blendStartPos = _mainCamera.transform.position;
                Quaternion blendStartRot = _mainCamera.transform.rotation;
                float blendElapsed = 0f;

                while (blendElapsed < cameraBlendBackDuration)
                {
                    blendElapsed += Time.unscaledDeltaTime;
                    float bt = Mathf.Clamp01(blendElapsed / cameraBlendBackDuration);
                    _cameraController.GetFollowPose(out Vector3 targetPos, out Quaternion targetRot);
                    _mainCamera.transform.position = Vector3.Lerp(blendStartPos, targetPos, bt);
                    _mainCamera.transform.rotation = Quaternion.Slerp(blendStartRot, targetRot, bt);
                    yield return null;
                }

                _cameraController.enabled = true;
            }

            if (_playerController != null) _playerController.enabled = true;
            if (_playerCombat != null) _playerCombat.enabled = true;
            if (_playerAnimatorRelay != null) _playerAnimatorRelay.enabled = true;
            if (mechAi != null) mechAi.enabled = true;

            foreach (var enemy in otherEnemies)
            {
                if (enemy == null || enemy == mechAi) continue;
                if (astronautHealth != null && enemy.gameObject == astronautHealth.gameObject) continue;
                enemy.SetFrozen(false);
            }
        }

        private IEnumerator ShakeCameraAt(Vector3 basePosition, Vector3 lookAt, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && _mainCamera != null)
            {
                elapsed += Time.unscaledDeltaTime;
                Vector3 jitter = Random.insideUnitSphere * introSlamShakeMagnitude;
                _mainCamera.transform.position = basePosition + jitter;
                _mainCamera.transform.LookAt(lookAt);
                yield return null;
            }
        }

        // Slight overshoot ("punch") past 1 before settling - a hand-rolled ease since the
        // project has no tweening dependency (see Context/Chunks conventions: no external plugins).
        private static float EaseOvershoot(float t)
        {
            const float overshoot = 1.15f;
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float bump = Mathf.Sin(t * Mathf.PI) * (overshoot - 1f) * (1f - t);
            return eased + bump;
        }

        private IEnumerator SlowMotionPulse()
        {
            float originalScale = Time.timeScale;
            Time.timeScale = slowMotionScale;
            yield return new WaitForSecondsRealtime(slowMotionDuration);
            Time.timeScale = originalScale;
        }

        private IEnumerator FlashRoutine()
        {
            for (int i = 0; i < flashCount; i++)
            {
                _flashImage.color = new Color(1f, 1f, 1f, 0.85f);
                yield return new WaitForSecondsRealtime(0.05f);
                float elapsed = 0f;
                while (elapsed < flashInterval)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float alpha = Mathf.Lerp(0.85f, 0f, elapsed / flashInterval);
                    _flashImage.color = new Color(1f, 1f, 1f, alpha);
                    yield return null;
                }
            }
        }

        // Built at runtime the same no-external-asset way the rest of the HUD is built
        // (PlayerSceneSetup.BuildUI) - a full-screen white Image toggled a few times.
        private void EnsureFlashImage()
        {
            if (_flashImage != null) return;

            var canvasGo = new GameObject("BossTransitionFlash", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var imageGo = new GameObject("Flash", typeof(Image));
            imageGo.transform.SetParent(canvasGo.transform, false);
            var rect = imageGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _flashImage = imageGo.GetComponent<Image>();
            _flashImage.color = new Color(1f, 1f, 1f, 0f);
            _flashImage.raycastTarget = false;
        }

        private static Material CreateParticleMaterial()
        {
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(particleShader);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f); // transparent
            return material;
        }

        // A single clean burst at the reveal moment - prefers the imported effect (real authored
        // VFX art) and only falls back to a procedural one if that asset isn't available. Used to
        // be two procedural systems (an outward burst plus a continuously-emitting rising pillar
        // sustained for the whole ~6.5s grow phase) - the pillar read as sparks that "kept
        // occurring" for no clear reason rather than a single deliberate reveal beat, so it's gone.
        private void SpawnBurstParticles()
        {
            float scaleFactor = Mathf.Max(mechTargetScale.x, mechTargetScale.y, mechTargetScale.z);
            Vector3 basePosition = mechRoot.transform.position + Vector3.up * scaleFactor;

            if (importedBurstEffectPrefab != null)
            {
                var imported = Instantiate(importedBurstEffectPrefab, basePosition, Quaternion.identity);
                imported.transform.localScale = Vector3.one * scaleFactor * 0.6f;
                Vfx.ImportedVfxUtility.FixUrpMaterials(imported);
                Destroy(imported, 4f);
                return;
            }

            var burstGo = new GameObject("BossTransitionBurst");
            burstGo.transform.position = basePosition;

            var burstPs = burstGo.AddComponent<ParticleSystem>();
            var burstMain = burstPs.main;
            burstMain.duration = 1f;
            burstMain.loop = false;
            burstMain.startLifetime = 1.8f;
            burstMain.startSpeed = 5f * scaleFactor;
            burstMain.startSize = 0.5f * scaleFactor;
            burstMain.startColor = new Color(1f, 0.85f, 0.3f);
            burstMain.simulationSpace = ParticleSystemSimulationSpace.World;
            burstMain.gravityModifier = 0.15f;

            var burstEmission = burstPs.emission;
            burstEmission.rateOverTime = 0f;
            burstEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 90) });

            var burstShape = burstPs.shape;
            burstShape.shapeType = ParticleSystemShapeType.Sphere;
            burstShape.radius = 0.6f * scaleFactor;

            var burstColorOverLifetime = burstPs.colorOverLifetime;
            burstColorOverLifetime.enabled = true;
            var burstGradient = new Gradient();
            burstGradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            burstColorOverLifetime.color = burstGradient;

            burstGo.GetComponent<ParticleSystemRenderer>().material = CreateParticleMaterial();
            Destroy(burstGo, 2.5f);
        }
    }
}

using System;
using System.Collections.Generic;
using Gameplay.Areas;
using Gameplay.Interaction;
using Player;
using Player.UI;
using Player.UI.Waves;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Gameplay.Waves
{
    /// <summary>
    /// Scene-facing adapter for the wave state machine. It owns player input, perimeter locks,
    /// HUD presentation, arena entry forwarding, and the game-over scene flow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveGameController : MonoBehaviour
    {
        private const float StartHoldDuration = 1f;

        [Header("Runtime")]
        [SerializeField] private WaveDirector director;
        [SerializeField] private PlayerAreaTracker areaTracker;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private ThirdPersonCameraController cameraController;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private SettingsMenuController settingsMenu;
        [SerializeField] private StationMenuController stationMenu;
        [SerializeField] private CrosshairUI crosshair;
        [SerializeField] private WaveAreaBarrier[] barriers = Array.Empty<WaveAreaBarrier>();

        [Header("Wave UI")]
        [SerializeField] private WaveHudView waveHud;
        [SerializeField] private IntermissionPromptView intermissionPrompt;
        [SerializeField] private ArenaNavigationView arenaNavigation;
        [SerializeField] private ArenaSealSweepView arenaSeal;
        [SerializeField] private ArenaObjectiveView arenaObjective;
        [SerializeField] private GameOverMissionSummaryView gameOver;

        private InputSystem_Actions _actions;
        private InputAction _startWaveAction;
        private float _holdElapsed;
        private Transform _guidanceTarget;
        private bool _gameOverOwnsCursor;
        private Vector3 _baseTeleportPosition;
        private Quaternion _baseTeleportRotation;
        private bool _hasBaseTeleportPose;

        public float StartHoldProgress => Mathf.Clamp01(_holdElapsed / StartHoldDuration);
        public bool CanTeleportToBase =>
            _hasBaseTeleportPose &&
            director != null &&
            IsTeleportAllowedDuring(director.Phase);

        private void Awake()
        {
            EnsureRuntimeState();
            InitializePresentation();
        }

        private void OnEnable()
        {
            EnsureRuntimeState();
            _actions.Player.Enable();
            Subscribe();
            RefreshBindingLabel();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _actions?.Player.Disable();
        }

        private void OnDestroy()
        {
            if (_actions != null)
            {
                PlayerInputBindings.ReleaseActions(_actions);
                _actions = null;
                _startWaveAction = null;
            }
        }

        private void EnsureRuntimeState()
        {
            ResolveReferences();
            if (!_hasBaseTeleportPose) CaptureBaseTeleportPose();
            if (_actions == null) _actions = PlayerInputBindings.CreateActions();
            if (_startWaveAction == null) _startWaveAction = _actions.Player.StartWave;
        }

        private void Update()
        {
            UpdateStartHold();
            RefreshPresentation();
        }

        public void Configure(
            WaveDirector configuredDirector,
            PlayerAreaTracker configuredTracker,
            PlayerController configuredPlayer,
            ThirdPersonCameraController configuredCamera,
            Transform configuredPlanetCenter,
            SettingsMenuController configuredSettings,
            StationMenuController configuredStationMenu,
            CrosshairUI configuredCrosshair,
            IReadOnlyList<WaveAreaBarrier> configuredBarriers,
            WaveHudView configuredWaveHud,
            IntermissionPromptView configuredIntermission,
            ArenaNavigationView configuredNavigation,
            ArenaSealSweepView configuredSeal,
            ArenaObjectiveView configuredObjective,
            GameOverMissionSummaryView configuredGameOver)
        {
            director = configuredDirector;
            areaTracker = configuredTracker;
            playerController = configuredPlayer;
            cameraController = configuredCamera;
            planetCenter = configuredPlanetCenter;
            settingsMenu = configuredSettings;
            stationMenu = configuredStationMenu;
            crosshair = configuredCrosshair;
            barriers = configuredBarriers == null ? Array.Empty<WaveAreaBarrier>() : CopyBarriers(configuredBarriers);
            ConfigureWaveViews(
                configuredWaveHud,
                configuredIntermission,
                configuredNavigation,
                configuredSeal,
                configuredObjective,
                configuredGameOver);
        }

        public void ConfigureWaveViews(
            WaveHudView configuredWaveHud,
            IntermissionPromptView configuredIntermission,
            ArenaNavigationView configuredNavigation,
            ArenaSealSweepView configuredSeal,
            ArenaObjectiveView configuredObjective,
            GameOverMissionSummaryView configuredGameOver)
        {
            waveHud = configuredWaveHud;
            intermissionPrompt = configuredIntermission;
            arenaNavigation = configuredNavigation;
            arenaSeal = configuredSeal;
            arenaObjective = configuredObjective;
            gameOver = configuredGameOver;
        }

        public static bool IsTeleportAllowedDuring(WavePhase phase)
        {
            return phase == WavePhase.Intermission;
        }

        public bool TryTeleportToBase()
        {
            if (!CanTeleportToBase || playerController == null)
            {
                return false;
            }

            playerController.TeleportToSurface(_baseTeleportPosition, _baseTeleportRotation);
            areaTracker?.EvaluateCurrentArea();
            cameraController?.SnapToFollowPose();
            return true;
        }

        private void ResolveReferences()
        {
            if (director == null) director = FindFirstObjectByType<WaveDirector>();
            if (areaTracker == null) areaTracker = GetComponent<PlayerAreaTracker>() ?? FindFirstObjectByType<PlayerAreaTracker>();
            if (playerController == null) playerController = GetComponentInChildren<PlayerController>(true);
            if (cameraController == null) cameraController = GetComponentInChildren<ThirdPersonCameraController>(true);
            if (settingsMenu == null) settingsMenu = GetComponent<SettingsMenuController>();
            if (stationMenu == null) stationMenu = GetComponentInChildren<StationMenuController>(true);
            if (crosshair == null) crosshair = GetComponentInChildren<CrosshairUI>(true);
            if (planetCenter == null)
            {
                GameObject planet = GameObject.Find("Planet Ground");
                planetCenter = planet != null ? planet.transform : null;
            }
            if (barriers == null || barriers.Length == 0)
            {
                barriers = FindObjectsByType<WaveAreaBarrier>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }
        }

        private void CaptureBaseTeleportPose()
        {
            if (playerController == null)
            {
                return;
            }

            _baseTeleportPosition = playerController.transform.position;
            _baseTeleportRotation = playerController.transform.rotation;
            _hasBaseTeleportPose = true;
        }

        private void Subscribe()
        {
            if (director != null)
            {
                director.PhaseChanged += OnPhaseChanged;
                director.AreaLockChanged += OnAreaLockChanged;
                director.ArenaTravelRequested += OnArenaTravelRequested;
                director.RunEnded += OnRunEnded;
            }
            if (areaTracker != null) areaTracker.AreaEntered += OnAreaEntered;
            if (gameOver != null)
            {
                gameOver.RestartRequested += RestartRun;
                gameOver.MainMenuRequested += ReturnToMainMenu;
            }
            PlayerInputBindings.BindingsChanged += RefreshBindingLabel;
        }

        private void Unsubscribe()
        {
            if (director != null)
            {
                director.PhaseChanged -= OnPhaseChanged;
                director.AreaLockChanged -= OnAreaLockChanged;
                director.ArenaTravelRequested -= OnArenaTravelRequested;
                director.RunEnded -= OnRunEnded;
            }
            if (areaTracker != null) areaTracker.AreaEntered -= OnAreaEntered;
            if (gameOver != null)
            {
                gameOver.RestartRequested -= RestartRun;
                gameOver.MainMenuRequested -= ReturnToMainMenu;
            }
            PlayerInputBindings.BindingsChanged -= RefreshBindingLabel;
        }

        private void InitializePresentation()
        {
            waveHud?.SetVisible(true);
            intermissionPrompt?.SetIntermissionVisible(true);
            arenaNavigation?.SetActive(false);
            arenaSeal?.Stop();
            arenaObjective?.SetVisible(false);
            gameOver?.Hide();
            foreach (WaveAreaBarrier barrier in barriers)
            {
                barrier?.SetLocked(false);
            }
            RefreshPresentation();
        }

        private void UpdateStartHold()
        {
            bool canStart = CanStartFromCurrentState();
            bool holding = canStart && _startWaveAction != null && _startWaveAction.IsPressed();
            if (!holding)
            {
                _holdElapsed = 0f;
                intermissionPrompt?.SetHoldProgress(0f, false);
                return;
            }

            _holdElapsed = Mathf.Min(StartHoldDuration, _holdElapsed + Time.deltaTime);
            intermissionPrompt?.SetHoldProgress(StartHoldProgress, true);
            if (_holdElapsed < StartHoldDuration)
            {
                return;
            }

            _holdElapsed = 0f;
            if (director != null && director.TryStartNextWave())
            {
                intermissionPrompt?.SetHoldProgress(0f, false);
            }
        }

        private bool CanStartFromCurrentState()
        {
            if (director == null || director.Phase != WavePhase.Intermission || Time.timeScale <= 0f)
            {
                return false;
            }
            if (areaTracker != null && areaTracker.CurrentArea != null)
            {
                return false;
            }
            if (playerController == null || !playerController.enabled)
            {
                return false;
            }
            return (settingsMenu == null || !settingsMenu.IsOpen) && (stationMenu == null || !stationMenu.IsOpen);
        }

        private void RefreshPresentation()
        {
            if (director == null)
            {
                return;
            }

            bool intermission = director.Phase == WavePhase.Intermission;
            bool startPositionValid = areaTracker == null || areaTracker.CurrentArea == null;
            intermissionPrompt?.SetIntermissionVisible(intermission);
            intermissionPrompt?.SetStartAllowed(startPositionValid);

            int displayedWave = intermission ? director.CurrentWave + 1 : director.CurrentWave;
            waveHud?.SetWave(displayedWave);
            waveHud?.SetWaveState(PhaseLabel(director.Phase, director.CurrentKind));
            if (director.Phase == WavePhase.Regular)
                waveHud?.SetTimer(director.PhaseRemaining, WaveRules.RegularDurationForWave(director.CurrentWave));
            else
                waveHud?.SetTimer(0f, 0f);

            if (director.Phase == WavePhase.ArenaCombat && director.CurrentKind == WaveKind.Arena1)
            {
                // Arena spawns can wait for a safe point while the barrier and environment
                // colliders settle. Queued objectives are still alive, so derive this from the
                // director's objective ledger rather than its instantiated-enemy list.
                int remaining = director.ArenaObjectivesRemaining;
                int total = director.ArenaObjectivesTotal;
                arenaObjective?.SetArena1Progress(total - remaining, remaining);
            }
            else if (director.Phase == WavePhase.ArenaCombat && director.CurrentKind == WaveKind.Arena2)
            {
                Combat.Health bossHealth = director.ActiveArenaBossHealth;
                arenaObjective?.SetArena2Health(
                    Enemies.BossFightController.BossFightActive ? "STAGE 2 // MECH" : "STAGE 1 // ASTRONAUT",
                    bossHealth != null ? bossHealth.CurrentHealth : 0f,
                    bossHealth != null ? bossHealth.MaxHealth : 0f);
            }
            else if (director.Phase != WavePhase.ArenaSeal)
            {
                arenaObjective?.SetVisible(false);
            }
        }

        private void OnPhaseChanged(WavePhase previous, WavePhase current)
        {
            if (current == WavePhase.ArenaSeal)
            {
                arenaNavigation?.SetActive(false);
                arenaObjective?.SetObjective(
                    director.CurrentKind == WaveKind.Arena1 ? "ARENA 1 // SWARM" : "ARENA 2 // BOSS",
                    "PERIMETER SEALING",
                    "HOSTILES INBOUND");
                arenaSeal?.Play(WaveRules.ArenaSealDuration);
            }
            else if (current == WavePhase.Intermission)
            {
                arenaNavigation?.SetActive(false);
                arenaObjective?.SetVisible(false);
                arenaSeal?.Stop();
            }
        }

        private void OnAreaLockChanged(GameplayArea area, bool locked)
        {
            if (area == null || barriers == null)
            {
                return;
            }
            foreach (WaveAreaBarrier barrier in barriers)
            {
                if (barrier != null && barrier.Area == area)
                {
                    barrier.SetLocked(locked);
                }
            }
        }

        private void OnArenaTravelRequested(GameplayArea arena)
        {
            if (arena == null || arenaNavigation == null)
            {
                return;
            }

            Transform target = BuildGuidanceTarget(arena);
            arenaNavigation.SetTarget(
                target,
                arena.AreaId == GameplayAreaId.Arena1 ? "ARENA 1" : "ARENA 2",
                playerController != null ? playerController.transform : null,
                planetCenter);
        }

        private void OnAreaEntered(GameplayArea area)
        {
            director?.NotifyArenaEntered(area);
        }

        private void OnRunEnded(WaveRunResult result)
        {
            arenaNavigation?.SetActive(false);
            arenaObjective?.SetVisible(false);
            intermissionPrompt?.SetIntermissionVisible(false);
            crosshair?.SetVisible(false);
            if (cameraController != null) cameraController.enabled = false;

            gameOver?.Show(new WaveRunSummary(result.WaveReached, result.Kills, result.GoldEarned, result.Duration));
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _gameOverOwnsCursor = true;
            Time.timeScale = 0f;
        }

        private void RestartRun()
        {
            ReleaseGameOverOwnership();
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        private void ReturnToMainMenu()
        {
            ReleaseGameOverOwnership();
            SceneManager.LoadScene("MainMenu");
        }

        private void ReleaseGameOverOwnership()
        {
            Time.timeScale = 1f;
            if (_gameOverOwnsCursor)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                _gameOverOwnsCursor = false;
            }
        }

        private Transform BuildGuidanceTarget(GameplayArea arena)
        {
            if (_guidanceTarget == null)
            {
                var targetObject = new GameObject("Arena Guidance Target (Runtime)");
                _guidanceTarget = targetObject.transform;
                _guidanceTarget.SetParent(transform, true);
            }

            if (arena.Entrance != null)
            {
                _guidanceTarget.SetPositionAndRotation(
                    arena.Entrance.position,
                    arena.Entrance.rotation);
                return _guidanceTarget;
            }

            Transform poles = arena.PerimeterPoles;
            if (poles == null || poles.childCount == 0 || arena.PlanetCenter == null)
            {
                _guidanceTarget.SetPositionAndRotation(arena.transform.position, arena.transform.rotation);
                return _guidanceTarget;
            }

            Vector3 center = arena.PlanetCenter.position;
            Vector3 directionSum = Vector3.zero;
            float radiusSum = 0f;
            for (int index = 0; index < poles.childCount; index++)
            {
                Vector3 radial = poles.GetChild(index).position - center;
                directionSum += radial.normalized;
                radiusSum += radial.magnitude;
            }
            Vector3 direction = directionSum.normalized;
            _guidanceTarget.position = center + direction * (radiusSum / poles.childCount);
            _guidanceTarget.rotation = Quaternion.FromToRotation(Vector3.up, direction);
            return _guidanceTarget;
        }

        private void RefreshBindingLabel()
        {
            if (_startWaveAction == null || intermissionPrompt == null)
            {
                return;
            }
            string label = _startWaveAction.GetBindingDisplayString().ToUpperInvariant();
            intermissionPrompt.SetBindingLabel(string.IsNullOrWhiteSpace(label) ? "F" : label);
        }

        private static string PhaseLabel(WavePhase phase, WaveKind kind)
        {
            switch (phase)
            {
                case WavePhase.Intermission: return "PREPARATION // READY";
                case WavePhase.Regular: return "SURVIVE // PERIMETERS SEALED";
                case WavePhase.ArenaTravel: return kind == WaveKind.Arena1 ? "REACH ARENA 1" : "REACH ARENA 2";
                case WavePhase.ArenaSeal: return "PERIMETER SEALING";
                case WavePhase.ArenaCombat: return kind == WaveKind.Arena1 ? "CLEAR THE SWARM" : "DEFEAT BARBARA";
                case WavePhase.GameOver: return "RUN TERMINATED";
                default: return string.Empty;
            }
        }

        private static WaveAreaBarrier[] CopyBarriers(IReadOnlyList<WaveAreaBarrier> source)
        {
            var result = new WaveAreaBarrier[source.Count];
            for (int index = 0; index < source.Count; index++) result[index] = source[index];
            return result;
        }
    }
}

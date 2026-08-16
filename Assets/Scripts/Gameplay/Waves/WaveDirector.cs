using System;
using System.Collections.Generic;
using Combat;
using Enemies;
using Gameplay.Areas;
using Items;
using Player.UI.Progression;
using UnityEngine;

namespace Gameplay.Waves
{
    /// <summary>
    /// Run-scoped wave state machine. UI, input, barriers, and enemy AI integrate through events;
    /// this component deliberately owns no scene-specific assumptions beyond supplied areas/prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveDirector : MonoBehaviour
    {
        private const float EnemySizeMultiplier = 3f;
        private const float ArenaSpawnRetryDelay = .25f;

        [Header("Run References")]
        [SerializeField] private Transform player;
        [SerializeField] private Health playerHealth;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private PlayerProgression progression;
        [SerializeField] private GameplayArea landingBase;
        [SerializeField] private GameplayArea arena1;
        [SerializeField] private GameplayArea arena2;
        [SerializeField] private WaveSurfaceSpawnSampler surfaceSampler = new WaveSurfaceSpawnSampler();

        [Header("Optional Direct Spawn Prefabs")]
        [SerializeField] private GameObject smallEnemyPrefab;
        [SerializeField] private GameObject flyingEnemyPrefab;
        [SerializeField] private GameObject largeEnemyPrefab;
        [SerializeField] private GameObject arena2BossPrefab;
        [SerializeField] private bool instantiateAssignedPrefabs = true;

        [Header("Special Skill Pickups")]
        [SerializeField] private GameObject healthPickupPrefab;
        [SerializeField] private GameObject ammoPickupPrefab;
        [SerializeField] private GameObject thunderPickupPrefab;
        [SerializeField] private WavePickupSpawner pickupSpawner = new WavePickupSpawner();

        [Header("Coin VFX")]
        [Tooltip("Purely cosmetic - the gold amount is already added instantly by AwardGold. " +
                 "Null = no coin burst, kills still award gold silently.")]
        [SerializeField] private GameObject coinPickupPrefab;
        [SerializeField, Range(1, 10)] private int minCoinsPerDrop = 2;
        [SerializeField, Range(1, 10)] private int maxCoinsPerDrop = 6;
        [Tooltip("How much gold one visual coin represents, for scaling burst count with reward.")]
        [SerializeField, Min(1)] private int goldPerCoin = 10;
        [Tooltip("Fixed, larger burst for the Arena 2 boss kill specifically - a bigger fireworks " +
                 "moment than a regular enemy's reward-scaled burst.")]
        [SerializeField, Range(4, 24)] private int bossCoinCount = 16;

        [Header("Recycling")]
        [SerializeField, Min(20f)] private float recycleDistance = 90f;

        private readonly List<WaveEnemyHandle> _activeEnemies = new List<WaveEnemyHandle>();
        private readonly List<WaveEnemyType> _regularDeck = new List<WaveEnemyType>();
        private readonly List<WaveEnemyType> _pendingArenaSpawns = new List<WaveEnemyType>();
        private readonly HashSet<GameplayArea> _lockedAreas = new HashSet<GameplayArea>();
        private readonly Dictionary<int, WaveSpawnFootprint> _prefabFootprints = new Dictionary<int, WaveSpawnFootprint>();
        private float _phaseRemaining;
        private float _spawnTimer;
        private float _nextArenaSpawnAttemptAt;
        private float _runStartedAt;
        private bool _regularExpiryPending;
        private bool _arenaCompletionPending;
        private bool _playerDead;
        private int _arenaObjectivesRemaining;
        private int _arenaObjectivesTotal;
        private int _deckIndex;
        private Health _arenaBossStageOneHealth;
        private Health _arenaBossStageTwoHealth;

        public WavePhase Phase { get; private set; } = WavePhase.Intermission;
        public WaveKind CurrentKind { get; private set; } = WaveKind.Regular;
        public int CurrentWave { get; private set; }
        public int Kills { get; private set; }
        public int GoldEarned { get; private set; }
        public int Gold => progression != null ? progression.Gold : WaveRules.StartingGold + GoldEarned;
        public int Score => ScoreRules.Score(CurrentWave, Kills, GoldEarned);
        public int ActiveSpecialPickupCount => pickupSpawner != null ? pickupSpawner.ActiveCount : 0;
        public float RunDuration => Mathf.Max(0f, Time.time - _runStartedAt);
        public float PhaseRemaining => _phaseRemaining;
        public IReadOnlyList<WaveEnemyHandle> ActiveEnemies => _activeEnemies;
        /// <summary>
        /// Includes arena enemies that have been queued but are still waiting for a collision-safe
        /// spawn point. HUDs must use this instead of the active list so a queued swarm is never
        /// displayed as already defeated.
        /// </summary>
        public int ArenaObjectivesRemaining => Mathf.Max(0, _arenaObjectivesRemaining);
        public int ArenaObjectivesTotal => Mathf.Max(0, _arenaObjectivesTotal);
        public Health ActiveArenaBossHealth =>
            _arenaBossStageTwoHealth != null && (_arenaBossStageOneHealth == null || _arenaBossStageOneHealth.IsDead)
                ? _arenaBossStageTwoHealth
                : _arenaBossStageOneHealth;
        public bool IsAreaLocked(GameplayArea area) => area != null && _lockedAreas.Contains(area);

        public event Action<WavePhase, WavePhase> PhaseChanged;
        public event Action<int, WaveKind> WaveStarted;
        public event Action<int, WaveKind> WaveCompleted;
        public event Action<GameplayArea, bool> AreaLockChanged;
        public event Action<GameplayArea> ArenaTravelRequested;
        public event Action<WaveSpawnRequest> SpawnRequested;
        public event Action<WaveEnemyHandle> EnemyRecycled;
        public event Action<int, int> GoldChanged;
        public event Action<WaveRunResult> RunEnded;

        private void Awake()
        {
            if (pickupSpawner == null) pickupSpawner = new WavePickupSpawner();
            if (playerHealth == null && player != null) playerHealth = player.GetComponentInChildren<Health>();
            if (progression == null && player != null) progression = player.GetComponentInChildren<PlayerProgression>();
            if (gameplayCamera == null) gameplayCamera = Camera.main;
            if (surfaceSampler != null && planetCenter != null) surfaceSampler.ConfigurePlanet(planetCenter);
            BeginNewRun();
        }
        private void OnEnable() { if (playerHealth != null) playerHealth.Died += NotifyPlayerDied; }
        private void OnDisable() { if (playerHealth != null) playerHealth.Died -= NotifyPlayerDied; }
        private void Update()
        {
            UpdateEnemyAggro();
            if (Phase == WavePhase.Regular)
            {
                _phaseRemaining = Mathf.Max(0f, _phaseRemaining - Time.deltaTime);
                _spawnTimer -= Time.deltaTime;
                while (_spawnTimer <= 0f && _activeEnemies.Count < WaveRules.ActiveCap(CurrentWave))
                {
                    RequestRegularSpawn();
                    _spawnTimer += WaveRules.SpawnInterval(CurrentWave);
                }
                RecycleDistantEnemies();
                if (_phaseRemaining <= 0f) _regularExpiryPending = true;
            }
            else if (Phase == WavePhase.ArenaSeal)
            {
                _phaseRemaining = Mathf.Max(0f, _phaseRemaining - Time.deltaTime);
                if (_phaseRemaining <= 0f) StartArenaCombat();
            }
            else if (Phase == WavePhase.ArenaCombat)
            {
                TrySpawnPendingArenaObjectives();
                if (_arenaObjectivesRemaining == 0 && _pendingArenaSpawns.Count == 0) _arenaCompletionPending = true;
            }
        }
        private void LateUpdate()
        {
            GameplayArea[] protectedAreas = AllAreas();
            foreach (WaveEnemyHandle enemy in _activeEnemies)
                if (enemy != null) enemy.KeepOutsideAreas(protectedAreas);
            // Death is resolved first and clears deferred completion from the same simulation frame.
            if (_playerDead || Phase == WavePhase.GameOver) return;
            if (_regularExpiryPending) { _regularExpiryPending = false; FinishRegularWave(); }
            if (_arenaCompletionPending) { _arenaCompletionPending = false; FinishArenaWave(); }
        }

        public void BeginNewRun()
        {
            pickupSpawner?.Cleanup();
            foreach (WaveEnemyHandle enemy in _activeEnemies.ToArray()) if (enemy != null) enemy.Recycle();
            _activeEnemies.Clear(); _regularDeck.Clear(); _pendingArenaSpawns.Clear();
            _arenaBossStageOneHealth = null; _arenaBossStageTwoHealth = null;
            CurrentWave = 0; Kills = 0; GoldEarned = 0; _playerDead = false; _arenaObjectivesRemaining = 0; _arenaObjectivesTotal = 0; _nextArenaSpawnAttemptAt = 0f; _runStartedAt = Time.time;
            SetPhase(WavePhase.Intermission); SetAreaLocks(false, false, false); GoldChanged?.Invoke(Gold, GoldEarned);
        }
        public void ConfigureReferences(Transform playerTransform, Health health, Transform center, Camera camera,
            GameplayArea baseArea, GameplayArea firstArena, GameplayArea secondArena)
        {
            player = playerTransform; playerHealth = health; planetCenter = center; gameplayCamera = camera;
            landingBase = baseArea; arena1 = firstArena; arena2 = secondArena;
            if (surfaceSampler != null) surfaceSampler.ConfigurePlanet(center);
        }
        public bool TryStartNextWave()
        {
            if (Phase != WavePhase.Intermission || _playerDead || player == null || IsPlayerInsideProtectedArea()) return false;
            CurrentWave++; CurrentKind = WaveRules.GetKind(CurrentWave); WaveStarted?.Invoke(CurrentWave, CurrentKind);
            if (CurrentKind == WaveKind.Regular) StartRegularWave(); else StartArenaTravel();
            return true;
        }
        public void NotifyPlayerDied()
        {
            if (_playerDead || Phase == WavePhase.GameOver) return;
            _playerDead = true; _regularExpiryPending = false; _arenaCompletionPending = false;
            pickupSpawner?.Cleanup();
            SetAreaLocks(false, false, false); SetPhase(WavePhase.GameOver);
            RunEnded?.Invoke(new WaveRunResult { WaveReached = CurrentWave, Kills = Kills, GoldEarned = GoldEarned, Duration = RunDuration, Score = Score });
        }
        public void RegisterEnemy(WaveEnemyHandle enemy)
        {
            if (enemy == null || _activeEnemies.Contains(enemy) || Phase == WavePhase.GameOver) return;
            _activeEnemies.Add(enemy); enemy.Killed += HandleEnemyKilled; enemy.Removed += HandleEnemyRemoved;
        }
        public void NotifyArenaObjectiveCleared()
        {
            if (Phase != WavePhase.ArenaCombat || _arenaObjectivesRemaining <= 0) return;
            _arenaObjectivesRemaining--;
            if (_arenaObjectivesRemaining == 0) _arenaCompletionPending = true;
        }
        public void NotifyArenaBossDefeated()
        {
            if (Phase != WavePhase.ArenaCombat || CurrentKind != WaveKind.Arena2 || _arenaObjectivesRemaining <= 0) return;
            Kills++;
            // Boss doesn't carry a WaveEnemyHandle (see spawn setup), so it never runs through
            // HandleEnemyKilled's per-kill coin burst - fire a bigger, fixed-size one here
            // instead. Still cosmetic only: the actual gold comes from FinishArenaWave's arena
            // completion reward, same as before.
            if (_arenaBossStageTwoHealth != null && coinPickupPrefab != null && player != null)
            {
                SpawnCoins(_arenaBossStageTwoHealth.transform.position, bossCoinCount);
            }
            NotifyArenaObjectiveCleared();
        }
        public void NotifyArenaEntered(GameplayArea enteredArena)
        {
            if (Phase != WavePhase.ArenaTravel || enteredArena != TargetArena()) return;
            SetAreaLocks(true, CurrentKind == WaveKind.Arena1, CurrentKind == WaveKind.Arena2);
            _phaseRemaining = WaveRules.ArenaSealDuration; SetPhase(WavePhase.ArenaSeal);
        }
        public void NotifyEnemyKilled(WaveEnemyHandle enemy) { if (enemy != null) HandleEnemyKilled(enemy); }
        public void NotifyEnemyRemoved(WaveEnemyHandle enemy) { if (enemy != null) HandleEnemyRemoved(enemy); }

        private void StartRegularWave()
        {
            _phaseRemaining = WaveRules.RegularDurationForWave(CurrentWave); _spawnTimer = 0f; _regularExpiryPending = false;
            pickupSpawner?.Cleanup();
            pickupSpawner?.SpawnRegularPickups(surfaceSampler, planetCenter, player, AllAreas(), healthPickupPrefab,
                OwnsSpecial(ProgressionSpecialSkill.MedKit), ammoPickupPrefab,
                OwnsSpecial(ProgressionSpecialSkill.AmmoKit), transform);
            BuildRegularDeck(); SetAreaLocks(true, true, true); SetPhase(WavePhase.Regular);
        }
        private void StartArenaTravel()
        {
            SetAreaLocks(true, CurrentKind == WaveKind.Arena2, CurrentKind == WaveKind.Arena1);
            SetPhase(WavePhase.ArenaTravel);
            ArenaTravelRequested?.Invoke(TargetArena());
        }
        private void StartArenaCombat()
        {
            SetPhase(WavePhase.ArenaCombat);
            if (OwnsSpecial(ProgressionSpecialSkill.Ultimate))
                pickupSpawner?.SpawnArenaUltimate(surfaceSampler, planetCenter, AllAreas(), TargetArena(),
                    thunderPickupPrefab, transform);
            if (CurrentKind == WaveKind.Arena1)
            {
                int count = WaveRules.Arena1Count(CurrentWave);
                WaveRules.GetArena1Composition(count, out int small, out int flying, out int large);
                AddArenaSpawns(WaveEnemyType.Small, small); AddArenaSpawns(WaveEnemyType.Flying, flying); AddArenaSpawns(WaveEnemyType.Large, large);
            }
            else AddArenaSpawns(WaveEnemyType.Arena2Boss, 1);
            _arenaObjectivesRemaining = _pendingArenaSpawns.Count;
            _arenaObjectivesTotal = _arenaObjectivesRemaining;
            TrySpawnPendingArenaObjectives();
        }
        private void AddArenaSpawns(WaveEnemyType type, int count) { for (int i = 0; i < count; i++) _pendingArenaSpawns.Add(type); }
        private void RequestRegularSpawn()
        {
            if (surfaceSampler == null) return;
            WaveEnemyType type = PeekNextRegularType();
            if (!surfaceSampler.TrySample(player, gameplayCamera, AllAreas(), _activeEnemies, FootprintFor(type), out Vector3 point, out Quaternion rotation)) return;
            if (RequestSpawn(new WaveSpawnRequest(CurrentWave, CurrentKind, type, point, rotation))) TakeNextRegularType();
        }
        private void TrySpawnPendingArenaObjectives()
        {
            if (Time.time < _nextArenaSpawnAttemptAt) return;
            while (_pendingArenaSpawns.Count > 0)
            {
                if (!RequestArenaSpawn(_pendingArenaSpawns[0]))
                {
                    _nextArenaSpawnAttemptAt = Time.time + ArenaSpawnRetryDelay;
                    return;
                }
            }
            _nextArenaSpawnAttemptAt = 0f;
        }
        private bool RequestArenaSpawn(WaveEnemyType type)
        {
            GameplayArea arena = TargetArena();
            if (surfaceSampler == null || arena == null ||
                !surfaceSampler.TrySampleInside(arena, _activeEnemies, FootprintFor(type), out Vector3 point, out Quaternion rotation)) return false;
            if (!RequestSpawn(new WaveSpawnRequest(CurrentWave, CurrentKind, type, point, rotation))) return false;
            _pendingArenaSpawns.Remove(type);
            return true;
        }
        private bool RequestSpawn(WaveSpawnRequest request)
        {
            bool accepted = SpawnRequested != null;
            SpawnRequested?.Invoke(request);
            if (!instantiateAssignedPrefabs) return accepted;
            GameObject prefab = PrefabFor(request.EnemyType);
            if (prefab == null) return accepted;
            GameObject spawned = Instantiate(prefab, request.Position, request.Rotation);
            if (spawned == null) return accepted;
            accepted = true;
            ApplyEnemySizeMultiplier(spawned.transform);
            foreach (EnemyBase enemy in spawned.GetComponentsInChildren<EnemyBase>(true))
                enemy.ConfigureWorldDistanceMultiplier(EnemySizeMultiplier);
            WaveEnemyHandle handle = spawned.GetComponent<WaveEnemyHandle>();
            if (handle == null && request.EnemyType != WaveEnemyType.Arena2Boss) handle = spawned.AddComponent<WaveEnemyHandle>();
            if (handle != null) { handle.Configure(request.EnemyType, request.Stats, request.Kind != WaveKind.Regular); RegisterEnemy(handle); }
            if (request.EnemyType == WaveEnemyType.Arena2Boss)
            {
                ConfigureBossWaveScaling(spawned, request.Stats, request.Wave);
                WaveArenaObjective objective = spawned.GetComponent<WaveArenaObjective>();
                if (objective == null) objective = spawned.AddComponent<WaveArenaObjective>();
                BossAstronautAI astronaut = spawned.GetComponentInChildren<BossAstronautAI>(true);
                _arenaBossStageOneHealth = astronaut != null ? astronaut.GetComponent<Health>() : null;
                Health finalHealth = FindFinalBossHealth(spawned);
                _arenaBossStageTwoHealth = finalHealth;
                objective.Configure(this, finalHealth);
                if (finalHealth == null) Debug.LogError("Arena 2 boss prefab needs a reachable BossMechAI with final Health.", spawned);
            }
            return accepted;
        }
        private void HandleEnemyKilled(WaveEnemyHandle enemy)
        {
            if (!RemoveEnemy(enemy)) return;
            Kills++;
            bool useFortune = CurrentKind == WaveKind.Regular
                ? OwnsSpecial(ProgressionSpecialSkill.Fortune)
                : OwnsSpecial(ProgressionSpecialSkill.FortuneII);
            int reward = WaveRules.GoldWithSpecialBonus(WaveRules.KillGold(enemy.EnemyType, CurrentWave), useFortune);
            AwardGold(reward);
            SpawnCoinBurst(enemy.transform.position, reward);
            if (Phase == WavePhase.ArenaCombat) NotifyArenaObjectiveCleared();
        }

        // Cosmetic only - reward has already been added to the run total via AwardGold above.
        // Coin count scales with the reward but is clamped so a big multi-kill wave doesn't spawn
        // dozens of homing coins at once.
        private void SpawnCoinBurst(Vector3 position, int reward)
        {
            if (coinPickupPrefab == null || reward <= 0 || player == null) return;

            int count = Mathf.Clamp(
                Mathf.CeilToInt((float)reward / goldPerCoin), minCoinsPerDrop, maxCoinsPerDrop);
            SpawnCoins(position, count);
        }

        private void SpawnCoins(Vector3 position, int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject coinGo = Instantiate(coinPickupPrefab, position, Quaternion.identity);
                CoinPickup coin = coinGo.GetComponent<CoinPickup>();
                if (coin != null) coin.Launch(position, player);
            }
        }
        private void HandleEnemyRemoved(WaveEnemyHandle enemy) { RemoveEnemy(enemy); }
        private bool RemoveEnemy(WaveEnemyHandle enemy)
        {
            if (enemy == null || !_activeEnemies.Remove(enemy)) return false;
            enemy.Killed -= HandleEnemyKilled; enemy.Removed -= HandleEnemyRemoved; return true;
        }
        private void FinishRegularWave()
        {
            foreach (WaveEnemyHandle enemy in _activeEnemies.ToArray()) if (enemy != null) { EnemyRecycled?.Invoke(enemy); enemy.RetreatAndRecycle(); }
            _activeEnemies.Clear(); FinishWave();
        }
        private void FinishArenaWave()
        {
            int reward = WaveRules.ArenaCompletionGold(CurrentKind, CurrentWave);
            AwardGold(WaveRules.GoldWithSpecialBonus(reward, OwnsSpecial(ProgressionSpecialSkill.FortuneII)));
            FinishWave();
        }
        private void FinishWave()
        {
            pickupSpawner?.Cleanup();
            SetAreaLocks(false, false, false); WaveCompleted?.Invoke(CurrentWave, CurrentKind); SetPhase(WavePhase.Intermission);
        }
        private void RecycleDistantEnemies()
        {
            if (player == null) return;
            foreach (WaveEnemyHandle enemy in _activeEnemies.ToArray())
            {
                if (enemy == null) continue;
                bool offscreen = gameplayCamera == null || !IsVisible(enemy.transform.position, gameplayCamera);
                if (enemy.IsEligibleForOffscreenRecycle && offscreen && Vector3.Distance(player.position, enemy.transform.position) > recycleDistance)
                { EnemyRecycled?.Invoke(enemy); enemy.Recycle(); }
            }
        }
        private void UpdateEnemyAggro()
        {
            if (player == null) return;
            foreach (WaveEnemyHandle enemy in _activeEnemies) if (enemy != null) enemy.EvaluateAggro(player);
        }
        private bool IsPlayerInsideProtectedArea() { foreach (GameplayArea area in AllAreas()) if (area != null && area.Contains(player.position)) return true; return false; }
        private GameplayArea TargetArena() => CurrentKind == WaveKind.Arena1 ? arena1 : CurrentKind == WaveKind.Arena2 ? arena2 : null;
        private GameplayArea[] AllAreas() => new[] { landingBase, arena1, arena2 };
        private void SetAreaLocks(bool baseLocked, bool arena1Locked, bool arena2Locked)
        { SetLock(landingBase, baseLocked); SetLock(arena1, arena1Locked); SetLock(arena2, arena2Locked); }
        private void SetLock(GameplayArea area, bool locked)
        {
            if (area == null) return;
            bool changed = locked ? _lockedAreas.Add(area) : _lockedAreas.Remove(area);
            if (changed) AreaLockChanged?.Invoke(area, locked);
        }
        private void SetPhase(WavePhase next) { if (Phase == next) return; WavePhase previous = Phase; Phase = next; PhaseChanged?.Invoke(previous, next); }
        private void BuildRegularDeck()
        {
            _regularDeck.Clear(); _deckIndex = 0;
            WaveRules.GetRegularComposition(CurrentWave, out int small, out int flying, out int large);
            for (int i = 0; i < small; i++) _regularDeck.Add(WaveEnemyType.Small);
            for (int i = 0; i < flying; i++) _regularDeck.Add(WaveEnemyType.Flying);
            for (int i = 0; i < large; i++) _regularDeck.Add(WaveEnemyType.Large);
            Shuffle(_regularDeck);
        }
        private WaveEnemyType PeekNextRegularType()
        {
            if (_regularDeck.Count == 0) BuildRegularDeck();
            if (_deckIndex >= _regularDeck.Count) { Shuffle(_regularDeck); _deckIndex = 0; }
            return _regularDeck[_deckIndex];
        }
        private void TakeNextRegularType() { _deckIndex++; }
        private static void Shuffle(List<WaveEnemyType> items) { for (int i = items.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (items[i], items[j]) = (items[j], items[i]); } }
        private static bool IsVisible(Vector3 point, Camera camera) { Vector3 v = camera.WorldToViewportPoint(point); return v.z > 0 && v.x >= 0 && v.x <= 1 && v.y >= 0 && v.y <= 1; }
        private void AwardGold(int amount)
        {
            if (amount <= 0) return;
            GoldEarned += amount;
            if (progression != null) progression.AddGold(amount);
            GoldChanged?.Invoke(Gold, GoldEarned);
        }
        private bool OwnsSpecial(ProgressionSpecialSkill skill) => progression != null && progression.OwnsSpecial(skill);
        private static Health FindFinalBossHealth(GameObject root)
        {
            BossMechAI mech = root.GetComponentInChildren<BossMechAI>(true);
            return mech != null ? mech.GetComponent<Health>() : null;
        }
        private static void ApplyEnemySizeMultiplier(Transform enemyTransform)
        {
            if (enemyTransform != null) enemyTransform.localScale *= EnemySizeMultiplier;
        }
        private WaveSpawnFootprint FootprintFor(WaveEnemyType type)
        {
            GameObject prefab = PrefabFor(type);
            if (prefab == null) return WaveSpawnFootprint.FromPrefab(null, EnemySizeMultiplier);
            int id = prefab.GetInstanceID();
            if (!_prefabFootprints.TryGetValue(id, out WaveSpawnFootprint footprint))
            {
                footprint = WaveSpawnFootprint.FromPrefab(prefab, EnemySizeMultiplier);
                _prefabFootprints.Add(id, footprint);
            }
            return footprint;
        }
        private GameObject PrefabFor(WaveEnemyType type) =>
            type == WaveEnemyType.Small ? smallEnemyPrefab :
            type == WaveEnemyType.Flying ? flyingEnemyPrefab :
            type == WaveEnemyType.Large ? largeEnemyPrefab : arena2BossPrefab;
        private static void ConfigureBossWaveScaling(GameObject root, WaveStatModifiers stats, int wave)
        {
            EnemyWaveScaling scaling = new EnemyWaveScaling(WaveRules.BarbaraHealthMultiplier(wave),
                stats.Damage, stats.Movement, stats.AttackRate, stats.ProjectileSpeed);
            foreach (EnemyBase enemy in root.GetComponentsInChildren<EnemyBase>(true))
            {
                enemy.ConfigureWaveScaling(scaling);
                enemy.ConfigureDetectionRadius(20f);
                enemy.ForceImmediateAggro();
            }
        }
    }
}

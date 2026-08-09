using System;
using System.Collections.Generic;
using Combat;
using Enemies;
using Gameplay.Areas;
using UnityEngine;

namespace Gameplay.Waves
{
    /// <summary>
    /// Minimal bridge a spawned prefab can carry. Existing enemy code need not know about waves:
    /// a spawning adapter may also call NotifyEnemyKilled/NotifyEnemyRemoved on the director directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveEnemyHandle : MonoBehaviour
    {
        [SerializeField] private WaveEnemyType enemyType;
        [SerializeField] private Health health;
        [SerializeField] private EnemyBase enemy;
        [SerializeField] private bool inCombat;
        [SerializeField, Min(0f)] private float aggroRadius = 20f;
        [SerializeField, Min(0f)] private float offscreenCombatGraceSeconds = 5f;
        public WaveEnemyType EnemyType => enemyType;
        public bool InCombat { get => inCombat; set { inCombat = value; if (value) _lastCombatActivityAt = Time.time; } }
        public bool AggroAcquired => inCombat;
        public float AggroRadius => aggroRadius;
        public event Action<WaveEnemyHandle> Killed;
        public event Action<WaveEnemyHandle> Removed;

        private bool _reported;
        private bool _arenaObjective;
        private Vector3 _lastAllowedPosition;
        private WaveSpawnFootprint _spawnFootprint;
        private float _lastCombatActivityAt = float.NegativeInfinity;
        public WaveSpawnFootprint SpawnFootprint => _spawnFootprint.ShapeCount > 0
            ? _spawnFootprint
            : WaveSpawnFootprint.FromSpawnedRoot(transform);
        private void Awake()
        {
            if (enemy == null) enemy = GetComponentInChildren<EnemyBase>();
            if (health == null) health = GetComponentInChildren<Health>();
        }
        private void OnEnable()
        {
            if (enemy != null) { enemy.Killed += OnEnemyKilled; enemy.Despawned += OnEnemyDespawned; }
            else if (health != null) health.Died += ReportKilled;
        }
        private void OnDisable()
        {
            if (enemy != null) { enemy.Killed -= OnEnemyKilled; enemy.Despawned -= OnEnemyDespawned; }
            else if (health != null) health.Died -= ReportKilled;
        }
        private void OnDestroy() { if (!_reported) Removed?.Invoke(this); }
        public void Configure(WaveEnemyType type, WaveStatModifiers stats, bool forceAggro)
        {
            enemyType = type;
            _arenaObjective = forceAggro;
            _lastAllowedPosition = transform.position;
            _spawnFootprint = WaveSpawnFootprint.FromSpawnedRoot(transform);
            if (enemy == null) enemy = GetComponentInChildren<EnemyBase>();
            if (enemy == null) return;
            enemy.ConfigureWaveScaling(new EnemyWaveScaling(stats.Health, stats.Damage, stats.Movement, stats.AttackRate, stats.ProjectileSpeed));
            enemy.ConfigureDetectionRadius(aggroRadius);
            if (forceAggro) { enemy.ForceImmediateAggro(); InCombat = true; }
        }
        /// <summary>Call from a movement adapter each frame, or let WaveDirector call it for registered enemies.</summary>
        public void EvaluateAggro(Transform player)
        {
            if (enemy != null)
            {
                if (enemy.IsAggroed && !inCombat) InCombat = true;
                return;
            }
            if (!inCombat && player != null && Vector3.Distance(transform.position, player.position) <= aggroRadius) InCombat = true;
        }
        /// <summary>Call this from an AI/combat adapter while it is actively attacking or being attacked.</summary>
        public void ReportCombatActivity() => InCombat = true;
        public void KeepOutsideAreas(IReadOnlyList<GameplayArea> protectedAreas)
        {
            if (_arenaObjective || protectedAreas == null) return;
            for (int index = 0; index < protectedAreas.Count; index++)
            {
                GameplayArea area = protectedAreas[index];
                if (area != null && area.Contains(transform.position))
                {
                    if (enemy != null)
                    {
                        Vector3 escape = _lastAllowedPosition - transform.position;
                        if (escape.sqrMagnitude < 0.0001f) escape = DirectionToNearestPerimeter(area, transform.position);
                        // Roll back this single boundary crossing, then let navigation recovery
                        // steer away. This prevents the old every-frame rewind jitter without
                        // leaving an enemy trapped behind the sealed physical barrier.
                        if (_lastAllowedPosition != transform.position && !area.Contains(_lastAllowedPosition))
                            transform.position = _lastAllowedPosition;
                        enemy.RequestNavigationRecovery(escape, 1.5f);
                    }
                    return;
                }
            }
            _lastAllowedPosition = transform.position;
        }

        private static Vector3 DirectionToNearestPerimeter(GameplayArea area, Vector3 point)
        {
            Transform poles = area != null ? area.PerimeterPoles : null;
            if (poles == null || poles.childCount < 2) return Vector3.zero;
            Vector3 nearest = poles.GetChild(0).position;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < poles.childCount; index++)
            {
                Vector3 a = poles.GetChild(index).position;
                Vector3 b = poles.GetChild((index + 1) % poles.childCount).position;
                Vector3 segment = b - a;
                float t = segment.sqrMagnitude < 0.0001f
                    ? 0f
                    : Mathf.Clamp01(Vector3.Dot(point - a, segment) / segment.sqrMagnitude);
                Vector3 candidate = a + segment * t;
                float distance = (candidate - point).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = candidate;
            }
            return nearest - point;
        }
        public bool IsEligibleForOffscreenRecycle => Time.time - _lastCombatActivityAt >= offscreenCombatGraceSeconds;
        public void ReportKilled()
        {
            if (_reported) return;
            _reported = true;
            Killed?.Invoke(this);
        }
        public void Recycle()
        {
            if (_reported) return;
            if (enemy != null) { enemy.RequestDespawnWithoutRewards(); return; }
            _reported = true; Removed?.Invoke(this); Destroy(gameObject);
        }
        public void RetreatAndRecycle(float retreatDuration = .75f)
        {
            if (_reported) return;
            if (enemy != null) { enemy.RequestRetreatAndDespawn(retreatDuration); return; }
            Recycle();
        }

        private void OnEnemyKilled(EnemyBase _) { ReportKilled(); }
        private void OnEnemyDespawned(EnemyBase _, EnemyDespawnReason __)
        {
            if (_reported) return;
            _reported = true; Removed?.Invoke(this);
        }
    }
}

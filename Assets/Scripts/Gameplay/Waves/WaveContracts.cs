using System;
using UnityEngine;

namespace Gameplay.Waves
{
    public enum WavePhase { Intermission, ArenaTravel, Regular, ArenaSeal, ArenaCombat, GameOver }
    public enum WaveKind { Regular, Arena1, Arena2 }
    public enum WaveEnemyType { Small, Flying, Large, Arena2Boss }

    [Serializable]
    public struct WaveStatModifiers
    {
        public float Health;
        public float Damage;
        public float Movement;
        public float AttackRate;
        public float ProjectileSpeed;
        public static WaveStatModifiers ForWave(int wave) => new WaveStatModifiers
        {
            Health = WaveRules.HealthMultiplier(wave), Damage = WaveRules.DamageMultiplier(wave),
            Movement = WaveRules.MovementMultiplier(wave), AttackRate = WaveRules.AttackRateMultiplier(wave),
            ProjectileSpeed = WaveRules.ProjectileSpeedMultiplier(wave)
        };
    }

    [Serializable]
    public struct WaveSpawnRequest
    {
        public WaveSpawnRequest(int wave, WaveKind kind, WaveEnemyType enemyType, Vector3 position, Quaternion rotation)
        {
            Wave = wave; Kind = kind; EnemyType = enemyType; Position = position; Rotation = rotation; Stats = WaveStatModifiers.ForWave(wave);
        }
        public int Wave;
        public WaveKind Kind;
        public WaveEnemyType EnemyType;
        public Vector3 Position;
        public Quaternion Rotation;
        public WaveStatModifiers Stats;
    }

    [Serializable]
    public struct WaveRunResult
    {
        public int WaveReached;
        public int Kills;
        public int GoldEarned;
        public float Duration;
    }

    /// <summary>Numerical rules deliberately live outside the director so UI and tests can use the exact same contract.</summary>
    public static class WaveRules
    {
        public const float EarlyRegularDuration = 30f;
        public const float MidRegularDuration = 25f;
        public const float LateRegularDuration = 20f;
        public const float ArenaSealDuration = 3f;
        public const int StartingGold = 100;
        public const float FortuneGoldMultiplier = 1.15f;
        public const int MedKitPickupsPerRegularWave = 15;
        public const int AmmoKitPickupsPerRegularWave = 10;

        public static WaveKind GetKind(int wave) => wave > 0 && wave % 10 == 0 ? WaveKind.Arena2 :
            wave > 0 && wave % 10 == 5 ? WaveKind.Arena1 : WaveKind.Regular;
        public static float RegularDurationForWave(int wave) => wave >= 21 ? LateRegularDuration :
            wave >= 11 ? MidRegularDuration : EarlyRegularDuration;
        public static int ActiveCap(int wave) => Mathf.Min(5 + Mathf.Max(1, wave), 40);
        public static float SpawnInterval(int wave) => Mathf.Max(2.2f - .04f * (Mathf.Max(1, wave) - 1), .55f);
        public static int Arena1Count(int wave) => 10 + 10 * ((Mathf.Max(5, wave) - 5) / 10);
        public static float KillMultiplier(int wave) => Mathf.Min(1f + .10f * Mathf.Max(0, wave - 1), 3f);
        public static float ArenaCompletionMultiplier(int wave) => Mathf.Min(1f + .05f * Mathf.Max(0, wave - 1), 3f);
        public static int KillGold(WaveEnemyType type, int wave) => Mathf.RoundToInt(BaseGold(type) * KillMultiplier(wave));
        public static int ArenaCompletionGold(WaveKind kind, int wave) =>
            Mathf.RoundToInt((kind == WaveKind.Arena1 ? 100 : kind == WaveKind.Arena2 ? 300 : 0) * ArenaCompletionMultiplier(wave));
        /// <summary>Applies a special-skill reward bonus to one award, retaining the run economy's integer convention.</summary>
        public static int GoldWithSpecialBonus(int amount, bool bonusOwned) =>
            bonusOwned ? Mathf.RoundToInt(Mathf.Max(0, amount) * FortuneGoldMultiplier) : Mathf.Max(0, amount);
        public static float HealthMultiplier(int wave) => 1f + .10f * Mathf.Max(0, wave - 1);
        public static float BarbaraHealthMultiplier(int wave) => 1f + .15f * Mathf.Max(0, wave - 1);
        public static float DamageMultiplier(int wave) => 1f + .075f * Mathf.Max(0, wave - 1);
        public static float MovementMultiplier(int wave) => Mathf.Min(1f + .015f * Mathf.Max(0, wave - 1), 2f);
        public static float AttackRateMultiplier(int wave) => Mathf.Min(1f + .02f * Mathf.Max(0, wave - 1), 2f);
        public static float ProjectileSpeedMultiplier(int wave) => Mathf.Min(1f + .02f * Mathf.Max(0, wave - 1), 2f);

        public static void GetArena1Composition(int count, out int small, out int flying, out int large)
        {
            count = Mathf.Max(0, count);
            small = Mathf.FloorToInt(count * .5f);
            flying = Mathf.FloorToInt(count * .3f);
            large = count - small - flying;
        }
        public static void GetRegularComposition(int wave, out int small, out int flying, out int large)
        {
            small = wave <= 1 ? 10 : wave == 2 ? 7 : 5;
            flying = wave <= 1 ? 0 : 3;
            large = wave < 3 ? 0 : 2;
        }

        private static int BaseGold(WaveEnemyType type) => type == WaveEnemyType.Flying ? 25 : type == WaveEnemyType.Large ? 30 : type == WaveEnemyType.Small ? 20 : 0;
    }
}

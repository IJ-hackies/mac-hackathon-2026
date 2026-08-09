namespace Gameplay.Waves
{
    /// <summary>Numerical score formula, kept separate from WaveDirector the same way WaveRules
    /// is - so UI, submission, and tests can share the exact same contract.</summary>
    public static class ScoreRules
    {
        public const int PointsPerKill = 25;
        public const int PointsPerWaveReached = 150;
        public const int PointsPerGoldEarned = 1;

        /// WaveRunResult only carries aggregate Kills/GoldEarned (not a per-enemy-tier
        /// breakdown), so score rewards depth (wave reached) and volume (kills, gold) rather
        /// than individual enemy value.
        public static int ComputeScore(int kills, int goldEarned, int waveReached)
        {
            int safeKills = kills < 0 ? 0 : kills;
            int safeGold = goldEarned < 0 ? 0 : goldEarned;
            int safeWave = waveReached < 0 ? 0 : waveReached;
            return safeKills * PointsPerKill + safeGold * PointsPerGoldEarned + safeWave * PointsPerWaveReached;
        }

        public static int ComputeScore(WaveRunResult result) =>
            ComputeScore(result.Kills, result.GoldEarned, result.WaveReached);
    }
}

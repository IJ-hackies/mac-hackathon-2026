using UnityEngine;

namespace Gameplay.Waves
{
    /// <summary>Instant local best-score/best-wave readout (PlayerPrefs-backed, mirrors how
    /// GameSettings persists locally) so menus/HUD never have to wait on a cloud round-trip just
    /// to show "your best". The cloud leaderboard remains the source of truth for ranking.</summary>
    public static class LocalScoreRecord
    {
        private const string BestScoreKey = "score.best";
        private const string BestWaveKey = "score.bestWave";

        public static int BestScore => PlayerPrefs.GetInt(BestScoreKey, 0);
        public static int BestWaveReached => PlayerPrefs.GetInt(BestWaveKey, 0);

        /// Returns true if either best was improved.
        public static bool TryRecordRun(int score, int waveReached)
        {
            bool improved = false;

            if (score > BestScore)
            {
                PlayerPrefs.SetInt(BestScoreKey, score);
                improved = true;
            }

            if (waveReached > BestWaveReached)
            {
                PlayerPrefs.SetInt(BestWaveKey, waveReached);
                improved = true;
            }

            if (improved) PlayerPrefs.Save();
            return improved;
        }
    }
}

using System.Threading.Tasks;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

namespace Services.Leaderboards
{
    /// <summary>Thin static wrapper around the two configured leaderboards (furthest-wave,
    /// highest-score). Both leaderboards are configured "Keep Best" on the dashboard, so
    /// resubmitting a lower run never overwrites a player's better one.</summary>
    public static class LeaderboardsClient
    {
        public const int TopPageSize = 50;

        public static async Task SubmitRunAsync(int waveReached, int score)
        {
            await CloudIdentity.EnsureSignedInAsync();

            // Run both submissions concurrently rather than sequentially - they are independent.
            Task waveTask = LeaderboardsService.Instance.AddPlayerScoreAsync(
                LeaderboardIds.FurthestWave, waveReached);
            Task scoreTask = LeaderboardsService.Instance.AddPlayerScoreAsync(
                LeaderboardIds.HighestScore, score);

            try
            {
                await Task.WhenAll(waveTask, scoreTask);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"LeaderboardsClient: score submission failed - {exception.Message}");
            }
        }

        public static async Task<LeaderboardScoresPage> GetTopAsync(string leaderboardId, int offset)
        {
            await CloudIdentity.EnsureSignedInAsync();
            var options = new GetScoresOptions { Offset = offset, Limit = TopPageSize };
            return await LeaderboardsService.Instance.GetScoresAsync(leaderboardId, options);
        }

        /// Null if the current player has no entry on this leaderboard yet.
        public static async Task<LeaderboardEntry> GetOwnEntryAsync(string leaderboardId)
        {
            await CloudIdentity.EnsureSignedInAsync();
            try
            {
                return await LeaderboardsService.Instance.GetPlayerScoreAsync(leaderboardId);
            }
            catch (System.Exception)
            {
                return null;
            }
        }
    }
}

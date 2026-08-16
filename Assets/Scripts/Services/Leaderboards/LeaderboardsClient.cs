using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

namespace Services.Leaderboards
{
    public readonly struct LeaderboardRowData
    {
        public readonly string PlayerId;
        public readonly string PlayerName;
        public readonly int Rank;
        public readonly double Value;

        public LeaderboardRowData(string playerId, string playerName, int rank, double value)
        {
            PlayerId = playerId;
            PlayerName = playerName;
            Rank = rank;
            Value = value;
        }
    }

    public readonly struct LeaderboardPage
    {
        public readonly IReadOnlyList<LeaderboardRowData> Entries;
        public readonly int Total;

        public LeaderboardPage(IReadOnlyList<LeaderboardRowData> entries, int total)
        {
            Entries = entries;
            Total = total;
        }
    }

    /// <summary>
    /// Thin wrapper around the Leaderboards service. Both leaderboards are configured on the
    /// Dashboard to keep each player's best entry and sort descending, so this client does no
    /// dedup/sort/keep-best logic of its own - it only reads/writes.
    /// </summary>
    public static class LeaderboardsClient
    {
        public static async Task SubmitAsync(string leaderboardId, double score, string displayName)
        {
            await CloudIdentity.EnsureSignedInAsync();
            var options = new AddPlayerScoreOptions { Metadata = new Dictionary<string, object> { { "name", displayName } } };
            await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboardId, score, options);
        }

        public static async Task<LeaderboardPage> GetPageAsync(string leaderboardId, int offset, int limit)
        {
            await CloudIdentity.EnsureSignedInAsync();
            var options = new GetScoresOptions { Offset = offset, Limit = limit, IncludeMetadata = true };
            var response = await LeaderboardsService.Instance.GetScoresAsync(leaderboardId, options);

            var entries = new List<LeaderboardRowData>(response.Results.Count);
            foreach (LeaderboardEntry result in response.Results)
            {
                string name = ExtractDisplayName(result.Metadata) ?? result.PlayerName;
                entries.Add(new LeaderboardRowData(result.PlayerId, name, result.Rank + 1, result.Score));
            }

            return new LeaderboardPage(entries, response.Total);
        }

        // Metadata comes back as a raw JSON object string (e.g. {"name":"Astro"}), not a
        // dictionary - JsonUtility needs a concrete wrapper type to deserialize it into.
        private static string ExtractDisplayName(string metadataJson)
        {
            if (string.IsNullOrEmpty(metadataJson)) return null;
            try
            {
                NameMetadata parsed = JsonUtility.FromJson<NameMetadata>(metadataJson);
                return string.IsNullOrEmpty(parsed?.name) ? null : parsed.name;
            }
            catch (Exception)
            {
                return null;
            }
        }

        [Serializable]
        private sealed class NameMetadata
        {
            public string name;
        }
    }
}

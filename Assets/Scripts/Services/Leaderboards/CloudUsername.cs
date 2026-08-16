using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;

namespace Services.Leaderboards
{
    /// <summary>Persists the player's chosen leaderboard username against their Cloud Save player data.</summary>
    public static class CloudUsername
    {
        private const string Key = "username";

        public static async Task SaveAsync(string username)
        {
            await CloudIdentity.EnsureSignedInAsync();
            var data = new Dictionary<string, object> { { Key, username } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
        }

        public static async Task<string> LoadAsync()
        {
            await CloudIdentity.EnsureSignedInAsync();
            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { Key });
            if (result.TryGetValue(Key, out var item))
            {
                return item.Value.GetAs<string>();
            }
            return null;
        }
    }
}

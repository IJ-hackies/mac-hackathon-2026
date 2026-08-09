using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Services.Leaderboards
{
    /// <summary>Thin static wrapper around Unity Services init + anonymous auth + player display
    /// name, shared by every caller so init/sign-in only happens once per process. Anonymous auth
    /// caches its session token in PlayerPrefs (browser localStorage on WebGL), so the same
    /// browser profile resolves back to the same player across sessions - see [[leaderboard
    /// identity]] discussion: this is "one identity per browser profile", not "one identity per
    /// human".</summary>
    public static class CloudIdentity
    {
        private static Task _initializeTask;

        public static bool IsSignedIn =>
            UnityServices.State == ServicesInitializationState.Initialized
            && AuthenticationService.Instance.IsSignedIn;

        public static string PlayerId =>
            IsSignedIn ? AuthenticationService.Instance.PlayerId : null;

        /// Empty/null until the player has set a display name at least once.
        public static string PlayerName =>
            IsSignedIn ? AuthenticationService.Instance.PlayerName : null;

        public static bool HasPlayerName => !string.IsNullOrEmpty(PlayerName);

        public static Task EnsureSignedInAsync()
        {
            _initializeTask ??= InitializeAndSignInAsync();
            return _initializeTask;
        }

        private static async Task InitializeAndSignInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        /// UGS player names must be 1-50 chars; trims/truncates rather than rejecting so a
        /// slightly-too-long entry from the prompt UI still succeeds.
        public static async Task<bool> TrySetPlayerNameAsync(string desiredName)
        {
            await EnsureSignedInAsync();

            string trimmed = (desiredName ?? string.Empty).Trim();
            if (trimmed.Length == 0) return false;
            if (trimmed.Length > 50) trimmed = trimmed.Substring(0, 50);

            try
            {
                await AuthenticationService.Instance.UpdatePlayerNameAsync(trimmed);
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"CloudIdentity: failed to set player name - {exception.Message}");
                return false;
            }
        }
    }
}

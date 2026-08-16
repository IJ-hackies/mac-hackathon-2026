using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Services.Leaderboards
{
    /// <summary>
    /// Anonymous Unity Authentication identity. Unity caches the signed-in session token itself
    /// (through its own persisted storage, which on WebGL survives a browser tab close/reopen), so
    /// EnsureSignedInAsync resolves to the *same* PlayerId across sessions without any custom
    /// browser-storage interop.
    /// </summary>
    public static class CloudIdentity
    {
        private static Task<string> _signInTask;

        public static string PlayerId =>
            AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn
                ? AuthenticationService.Instance.PlayerId
                : null;

        public static Task<string> EnsureSignedInAsync()
        {
            if (_signInTask == null || _signInTask.IsFaulted) _signInTask = SignInAsync();
            return _signInTask;
        }

        private static async Task<string> SignInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                // Resolves to the previously cached anonymous player if one exists, so a returning
                // player (same browser/profile) keeps their identity instead of minting a new one.
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            return AuthenticationService.Instance.PlayerId;
        }
    }
}

using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEditor;
using UnityEngine;

namespace LeaderboardEditor
{
    /// <summary>
    /// Testing/dev-only convenience: clears this machine's cached anonymous Authentication
    /// identity, so the next Play session mints a brand new PlayerId with no saved leaderboard
    /// username (see CloudUsername/CloudIdentity) - the Add To Leaderboard button will show again
    /// on the next run instead of auto-submitting under the old locked-in name.
    /// </summary>
    public static class LeaderboardResetLocalIdentity
    {
        [MenuItem("Tools/Leaderboard/Reset Local Leaderboard Identity")]
        public static async void ResetLocalIdentity()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            // clearCredentials wipes the cached session token/player id from local storage (the
            // same PlayerPrefs-backed store that survives a WebGL tab close/reopen), forcing a
            // fresh SignInAnonymouslyAsync next time instead of resuming this identity.
            AuthenticationService.Instance.SignOut(clearCredentials: true);
            Debug.Log("LeaderboardResetLocalIdentity: cleared the cached anonymous identity. " +
                "The next Play session will sign in as a brand new player with no saved username.");
        }
    }
}

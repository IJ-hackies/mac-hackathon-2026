using Services.Leaderboards;
using Unity.Services.Authentication;
using UnityEditor;
using UnityEngine;

namespace LeaderboardEditor
{
    /// <summary>Wipes every locally-cached leaderboard/identity signal so the next Play session
    /// behaves like a brand-new player: clears the cached Authentication credential (a fresh
    /// anonymous player ID is minted on next sign-in), the local best score/wave cache, and the
    /// "have I chosen a username" flag. Requires Play Mode for the Authentication part - the
    /// credential lives in the Authentication SDK's runtime state, not just PlayerPrefs.</summary>
    public static class LeaderboardResetLocalIdentity
    {
        [MenuItem("Tools/Leaderboard/Reset Local Player Identity")]
        public static void ResetLocalIdentity()
        {
            PlayerPrefs.DeleteKey("score.best");
            PlayerPrefs.DeleteKey("score.bestWave");
            PlayerPrefs.DeleteKey("leaderboard.hasChosenName");
            PlayerPrefs.Save();

            if (Application.isPlaying && CloudIdentity.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut(clearCredentials: true);
                Debug.Log("Leaderboard Reset: cleared local best/username-chosen flag and signed " +
                    "out with credentials cleared - the next sign-in mints a brand-new anonymous " +
                    "player ID.");
            }
            else
            {
                Debug.Log("Leaderboard Reset: cleared local best/username-chosen flag. Not in " +
                    "Play Mode (or not signed in yet), so the cached Authentication credential " +
                    "was left alone - enter Play Mode and run this again to also clear that, or " +
                    "it will simply resume the same identity next launch.");
            }
        }
    }
}

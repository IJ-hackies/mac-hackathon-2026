using System;
using System.Threading.Tasks;
using Gameplay.Waves;
using Services.Leaderboards;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEditor;
using UnityEngine;

namespace LeaderboardEditor
{
    /// <summary>Submits 10 fake runs under 10 separate local Authentication profiles so the
    /// leaderboard UI (podium + pagination) can be tested with a full table before real players
    /// exist. Each dummy profile is a distinct cached anonymous identity, isolated from the
    /// profile the actual game uses ("default") via SwitchProfile - so this cannot clobber or
    /// overwrite the developer's own local player/session. Requires Play Mode (Unity Services
    /// only initializes with a running player loop).</summary>
    public static class LeaderboardDummyDataSeeder
    {
        private const string DefaultProfile = "default";
        private const int DummyCount = 10;

        private static readonly string[] SampleNames =
        {
            "Nova", "Comet", "Orion", "Vega", "Lyra", "Astra", "Krux", "Zephyr", "Ion", "Rex"
        };

        [MenuItem("Tools/Leaderboard/Seed 10 Dummy Leaderboard Entries")]
        public static async void SeedDummyEntries()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("Leaderboard Dummy Data Seeder: enter Play Mode first - " +
                    "Unity Services only initializes with a running player loop.");
                return;
            }

            Debug.Log("Leaderboard Dummy Data Seeder: starting - this temporarily switches " +
                "Authentication profiles and will restore your own session when finished.");

            var random = new System.Random();

            try
            {
                // Don't assume something else in the running game has already initialized
                // Unity Services - the seeder must be able to run as the very first thing that
                // touches Authentication in a fresh Play Mode session.
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (AuthenticationService.Instance.IsSignedIn)
                {
                    AuthenticationService.Instance.SignOut();
                }

                for (int i = 0; i < DummyCount; i++)
                {
                    string profile = $"leaderboard-dummy-{i}";
                    string name = $"{SampleNames[i % SampleNames.Length]}{random.Next(10, 99)}";
                    int waveReached = random.Next(1, 41);
                    int kills = waveReached * random.Next(3, 8);
                    int goldEarned = kills * random.Next(15, 45);
                    int score = ScoreRules.ComputeScore(kills, goldEarned, waveReached);

                    AuthenticationService.Instance.SwitchProfile(profile);
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    await AuthenticationService.Instance.UpdatePlayerNameAsync(name);
                    await LeaderboardsClient.SubmitRunAsync(waveReached, score);

                    Debug.Log($"Leaderboard Dummy Data Seeder: submitted {name} - wave {waveReached}, {score} pts.");
                    AuthenticationService.Instance.SignOut();
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Leaderboard Dummy Data Seeder: failed - {exception.Message}");
            }
            finally
            {
                // Not CloudIdentity.EnsureSignedInAsync() - its sign-in task is memoized from
                // whatever ran at normal game startup, so it would no-op here after we've
                // explicitly signed out mid-seed. Sign back in directly instead.
                AuthenticationService.Instance.SwitchProfile(DefaultProfile);
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("Leaderboard Dummy Data Seeder: done, restored to your own default profile.");
            }
        }
    }
}

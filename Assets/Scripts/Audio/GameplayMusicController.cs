using Enemies;
using Gameplay.Areas;
using Player;
using Player.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Audio
{
    /// Switches background music based on which GameplayArea the player is currently in -
    /// baseMusic while in LandingBase, waveMusic everywhere else - unless a boss fight is
    /// currently active (see Enemies.BossFightController.BossFightActive /
    /// Enemies.BossMechAI.BossFightActive), in which case it leaves bossMusic alone rather than
    /// yanking it out from under the fight on an area re-evaluation.
    public class GameplayMusicController : MonoBehaviour
    {
        [SerializeField] private PlayerAreaTracker areaTracker;

        // Self-bootstraps on every scene load (not just app startup - see SceneManager.sceneLoaded
        // below, since RuntimeInitializeOnLoadMethod alone only fires once for the very first
        // scene, and the gameplay scene here is loaded later via MainMenuController.LoadSingleplayer).
        // If a PlayerAreaTracker exists anywhere in the freshly loaded scene (i.e. this is the
        // gameplay scene, not the main menu), spin up a controller for it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterBootstrap()
        {
            SceneManager.sceneLoaded += (scene, mode) => TryBootstrap();
        }

        // Bootstraps in any gameplay scene - the planet scene (which has a PlayerAreaTracker for
        // base-vs-wave music) AND the flat-ground Player.unity sandbox (which doesn't, since
        // GameplayAreaId/PlayerAreaTracker are planet-only). Only skips the main menu scene
        // itself (MainMenuController already starts menuMusic there). Previously this required a
        // PlayerAreaTracker to exist at all, which meant no music ever played in the sandbox scene
        // until BossFightController explicitly started bossMusic at the transformation cutscene.
        private static void TryBootstrap()
        {
            if (FindFirstObjectByType<GameplayMusicController>() != null) return;
            if (FindFirstObjectByType<MainMenuController>() != null) return;
            if (FindFirstObjectByType<PlayerController>() == null) return;

            var go = new GameObject("GameplayMusicController (Auto)");
            go.AddComponent<GameplayMusicController>();
        }

        private void Start()
        {
            if (areaTracker == null) areaTracker = FindFirstObjectByType<PlayerAreaTracker>();
            if (areaTracker != null)
            {
                areaTracker.AreaChanged += OnAreaChanged;
            }

            // No area tracker (e.g. the flat-ground boss sandbox) - just default straight to wave
            // music instead of leaving the scene silent until a boss fight explicitly starts.
            ApplyMusicForArea(areaTracker != null ? areaTracker.CurrentArea : null);
        }

        private void OnDestroy()
        {
            if (areaTracker != null) areaTracker.AreaChanged -= OnAreaChanged;
        }

        private void OnAreaChanged(GameplayArea previous, GameplayArea next)
        {
            ApplyMusicForArea(next);
        }

        private void ApplyMusicForArea(GameplayArea area)
        {
            if (BossFightController.BossFightActive) return;

            var musicManager = MusicManager.Instance;
            if (musicManager == null) return;

            bool inLandingBase = area != null && area.AreaId == GameplayAreaId.LandingBase;
            musicManager.PlayMusic(inLandingBase ? musicManager.baseMusic : musicManager.waveMusic);
        }
    }
}

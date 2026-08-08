using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Areas;
using Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameplayEditor
{
    public static class GameplayAreaSceneSetup
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PlanetRootName = "Planet Ground";
        private const string PerimeterPath = "Perimeter/Poles";
        private const float DefaultExitPadding = 1.5f;
        private const float LandingBaseSpeedMultiplier = 2f;

        [MenuItem("Tools/Gameplay/Configure Area Membership %#g")]
        public static void ConfigureActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException("No active, loaded scene is available.");
            }

            ConfigureScene(scene, saveScene: true);
        }

        [MenuItem("Tools/Gameplay/Validate Area Membership")]
        public static void ValidateActiveScene()
        {
            ValidateScene(SceneManager.GetActiveScene(), logSuccess: true);
        }

        public static void ConfigureSampleSceneFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            ConfigureScene(scene, saveScene: true);
        }

        public static void ValidateSampleSceneFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            ValidateScene(scene, logSuccess: true);
        }

        private static void ConfigureScene(Scene scene, bool saveScene)
        {
            GameObject planet = FindRoot(scene, PlanetRootName);
            if (planet == null)
            {
                throw new InvalidOperationException(
                    $"Scene '{scene.name}' has no root named '{PlanetRootName}'.");
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Configure Gameplay Areas");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                GameplayArea landingBase = ConfigureArea(
                    scene,
                    nameof(GameplayAreaId.LandingBase),
                    GameplayAreaId.LandingBase,
                    planet.transform);
                GameplayArea arena1 = ConfigureArea(
                    scene,
                    nameof(GameplayAreaId.Arena1),
                    GameplayAreaId.Arena1,
                    planet.transform);
                GameplayArea arena2 = ConfigureArea(
                    scene,
                    nameof(GameplayAreaId.Arena2),
                    GameplayAreaId.Arena2,
                    planet.transform);

                PlayerController player = FindInScene<PlayerController>(scene);
                if (player == null)
                {
                    throw new InvalidOperationException(
                        $"Scene '{scene.name}' has no active PlayerController.");
                }

                PlayerAreaTracker tracker = FindInScene<PlayerAreaTracker>(scene);
                if (tracker == null)
                {
                    tracker = Undo.AddComponent<PlayerAreaTracker>(player.gameObject);
                }

                Undo.RecordObject(tracker, "Configure Player Area Tracker");
                tracker.Configure(
                    player.transform,
                    new[] { landingBase, arena1, arena2 });
                EditorUtility.SetDirty(tracker);

                LandingBaseMovementSpeedEffect speedEffect =
                    tracker.GetComponent<LandingBaseMovementSpeedEffect>();
                if (speedEffect == null)
                {
                    speedEffect = Undo.AddComponent<LandingBaseMovementSpeedEffect>(
                        tracker.gameObject);
                }

                Undo.RecordObject(speedEffect, "Configure Landing Base Speed Effect");
                speedEffect.Configure(tracker, player, LandingBaseSpeedMultiplier);
                EditorUtility.SetDirty(speedEffect);

                ValidateScene(scene, logSuccess: false);
                EditorSceneManager.MarkSceneDirty(scene);
                if (saveScene && !EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        $"Unity could not save configured scene '{scene.path}'.");
                }

                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log(
                    "Gameplay Area Setup: configured perimeter membership for " +
                    "LandingBase, Arena1, and Arena2, wired the shared astronaut tracker, " +
                    "and enabled the Landing Base movement-speed effect.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        private static GameplayArea ConfigureArea(
            Scene scene,
            string rootName,
            GameplayAreaId id,
            Transform planetCenter)
        {
            GameObject root = FindRoot(scene, rootName);
            Transform poles = root?.transform.Find(PerimeterPath);
            if (root == null || poles == null || poles.childCount < 3)
            {
                throw new InvalidOperationException(
                    $"Gameplay Area Setup requires at least three direct poles at " +
                    $"'{rootName}/{PerimeterPath}'.");
            }

            GameplayArea area = root.GetComponent<GameplayArea>();
            if (area == null)
            {
                area = Undo.AddComponent<GameplayArea>(root);
            }

            Undo.RecordObject(area, $"Configure {rootName} Gameplay Area");
            area.Configure(id, planetCenter, poles, DefaultExitPadding);
            EditorUtility.SetDirty(area);
            return area;
        }

        private static void ValidateScene(Scene scene, bool logSuccess)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException("The gameplay-area scene is not loaded.");
            }

            GameplayArea[] areas = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameplayArea>(true))
                .ToArray();
            var byId = new Dictionary<GameplayAreaId, GameplayArea>();
            foreach (GameplayArea area in areas)
            {
                if (byId.ContainsKey(area.AreaId))
                {
                    throw new InvalidOperationException(
                        $"Scene '{scene.name}' contains duplicate gameplay area '{area.AreaId}'.");
                }

                if (!area.RebuildPerimeter())
                {
                    throw new InvalidOperationException(
                        $"Gameplay area '{area.name}' is invalid: {area.ValidationError}");
                }

                byId.Add(area.AreaId, area);
            }

            foreach (GameplayAreaId id in Enum.GetValues(typeof(GameplayAreaId)))
            {
                if (!byId.ContainsKey(id))
                {
                    throw new InvalidOperationException(
                        $"Scene '{scene.name}' is missing gameplay area '{id}'.");
                }
            }

            PlayerController player = FindInScene<PlayerController>(scene);
            PlayerAreaTracker tracker = FindInScene<PlayerAreaTracker>(scene);
            bool explicitAreasMatch = tracker != null &&
                                      tracker.Areas.Count == byId.Count &&
                                      tracker.Areas.All(area =>
                                          area != null &&
                                          byId.TryGetValue(area.AreaId, out GameplayArea expected) &&
                                          ReferenceEquals(area, expected));
            bool trackerIsWired = tracker != null &&
                                  player != null &&
                                  tracker.TrackedBody == player.transform &&
                                  (explicitAreasMatch ||
                                   (tracker.Areas.Count == 0 && tracker.DiscoverAreasWhenEmpty));
            if (!trackerIsWired)
            {
                throw new InvalidOperationException(
                    $"Scene '{scene.name}' does not have one tracker wired to all gameplay areas.");
            }

            LandingBaseMovementSpeedEffect[] speedEffects = scene.GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<LandingBaseMovementSpeedEffect>(true))
                .ToArray();
            bool speedEffectIsWired = speedEffects.Length == 1 &&
                                      speedEffects[0].AreaTracker == tracker &&
                                      speedEffects[0].PlayerController == player &&
                                      Mathf.Approximately(
                                          speedEffects[0].SpeedMultiplier,
                                          LandingBaseSpeedMultiplier);
            if (!speedEffectIsWired)
            {
                throw new InvalidOperationException(
                    $"Scene '{scene.name}' does not have one 2x Landing Base speed effect " +
                    "wired to the shared player and area tracker.");
            }

            if (logSuccess)
            {
                Debug.Log(
                    $"Gameplay Area Validation: '{scene.name}' has three valid perimeter " +
                    "areas, one shared-body tracker, and one 2x Landing Base speed effect.");
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault();
        }
    }
}

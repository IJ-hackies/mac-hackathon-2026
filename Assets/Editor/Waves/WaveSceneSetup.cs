using System;
using System.Collections.Generic;
using System.Linq;
using Combat;
using Gameplay.Areas;
using Gameplay.Interaction;
using Gameplay.Waves;
using Player;
using Player.UI;
using Player.UI.Progression;
using Player.UI.Waves;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Waves.Editor
{
    /// <summary>Idempotently connects the reusable wave runtime to PlayerRig and SampleScene.</summary>
    public static class WaveSceneSetup
    {
        private const string PlayerRigPath = "Assets/Prefabs/PlayerRig.prefab";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string SmallPrefabPath = "Assets/Prefabs/Enemies/Enemy_Small.prefab";
        private const string FlyingPrefabPath = "Assets/Prefabs/Enemies/Enemy_Flying.prefab";
        private const string LargePrefabPath = "Assets/Prefabs/Enemies/Enemy_Large.prefab";
        private const string BossPrefabPath = "Assets/Prefabs/Enemies/BossFight_BarbaraTheBee.prefab";
        private const string HealthPickupPrefabPath = "Assets/Prefabs/Items/Pickup_Health.prefab";
        private const string AmmoPickupPrefabPath = "Assets/Prefabs/Items/Pickup_Ammo.prefab";
        private const string ThunderPickupPrefabPath = "Assets/Prefabs/Items/Pickup_Thunder.prefab";
        private const string BarrierShaderPath = "Assets/Art/Resources/S_WaveEnergyBarrier.shader";

        private static readonly Color LandingBaseCyan = Hex("85D8FF");
        private static readonly Color ArenaAmber = Hex("FFB347");
        private static readonly Color ArenaRed = Hex("FF5C63");

        [MenuItem("Tools/Waves/Configure Complete Wave Loop %#&w")]
        public static void ConfigureCompleteWaveLoop()
        {
            WaveUiPrefabSetup.BuildPlayerRigWaveUi();
            ConfigurePlayerRigAdapter();
            Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            ConfigureScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ValidateCompleteWaveLoop();
            Debug.Log("Configured complete wave loop, PlayerRig wave UI, area barriers, enemy prefabs, and SampleScene references.");
        }

        [MenuItem("Tools/Waves/Validate Complete Wave Loop")]
        public static void ValidateCompleteWaveLoop()
        {
            var errors = new List<string>();
            ValidateInput(errors);
            ValidateBarrierPresentation(errors);
            ValidatePrefab(errors);
            ValidateScene(errors);
            int progressionStartingGold = PlayerProgression.StartingGold;
            int waveStartingGold = WaveRules.StartingGold;
            if (progressionStartingGold != waveStartingGold || progressionStartingGold != 300)
                errors.Add("PlayerProgression and WaveRules must both start a run at 300g.");

            if (errors.Count > 0)
                throw new InvalidOperationException("Wave loop validation failed:\n- " + string.Join("\n- ", errors));
            Debug.Log("Wave loop validation passed.");
        }

        private static void ConfigurePlayerRigAdapter()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerRigPath);
            try
            {
                WaveGameController controller = root.GetComponent<WaveGameController>() ?? root.AddComponent<WaveGameController>();
                PlayerAreaTracker tracker = root.GetComponent<PlayerAreaTracker>();
                PlayerController player = root.GetComponentInChildren<PlayerController>(true);
                ThirdPersonCameraController cameraController = root.GetComponentInChildren<ThirdPersonCameraController>(true);
                SettingsMenuController settings = root.GetComponent<SettingsMenuController>();
                StationMenuController station = root.GetComponentInChildren<StationMenuController>(true);
                CrosshairUI crosshair = root.GetComponentInChildren<CrosshairUI>(true);

                controller.Configure(
                    null,
                    tracker,
                    player,
                    cameraController,
                    null,
                    settings,
                    station,
                    crosshair,
                    Array.Empty<WaveAreaBarrier>(),
                    root.GetComponentInChildren<WaveHudView>(true),
                    root.GetComponentInChildren<IntermissionPromptView>(true),
                    root.GetComponentInChildren<ArenaNavigationView>(true),
                    root.GetComponentInChildren<ArenaSealSweepView>(true),
                    root.GetComponentInChildren<ArenaObjectiveView>(true),
                    root.GetComponentInChildren<GameOverMissionSummaryView>(true));
                EditorUtility.SetDirty(controller);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerRigPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureScene(Scene scene)
        {
            GameObject planet = FindSceneObject(scene, "Planet Ground") ?? throw new InvalidOperationException("SampleScene is missing Planet Ground.");
            GameplayArea[] areas = FindSceneComponents<GameplayArea>(scene);
            GameplayArea landingBase = FindArea(areas, GameplayAreaId.LandingBase);
            GameplayArea arena1 = FindArea(areas, GameplayAreaId.Arena1);
            GameplayArea arena2 = FindArea(areas, GameplayAreaId.Arena2);

            PlayerAreaTracker tracker = FindSceneComponent<PlayerAreaTracker>(scene) ?? throw new InvalidOperationException("SampleScene is missing PlayerAreaTracker.");
            GameObject rig = tracker.gameObject;
            PlayerController player = rig.GetComponentInChildren<PlayerController>(true) ?? throw new InvalidOperationException("PlayerRig is missing PlayerController.");
            Health health = player.GetComponent<Health>() ?? throw new InvalidOperationException("Player is missing Health.");
            Camera camera = rig.GetComponentInChildren<Camera>(true) ?? throw new InvalidOperationException("PlayerRig is missing gameplay Camera.");
            PlayerProgression progression = player.GetComponent<PlayerProgression>() ?? player.GetComponentInChildren<PlayerProgression>(true);

            WaveAreaBarrier[] barriers =
            {
                ConfigureBarrier(landingBase, LandingBaseCyan),
                ConfigureBarrier(arena1, ArenaAmber),
                ConfigureBarrier(arena2, ArenaRed)
            };

            GameObject waveRoot = FindSceneObject(scene, "Wave System");
            if (waveRoot == null)
            {
                waveRoot = new GameObject("Wave System");
                SceneManager.MoveGameObjectToScene(waveRoot, scene);
            }
            WaveDirector director = waveRoot.GetComponent<WaveDirector>() ?? waveRoot.AddComponent<WaveDirector>();
            director.ConfigureReferences(player.transform, health, planet.transform, camera, landingBase, arena1, arena2);
            SerializedObject serializedDirector = new SerializedObject(director);
            Assign(serializedDirector, "progression", progression);
            Assign(serializedDirector, "smallEnemyPrefab", RequirePrefab(SmallPrefabPath));
            Assign(serializedDirector, "flyingEnemyPrefab", RequirePrefab(FlyingPrefabPath));
            Assign(serializedDirector, "largeEnemyPrefab", RequirePrefab(LargePrefabPath));
            Assign(serializedDirector, "arena2BossPrefab", RequirePrefab(BossPrefabPath));
            Assign(serializedDirector, "healthPickupPrefab", RequirePrefab(HealthPickupPrefabPath));
            Assign(serializedDirector, "ammoPickupPrefab", RequirePrefab(AmmoPickupPrefabPath));
            Assign(serializedDirector, "thunderPickupPrefab", RequirePrefab(ThunderPickupPrefabPath));
            serializedDirector.FindProperty("instantiateAssignedPrefabs").boolValue = true;
            serializedDirector.ApplyModifiedPropertiesWithoutUndo();

            WaveGameController controller = rig.GetComponent<WaveGameController>() ?? rig.AddComponent<WaveGameController>();
            controller.Configure(
                director,
                tracker,
                player,
                rig.GetComponentInChildren<ThirdPersonCameraController>(true),
                planet.transform,
                rig.GetComponent<SettingsMenuController>(),
                rig.GetComponentInChildren<StationMenuController>(true),
                rig.GetComponentInChildren<CrosshairUI>(true),
                barriers,
                rig.GetComponentInChildren<WaveHudView>(true),
                rig.GetComponentInChildren<IntermissionPromptView>(true),
                rig.GetComponentInChildren<ArenaNavigationView>(true),
                rig.GetComponentInChildren<ArenaSealSweepView>(true),
                rig.GetComponentInChildren<ArenaObjectiveView>(true),
                rig.GetComponentInChildren<GameOverMissionSummaryView>(true));

            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(controller);
            foreach (WaveAreaBarrier barrier in barriers) EditorUtility.SetDirty(barrier);
        }

        private static WaveAreaBarrier ConfigureBarrier(GameplayArea area, Color color)
        {
            if (area == null) throw new InvalidOperationException("Cannot configure a missing GameplayArea.");
            WaveAreaBarrier barrier = area.GetComponent<WaveAreaBarrier>() ?? area.gameObject.AddComponent<WaveAreaBarrier>();
            barrier.Configure(area, color, 16f);
            return barrier;
        }

        private static void ValidateInput(ICollection<string> errors)
        {
            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            InputAction action = input != null ? input.FindAction("Player/StartWave", false) : null;
            if (action == null) errors.Add("Input actions are missing Player/StartWave.");
            else if (!action.bindings.Any(binding => binding.path == "<Keyboard>/f")) errors.Add("StartWave has no default F binding.");
            if (!PlayerInputBindings.RebindableControls.Any(definition => definition.ActionName == "StartWave"))
                errors.Add("StartWave is missing from the rebindable PC control map.");
        }

        private static void ValidateBarrierPresentation(ICollection<string> errors)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(BarrierShaderPath);
            if (shader == null)
            {
                errors.Add($"Wave barrier shader is missing: {BarrierShaderPath}");
            }
            else if (shader.name != "Custom/WaveEnergyBarrier")
            {
                errors.Add($"Wave barrier shader has unexpected name '{shader.name}'.");
            }
        }

        private static void ValidatePrefab(ICollection<string> errors)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigPath);
            if (prefab == null) { errors.Add("PlayerRig prefab is missing."); return; }
            if (prefab.GetComponent<WaveGameController>() == null) errors.Add("PlayerRig is missing WaveGameController.");
            if (prefab.GetComponentInChildren<WaveHudView>(true) == null) errors.Add("PlayerRig is missing WaveHudView.");
            if (prefab.GetComponentInChildren<IntermissionPromptView>(true) == null) errors.Add("PlayerRig is missing IntermissionPromptView.");
            if (prefab.GetComponentInChildren<ArenaNavigationView>(true) == null) errors.Add("PlayerRig is missing ArenaNavigationView.");
            if (prefab.GetComponentInChildren<ArenaSealSweepView>(true) == null) errors.Add("PlayerRig is missing ArenaSealSweepView.");
            if (prefab.GetComponentInChildren<ArenaObjectiveView>(true) == null) errors.Add("PlayerRig is missing ArenaObjectiveView.");
            if (prefab.GetComponentInChildren<GameOverMissionSummaryView>(true) == null) errors.Add("PlayerRig is missing GameOverMissionSummaryView.");
        }

        private static void ValidateScene(ICollection<string> errors)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SampleScenePath)
            {
                errors.Add("SampleScene must be open for scene validation.");
                return;
            }
            WaveDirector[] directors = FindSceneComponents<WaveDirector>(scene);
            if (directors.Length != 1) errors.Add($"SampleScene needs exactly one WaveDirector; found {directors.Length}.");
            WaveGameController[] controllers = FindSceneComponents<WaveGameController>(scene);
            if (controllers.Length != 1) errors.Add($"SampleScene needs exactly one WaveGameController; found {controllers.Length}.");
            GameplayArea[] areas = FindSceneComponents<GameplayArea>(scene);
            foreach (GameplayAreaId id in Enum.GetValues(typeof(GameplayAreaId)))
            {
                GameplayArea area = areas.FirstOrDefault(candidate => candidate.AreaId == id);
                if (area == null) errors.Add($"SampleScene is missing {id} GameplayArea.");
                else if (area.GetComponent<WaveAreaBarrier>() == null) errors.Add($"{id} is missing WaveAreaBarrier.");
            }
            if (directors.Length == 1)
            {
                SerializedObject serialized = new SerializedObject(directors[0]);
                foreach (string property in new[]
                {
                    "smallEnemyPrefab", "flyingEnemyPrefab", "largeEnemyPrefab", "arena2BossPrefab",
                    "healthPickupPrefab", "ammoPickupPrefab", "thunderPickupPrefab"
                })
                    if (serialized.FindProperty(property)?.objectReferenceValue == null) errors.Add($"WaveDirector is missing {property}.");
            }
        }

        private static GameplayArea FindArea(IEnumerable<GameplayArea> areas, GameplayAreaId id) =>
            areas.FirstOrDefault(candidate => candidate != null && candidate.AreaId == id) ??
            throw new InvalidOperationException($"SampleScene is missing gameplay area {id}.");

        private static GameObject RequirePrefab(string path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new InvalidOperationException($"Missing prefab: {path}");

        private static void Assign(SerializedObject target, string property, UnityEngine.Object value)
        {
            SerializedProperty serialized = target.FindProperty(property) ?? throw new InvalidOperationException($"Missing serialized field '{property}' on {target.targetObject}.");
            serialized.objectReferenceValue = value;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component => FindSceneComponents<T>(scene).FirstOrDefault();

        private static T[] FindSceneComponents<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static GameObject FindSceneObject(Scene scene, string name) =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(candidate => candidate.name == name)?.gameObject;

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }
    }
}

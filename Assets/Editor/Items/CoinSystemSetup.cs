using System.IO;
using Gameplay.Waves;
using Items;
using Player.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ItemsEditor
{
    /// One-shot setup for the coin-drop VFX system: builds Pickup_Coin.prefab (CoinPickup +
    /// the imported coin model, scaled for gameplay), wires it into WaveDirector in SampleScene,
    /// and swaps the gold HUD's flat CoinIcon sprite for a live-rendered 3D coin
    /// (Coin3DIconRenderer). Idempotent: safe to re-run.
    public static class CoinSystemSetup
    {
        private const string CoinModelPath = "Assets/Art/Models/Items/Coin/coin.prefab";
        private const string PickupPrefabPath = "Assets/Prefabs/Items/Pickup_Coin.prefab";
        private const string PlayerRigPrefabPath = "Assets/Prefabs/PlayerRig.prefab";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Items/Build Coin Drop System")]
        public static void Run()
        {
            GameObject coinModel = AssetDatabase.LoadAssetAtPath<GameObject>(CoinModelPath);
            if (coinModel == null)
            {
                Debug.LogError($"CoinSystemSetup: coin model not found at {CoinModelPath}.");
                return;
            }

            GameObject pickupPrefab = BuildPickupPrefab(coinModel);
            WireWaveDirector(pickupPrefab);
            WireGoldHudIcon(coinModel);

            Debug.Log("CoinSystemSetup: done - coin drops and the 3D gold HUD icon are wired up.");
        }

        private static GameObject BuildPickupPrefab(GameObject coinModel)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PickupPrefabPath)!);

            var root = new GameObject("Pickup_Coin");
            try
            {
                root.AddComponent<CoinPickup>();

                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(coinModel, root.transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                // The source model's own bounds run ~2 units across (see coin.prefab's
                // SkinnedMeshRenderer AABB) - scaled down to a plausible in-hand coin size next
                // to the 2.55-tall player capsule.
                visual.transform.localScale = Vector3.one * 0.16f;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PickupPrefabPath, out bool success);
                if (!success)
                {
                    Debug.LogError("CoinSystemSetup: failed to save Pickup_Coin.prefab.");
                    return null;
                }

                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void WireWaveDirector(GameObject pickupPrefab)
        {
            if (pickupPrefab == null) return;

            Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            var director = Object.FindFirstObjectByType<WaveDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                Debug.LogError("CoinSystemSetup: no WaveDirector found in SampleScene.");
                return;
            }

            var so = new SerializedObject(director);
            so.FindProperty("coinPickupPrefab").objectReferenceValue = pickupPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void WireGoldHudIcon(GameObject coinModel)
        {
            GameObject rigRoot = PrefabUtility.LoadPrefabContents(PlayerRigPrefabPath);
            try
            {
                Transform coinIcon = FindDeep(rigRoot.transform, "CoinIcon");
                if (coinIcon == null)
                {
                    Debug.LogError("CoinSystemSetup: no \"CoinIcon\" object found in PlayerRig.prefab.");
                    return;
                }

                var existingImage = coinIcon.GetComponent<Image>();
                if (existingImage != null) Object.DestroyImmediate(existingImage);

                var rawImage = coinIcon.GetComponent<RawImage>();
                if (rawImage == null) rawImage = coinIcon.gameObject.AddComponent<RawImage>();
                rawImage.color = Color.white;
                rawImage.raycastTarget = false;

                var renderer = coinIcon.GetComponent<Coin3DIconRenderer>();
                if (renderer == null) renderer = coinIcon.gameObject.AddComponent<Coin3DIconRenderer>();

                var so = new SerializedObject(renderer);
                so.FindProperty("coinModelPrefab").objectReferenceValue = coinModel;
                so.ApplyModifiedPropertiesWithoutUndo();

                if (PrefabUtility.SaveAsPrefabAsset(rigRoot, PlayerRigPrefabPath) == null)
                {
                    Debug.LogError("CoinSystemSetup: failed to save PlayerRig.prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rigRoot);
            }

            AssetDatabase.ImportAsset(PlayerRigPrefabPath, ImportAssetOptions.ForceUpdate);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }

            return null;
        }
    }
}

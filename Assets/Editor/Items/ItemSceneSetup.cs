using System;
using Items;
using Player;
using Player.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ItemsEditor
{
    /// <summary>
    /// Builds the three power-up pickup prefabs (visuals-only for Thunder, full pickup
    /// interaction for Health/Ammo) and drops them into the existing Player.unity sandbox
    /// non-destructively - unlike PlayerEditor.PlayerSceneSetup.BuildTestScene, which rebuilds
    /// the whole scene from scratch, this only opens/edits the already-authored scene, the same
    /// way GameplayEditor.GameplayAreaSceneSetup operates on the active scene.
    /// </summary>
    public static class ItemSceneSetup
    {
        private const string ModelFolder = "Assets/Art/Models/Items";
        private const string PrefabFolder = "Assets/Prefabs/Items";
        private const string ScenePath = "Assets/Scenes/Player.unity";
        private const string PlayerRigPrefabPath = "Assets/Prefabs/PlayerRig.prefab";

        private const string LanaVfxFolder = "Assets/Lana Studio/Casual RPG VFX/Prefabs/";
        private const string SpawnEffectPath = LanaVfxFolder + "Burst/Poof_generic.prefab";
        private const string PickupEffectPath = LanaVfxFolder + "Loot/Loot_pick_up.prefab";
        private const string HealthBacklightPath = LanaVfxFolder + "Regeneration/Regeneration_health_loop.prefab";
        private const string AmmoBacklightPath = LanaVfxFolder + "States/Aura_acceleration.prefab";
        private const string ThunderBacklightPath = LanaVfxFolder + "Fog/Fog_electric.prefab";

        private struct ItemSpec
        {
            public string ModelName;
            public string PrefabName;
            public Type PickupType;
            public string BacklightPath;
        }

        private static readonly ItemSpec[] Specs =
        {
            new ItemSpec
            {
                ModelName = "Pickup_Health", PrefabName = "Pickup_Health",
                PickupType = typeof(HealthPickup), BacklightPath = HealthBacklightPath,
            },
            new ItemSpec
            {
                ModelName = "Pickup_Bullets", PrefabName = "Pickup_Ammo",
                PickupType = typeof(AmmoPickup), BacklightPath = AmmoBacklightPath,
            },
            new ItemSpec
            {
                ModelName = "Pickup_Thunder", PrefabName = "Pickup_Thunder",
                PickupType = typeof(ThunderPickup), BacklightPath = ThunderBacklightPath,
            },
        };

        [MenuItem("Tools/Items/Build Item Prefabs")]
        public static void BuildItemPrefabs()
        {
            EnsureFolder(PrefabFolder);

            GameObject spawnEffect = RequireAsset<GameObject>(SpawnEffectPath);
            GameObject pickupEffect = RequireAsset<GameObject>(PickupEffectPath);

            foreach (ItemSpec spec in Specs)
            {
                string modelPath = $"{ModelFolder}/{spec.ModelName}.fbx";
                GameObject model = RequireAsset<GameObject>(modelPath);
                GameObject backlight = RequireAsset<GameObject>(spec.BacklightPath);

                var root = new GameObject(spec.PrefabName);
                try
                {
                    var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                    modelInstance.transform.localPosition = Vector3.zero;
                    modelInstance.transform.localRotation = Quaternion.identity;

                    var pickup = root.AddComponent(spec.PickupType);
                    var so = new SerializedObject(pickup);
                    so.FindProperty("backlightPrefab").objectReferenceValue = backlight;
                    so.FindProperty("spawnEffectPrefab").objectReferenceValue = spawnEffect;
                    so.FindProperty("pickupEffectPrefab").objectReferenceValue = pickupEffect;
                    so.ApplyModifiedProperties();

                    string prefabPath = $"{PrefabFolder}/{spec.PrefabName}.prefab";
                    if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                    {
                        throw new InvalidOperationException(
                            $"Item Scene Setup: failed to save {prefabPath}.");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Item Scene Setup: built {Specs.Length} item prefabs in {PrefabFolder}.");
        }

        [MenuItem("Tools/Items/Place Items In Test Scene")]
        public static void PlaceItemsInTestScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            Vector3 origin = player != null ? player.transform.position : Vector3.zero;
            Vector3 forward = player != null ? player.transform.forward : Vector3.forward;
            Vector3 right = player != null ? player.transform.right : Vector3.right;

            var existingRoot = GameObject.Find("Generated Test Items");
            if (existingRoot != null) UnityEngine.Object.DestroyImmediate(existingRoot);
            var itemsRoot = new GameObject("Generated Test Items");

            for (int i = 0; i < Specs.Length; i++)
            {
                string prefabPath = $"{PrefabFolder}/{Specs[i].PrefabName}.prefab";
                GameObject prefab = RequireAsset<GameObject>(prefabPath);

                Vector3 position = origin + forward * 3f + right * ((i - (Specs.Length - 1) / 2f) * 2.5f);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, itemsRoot.transform);
                instance.transform.position = position;
                instance.transform.rotation = Quaternion.identity;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Item Scene Setup: placed {Specs.Length} item pickups in {ScenePath}.");
        }

        [MenuItem("Tools/Items/Wire Ammo Into Player Rig")]
        public static void WireAmmoIntoPlayerRig()
        {
            GameObject rigRoot = PrefabUtility.LoadPrefabContents(PlayerRigPrefabPath);
            try
            {
                Transform playerTransform = rigRoot.transform.Find("Player");
                if (playerTransform == null)
                {
                    throw new InvalidOperationException(
                        $"Item Scene Setup: {PlayerRigPrefabPath} has no direct 'Player' child.");
                }

                PlayerAmmo ammo = playerTransform.GetComponent<PlayerAmmo>();
                if (ammo == null) ammo = playerTransform.gameObject.AddComponent<PlayerAmmo>();

                Transform hudCanvas = rigRoot.transform.Find("HUD Canvas");
                if (hudCanvas == null)
                {
                    throw new InvalidOperationException(
                        $"Item Scene Setup: {PlayerRigPrefabPath} has no direct 'HUD Canvas' child.");
                }

                AmmoHudUI ammoHud = hudCanvas.GetComponentInChildren<AmmoHudUI>(true);
                if (ammoHud == null) ammoHud = BuildAmmoHud(hudCanvas);

                ammoHud.Bind(ammo);

                if (PrefabUtility.SaveAsPrefabAsset(rigRoot, PlayerRigPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        $"Item Scene Setup: failed to save {PlayerRigPrefabPath}.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rigRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PlayerRigPrefabPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"Item Scene Setup: wired PlayerAmmo/AmmoHudUI into {PlayerRigPrefabPath}.");
        }

        /// Bottom-right "magazine / storage" readout plus a hidden-by-default "RELOADING" line,
        /// built the same procedural-UI-rect way as PlayerSceneSetup.BuildHealthHud.
        private static AmmoHudUI BuildAmmoHud(Transform hudCanvas)
        {
            const float panelWidth = 220f;
            const float panelHeight = 64f;
            var bottomRight = new Vector2(1f, 0f);

            RectTransform root = CreateUiRect("AmmoHud", hudCanvas, new Vector2(panelWidth, panelHeight),
                new Vector2(-24f, 24f), bottomRight);
            var hud = root.gameObject.AddComponent<AmmoHudUI>();

            RectTransform backdrop = CreateUiRect("Backdrop", root, new Vector2(panelWidth, panelHeight),
                Vector2.zero, bottomRight);
            backdrop.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.08f, 0.55f);

            RectTransform ammoRect = CreateUiRect("AmmoText", root, new Vector2(panelWidth - 24f, 28f),
                new Vector2(-12f, 26f), bottomRight);
            var ammoText = ammoRect.gameObject.AddComponent<Text>();
            ammoText.alignment = TextAnchor.MiddleRight;
            ammoText.color = Color.white;
            ammoText.fontSize = 22;
            ammoText.fontStyle = FontStyle.Bold;
            ammoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ammoText.raycastTarget = false;
            ammoText.text = "0 / 0";

            RectTransform reloadingRect = CreateUiRect("ReloadingText", root, new Vector2(panelWidth - 24f, 16f),
                new Vector2(-12f, 6f), bottomRight);
            var reloadingText = reloadingRect.gameObject.AddComponent<Text>();
            reloadingText.alignment = TextAnchor.MiddleRight;
            reloadingText.color = new Color(1f, 0.6f, 0.2f);
            reloadingText.fontSize = 13;
            reloadingText.fontStyle = FontStyle.Bold;
            reloadingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            reloadingText.raycastTarget = false;
            reloadingText.text = "RELOADING";
            reloadingText.enabled = false;

            hud.SetAmmoText(ammoText);
            hud.SetReloadingText(reloadingText);

            return hud;
        }

        private static RectTransform CreateUiRect(
            string name, Transform parent, Vector2 size, Vector2 anchoredPosition, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            return rect;
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"Item Scene Setup: missing required asset '{path}'.");
            }

            return asset;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}

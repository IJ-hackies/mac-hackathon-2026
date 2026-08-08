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
        private const string HudBarTrackPath =
            "Assets/Art/Textures/UI/Health/SpaceExpansion_BarTrack_Grey.png";
        private const string HudBarFillPath =
            "Assets/Art/Textures/UI/Health/SpaceExpansion_BarFill_Gloss.png";
        private const string HudUtilityFontPath = "Assets/Art/Fonts/UI/KenneyFutureNarrow.ttf";

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
                int siblingIndex = ammoHud != null ? ammoHud.transform.GetSiblingIndex() : -1;
                if (ammoHud != null) UnityEngine.Object.DestroyImmediate(ammoHud.gameObject);
                ammoHud = BuildAmmoHud(hudCanvas);
                if (siblingIndex >= 0) ammoHud.transform.SetSiblingIndex(siblingIndex);

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

        /// Minimal blue ammo bar used when the item workflow rewires the rig's AmmoHud.
        private static AmmoHudUI BuildAmmoHud(Transform hudCanvas)
        {
            const float barWidth = 304f;
            const float barHeight = 36f;
            var topRight = new Vector2(1f, 1f);
            Sprite trackSprite = LoadHudSprite(HudBarTrackPath, new Vector4(24f, 12f, 24f, 12f));
            Sprite fillSprite = LoadHudSprite(HudBarFillPath, new Vector4(24f, 12f, 24f, 12f));
            Font utilityFont = RequireAsset<Font>(HudUtilityFontPath);

            RectTransform root = CreateUiRect("AmmoHud", hudCanvas, new Vector2(barWidth, barHeight),
                new Vector2(-28f, -72f), topRight);
            var hud = root.gameObject.AddComponent<AmmoHudUI>();

            Image track = CreateStretchImage("Track", root, trackSprite);
            track.type = Image.Type.Sliced;
            track.color = new Color(0.02f, 0.075f, 0.19f, 0.92f);

            Image fill = CreateStretchImage("Fill", root, fillSprite);
            fill.type = Image.Type.Sliced;
            fill.color = new Color(0.06f, 0.43f, 1f, 1f);

            RectTransform ammoRect = CreateUiRect("AmmoValue", root, new Vector2(barWidth, barHeight),
                Vector2.zero, topRight);
            var ammoText = ammoRect.gameObject.AddComponent<Text>();
            ammoText.alignment = TextAnchor.MiddleCenter;
            ammoText.color = Color.white;
            ammoText.fontSize = 20;
            ammoText.fontStyle = FontStyle.Bold;
            ammoText.font = utilityFont;
            ammoText.raycastTarget = false;
            var valueOutline = ammoRect.gameObject.AddComponent<Outline>();
            valueOutline.effectColor = new Color(0f, 0.03f, 0.12f, 0.9f);
            valueOutline.effectDistance = new Vector2(1f, -1f);

            hud.Configure(fill, ammoText);

            return hud;
        }

        private static Image CreateStretchImage(string name, Transform parent, Sprite sprite)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);

            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = gameObject.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite LoadHudSprite(string path, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Item Scene Setup: no UI texture found at {path}.");
            }

            bool requiresImport = importer.textureType != TextureImporterType.Sprite ||
                                  importer.spriteImportMode != SpriteImportMode.Single ||
                                  importer.mipmapEnabled ||
                                  importer.wrapMode != TextureWrapMode.Clamp ||
                                  importer.spriteBorder != border;
            if (requiresImport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }

            return RequireAsset<Sprite>(path);
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

using System;
using System.Collections.Generic;
using System.IO;
using Player.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WorldEditor;
using WorldRuntime;

namespace MainMenu.Editor
{
    public static class MainMenuSceneSetup
    {
        private const string AutoConfigureSessionKey = "MainMenu.AutoConfigureScheduled.V5";
        private const string GeneratedRootName = "Main Menu [Generated v3]";
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameplayScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PlanetPrefabPath = "Assets/Art/Prefabs/Planet.prefab";
        private const string SkyboxMaterialPath = "Assets/Art/Materials/M_ProceduralSpaceSkybox.mat";
        private const string FontPath = "Assets/Art/Fonts/UI/KenneyFuture.ttf";
        private const string NarrowFontPath = "Assets/Art/Fonts/UI/KenneyFutureNarrow.ttf";
        private const string PopupPath = "Assets/Art/Textures/UI/Settings/CartoonSciFi_Popup.png";
        private const string ButtonIdlePath = "Assets/Art/Textures/UI/Settings/CartoonSciFi_Button_Idle.png";
        private const string ButtonHoverPath = "Assets/Art/Textures/UI/Settings/CartoonSciFi_Button_Hover.png";
        private const string ButtonPressedPath = "Assets/Art/Textures/UI/Settings/CartoonSciFi_Button_Pressed.png";
        private const string HeaderPath = "Assets/Art/Textures/UI/Settings/SpaceExpansion_Header_Grey.png";
        private const string SliderTrackPath = "Assets/Art/Textures/UI/Settings/SpaceExpansion_SliderTrack_Grey.png";
        private const string SliderFillPath = "Assets/Art/Textures/UI/Settings/SpaceExpansion_SliderFill_Yellow.png";
        private const string SliderHandlePath = "Assets/Art/Textures/UI/Settings/SpaceExpansion_SliderHandle_Yellow.png";
        private const string PlayIconPath = "Assets/Art/Textures/UI/MainMenu/CartoonSciFi_Icon_Play.png";
        private const string SettingsIconPath = "Assets/Art/Textures/UI/MainMenu/CartoonSciFi_Icon_Settings.png";
        private const string PlayIconSource = "asset packs/visuals/cartoon-ui/sci fi/icons play.png";
        private const string SettingsIconSource = "asset packs/visuals/cartoon-ui/sci fi/icons settings.png";
        private const string EnvironmentRoot = "Assets/Art/Models/Environment";

        private static readonly string[] RockPaths =
        {
            EnvironmentRoot + "/PlanetRocks/Rock_1.fbx",
            EnvironmentRoot + "/PlanetRocks/Rock_2.fbx",
            EnvironmentRoot + "/PlanetRocks/Rock_3.fbx",
            EnvironmentRoot + "/PlanetRocks/Rock_4.fbx",
            EnvironmentRoot + "/PlanetRocks/Rock_Large_1.fbx",
            EnvironmentRoot + "/PlanetRocks/Rock_Large_2.fbx",
            EnvironmentRoot + "/PlanetRocks/Rock_Large_3.fbx"
        };

        private static readonly string[] VegetationPaths =
        {
            EnvironmentRoot + "/PlanetVegetation/Grass_1.fbx",
            EnvironmentRoot + "/PlanetVegetation/Grass_2.fbx",
            EnvironmentRoot + "/PlanetVegetation/Grass_3.fbx",
            EnvironmentRoot + "/PlanetVegetation/Bush_1.fbx",
            EnvironmentRoot + "/PlanetVegetation/Bush_2.fbx",
            EnvironmentRoot + "/PlanetVegetation/Bush_3.fbx",
            EnvironmentRoot + "/PlanetVegetation/Plant_1.fbx",
            EnvironmentRoot + "/PlanetVegetation/Plant_2.fbx",
            EnvironmentRoot + "/PlanetVegetation/Plant_3.fbx"
        };

        private static readonly string[] StructurePaths =
        {
            EnvironmentRoot + "/LandingBase/GeodesicDome.fbx",
            EnvironmentRoot + "/LandingBase/House_Long.fbx",
            EnvironmentRoot + "/LandingBase/SolarPanel_Structure.fbx"
        };

        private static readonly Color Void = Hex("06131F");
        private static readonly Color DeepSpace = Hex("081B2A");
        private static readonly Color Glass = Hex("0B2638");
        private static readonly Color Panel = Hex("103A50");
        private static readonly Color Ice = Hex("F3FBFF");
        private static readonly Color Cyan = Hex("85D8FF");
        private static readonly Color Muted = Hex("A9C9D8");
        private static readonly Color Solar = Hex("FFD24A");
        private static readonly Color Offline = Hex("D87567");

        [InitializeOnLoadMethod]
        private static void ConfigureAfterScriptReload()
        {
            if (SessionState.GetBool(AutoConfigureSessionKey, false) ||
                !MenuSceneNeedsRebuild())
            {
                return;
            }

            SessionState.SetBool(AutoConfigureSessionKey, true);
            EditorApplication.delayCall += ConfigureWhenEditorIsReady;
        }

        private static bool MenuSceneNeedsRebuild()
        {
            return !File.Exists(MainMenuScenePath) ||
                   !File.ReadAllText(MainMenuScenePath).Contains(GeneratedRootName);
        }

        private static void ConfigureWhenEditorIsReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
                EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
                return;
            }

            try
            {
                ConfigureMainMenu();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.delayCall += ConfigureWhenEditorIsReady;
        }

        [MenuItem("Tools/Main Menu/Rebuild Main Menu Scene")]
        public static void ConfigureMainMenu()
        {
            EnsureRuntimeIcon(PlayIconSource, PlayIconPath);
            EnsureRuntimeIcon(SettingsIconSource, SettingsIconPath);
            ConfigureTextureImports();

            MenuAssets assets = LoadAssets();
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene menuScene = SceneManager.GetSceneByPath(MainMenuScenePath);
            bool createdForBuild = !menuScene.IsValid() || !menuScene.isLoaded;
            if (createdForBuild)
            {
                menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            }
            else
            {
                foreach (GameObject root in menuScene.GetRootGameObjects())
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            try
            {
                SceneManager.SetActiveScene(menuScene);
                BuildPresentation(menuScene, assets, out Transform planet);
                MainMenuController controller = BuildInterface(menuScene, assets, planet);

                if (controller == null)
                {
                    throw new InvalidOperationException("MainMenuController was not created.");
                }

                if (!EditorSceneManager.SaveScene(menuScene, MainMenuScenePath))
                {
                    throw new InvalidOperationException($"Could not save {MainMenuScenePath}.");
                }

                ConfigureBuildScenes();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (createdForBuild && menuScene.IsValid() && menuScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(menuScene, removeScene: true);
                }

                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }
            }

            ValidateMainMenu();
            Debug.Log("Built MainMenu.unity and placed it before SampleScene in Build Settings.");
        }

        [MenuItem("Tools/Main Menu/Validate Main Menu Scene")]
        public static void ValidateMainMenu()
        {
            if (!File.Exists(MainMenuScenePath))
            {
                throw new InvalidOperationException($"Missing scene: {MainMenuScenePath}");
            }

            Scene scene = SceneManager.GetSceneByPath(MainMenuScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation)
            {
                scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);
            }

            try
            {
                MainMenuController controller = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    controller = root.GetComponentInChildren<MainMenuController>(true);
                    if (controller != null) break;
                }

                if (controller == null)
                {
                    throw new InvalidOperationException("MainMenu scene has no MainMenuController.");
                }

                SerializedObject serialized = new SerializedObject(controller);
                SerializedProperty singleplayerScene = serialized.FindProperty("singleplayerScene");
                if (singleplayerScene == null || singleplayerScene.stringValue != "SampleScene")
                {
                    throw new InvalidOperationException(
                        "Singleplayer must load SampleScene directly in Single mode.");
                }
                RequireReference(serialized, "homePage");
                RequireReference(serialized, "settingsPage");
                RequireReference(serialized, "controlsPage");
                RequireReference(serialized, "singleplayerButton");
                Button multiplayer = RequireReference(serialized, "multiplayerButton") as Button;
                RequireReference(serialized, "settingsButton");
                RequireReference(serialized, "volumeSlider");
                RequireReference(serialized, "sensitivitySlider");
                RequireReference(serialized, "controlsButton");

                Transform planet = RequireReference(serialized, "menuPlanet") as Transform;
                Transform dressing = planet != null
                    ? planet.Find("Menu Planet Dressing")
                    : null;
                if (dressing == null || dressing.childCount < 50)
                {
                    throw new InvalidOperationException(
                        "Main menu planet must include the lightweight presentation dressing.");
                }

                foreach (Collider collider in planet.GetComponentsInChildren<Collider>(true))
                {
                    if (collider.enabled)
                    {
                        throw new InvalidOperationException(
                            $"Presentation collider must stay disabled: {collider.name}");
                    }
                }

                if (multiplayer == null || multiplayer.interactable)
                {
                    throw new InvalidOperationException("Multiplayer must exist and remain unavailable.");
                }

                EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
                if (buildScenes.Length < 2 ||
                    !buildScenes[0].enabled || buildScenes[0].path != MainMenuScenePath ||
                    !buildScenes[1].enabled || buildScenes[1].path != GameplayScenePath)
                {
                    throw new InvalidOperationException(
                        "Build Settings must start with enabled MainMenu and SampleScene entries.");
                }
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }

            Debug.Log("Main menu validation passed: navigation, settings, disabled multiplayer, and build order are wired.");
        }

        public static void ConfigureMainMenuBatch()
        {
            ConfigureMainMenu();
        }

        public static void ValidateMainMenuBatch()
        {
            ValidateMainMenu();
        }

        private static void BuildPresentation(Scene scene, MenuAssets assets, out Transform planet)
        {
            RenderSettings.skybox = assets.Skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.23f, 0.31f);
            RenderSettings.ambientEquatorColor = new Color(0.06f, 0.10f, 0.15f);
            RenderSettings.ambientGroundColor = new Color(0.018f, 0.025f, 0.035f);
            RenderSettings.ambientIntensity = 0.72f;

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, -680f),
                Quaternion.identity);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 32f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 2000f;
            camera.allowHDR = true;

            GameObject lightObject = new GameObject("Menu Sun", typeof(Light));
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.rotation = Quaternion.Euler(28f, -132f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.82f, 0.62f);
            light.intensity = 3.4f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.72f;
            RenderSettings.sun = light;

            GameObject planetObject = PrefabUtility.InstantiatePrefab(assets.Planet, scene) as GameObject;
            if (planetObject == null)
            {
                throw new InvalidOperationException("Could not instantiate the menu planet.");
            }

            planetObject.name = "Menu Planet";
            planetObject.transform.position = new Vector3(170f, -18f, 90f);
            planetObject.transform.rotation = Quaternion.Euler(-5f, -22f, 8f);

            MeshCollider surface = null;
            foreach (MeshCollider candidate in planetObject.GetComponentsInChildren<MeshCollider>(true))
            {
                if (candidate.sharedMesh == null)
                {
                    continue;
                }

                surface = candidate;
                break;
            }

            if (surface == null)
            {
                throw new InvalidOperationException("Menu planet has no usable crater MeshCollider.");
            }

            surface.enabled = true;
            Physics.SyncTransforms();
            BuildMenuPlanetDressing(scene, planetObject.transform, surface, assets);
            foreach (Collider collider in planetObject.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            SphericalPropInstancingRenderer instancing =
                planetObject.GetComponent<SphericalPropInstancingRenderer>();
            if (instancing != null)
            {
                instancing.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instancing);
                EditorUtility.SetDirty(instancing);
            }

            planet = planetObject.transform;
        }

        private static void BuildMenuPlanetDressing(
            Scene scene,
            Transform planet,
            MeshCollider surface,
            MenuAssets assets)
        {
            GameObject dressingRoot = new GameObject("Menu Planet Dressing");
            SceneManager.MoveGameObjectToScene(dressingRoot, scene);
            dressingRoot.transform.SetParent(planet, false);

            Vector3 center = surface.bounds.center;
            Vector3 facing = (new Vector3(0f, 0f, -680f) - center).normalized;
            Vector3 right = Vector3.ProjectOnPlane(Vector3.right, facing).normalized;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, facing).normalized;
            var random = new System.Random(15026);

            Vector2[] structureOffsets =
            {
                new Vector2(0.28f, 0.34f),
                new Vector2(0.62f, 0.08f),
                new Vector2(0.04f, 0.61f)
            };
            float[] structureScales = { 300f, 245f, 230f };
            for (int index = 0; index < assets.Structures.Length; index++)
            {
                Vector2 offset = structureOffsets[index];
                Vector3 direction = (facing + right * offset.x + up * offset.y).normalized;
                PlaceMenuProp(
                    scene,
                    dressingRoot.transform,
                    surface,
                    center,
                    direction,
                    assets.Structures[index],
                    structureScales[index],
                    18f + index * 73f,
                    $"Outpost {index + 1:00}");
            }

            for (int index = 0; index < 16; index++)
            {
                bool silhouetteRock = index < 6;
                float x = silhouetteRock
                    ? RandomRange(random, 0.92f, 1.32f)
                    : RandomRange(random, -0.08f, 0.92f);
                float y = RandomRange(random, -0.62f, 0.78f);
                Vector3 direction = (facing + right * x + up * y).normalized;
                GameObject source = assets.Rocks[index % assets.Rocks.Length];
                bool large = (index % assets.Rocks.Length) >= 4;
                float scale = large
                    ? RandomRange(random, 145f, 180f)
                    : RandomRange(random, 105f, 148f);
                PlaceMenuProp(
                    scene,
                    dressingRoot.transform,
                    surface,
                    center,
                    direction,
                    source,
                    scale,
                    RandomRange(random, 0f, 360f),
                    $"Rock {index + 1:00}");
            }

            for (int index = 0; index < 42; index++)
            {
                float x = RandomRange(random, -0.12f, 1.02f);
                float y = RandomRange(random, -0.62f, 0.82f);
                Vector3 direction = (facing + right * x + up * y).normalized;
                int sourceIndex = index % assets.Vegetation.Length;
                GameObject source = assets.Vegetation[sourceIndex];
                float scale = sourceIndex < 3
                    ? RandomRange(random, 60f, 68f)
                    : sourceIndex < 6
                        ? RandomRange(random, 42f, 49f)
                        : RandomRange(random, 52f, 60f);
                PlaceMenuProp(
                    scene,
                    dressingRoot.transform,
                    surface,
                    center,
                    direction,
                    source,
                    scale,
                    RandomRange(random, 0f, 360f),
                    $"Vegetation {index + 1:00}");
            }
        }

        private static void PlaceMenuProp(
            Scene scene,
            Transform parent,
            MeshCollider surface,
            Vector3 center,
            Vector3 direction,
            GameObject source,
            float scale,
            float heading,
            string instanceName)
        {
            if (!RadialSurfaceSnapWindow.TryGetSurfaceHit(
                    center + direction,
                    center,
                    surface,
                    10f,
                    out RaycastHit hit,
                    out _))
            {
                throw new InvalidOperationException(
                    $"Could not place '{source.name}' on the visible menu-planet surface.");
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(source, scene) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"Could not instantiate '{source.name}'.");
            }

            Vector3 surfaceUp = hit.normal.sqrMagnitude > 0.001f
                ? hit.normal.normalized
                : direction;
            Quaternion surfaceAlignment = Quaternion.FromToRotation(Vector3.up, surfaceUp);
            Quaternion randomHeading = Quaternion.AngleAxis(heading, surfaceUp);
            instance.transform.SetPositionAndRotation(
                hit.point,
                randomHeading * surfaceAlignment * Quaternion.Euler(-90f, 0f, 0f));
            instance.transform.localScale = Vector3.one * scale;

            float supportOffset = GetRendererSupportOffset(instance, hit.point, surfaceUp);
            instance.transform.position = hit.point + surfaceUp * (supportOffset - 0.075f);
            instance.transform.SetParent(parent, true);
            instance.name = instanceName;

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        private static float GetRendererSupportOffset(
            GameObject instance,
            Vector3 surfacePoint,
            Vector3 surfaceUp)
        {
            float lowestProjection = float.PositiveInfinity;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Bounds bounds = renderer.bounds;
                Vector3 extents = bounds.extents;
                float projectedRadius =
                    Mathf.Abs(surfaceUp.x) * extents.x +
                    Mathf.Abs(surfaceUp.y) * extents.y +
                    Mathf.Abs(surfaceUp.z) * extents.z;
                float projectedCenter = Vector3.Dot(bounds.center - surfacePoint, surfaceUp);
                lowestProjection = Mathf.Min(lowestProjection, projectedCenter - projectedRadius);
            }

            return float.IsPositiveInfinity(lowestProjection) ? 0f : -lowestProjection;
        }

        private static float RandomRange(System.Random random, float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }

        private static MainMenuController BuildInterface(Scene scene, MenuAssets assets, Transform planet)
        {
            GameObject root = new GameObject(GeneratedRootName, typeof(MainMenuController));
            SceneManager.MoveGameObjectToScene(root, scene);
            MainMenuController controller = root.GetComponent<MainMenuController>();

            GameObject canvasObject = new GameObject(
                "Mission Control Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            RectTransform veil = CreateRect("Deep Space Veil", canvasRect, Vector2.zero, Vector2.zero);
            Stretch(veil);
            Image veilImage = veil.gameObject.AddComponent<Image>();
            veilImage.color = new Color(0.01f, 0.025f, 0.04f, 0.18f);
            veilImage.raycastTarget = false;

            CreateShadeBand(canvasRect, "Command Deck Shade", 0f, 0.41f, 0.76f);
            CreateShadeBand(canvasRect, "Command Deck Feather 1", 0.41f, 0.47f, 0.52f);
            CreateShadeBand(canvasRect, "Command Deck Feather 2", 0.47f, 0.53f, 0.29f);
            CreateShadeBand(canvasRect, "Command Deck Feather 3", 0.53f, 0.59f, 0.11f);

            CreateText(
                "System Eyebrow",
                canvasRect,
                "NAUT  //  FLIGHT COMPUTER",
                assets.UtilityFont,
                20,
                Solar,
                TextAnchor.MiddleLeft,
                new Vector2(690f, 34f),
                new Vector2(-484f, 466f));
            Text title = CreateText(
                "Title",
                canvasRect,
                "NAUT",
                assets.DisplayFont,
                116,
                Ice,
                TextAnchor.MiddleLeft,
                new Vector2(710f, 144f),
                new Vector2(-474f, 370f));
            Shadow titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            titleShadow.effectDistance = new Vector2(4f, -5f);
            CreateText(
                "Title Strapline",
                canvasRect,
                "ONE BODY  /  TWO MINDS  /  ONE PLANET",
                assets.UtilityFont,
                22,
                Cyan,
                TextAnchor.MiddleLeft,
                new Vector2(710f, 40f),
                new Vector2(-472f, 292f));

            RectTransform homePage = CreatePage("Home Page", canvasRect);
            Button singleplayer = BuildHomePage(
                homePage,
                assets,
                out Button multiplayer,
                out Button settings);
            RectTransform settingsPage = CreatePage("Settings Page", canvasRect);
            BuildSettingsPage(
                settingsPage,
                assets,
                out Slider volumeSlider,
                out Text volumeValue,
                out Slider sensitivitySlider,
                out Text sensitivityValue,
                out Button controlsButton,
                out Button settingsBackButton);
            RectTransform controlsPage = CreatePage("Controls Page", canvasRect);
            Button controlsBackButton = BuildControlsPage(controlsPage, assets);

            BuildPlanetTelemetry(canvasRect, assets);

            CreateText(
                "Footer",
                canvasRect,
                "ESC  BACK     //     ENTER  CONFIRM     //     LOCAL SAVE ENABLED",
                assets.UtilityFont,
                17,
                Muted,
                TextAnchor.MiddleLeft,
                new Vector2(700f, 34f),
                new Vector2(-475f, -500f));

            GameObject eventSystemObject = new GameObject(
                "Main Menu EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(root.transform, false);
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            settingsPage.gameObject.SetActive(false);
            controlsPage.gameObject.SetActive(false);

            SerializedObject serialized = new SerializedObject(controller);
            Assign(serialized, "homePage", homePage.gameObject);
            Assign(serialized, "settingsPage", settingsPage.gameObject);
            Assign(serialized, "controlsPage", controlsPage.gameObject);
            Assign(serialized, "singleplayerButton", singleplayer);
            Assign(serialized, "multiplayerButton", multiplayer);
            Assign(serialized, "settingsButton", settings);
            Assign(serialized, "settingsBackButton", settingsBackButton);
            Assign(serialized, "controlsButton", controlsButton);
            Assign(serialized, "controlsBackButton", controlsBackButton);
            Assign(serialized, "volumeSlider", volumeSlider);
            Assign(serialized, "volumeValue", volumeValue);
            Assign(serialized, "sensitivitySlider", sensitivitySlider);
            Assign(serialized, "sensitivityValue", sensitivityValue);
            Assign(serialized, "menuPlanet", planet);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }

        private static RectTransform CreatePage(string name, Transform parent)
        {
            RectTransform page = CreateRect(name, parent, Vector2.zero, Vector2.zero);
            Stretch(page);
            return page;
        }

        private static Button BuildHomePage(
            Transform page,
            MenuAssets assets,
            out Button multiplayer,
            out Button settings)
        {
            RectTransform console = CreateConsole(page, assets.Popup, "Mission Console");
            CreateHeader(console, assets, "MISSION SELECT", "CRASH SITE DEPLOYMENT");

            Button singleplayer = CreateMenuButton(
                "Singleplayer",
                console,
                "SINGLEPLAYER",
                "BEGIN CRASH SITE DEPLOYMENT",
                assets,
                assets.PlayIcon,
                new Vector2(0f, 62f),
                true);
            multiplayer = CreateMenuButton(
                "Multiplayer",
                console,
                "MULTIPLAYER",
                "CREW UPLINK UNAVAILABLE",
                assets,
                null,
                new Vector2(0f, -58f),
                false);
            CreateText(
                "Offline Badge",
                multiplayer.transform,
                "OFFLINE",
                assets.UtilityFont,
                16,
                Offline,
                TextAnchor.MiddleRight,
                new Vector2(100f, 28f),
                new Vector2(216f, 0f));
            settings = CreateMenuButton(
                "Settings",
                console,
                "SETTINGS",
                "CALIBRATE SUIT SYSTEMS",
                assets,
                assets.SettingsIcon,
                new Vector2(0f, -178f),
                true);

            CreateText(
                "Console Footer",
                console,
                "MISSION CLOCK  00:00:00     //     SURVIVAL LINK  READY",
                assets.UtilityFont,
                15,
                Muted,
                TextAnchor.MiddleCenter,
                new Vector2(590f, 26f),
                new Vector2(0f, -258f));
            return singleplayer;
        }

        private static void BuildSettingsPage(
            Transform page,
            MenuAssets assets,
            out Slider volumeSlider,
            out Text volumeValue,
            out Slider sensitivitySlider,
            out Text sensitivityValue,
            out Button controlsButton,
            out Button backButton)
        {
            RectTransform console = CreateConsole(page, assets.Popup, "Calibration Console");
            CreateHeader(console, assets, "SYSTEM SETTINGS", "SUIT CALIBRATION");

            volumeSlider = CreateSlider(
                "Master Volume",
                console,
                "MASTER VOLUME",
                assets,
                new Vector2(0f, 72f),
                out volumeValue);
            sensitivitySlider = CreateSlider(
                "Look Sensitivity",
                console,
                "LOOK SENSITIVITY",
                assets,
                new Vector2(0f, -44f),
                out sensitivityValue);

            controlsButton = CreateMenuButton(
                "Controls",
                console,
                "CONTROLS",
                "VIEW CURRENT INPUT MAP",
                assets,
                null,
                new Vector2(0f, -164f),
                true,
                new Vector2(560f, 82f));
            backButton = CreateCompactButton(
                "Back",
                console,
                "<  BACK TO MISSIONS",
                assets,
                new Vector2(0f, -254f));
        }

        private static Button BuildControlsPage(Transform page, MenuAssets assets)
        {
            RectTransform console = CreateConsole(page, assets.Popup, "Control Map Console");
            CreateHeader(console, assets, "CONTROL MAP", "CURRENT SINGLEPLAYER BINDINGS");

            RectTransform mapWell = CreateRect(
                "Bindings",
                console,
                new Vector2(588f, 330f),
                new Vector2(0f, -54f));
            Image wellImage = mapWell.gameObject.AddComponent<Image>();
            wellImage.color = new Color(Panel.r, Panel.g, Panel.b, 0.72f);
            wellImage.raycastTarget = false;

            string[] actions = { "MOVE", "LOOK", "JUMP", "SPRINT", "FIRE", "MELEE", "EMOTES", "SETTINGS" };
            string[] bindings = { "WASD", "MOUSE", "SPACE", "LEFT SHIFT", "LEFT MOUSE", "V", "B", "ESC" };
            for (int i = 0; i < actions.Length; i++)
            {
                int column = i / 4;
                int row = i % 4;
                float x = column == 0 ? -148f : 148f;
                float y = 104f - row * 68f;
                CreateControlRow(mapWell, assets, actions[i], bindings[i], new Vector2(x, y));
            }

            CreateText(
                "Control Note",
                console,
                "CO-OP RESPONSIBILITIES WILL APPEAR HERE WHEN THE CREW LINK IS LOCKED.",
                assets.UtilityFont,
                15,
                Muted,
                TextAnchor.MiddleCenter,
                new Vector2(590f, 28f),
                new Vector2(0f, -226f));
            return CreateCompactButton(
                "Back",
                console,
                "<  BACK TO SETTINGS",
                assets,
                new Vector2(0f, -270f));
        }

        private static void CreateControlRow(
            Transform parent,
            MenuAssets assets,
            string action,
            string binding,
            Vector2 position)
        {
            RectTransform row = CreateRect("Binding " + action, parent, new Vector2(250f, 54f), position);
            Image rowImage = row.gameObject.AddComponent<Image>();
            rowImage.color = new Color(Glass.r, Glass.g, Glass.b, 0.9f);
            rowImage.raycastTarget = false;
            CreateText(
                "Action",
                row,
                action,
                assets.UtilityFont,
                16,
                Cyan,
                TextAnchor.MiddleLeft,
                new Vector2(96f, 32f),
                new Vector2(-67f, 0f));
            CreateText(
                "Binding",
                row,
                binding,
                assets.DisplayFont,
                17,
                Ice,
                TextAnchor.MiddleRight,
                new Vector2(128f, 32f),
                new Vector2(49f, 0f));
        }

        private static RectTransform CreateConsole(Transform parent, Sprite popup, string name)
        {
            RectTransform console = CreateRect(name, parent, new Vector2(700f, 620f), new Vector2(-475f, -86f));
            Image frame = console.gameObject.AddComponent<Image>();
            frame.sprite = popup;
            frame.type = Image.Type.Sliced;
            frame.color = new Color(0.88f, 0.96f, 1f, 0.98f);
            frame.raycastTarget = true;

            RectTransform glass = CreateRect("Void Glass", console, new Vector2(642f, 562f), Vector2.zero);
            Image glassImage = glass.gameObject.AddComponent<Image>();
            glassImage.color = new Color(Glass.r, Glass.g, Glass.b, 0.975f);
            glassImage.raycastTarget = false;
            return console;
        }

        private static void CreateHeader(Transform console, MenuAssets assets, string title, string eyebrow)
        {
            RectTransform header = CreateRect(
                "Instrument Header",
                console,
                new Vector2(580f, 94f),
                new Vector2(0f, 233f));
            Image headerImage = header.gameObject.AddComponent<Image>();
            headerImage.sprite = assets.Header;
            headerImage.type = Image.Type.Sliced;
            headerImage.color = new Color(0.72f, 0.88f, 0.96f, 1f);
            headerImage.raycastTarget = false;
            CreateText(
                "Eyebrow",
                header,
                eyebrow,
                assets.UtilityFont,
                14,
                DeepSpace,
                TextAnchor.MiddleLeft,
                new Vector2(470f, 20f),
                new Vector2(18f, 19f));
            CreateText(
                "Title",
                header,
                title,
                assets.DisplayFont,
                28,
                Void,
                TextAnchor.MiddleLeft,
                new Vector2(480f, 42f),
                new Vector2(24f, -13f));

            RectTransform beacon = CreateRect(
                "Header Beacon",
                header,
                new Vector2(9f, 58f),
                new Vector2(-258f, 0f));
            Image beaconImage = beacon.gameObject.AddComponent<Image>();
            beaconImage.color = Solar;
            beaconImage.raycastTarget = false;
        }

        private static Button CreateMenuButton(
            string name,
            Transform parent,
            string title,
            string detail,
            MenuAssets assets,
            Sprite icon,
            Vector2 position,
            bool interactable,
            Vector2? sizeOverride = null)
        {
            Vector2 size = sizeOverride ?? new Vector2(580f, 94f);
            RectTransform rect = CreateRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = assets.ButtonIdle;
            image.type = Image.Type.Sliced;
            image.color = interactable ? Color.white : new Color(0.42f, 0.52f, 0.58f, 0.82f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = assets.ButtonHover,
                selectedSprite = assets.ButtonHover,
                pressedSprite = assets.ButtonPressed,
                disabledSprite = assets.ButtonIdle
            };
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            button.interactable = interactable;

            float textX = icon != null ? 38f : -8f;
            if (icon != null)
            {
                RectTransform iconRect = CreateRect("Icon", rect, new Vector2(46f, 46f), new Vector2(-238f, 0f));
                Image iconImage = iconRect.gameObject.AddComponent<Image>();
                iconImage.sprite = icon;
                iconImage.color = interactable ? Ice : Muted;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }

            CreateText(
                "Label",
                rect,
                title,
                assets.DisplayFont,
                27,
                interactable ? Ice : Muted,
                TextAnchor.MiddleLeft,
                new Vector2(390f, 38f),
                new Vector2(textX, 15f));
            CreateText(
                "Detail",
                rect,
                detail,
                assets.UtilityFont,
                15,
                interactable ? Cyan : Offline,
                TextAnchor.MiddleLeft,
                new Vector2(390f, 24f),
                new Vector2(textX, -21f));
            return button;
        }

        private static Button CreateCompactButton(
            string name,
            Transform parent,
            string label,
            MenuAssets assets,
            Vector2 position)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(390f, 64f), position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = assets.ButtonIdle;
            image.type = Image.Type.Sliced;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = assets.ButtonHover,
                selectedSprite = assets.ButtonHover,
                pressedSprite = assets.ButtonPressed,
                disabledSprite = assets.ButtonIdle
            };
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            CreateText(
                "Label",
                rect,
                label,
                assets.DisplayFont,
                18,
                Ice,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.zero,
                true);
            return button;
        }

        private static Slider CreateSlider(
            string name,
            Transform parent,
            string label,
            MenuAssets assets,
            Vector2 position,
            out Text valueText)
        {
            RectTransform row = CreateRect(name, parent, new Vector2(580f, 105f), position);
            CreateText(
                "Label",
                row,
                label,
                assets.DisplayFont,
                21,
                Ice,
                TextAnchor.MiddleLeft,
                new Vector2(410f, 34f),
                new Vector2(-72f, 31f));
            valueText = CreateText(
                "Value",
                row,
                "100%",
                assets.UtilityFont,
                22,
                Solar,
                TextAnchor.MiddleRight,
                new Vector2(110f, 34f),
                new Vector2(226f, 31f));

            RectTransform track = CreateRect("Track", row, new Vector2(530f, 34f), new Vector2(0f, -20f));
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = assets.SliderTrack;
            trackImage.type = Image.Type.Sliced;
            trackImage.raycastTarget = true;

            RectTransform fillArea = CreateRect("Fill Area", track, Vector2.zero, Vector2.zero);
            Stretch(fillArea, 8f, 8f, 8f, 8f);
            RectTransform fill = CreateRect("Fill", fillArea, Vector2.zero, Vector2.zero);
            Stretch(fill);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = assets.SliderFill;
            fillImage.type = Image.Type.Sliced;
            fillImage.raycastTarget = false;

            RectTransform handleArea = CreateRect("Handle Slide Area", track, Vector2.zero, Vector2.zero);
            Stretch(handleArea, 18f, 18f, 0f, 0f);
            RectTransform handle = CreateRect("Handle", handleArea, new Vector2(40f, 40f), Vector2.zero);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = assets.SliderHandle;
            handleImage.preserveAspect = true;

            Slider slider = track.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            return slider;
        }

        private static void BuildPlanetTelemetry(Transform parent, MenuAssets assets)
        {
            RectTransform card = CreateRect(
                "Planet Telemetry",
                parent,
                new Vector2(430f, 136f),
                new Vector2(676f, -394f));
            Image cardImage = card.gameObject.AddComponent<Image>();
            cardImage.color = new Color(Void.r, Void.g, Void.b, 0.68f);
            cardImage.raycastTarget = false;
            RectTransform bar = CreateRect("Signal Bar", card, new Vector2(6f, 96f), new Vector2(-190f, 0f));
            Image barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = Solar;
            barImage.raycastTarget = false;
            CreateText(
                "Label",
                card,
                "LANDING WORLD  //  N-150",
                assets.UtilityFont,
                16,
                Solar,
                TextAnchor.MiddleLeft,
                new Vector2(360f, 26f),
                new Vector2(20f, 38f));
            CreateText(
                "Readout",
                card,
                "CRASH SITE SIGNAL LOCKED",
                assets.DisplayFont,
                20,
                Ice,
                TextAnchor.MiddleLeft,
                new Vector2(360f, 34f),
                new Vector2(20f, 4f));
            CreateText(
                "Status",
                card,
                "ATMOSPHERE: VOID    /    CREW: 1",
                assets.UtilityFont,
                15,
                Cyan,
                TextAnchor.MiddleLeft,
                new Vector2(360f, 26f),
                new Vector2(20f, -34f));
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            Font font,
            int fontSize,
            Color color,
            TextAnchor alignment,
            Vector2 size,
            Vector2 position,
            bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch) Stretch(rect, 12f, 12f, 6f, 6f);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.text = value;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void CreateShadeBand(
            Transform parent,
            string name,
            float minimumX,
            float maximumX,
            float alpha)
        {
            RectTransform band = CreateRect(name, parent, Vector2.zero, Vector2.zero);
            band.anchorMin = new Vector2(minimumX, 0f);
            band.anchorMax = new Vector2(maximumX, 1f);
            band.offsetMin = Vector2.zero;
            band.offsetMax = Vector2.zero;
            Image image = band.gameObject.AddComponent<Image>();
            image.color = new Color(Void.r, Void.g, Void.b, alpha);
            image.raycastTarget = false;
        }

        private static void Stretch(
            RectTransform rect,
            float left = 0f,
            float right = 0f,
            float bottom = 0f,
            float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void ConfigureBuildScenes()
        {
            var ordered = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(GameplayScenePath, true)
            };

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path == MainMenuScenePath || scene.path == GameplayScenePath)
                {
                    continue;
                }

                ordered.Add(scene);
            }

            EditorBuildSettings.scenes = ordered.ToArray();
        }

        private static void EnsureRuntimeIcon(string source, string destination)
        {
            if (File.Exists(destination))
            {
                return;
            }

            if (!File.Exists(source))
            {
                throw new InvalidOperationException($"Missing Cartoon UI source icon: {source}");
            }

            string directory = Path.GetDirectoryName(destination);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException($"Invalid destination path: {destination}");
            }

            Directory.CreateDirectory(directory);
            FileUtil.CopyFileOrDirectory(source, destination);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ConfigureTextureImports()
        {
            var borders = new Dictionary<string, Vector4>
            {
                [PopupPath] = new Vector4(28f, 28f, 28f, 28f),
                [ButtonIdlePath] = new Vector4(32f, 28f, 32f, 28f),
                [ButtonHoverPath] = new Vector4(32f, 28f, 32f, 28f),
                [ButtonPressedPath] = new Vector4(32f, 28f, 32f, 28f),
                [HeaderPath] = new Vector4(24f, 24f, 24f, 24f),
                [SliderTrackPath] = new Vector4(22f, 22f, 22f, 22f),
                [SliderFillPath] = new Vector4(22f, 22f, 22f, 22f),
                [SliderHandlePath] = Vector4.zero,
                [PlayIconPath] = Vector4.zero,
                [SettingsIconPath] = Vector4.zero
            };

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (KeyValuePair<string, Vector4> entry in borders)
            {
                TextureImporter importer = AssetImporter.GetAtPath(entry.Key) as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException($"Could not configure texture importer: {entry.Key}");
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.spriteBorder = entry.Value;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static MenuAssets LoadAssets()
        {
            return new MenuAssets
            {
                DisplayFont = RequireAsset<Font>(FontPath),
                UtilityFont = RequireAsset<Font>(NarrowFontPath),
                Popup = RequireAsset<Sprite>(PopupPath),
                ButtonIdle = RequireAsset<Sprite>(ButtonIdlePath),
                ButtonHover = RequireAsset<Sprite>(ButtonHoverPath),
                ButtonPressed = RequireAsset<Sprite>(ButtonPressedPath),
                Header = RequireAsset<Sprite>(HeaderPath),
                SliderTrack = RequireAsset<Sprite>(SliderTrackPath),
                SliderFill = RequireAsset<Sprite>(SliderFillPath),
                SliderHandle = RequireAsset<Sprite>(SliderHandlePath),
                PlayIcon = RequireAsset<Sprite>(PlayIconPath),
                SettingsIcon = RequireAsset<Sprite>(SettingsIconPath),
                Planet = RequireAsset<GameObject>(PlanetPrefabPath),
                Skybox = RequireAsset<Material>(SkyboxMaterialPath),
                Rocks = RequireAssets(RockPaths),
                Vegetation = RequireAssets(VegetationPaths),
                Structures = RequireAssets(StructurePaths)
            };
        }

        private static GameObject[] RequireAssets(string[] paths)
        {
            var assets = new GameObject[paths.Length];
            for (int index = 0; index < paths.Length; index++)
            {
                assets[index] = RequireAsset<GameObject>(paths[index]);
            }
            return assets;
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"Required main-menu asset is missing: {path}");
            }
            return asset;
        }

        private static UnityEngine.Object RequireReference(
            SerializedObject serialized,
            string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
            {
                throw new InvalidOperationException($"Main menu is missing '{propertyName}'.");
            }
            return property.objectReferenceValue;
        }

        private static void Assign(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Missing serialized property '{propertyName}'.");
            }
            property.objectReferenceValue = value;
        }

        private static Color Hex(string rgb)
        {
            if (!ColorUtility.TryParseHtmlString("#" + rgb, out Color color))
            {
                throw new ArgumentException($"Invalid colour '{rgb}'.", nameof(rgb));
            }
            return color;
        }

        private sealed class MenuAssets
        {
            public Font DisplayFont;
            public Font UtilityFont;
            public Sprite Popup;
            public Sprite ButtonIdle;
            public Sprite ButtonHover;
            public Sprite ButtonPressed;
            public Sprite Header;
            public Sprite SliderTrack;
            public Sprite SliderFill;
            public Sprite SliderHandle;
            public Sprite PlayIcon;
            public Sprite SettingsIcon;
            public GameObject Planet;
            public Material Skybox;
            public GameObject[] Rocks;
            public GameObject[] Vegetation;
            public GameObject[] Structures;
        }
    }
}

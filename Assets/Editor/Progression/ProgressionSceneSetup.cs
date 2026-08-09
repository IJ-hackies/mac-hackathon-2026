using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Progression.Editor
{
    /// <summary>
    /// Explicit, idempotent authoring support for the v1 progression presentation.
    /// It deliberately does not run automatically: the generated UI and station markers are
    /// intended to be reviewed in Unity before saving their prefab/scene changes.
    /// Runtime components are discovered by name so this tool stays independent from the
    /// gameplay implementation assembly while the feature is being integrated.
    /// </summary>
    public static class ProgressionSceneSetup
    {
        private const string PlayerRigPath = "Assets/Prefabs/PlayerRig.prefab";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string TextureFolder = "Assets/Art/Textures/UI/Progression";
        private const string FontPath = "Assets/Art/Fonts/UI/KenneyFuture.ttf";
        private const string NarrowFontPath = "Assets/Art/Fonts/UI/KenneyFutureNarrow.ttf";
        private const string GeneratedUiRoot = "Progression UI";
        private const string MarkerName = "Progression Marker";
        private const float StationApproachDistance = 4f;
        private const float MinimumInteractionRadius = 8f;
        private const float InteractionHeight = 1.35f;
        private const float BeaconRoofClearance = 3f;
        private static TestRunnerApi _testRunner;
        private static ProgressionTestCallback _testCallback;

        private static readonly StationSpec[] Stations =
        {
            new StationSpec("Base_Large", "SUPPLY CONSOLE", "Health + Ammo", "supply", new Color(0.10f, 0.92f, 0.72f), "CartoonSciFi_Icon_Heart.png"),
            new StationSpec("GeodesicDome", "SKILL ARCHIVE", "Run upgrades", "skillTree", new Color(0.57f, 0.38f, 1f), "CartoonSciFi_Icon_Info.png"),
            new StationSpec("SolarPanel_Structure", "SPECIAL SYSTEM", "Hold to Fire", "specialShop", new Color(1f, 0.67f, 0.13f), "CartoonSciFi_Icon_Lightning.png"),
        };

        [MenuItem("Tools/Progression/Prepare Presentation Assets")]
        public static void PreparePresentationAssets()
        {
            EnsureFolder(TextureFolder);
            foreach (AssetCopy spec in AssetCopies)
            {
                string destination = TextureFolder + "/" + spec.Destination;
                if (File.Exists(destination))
                {
                    continue;
                }

                string source = Path.GetFullPath(spec.Source);
                if (!File.Exists(source))
                {
                    throw new InvalidOperationException("Progression asset source is missing: " + spec.Source);
                }

                FileUtil.CopyFileOrDirectory(source, destination);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
            }

            foreach (string path in AssetCopies.Select(copy => TextureFolder + "/" + copy.Destination))
            {
                ConfigureSprite(path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Progression Setup: selected vendor UI assets copied into " + TextureFolder + ".");
        }

        [MenuItem("Tools/Progression/Build Player Rig UI")]
        public static void BuildPlayerRigUi()
        {
            PreparePresentationAssets();
            Font displayFont = RequireAsset<Font>(FontPath);
            Font utilityFont = RequireAsset<Font>(NarrowFontPath);
            GameObject rig = PrefabUtility.LoadPrefabContents(PlayerRigPath);
            try
            {
                Transform canvas = rig.transform.Find("HUD Canvas");
                if (canvas == null)
                {
                    throw new InvalidOperationException("PlayerRig has no direct HUD Canvas child.");
                }

                if (canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
                }

                Transform existing = canvas.Find(GeneratedUiRoot);
                if (existing != null)
                {
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);
                }

                RectTransform root = CreateRect(GeneratedUiRoot, canvas, Vector2.zero, Vector2.zero);
                Stretch(root);
                BuildGoldHud(root, utilityFont);
                BuildInteractionPrompt(root, utilityFont);
                BuildStationConsole(root, displayFont, utilityFont);
                BuildStatsOverview(root, displayFont, utilityFont);
                ConfigureKnownUiComponents(root, rig.transform.Find("Player"));

                if (PrefabUtility.SaveAsPrefabAsset(rig, PlayerRigPath) == null)
                {
                    throw new InvalidOperationException("Progression Setup could not save " + PlayerRigPath + ".");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rig);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PlayerRigPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("Progression Setup: rebuilt the isolated Progression UI hierarchy on PlayerRig.");
        }

        [MenuItem("Tools/Progression/Configure Sample Scene Stations")]
        public static void ConfigureSampleSceneStations()
        {
            Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            ConfigureStations(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("Progression Setup could not save " + SampleScenePath + ".");
            }
            Debug.Log("Progression Setup: configured three station markers and runtime wiring candidates.");
        }

        [MenuItem("Tools/Progression/Configure All (Review Before Commit) %#&p")]
        public static void ConfigureAll()
        {
            BuildPlayerRigUi();
            ConfigureSampleSceneStations();
            ValidateSampleScene();
        }

        [MenuItem("Tools/Progression/Validate Sample Scene")]
        public static void ValidateSampleScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SampleScenePath)
            {
                scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            }

            var errors = new List<string>();
            Transform player = FindByName(scene, "Player");
            Component playerProgression = GetComponentByTypeName(player?.gameObject, "Player.UI.Progression.PlayerProgression");
            Component stationController = GetComponentByTypeName(player?.gameObject, "Gameplay.Interaction.StationInteractionController");
            if (playerProgression == null) errors.Add("Player is missing PlayerProgression.");
            if (stationController == null) errors.Add("Player is missing StationInteractionController.");
            Transform planet = FindByName(scene, "Planet Ground");
            Vector3 planetCenter = planet != null ? planet.position : Vector3.zero;

            foreach (StationSpec station in Stations)
            {
                Transform target = FindByName(scene, station.TargetName);
                Transform marker = target != null ? target.Find(MarkerName) : null;
                if (target == null) errors.Add("Missing station target '" + station.TargetName + "'.");
                else if (marker == null) errors.Add("Station '" + station.TargetName + "' has no " + MarkerName + ".");
                else if (marker.GetComponent<SphereCollider>() == null) errors.Add("Station marker '" + station.TargetName + "' has no interaction-radius collider.");
                else if (!HasComponent(marker.gameObject, "Gameplay.Interaction.InteractableStation")) errors.Add("Station marker '" + station.TargetName + "' is not wired to InteractableStation.");
                else if (!HasUnitWorldScale(marker)) errors.Add("Station marker '" + station.TargetName + "' does not normalize its inherited model scale.");
                else
                {
                    Component interactable = GetComponentByTypeName(marker.gameObject, "Gameplay.Interaction.InteractableStation");
                    StationPlacement placement = CalculateStationPlacement(target, planetCenter);
                    float radius = WorldRadius(marker);
                    float configuredRadius = FloatProperty(interactable, "interactionRadius");
                    ValidateEnum(errors, interactable, "kind", Array.IndexOf(Stations, station), station.TargetName + " station kind");
                    ValidateReference(errors, interactable, "interactionController", stationController, station.TargetName + " interaction controller");
                    if (!HasComponent(marker.gameObject, "Gameplay.Interaction.StationMarkerVisual")) errors.Add("Station marker '" + station.TargetName + "' is missing StationMarkerVisual.");
                    if (radius < MinimumInteractionRadius || Mathf.Abs(radius - placement.InteractionRadius) > .1f)
                        errors.Add("Station marker '" + station.TargetName + "' does not cover its ground-level building footprint.");
                    if (Mathf.Abs(configuredRadius - radius) > .1f)
                        errors.Add("Station marker '" + station.TargetName + "' collider and proximity-query radii disagree.");
                    if (Vector3.Distance(marker.position, placement.InteractionPoint) > .15f)
                        errors.Add("Station marker '" + station.TargetName + "' is not at its radial ground interaction point.");
                    Transform beacon = marker.Find("Floating Beacon");
                    if (beacon == null || Vector3.Distance(beacon.position, placement.BeaconPoint) > .2f)
                        errors.Add("Station marker '" + station.TargetName + "' has no correctly placed roof beacon.");
                    if (beacon?.Find("Station Label")?.GetComponent<TextMesh>() == null)
                        errors.Add("Station marker '" + station.TargetName + "' has no readable world label.");
                }
            }

            GameObject rig = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigPath);
            Transform ui = rig != null ? rig.transform.Find("HUD Canvas/" + GeneratedUiRoot) : null;
            if (ui == null) errors.Add("PlayerRig has no progression UI hierarchy.");
            else
            {
                foreach (string required in new[] { "ProgressionHud/GoldHud", "InteractPrompt", "StationConsole/Supply", "StationConsole/SkillTree", "StationConsole/SpecialShop", "RunStatsOverview" })
                {
                    if (ui.Find(required) == null) errors.Add("Progression UI is missing '" + required + "'.");
                }
                foreach (string requiredComponent in new[]
                {
                    "Player.UI.Progression.ProgressionDataAdapter", "Gameplay.Interaction.InteractionPromptView",
                    "Gameplay.Interaction.StationMenuController", "Player.UI.Progression.RunStatsOverview"
                })
                {
                    if (!HasComponent(ui.gameObject, requiredComponent)) errors.Add("Progression UI is missing " + requiredComponent + ".");
                }

                Component adapter = GetComponentByTypeName(ui.gameObject, "Player.UI.Progression.ProgressionDataAdapter");
                Component menu = GetComponentByTypeName(ui.gameObject, "Gameplay.Interaction.StationMenuController");
                Component prompt = GetComponentByTypeName(ui.gameObject, "Gameplay.Interaction.InteractionPromptView");
                Component overview = GetComponentByTypeName(ui.gameObject, "Player.UI.Progression.RunStatsOverview");
                Transform prefabPlayer = rig.transform.Find("Player");
                Component prefabProgression = GetComponentByTypeName(prefabPlayer?.gameObject, "Player.UI.Progression.PlayerProgression");
                Transform gold = ui.Find("ProgressionHud/GoldHud");
                Component goldHud = GetComponentByTypeName(gold?.gameObject, "Player.UI.Progression.ProgressionGoldHud");

                ValidateReference(errors, adapter, "source", prefabProgression, "progression adapter source");
                ValidateReference(errors, goldHud, "progression", adapter, "gold HUD adapter");
                ValidateReference(errors, goldHud, "valueText", FindText(gold, "Label"), "gold HUD value text");
                if (gold?.Find("CoinIcon")?.GetComponent<Image>() == null) errors.Add("Gold HUD is missing its visible coin icon.");
                ValidateReference(errors, prompt, "root", ui.Find("InteractPrompt")?.gameObject, "interaction prompt root");
                ValidateReference(errors, prompt, "promptText", FindText(ui.Find("InteractPrompt"), "Label"), "interaction prompt text");
                ValidateReference(errors, menu, "shellRoot", ui.Find("StationConsole")?.gameObject, "station menu shell");
                ValidateReference(errors, menu, "supplyRoot", ui.Find("StationConsole/Supply")?.gameObject, "station menu supply root");
                ValidateReference(errors, menu, "skillTreeRoot", ui.Find("StationConsole/SkillTree")?.gameObject, "station menu skill root");
                ValidateReference(errors, menu, "specialShopRoot", ui.Find("StationConsole/SpecialShop")?.gameObject, "station menu special root");
                ValidateReference(errors, menu, "closeButton", ui.Find("StationConsole/CloseButton")?.GetComponent<Button>(), "station menu close button");
                ValidateReference(errors, menu, "progression", adapter, "station menu adapter");
                ValidateReference(errors, overview, "root", ui.Find("RunStatsOverview")?.gameObject, "run overview root");
                ValidateReference(errors, overview, "progression", adapter, "run overview adapter");
                ValidateReferenceArray(errors, overview, "statRows", FindTextsByPrefix(ui.Find("RunStatsOverview"), "Stat_").Cast<UnityEngine.Object>().ToArray(), "run overview stats");
                ValidateReference(errors, overview, "healthRow", FindText(ui.Find("RunStatsOverview"), "HealthRow"), "run overview health row");
                ValidateReference(errors, overview, "ammoRow", FindText(ui.Find("RunStatsOverview"), "AmmoRow"), "run overview ammo row");
                ValidateReference(errors, overview, "skillsRow", FindText(ui.Find("RunStatsOverview"), "SkillsRow"), "run overview skills row");
                ValidateStationScreenReferences(errors, ui, adapter);
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException("Progression validation failed:\n- " + string.Join("\n- ", errors));
            }

            Debug.Log("Progression validation: three station markers and all UI presentation roots are present.");
        }

        private static void ValidateStationScreenReferences(List<string> errors, Transform ui, Component adapter)
        {
            Transform supply = ui.Find("StationConsole/Supply");
            Component supplyScreen = GetComponentByTypeName(supply?.gameObject, "Player.UI.Progression.SupplyStationScreen");
            ValidateReference(errors, supplyScreen, "progression", adapter, "supply screen adapter");
            ValidateReference(errors, supplyScreen, "healthPackButton", PurchaseButtonAt(supply, 0), "health pack button");
            ValidateReference(errors, supplyScreen, "largeHealthPackButton", PurchaseButtonAt(supply, 1), "large health pack button");
            ValidateReference(errors, supplyScreen, "ammoPackButton", PurchaseButtonAt(supply, 2), "ammo pack button");
            ValidateReference(errors, supplyScreen, "goldText", FindText(supply, "GoldValue"), "supply gold text");

            Transform skillTree = ui.Find("StationConsole/SkillTree");
            Component skillScreen = GetComponentByTypeName(skillTree?.gameObject, "Player.UI.Progression.SkillTreeStationScreen");
            ValidateReference(errors, skillScreen, "progression", adapter, "skill tree adapter");
            var skillCards = new List<UnityEngine.Object>();
            for (int index = 0; index < 7; index++)
            {
                Transform card = skillTree?.Find("Card_" + (index + 1).ToString("00"));
                Component cardComponent = GetComponentByTypeName(card?.gameObject, "Player.UI.Progression.SkillUpgradeCard");
                skillCards.Add(cardComponent);
                ValidateEnum(errors, cardComponent, "stat", index, "skill card " + (index + 1) + " stat");
                ValidateReference(errors, cardComponent, "title", FindText(card, "Name"), "skill card " + (index + 1) + " title");
                ValidateReference(errors, cardComponent, "description", FindText(card, "Detail"), "skill card " + (index + 1) + " description");
                ValidateReference(errors, cardComponent, "currentValue", FindText(card, "Current"), "skill card " + (index + 1) + " current value");
                ValidateReference(errors, cardComponent, "nextValue", FindText(card, "Next"), "skill card " + (index + 1) + " next value");
                ValidateReference(errors, cardComponent, "levelPips", FindText(card, "Pips"), "skill card " + (index + 1) + " level pips");
                ValidateReference(errors, cardComponent, "purchaseButton", PurchaseButtonAt(card, 0), "skill card " + (index + 1) + " purchase button");
            }
            ValidateReferenceArray(errors, skillScreen, "cards", skillCards, "skill tree cards");
            ValidateReference(errors, skillScreen, "goldText", FindText(skillTree, "GoldValue"), "skill tree gold text");

            Transform special = ui.Find("StationConsole/SpecialShop");
            Component specialScreen = GetComponentByTypeName(special?.gameObject, "Player.UI.Progression.SpecialShopStationScreen");
            ValidateReference(errors, specialScreen, "progression", adapter, "special shop adapter");
            Transform catalog = special?.Find("CatalogScroll");
            if (catalog?.GetComponent<ScrollRect>() == null) errors.Add("Special catalog is missing its ScrollRect.");
            if (catalog?.Find("Viewport")?.GetComponent<RectMask2D>() == null) errors.Add("Special catalog is missing its clipped viewport.");
            if (catalog?.Find("Viewport/Content") == null) errors.Add("Special catalog is missing its scroll content.");
            var specialButtons = new List<UnityEngine.Object>();
            int expectedSpecialCount = Player.UI.Progression.ProgressionSpecialSkillCatalog.All.Count;
            for (int index = 0; index < expectedSpecialCount; index++)
            {
                Component button = PurchaseButtonAt(special, index);
                specialButtons.Add(button);
                if (button == null) errors.Add("Special catalog is missing card " + (index + 1) + ".");
            }
            ValidateReferenceArray(errors, specialScreen, "skillButtons", specialButtons, "special catalog buttons");
            ValidateReference(errors, specialScreen, "goldText", FindText(special, "GoldValue"), "special shop gold text");
        }

        private static Component PurchaseButtonAt(Transform screenOrCard, int cardIndex)
        {
            Transform card = cardIndex == 0 && screenOrCard?.Find("Purchase") != null
                ? screenOrCard
                : FindCard(screenOrCard, cardIndex);
            return GetComponentByTypeName(card?.Find("Purchase")?.gameObject, "Player.UI.Progression.ProgressionPurchaseButton");
        }

        [MenuItem("Tools/Progression/Run Progression Contract Tests %#&t")]
        public static void RunProgressionContractTests()
        {
            var filter = new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "Progression.Contracts.Tests" }
            };
            if (_testRunner != null && _testCallback != null) _testRunner.UnregisterCallbacks(_testCallback);
            _testRunner = new TestRunnerApi();
            _testCallback = new ProgressionTestCallback();
            _testRunner.RegisterCallbacks(_testCallback);
            _testRunner.Execute(new ExecutionSettings(filter));
            Debug.Log("Progression contract tests started (EditMode).");
        }

        [MenuItem("Tools/Progression/Preview/Supply Console %#&1")]
        public static void PreviewSupplyConsole() => PreviewStation(Gameplay.Interaction.StationKind.Supply);

        [MenuItem("Tools/Progression/Preview/Skill Archive %#&2")]
        public static void PreviewSkillArchive() => PreviewStation(Gameplay.Interaction.StationKind.SkillTree);

        [MenuItem("Tools/Progression/Preview/Special System %#&3")]
        public static void PreviewSpecialSystem() => PreviewStation(Gameplay.Interaction.StationKind.SpecialShop);

        [MenuItem("Tools/Progression/Preview/Hold Run Stats %#&o")]
        public static void HoldRunStatsPreview()
        {
            if (!Application.isPlaying || UnityEngine.InputSystem.Keyboard.current == null)
            {
                Debug.LogWarning("Run-stats preview is available only in Play Mode with a keyboard.");
                return;
            }

            UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,
                new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Tab));
        }

        [MenuItem("Tools/Progression/Preview/Release Preview Input %#&l")]
        public static void ReleasePreviewInput()
        {
            if (UnityEngine.InputSystem.Keyboard.current == null) return;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,
                new UnityEngine.InputSystem.LowLevel.KeyboardState());
        }

        [MenuItem("Tools/Progression/QA/Press Interact %#&6")]
        public static void PressInteract()
        {
            if (!Application.isPlaying || UnityEngine.InputSystem.Keyboard.current == null)
            {
                Debug.LogWarning("Interaction-input QA is available only in Play Mode with a keyboard.");
                return;
            }

            UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,
                new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.E));
        }

        [MenuItem("Tools/Progression/QA/Approach Supply Console %#&7")]
        public static void ApproachSupplyConsole() => ApproachStation(Gameplay.Interaction.StationKind.Supply);

        [MenuItem("Tools/Progression/QA/Approach Skill Archive %#&8")]
        public static void ApproachSkillArchive() => ApproachStation(Gameplay.Interaction.StationKind.SkillTree);

        [MenuItem("Tools/Progression/QA/Approach Special System %#&9")]
        public static void ApproachSpecialSystem() => ApproachStation(Gameplay.Interaction.StationKind.SpecialShop);

        private static void ApproachStation(Gameplay.Interaction.StationKind kind)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Station approach QA is available only in Play Mode.");
                return;
            }

            Gameplay.Interaction.StationInteractionController controller =
                UnityEngine.Object.FindFirstObjectByType<Gameplay.Interaction.StationInteractionController>();
            Gameplay.Interaction.InteractableStation station =
                UnityEngine.Object.FindObjectsByType<Gameplay.Interaction.InteractableStation>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.Kind == kind);
            if (controller == null || station == null)
            {
                Debug.LogError("Station approach QA could not find the player controller or requested station.");
                return;
            }

            Transform planet = FindByName(SceneManager.GetActiveScene(), "Planet Ground");
            Vector3 planetCenter = planet != null ? planet.position : Vector3.zero;
            Vector3 stationOffset = station.InteractionPoint - planetCenter;
            float stationSurfaceRadius = stationOffset.magnitude;
            float approachOffset = Mathf.Max(1f, station.InteractionRadius - 2f);
            Vector3 targetDirection = (stationOffset + station.transform.right * approachOffset).normalized;
            Vector3 targetPosition = planetCenter + targetDirection * stationSurfaceRadius;
            Quaternion targetRotation = Quaternion.FromToRotation(controller.transform.up, targetDirection) *
                                        controller.transform.rotation;
            Rigidbody body = controller.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = targetPosition;
                body.rotation = targetRotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
            controller.transform.SetPositionAndRotation(targetPosition, targetRotation);
            controller.ClearNearby();
            controller.RefreshStations();
            Physics.SyncTransforms();
            controller.RefreshNearby();
            float distance = Vector3.Distance(controller.transform.position, station.InteractionPoint);
            Gameplay.Interaction.InteractionPromptView prompt =
                UnityEngine.Object.FindFirstObjectByType<Gameplay.Interaction.InteractionPromptView>();
            Debug.Log("Station approach QA: moved the player " + distance.ToString("F2") +
                      "m from " + station.DisplayName + " (range " +
                      station.InteractionRadius.ToString("F2") + "m, in range: " +
                      station.IsInRange(controller.transform.position) + ", selected: " +
                      (controller.NearbyStation != null ? controller.NearbyStation.DisplayName : "none") +
                      ", prompt visible: " + (prompt != null && prompt.IsVisible) +
                      "). Use E to verify the regular interaction path.");
        }

        private static void PreviewStation(Gameplay.Interaction.StationKind kind)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Progression preview is available only in Play Mode.");
                return;
            }

            Gameplay.Interaction.StationMenuController menu =
                UnityEngine.Object.FindFirstObjectByType<Gameplay.Interaction.StationMenuController>();
            Gameplay.Interaction.InteractableStation station =
                UnityEngine.Object.FindObjectsByType<Gameplay.Interaction.InteractableStation>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.Kind == kind);
            if (menu == null || station == null)
            {
                Debug.LogError("Progression preview could not find the station menu or requested station.");
                return;
            }

            menu.Open(station);
        }

        private static void ConfigureStations(Scene scene)
        {
            Transform player = FindByName(scene, "Player");
            Transform planet = FindByName(scene, "Planet Ground");
            Vector3 planetCenter = planet != null ? planet.position : Vector3.zero;
            Component progression = player != null
                ? TryAttachAndConfigure(player.gameObject, "Player.UI.Progression.PlayerProgression", null)
                : null;
            Component stationController = player != null
                ? TryAttachAndConfigure(player.gameObject, "Gameplay.Interaction.StationInteractionController", null)
                : null;
            Transform ui = FindByName(scene, GeneratedUiRoot);
            Component stationMenu = GetComponentByTypeName(ui?.gameObject, "Gameplay.Interaction.StationMenuController");
            Component prompt = GetComponentByTypeName(ui?.gameObject, "Gameplay.Interaction.InteractionPromptView");
            SetObject(stationController, "stationMenu", stationMenu);
            SetObject(stationController, "prompt", prompt);

            foreach (StationSpec station in Stations)
            {
                Transform target = FindByName(scene, station.TargetName);
                if (target == null)
                {
                    throw new InvalidOperationException("SampleScene is missing station model '" + station.TargetName + "'.");
                }

                Transform oldMarker = target.Find(MarkerName);
                if (oldMarker != null) UnityEngine.Object.DestroyImmediate(oldMarker.gameObject);
                StationPlacement placement = CalculateStationPlacement(target, planetCenter);
                GameObject marker = new GameObject(MarkerName);
                marker.transform.SetParent(target, false);
                marker.transform.position = placement.InteractionPoint;
                marker.transform.rotation = Quaternion.FromToRotation(Vector3.up, placement.RadialUp);
                // Non-uniform model scale affects a rotated child's axes differently. Measure
                // the inherited scale after applying the radial rotation, then cancel that exact
                // result so the collider radius and beacon dimensions remain world-space values.
                marker.transform.localScale = Vector3.one;
                marker.transform.localScale = InverseLossyScale(marker.transform.lossyScale);
                SphereCollider range = marker.AddComponent<SphereCollider>();
                range.isTrigger = true;
                range.radius = placement.InteractionRadius;
                Transform icon = BuildWorldIcon(marker.transform, station,
                    marker.transform.InverseTransformPoint(placement.BeaconPoint));
                ConfigureMarkerVisual(marker, icon, station.Accent);
                Component interactable = TryAttachAndConfigure(marker, "Gameplay.Interaction.InteractableStation", station);
                SetObject(interactable, "interactionController", stationController);
                SetFloat(interactable, "interactionRadius", placement.InteractionRadius);
            }

            if (player == null || progression == null || stationController == null)
                throw new InvalidOperationException("SampleScene must contain a Player for progression station wiring.");
        }

        private static StationPlacement CalculateStationPlacement(Transform target, Vector3 planetCenter)
        {
            Transform generatedMarker = target.Find(MarkerName);
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => generatedMarker == null || !renderer.transform.IsChildOf(generatedMarker))
                .ToArray();
            if (renderers.Length == 0)
            {
                Vector3 fallbackUp = (target.position - planetCenter).normalized;
                if (fallbackUp.sqrMagnitude < .5f) fallbackUp = target.up;
                return new StationPlacement(target.position + fallbackUp * InteractionHeight,
                    target.position + fallbackUp * (InteractionHeight + BeaconRoofClearance),
                    fallbackUp, MinimumInteractionRadius);
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            Vector3 radialUp = (bounds.center - planetCenter).normalized;
            if (radialUp.sqrMagnitude < .5f) radialUp = target.up;
            float radialExtent = ProjectedExtent(bounds.extents, radialUp);
            float tangentialExtent = TangentialExtent(bounds.extents, radialUp);
            Vector3 surfacePoint = bounds.center - radialUp * radialExtent;
            Vector3 interactionPoint = surfacePoint + radialUp * InteractionHeight;
            Vector3 beaconPoint = bounds.center + radialUp * (radialExtent + BeaconRoofClearance);
            float radius = Mathf.Max(MinimumInteractionRadius, tangentialExtent + StationApproachDistance);
            return new StationPlacement(interactionPoint, beaconPoint, radialUp, radius);
        }

        private static float ProjectedExtent(Vector3 extents, Vector3 direction) =>
            Mathf.Abs(direction.x) * extents.x + Mathf.Abs(direction.y) * extents.y +
            Mathf.Abs(direction.z) * extents.z;

        private static float TangentialExtent(Vector3 extents, Vector3 radialUp)
        {
            float largest = 0f;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = new Vector3(extents.x * x, extents.y * y, extents.z * z);
                largest = Mathf.Max(largest, Vector3.ProjectOnPlane(corner, radialUp).magnitude);
            }
            return largest;
        }

        private static Vector3 InverseLossyScale(Vector3 scale)
        {
            return new Vector3(InverseScaleAxis(scale.x), InverseScaleAxis(scale.y), InverseScaleAxis(scale.z));
        }

        private static float InverseScaleAxis(float scale)
        {
            return Mathf.Abs(scale) > .0001f ? 1f / Mathf.Abs(scale) : 1f;
        }

        private static float WorldRadius(Transform marker)
        {
            SphereCollider collider = marker != null ? marker.GetComponent<SphereCollider>() : null;
            if (collider == null) return 0f;
            Vector3 scale = marker.lossyScale;
            float largestAxis = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return collider.radius * largestAxis;
        }

        private static bool HasUnitWorldScale(Transform marker)
        {
            if (marker == null) return false;
            Vector3 scale = marker.lossyScale;
            return Mathf.Abs(Mathf.Abs(scale.x) - 1f) < .02f &&
                   Mathf.Abs(Mathf.Abs(scale.y) - 1f) < .02f &&
                   Mathf.Abs(Mathf.Abs(scale.z) - 1f) < .02f;
        }

        private static Transform BuildWorldIcon(Transform parent, StationSpec station, Vector3 localPosition)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/" + station.IconFile);
            var beacon = new GameObject("Floating Beacon");
            beacon.transform.SetParent(parent, false);
            beacon.transform.localPosition = localPosition;
            beacon.transform.localScale = Vector3.one;

            var halo = new GameObject("Halo", typeof(SpriteRenderer));
            halo.transform.SetParent(beacon.transform, false);
            halo.transform.localScale = Vector3.one * 12f;
            SpriteRenderer haloRenderer = halo.GetComponent<SpriteRenderer>();
            haloRenderer.sprite = SpriteAt("SpaceExpansion_Icon_Crosshair.png");
            haloRenderer.color = new Color(station.Accent.r, station.Accent.g, station.Accent.b, .62f);
            haloRenderer.sortingOrder = 9;

            var icon = new GameObject("Semantic Icon", typeof(SpriteRenderer));
            icon.transform.SetParent(beacon.transform, false);
            icon.transform.localScale = Vector3.one * 1.8f;
            SpriteRenderer renderer = icon.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = station.Accent;
            renderer.sortingOrder = 10;

            var label = new GameObject("Station Label", typeof(TextMesh));
            label.transform.SetParent(beacon.transform, false);
            label.transform.localPosition = Vector3.down * 2.05f;
            TextMesh text = label.GetComponent<TextMesh>();
            text.font = RequireAsset<Font>(FontPath);
            text.text = station.Title;
            text.anchor = TextAnchor.UpperCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 56;
            text.characterSize = .09f;
            text.color = station.Accent;
            text.richText = false;
            MeshRenderer labelRenderer = label.GetComponent<MeshRenderer>();
            labelRenderer.sharedMaterial = text.font.material;
            labelRenderer.sortingOrder = 11;
            return beacon.transform;
        }

        private static void ConfigureMarkerVisual(GameObject marker, Transform icon, Color accent)
        {
            Component visual = TryAttachAndConfigure(marker, "Gameplay.Interaction.StationMarkerVisual", null);
            SetObject(visual, "visualRoot", icon);
            SetObjectArray(visual, "colorTargets", icon != null
                ? new UnityEngine.Object[]
                {
                    icon.Find("Semantic Icon")?.GetComponent<SpriteRenderer>(),
                    icon.Find("Station Label")?.GetComponent<MeshRenderer>()
                }
                : Array.Empty<UnityEngine.Object>());
            SetColor(visual, "baseColor", accent);
            SetVector3(visual, "baseScale", icon != null ? icon.localScale : Vector3.one);
        }

        private static void BuildGoldHud(Transform parent, Font font)
        {
            RectTransform hud = CreateRect("ProgressionHud", parent, Vector2.zero, Vector2.zero);
            Stretch(hud);
            RectTransform gold = CreateRect("GoldHud", hud, new Vector2(210f, 42f), new Vector2(-350f, -28f), Vector2.one);
            Image panel = gold.gameObject.AddComponent<Image>();
            panel.sprite = SpriteAt("SpaceExpansion_Panel.png");
            panel.type = Image.Type.Sliced;
            panel.color = new Color(0.025f, 0.10f, 0.17f, 0.94f);
            RectTransform coin = CreateRect("CoinIcon", gold, new Vector2(29f, 29f), new Vector2(21f, 0f), new Vector2(0f, .5f));
            Image coinImage = coin.gameObject.AddComponent<Image>();
            coinImage.sprite = SpriteAt("CartoonSciFi_Icon_Star.png");
            coinImage.preserveAspect = true;
            coinImage.color = new Color(1f, .78f, .2f);
            coinImage.raycastTarget = false;
            AddText("Label", gold, "GOLD  10,000", font, 21, TextAnchor.MiddleCenter, Color.white, new Vector2(14f, 0f), new Vector2(186f, 42f));
        }

        private static void BuildInteractionPrompt(Transform parent, Font font)
        {
            RectTransform prompt = CreateRect("InteractPrompt", parent, new Vector2(360f, 54f), new Vector2(0f, -145f), new Vector2(.5f, .5f));
            Image panel = prompt.gameObject.AddComponent<Image>();
            panel.sprite = SpriteAt("CartoonSciFi_Button_Idle.png");
            panel.type = Image.Type.Sliced;
            panel.color = new Color(.05f, .20f, .29f, .96f);
            AddText("Label", prompt, "E  -  INTERACT", font, 22, TextAnchor.MiddleCenter, Color.white, Vector2.zero, prompt.sizeDelta);
            prompt.gameObject.SetActive(false);
        }

        private static void BuildStationConsole(Transform parent, Font displayFont, Font utilityFont)
        {
            RectTransform console = CreateRect("StationConsole", parent, Vector2.zero, Vector2.zero);
            Stretch(console);
            Image dimmer = console.gameObject.AddComponent<Image>();
            dimmer.color = new Color(.01f, .025f, .06f, .76f);
            dimmer.raycastTarget = true;
            BuildStationScreen(console, "Supply", "SUPPLY CONSOLE", "BASE_LARGE  /  FIELD RESUPPLY", new Color(.1f, .92f, .72f), new[] { "HEALTH PACK", "LARGE HEALTH PACK", "AMMO PACK" }, displayFont, utilityFont);
            BuildStationScreen(console, "SkillTree", "SKILL ARCHIVE", "GEODESIC DOME  /  RUN UPGRADES", new Color(.57f, .38f, 1f), new[] { "MAX HP", "MOVEMENT", "FIRE RATE", "SHOOTING DMG", "MELEE DMG", "DEFENSE", "MAX AMMO" }, displayFont, utilityFont);
            BuildSpecialSystemScreen(console, displayFont, utilityFont);
            CreateButton("CloseButton", console, "CLOSE", new Color(.65f, .78f, .88f), utilityFont,
                new Vector2(540f, -300f), new Vector2(132f, 34f), new Vector2(.5f, .5f));
            console.gameObject.SetActive(false);
        }

        private static void BuildStationScreen(Transform parent, string name, string title, string subtitle, Color accent, IReadOnlyList<string> cards, Font displayFont, Font utilityFont)
        {
            RectTransform screen = CreateRect(name, parent, new Vector2(1260f, 700f), Vector2.zero, new Vector2(.5f, .5f));
            Image background = screen.gameObject.AddComponent<Image>();
            background.sprite = SpriteAt("SpaceExpansion_Panel.png");
            background.type = Image.Type.Sliced;
            background.color = new Color(.035f, .105f, .17f, .985f);
            AddText("Title", screen, title, displayFont, 37, TextAnchor.MiddleLeft, accent, new Vector2(52f, -55f), new Vector2(600f, 46f), new Vector2(0f, 1f));
            AddText("Subtitle", screen, subtitle, utilityFont, 18, TextAnchor.MiddleLeft, new Color(.72f, .85f, .92f), new Vector2(55f, -102f), new Vector2(750f, 30f), new Vector2(0f, 1f));
            AddText("CloseHint", screen, "E / ESC  CLOSE", utilityFont, 16, TextAnchor.MiddleRight, new Color(.72f, .85f, .92f), new Vector2(-50f, -62f), new Vector2(250f, 30f), new Vector2(1f, 1f));
            AddText("GoldValue", screen, "G 10,000", utilityFont, 20, TextAnchor.MiddleRight, accent, new Vector2(-50f, -100f), new Vector2(240f, 30f), new Vector2(1f, 1f));

            for (int index = 0; index < cards.Count; index++)
            {
                CardLayout layout = GetCardLayout(name, index, cards.Count);
                RectTransform card = CreateRect("Card_" + (index + 1).ToString("00"), screen, layout.Size, layout.Position, new Vector2(0f, 1f));
                Image cardPanel = card.gameObject.AddComponent<Image>();
                cardPanel.sprite = SpriteAt("CartoonSciFi_Popup.png");
                cardPanel.type = Image.Type.Sliced;
                cardPanel.color = new Color(.08f, .18f, .25f, 1f);
                BuildCardIcon(card, IconFor(name, index), accent);
                AddText("Name", card, cards[index], displayFont, 22, TextAnchor.UpperLeft, Color.white, new Vector2(24f, -24f), new Vector2(layout.Size.x - 100f, 34f), new Vector2(0f, 1f));
                bool isSkillCard = name == "SkillTree";
                AddText("Detail", card, CardDescription(name, index), utilityFont, isSkillCard ? 14 : 17, TextAnchor.UpperLeft, new Color(.69f, .82f, .9f), new Vector2(24f, -55f), new Vector2(layout.Size.x - 48f, isSkillCard ? 23f : 38f), new Vector2(0f, 1f));
                if (isSkillCard)
                {
                    AddText("Current", card, "NOW  -", utilityFont, 16, TextAnchor.UpperLeft, Color.white, new Vector2(24f, -79f), new Vector2(140f, 22f), new Vector2(0f, 1f));
                    AddText("Next", card, "NEXT  -", utilityFont, 16, TextAnchor.UpperLeft, accent, new Vector2(170f, -79f), new Vector2(layout.Size.x - 194f, 22f), new Vector2(0f, 1f));
                    AddText("Pips", card, "LV 1  oooooooooo", utilityFont, 13, TextAnchor.UpperLeft, new Color(.69f, .82f, .9f), new Vector2(24f, -106f), new Vector2(layout.Size.x - 48f, 22f), new Vector2(0f, 1f));
                }
                else
                {
                    AddText("Value", card, CardValue(name, index), utilityFont, 18, TextAnchor.UpperLeft, accent, new Vector2(24f, -108f), new Vector2(layout.Size.x - 48f, 28f), new Vector2(0f, 1f));
                }
                AddText("Cost", card, InitialCardCost(name, index), utilityFont, 16, TextAnchor.UpperLeft, accent, new Vector2(24f, -143f), new Vector2(100f, 24f), new Vector2(0f, 1f));
                CreateButton("Purchase", card, "UPGRADE", accent, utilityFont, new Vector2(-22f, 18f), new Vector2(152f, 38f), new Vector2(1f, 0f));
            }
            screen.gameObject.SetActive(false);
        }

        /// <summary>Two-column, vertical-scroll catalog so every independent special is visible.</summary>
        private static void BuildSpecialSystemScreen(Transform parent, Font displayFont, Font utilityFont)
        {
            Color accent = new Color(1f, .67f, .13f);
            RectTransform screen = CreateRect("SpecialShop", parent, new Vector2(1260f, 700f), Vector2.zero, new Vector2(.5f, .5f));
            Image background = screen.gameObject.AddComponent<Image>();
            background.sprite = SpriteAt("SpaceExpansion_Panel.png");
            background.type = Image.Type.Sliced;
            background.color = new Color(.035f, .105f, .17f, .985f);
            AddText("Title", screen, "SPECIAL SYSTEM", displayFont, 37, TextAnchor.MiddleLeft, accent, new Vector2(52f, -55f), new Vector2(600f, 46f), new Vector2(0f, 1f));
            AddText("Subtitle", screen, "SOLAR ARRAY  /  ONE-TIME SKILL CATALOG", utilityFont, 18, TextAnchor.MiddleLeft, new Color(.72f, .85f, .92f), new Vector2(55f, -102f), new Vector2(750f, 30f), new Vector2(0f, 1f));
            AddText("CloseHint", screen, "E / ESC  CLOSE", utilityFont, 16, TextAnchor.MiddleRight, new Color(.72f, .85f, .92f), new Vector2(-50f, -62f), new Vector2(250f, 30f), new Vector2(1f, 1f));
            AddText("GoldValue", screen, "G 100", utilityFont, 20, TextAnchor.MiddleRight, accent, new Vector2(-50f, -100f), new Vector2(240f, 30f), new Vector2(1f, 1f));

            RectTransform scrollRoot = CreateRect("CatalogScroll", screen, new Vector2(1140f, 510f), new Vector2(0f, -150f), new Vector2(.5f, 1f));
            Image scrollBackground = scrollRoot.gameObject.AddComponent<Image>();
            scrollBackground.color = new Color(.015f, .04f, .08f, .35f);
            ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            RectTransform viewport = CreateRect("Viewport", scrollRoot, Vector2.zero, Vector2.zero);
            Stretch(viewport);
            // A sprite-less Image may not generate Mask geometry, clipping every child.
            // RectMask2D clips by this transform's rectangle without needing a Graphic.
            viewport.gameObject.AddComponent<RectMask2D>();

            var definitions = Player.UI.Progression.ProgressionSpecialSkillCatalog.All;
            int rowCount = Mathf.CeilToInt(definitions.Count / 2f);
            RectTransform content = CreateRect("Content", viewport, new Vector2(1100f, Mathf.Max(510f, rowCount * 190f + 12f)), Vector2.zero, new Vector2(.5f, 1f));
            scroll.viewport = viewport;
            scroll.content = content;

            for (int index = 0; index < definitions.Count; index++)
            {
                Player.UI.Progression.ProgressionSpecialSkillDefinition definition = definitions[index];
                int column = index % 2;
                int row = index / 2;
                RectTransform card = CreateRect("Card_" + (index + 1).ToString("00"), content,
                    new Vector2(520f, 174f), new Vector2(14f + column * 552f, -10f - row * 190f), new Vector2(0f, 1f));
                Image cardPanel = card.gameObject.AddComponent<Image>();
                cardPanel.sprite = SpriteAt("CartoonSciFi_Popup.png");
                cardPanel.type = Image.Type.Sliced;
                cardPanel.color = new Color(.08f, .18f, .25f, 1f);
                BuildCardIcon(card, IconFor("SpecialShop", index), accent);
                AddText("Name", card, definition.Title, displayFont, 20, TextAnchor.UpperLeft, Color.white,
                    new Vector2(22f, -20f), new Vector2(405f, 30f), new Vector2(0f, 1f));
                AddText("Flavor", card, "\"" + definition.Flavor + "\"", utilityFont, 14, TextAnchor.UpperLeft,
                    accent, new Vector2(22f, -51f), new Vector2(450f, 22f), new Vector2(0f, 1f));
                AddText("Detail", card, definition.HideEffect ? string.Empty : definition.Effect, utilityFont, 14,
                    TextAnchor.UpperLeft, new Color(.69f, .82f, .9f), new Vector2(22f, -76f), new Vector2(470f, 37f), new Vector2(0f, 1f));
                AddText("Cost", card, definition.Cost + " G", utilityFont, 16, TextAnchor.UpperLeft, accent,
                    new Vector2(22f, -139f), new Vector2(115f, 24f), new Vector2(0f, 1f));
                CreateButton("Purchase", card, "UNLOCK", accent, utilityFont, new Vector2(-20f, 17f),
                    new Vector2(152f, 38f), new Vector2(1f, 0f));
            }
            screen.gameObject.SetActive(false);
        }

        private static CardLayout GetCardLayout(string stationName, int index, int count)
        {
            const float cardHeight = 180f;
            if (stationName == "SkillTree")
            {
                const float cardWidth = 255f;
                bool firstRow = index < 4;
                float x = firstRow ? 90f + index * 275f : 222.5f + (index - 4) * 280f;
                return new CardLayout(new Vector2(cardWidth, cardHeight), new Vector2(x, firstRow ? -160f : -374f));
            }
            if (count == 3) return new CardLayout(new Vector2(350f, cardHeight), new Vector2(55f + index * 400f, -226f));
            if (count == 2) return new CardLayout(new Vector2(490f, cardHeight), new Vector2(index == 0 ? 105f : 665f, -226f));
            return new CardLayout(new Vector2(560f, cardHeight), new Vector2(350f, -226f));
        }

        private static string CardDescription(string stationName, int index)
        {
            if (stationName == "Supply")
            {
                if (index == 0) return "Restore 50 current HP.";
                if (index == 1) return "Restore 150 current HP.";
                return "Refill magazine and reserve.";
            }
            if (stationName == "SpecialShop") return "Fire continuously while held.";
            return "PURCHASED RUN STAT";
        }

        private static string CardValue(string stationName, int index)
        {
            if (stationName == "Supply")
            {
                if (index == 0) return "HEAL 50 HP";
                if (index == 1) return "HEAL 150 HP";
                return "FULL REFILL";
            }
            return "ONE-TIME UNLOCK";
        }

        private static string InitialCardCost(string stationName, int index)
        {
            if (stationName == "SpecialShop") return "50 G";
            if (stationName == "SkillTree") return "50 G";
            if (stationName == "Supply") return index == 0 ? "50 G" : "100 G";
            return "100 G";
        }

        private static string IconFor(string stationName, int index)
        {
            if (stationName == "Supply") return index < 2 ? "CartoonSciFi_Icon_Heart.png" : "SpaceExpansion_Icon_Crosshair.png";
            if (stationName == "SpecialShop") return "CartoonSciFi_Icon_Lightning.png";
            switch (index)
            {
                case 0: return "CartoonSciFi_Icon_Heart.png";
                case 1: return "CartoonSciFi_Icon_ArrowUp.png";
                case 2: return "CartoonSciFi_Icon_Lightning.png";
                case 3: return "SpaceExpansion_Icon_Crosshair.png";
                case 4: return "CartoonSciFi_Icon_Star.png";
                case 5: return "CartoonSciFi_Icon_Settings.png";
                default: return "SpaceExpansion_Icon_Crosshair.png";
            }
        }

        private static void BuildCardIcon(RectTransform card, string iconFile, Color accent)
        {
            RectTransform plate = CreateRect("IconPlate", card, new Vector2(54f, 54f), new Vector2(-20f, -20f), new Vector2(1f, 1f));
            Image plateImage = plate.gameObject.AddComponent<Image>();
            plateImage.sprite = SpriteAt("SpaceExpansion_Panel.png");
            plateImage.type = Image.Type.Sliced;
            plateImage.color = new Color(accent.r, accent.g, accent.b, .28f);
            RectTransform icon = CreateRect("Icon", plate, new Vector2(34f, 34f), Vector2.zero, new Vector2(.5f, .5f));
            Image iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = SpriteAt(iconFile);
            iconImage.preserveAspect = true;
            iconImage.color = accent;
            iconImage.raycastTarget = false;
        }

        private static void BuildStatsOverview(Transform parent, Font displayFont, Font utilityFont)
        {
            // Keep the read-only overlay beneath the persistent gold/health/ammo HUD row.
            RectTransform overview = CreateRect("RunStatsOverview", parent, new Vector2(620f, 650f), new Vector2(-35f, -190f), new Vector2(1f, 1f));
            Image panel = overview.gameObject.AddComponent<Image>();
            panel.sprite = SpriteAt("SpaceExpansion_Panel.png");
            panel.type = Image.Type.Sliced;
            panel.color = new Color(.025f, .10f, .17f, .96f);
            AddText("Title", overview, "RUN OVERVIEW", displayFont, 29, TextAnchor.UpperLeft, new Color(.55f, .82f, 1f), new Vector2(30f, -30f), new Vector2(350f, 40f), new Vector2(0f, 1f));
            string[] stats = { "MAX HP     100", "MOVE        100%", "FIRE RATE   100%", "SHOOTING DMG 15", "MELEE DMG    20", "DEFENSE       0%", "MAX AMMO  MAG 15 / RES 120" };
            for (int index = 0; index < stats.Length; index++)
            {
                AddText("Stat_" + index, overview, stats[index], utilityFont, 17, TextAnchor.MiddleLeft, Color.white, new Vector2(32f, -86f - 34f * index), new Vector2(505f, 28f), new Vector2(0f, 1f));
            }
            AddText("HealthRow", overview, "HP  100 / 100", utilityFont, 17, TextAnchor.MiddleLeft, new Color(.72f, .9f, .82f), new Vector2(32f, -334f), new Vector2(555f, 28f), new Vector2(0f, 1f));
            AddText("AmmoRow", overview, "AMMO  15 / 120", utilityFont, 17, TextAnchor.MiddleLeft, new Color(.72f, .84f, 1f), new Vector2(32f, -368f), new Vector2(555f, 28f), new Vector2(0f, 1f));
            AddText("SkillsRow", overview, "OWNED SKILLS  NONE", utilityFont, 14, TextAnchor.UpperLeft, new Color(1f, .78f, .4f), new Vector2(32f, -410f), new Vector2(555f, 180f), new Vector2(0f, 1f));
            overview.gameObject.SetActive(false);
        }

        private static void ConfigureKnownUiComponents(Transform root, Transform player)
        {
            Component progression = TryAttachAndConfigure(player != null ? player.gameObject : null, "Player.UI.Progression.PlayerProgression", null);
            Component adapter = TryAttachAndConfigure(root.gameObject, "Player.UI.Progression.ProgressionDataAdapter", null);
            SetObject(adapter, "source", progression);

            Transform gold = root.Find("ProgressionHud/GoldHud");
            Component goldHud = TryAttachAndConfigure(gold?.gameObject, "Player.UI.Progression.ProgressionGoldHud", null);
            SetObject(goldHud, "progression", adapter);
            SetObject(goldHud, "valueText", FindText(gold, "Label"));

            Transform prompt = root.Find("InteractPrompt");
            Component promptView = TryAttachAndConfigure(root.gameObject, "Gameplay.Interaction.InteractionPromptView", null);
            SetObject(promptView, "root", prompt != null ? prompt.gameObject : null);
            SetObject(promptView, "promptText", FindText(prompt, "Label"));

            Transform console = root.Find("StationConsole");
            Component menu = TryAttachAndConfigure(root.gameObject, "Gameplay.Interaction.StationMenuController", null);
            SetObject(menu, "shellRoot", console != null ? console.gameObject : null);
            SetObject(menu, "supplyRoot", console?.Find("Supply")?.gameObject);
            SetObject(menu, "skillTreeRoot", console?.Find("SkillTree")?.gameObject);
            SetObject(menu, "specialShopRoot", console?.Find("SpecialShop")?.gameObject);
            SetObject(menu, "closeButton", console?.Find("CloseButton")?.GetComponent<Button>());
            SetObject(menu, "progression", adapter);

            ConfigureSupplyScreen(console?.Find("Supply"), adapter);
            ConfigureSkillScreen(console?.Find("SkillTree"), adapter);
            ConfigureSpecialScreen(console?.Find("SpecialShop"), adapter);
            Transform overview = root.Find("RunStatsOverview");
            Component overviewView = TryAttachAndConfigure(root.gameObject, "Player.UI.Progression.RunStatsOverview", null);
            SetObject(overviewView, "root", overview != null ? overview.gameObject : null);
            SetObject(overviewView, "progression", adapter);
            SetObjectArray(overviewView, "statRows", FindTextsByPrefix(overview, "Stat_"));
            SetObject(overviewView, "healthRow", FindText(overview, "HealthRow"));
            SetObject(overviewView, "ammoRow", FindText(overview, "AmmoRow"));
            SetObject(overviewView, "skillsRow", FindText(overview, "SkillsRow"));
        }

        private static void ConfigureSupplyScreen(Transform screen, Component adapter)
        {
            Component component = TryAttachAndConfigure(screen?.gameObject, "Player.UI.Progression.SupplyStationScreen", null);
            SetObject(component, "progression", adapter);
            SetObject(component, "healthPackButton", ConfigurePurchaseButton(screen, 0));
            SetObject(component, "largeHealthPackButton", ConfigurePurchaseButton(screen, 1));
            SetObject(component, "ammoPackButton", ConfigurePurchaseButton(screen, 2));
            SetObject(component, "goldText", FindText(screen, "GoldValue"));
        }

        private static void ConfigureSkillScreen(Transform screen, Component adapter)
        {
            Component component = TryAttachAndConfigure(screen?.gameObject, "Player.UI.Progression.SkillTreeStationScreen", null);
            SetObject(component, "progression", adapter);
            var cards = new List<Component>();
            for (int index = 0; index < 7; index++)
            {
                Transform card = screen?.Find("Card_" + (index + 1).ToString("00"));
                Component cardComponent = TryAttachAndConfigure(card?.gameObject, "Player.UI.Progression.SkillUpgradeCard", null);
                SetEnum(cardComponent, "stat", index);
                SetObject(cardComponent, "title", FindText(card, "Name"));
                SetObject(cardComponent, "description", FindText(card, "Detail"));
                SetObject(cardComponent, "currentValue", FindText(card, "Current"));
                SetObject(cardComponent, "nextValue", FindText(card, "Next"));
                SetObject(cardComponent, "levelPips", FindText(card, "Pips"));
                SetObject(cardComponent, "purchaseButton", ConfigurePurchaseButton(card));
                if (cardComponent != null) cards.Add(cardComponent);
            }
            SetObjectArray(component, "cards", cards.Cast<UnityEngine.Object>().ToArray());
            SetObject(component, "goldText", FindText(screen, "GoldValue"));
        }

        private static void ConfigureSpecialScreen(Transform screen, Component adapter)
        {
            Component component = TryAttachAndConfigure(screen?.gameObject, "Player.UI.Progression.SpecialShopStationScreen", null);
            SetObject(component, "progression", adapter);
            var buttons = new List<UnityEngine.Object>();
            int specialCount = Player.UI.Progression.ProgressionSpecialSkillCatalog.All.Count;
            for (int index = 0; index < specialCount; index++) buttons.Add(ConfigurePurchaseButton(screen, index));
            SetObjectArray(component, "skillButtons", buttons);
            SetObject(component, "goldText", FindText(screen, "GoldValue"));
        }

        private static Component ConfigurePurchaseButton(Transform screen, int cardIndex = 0)
        {
            Transform card = cardIndex == 0 && screen?.Find("Purchase") != null
                ? screen
                : FindCard(screen, cardIndex);
            Transform buttonRoot = card?.Find("Purchase");
            Component component = TryAttachAndConfigure(buttonRoot?.gameObject, "Player.UI.Progression.ProgressionPurchaseButton", null);
            SetObject(component, "button", buttonRoot != null ? buttonRoot.GetComponent<Button>() : null);
            SetObject(component, "label", FindText(buttonRoot, "Label"));
            SetObject(component, "price", FindText(card, "Cost"));
            SetObject(component, "accent", buttonRoot != null ? buttonRoot.GetComponent<Image>() : null);
            return component;
        }

        private static Transform FindCard(Transform screen, int cardIndex)
        {
            if (screen == null) return null;
            string cardName = "Card_" + (cardIndex + 1).ToString("00");
            return screen.Find(cardName) ?? screen.Find("CatalogScroll/Viewport/Content/" + cardName);
        }

        private static Component TryAttachAndConfigure(GameObject target, string typeName, StationSpec station)
        {
            if (target == null) return null;
            Type type = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().FirstOrDefault(candidate => candidate.FullName == typeName);
            if (type == null) return null;
            Component component = target.GetComponent(type) ?? target.AddComponent(type);
            if (station == null) return component;
            SerializedObject serialized = new SerializedObject(component);
            SetString(serialized, "displayName", station.Title);
            SetEnum(serialized, "kind", Array.IndexOf(Stations, station));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
            return component;
        }

        private static void SetString(SerializedObject serialized, string propertyName, string value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.String) property.stringValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Float) property.floatValue = value;
        }

        private static void SetFloat(Component component, string propertyName, float value)
        {
            if (component == null) return;
            var serialized = new SerializedObject(component);
            SetFloat(serialized, propertyName, value);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
        }

        private static float FloatProperty(Component component, string propertyName)
        {
            if (component == null) return float.NaN;
            SerializedProperty property = new SerializedObject(component).FindProperty(propertyName);
            return property != null && property.propertyType == SerializedPropertyType.Float
                ? property.floatValue
                : float.NaN;
        }

        private static void SetEnum(SerializedObject serialized, string propertyName, int index)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Enum) property.enumValueIndex = index;
        }

        private static void SetEnum(Component component, string propertyName, int index)
        {
            if (component == null) return;
            var serialized = new SerializedObject(component);
            SetEnum(serialized, propertyName, index);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(Component component, string propertyName, UnityEngine.Object value)
        {
            if (component == null) return;
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) return;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
        }

        private static void SetObjectArray(Component component, string propertyName, IReadOnlyList<UnityEngine.Object> values)
        {
            if (component == null) return;
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray) return;
            property.arraySize = values.Count;
            for (int index = 0; index < values.Count; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
        }

        private static void SetColor(Component component, string propertyName, Color value)
        {
            if (component == null) return;
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Color) return;
            property.colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
        }

        private static void SetVector3(Component component, string propertyName, Vector3 value)
        {
            if (component == null) return;
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Vector3) return;
            property.vector3Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
        }

        private static Text FindText(Transform root, string name)
        {
            return root == null ? null : root.Find(name)?.GetComponent<Text>();
        }

        private static Text[] FindTextsByPrefix(Transform root, string prefix)
        {
            return root == null ? Array.Empty<Text>() : root.GetComponentsInChildren<Text>(true)
                .Where(text => text.name.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(text => text.name).ToArray();
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 anchoredPosition, Vector2 anchor = default)
        {
            if (anchor == default) anchor = new Vector2(.5f, .5f);
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text AddText(string name, RectTransform parent, string value, Font font, int size, TextAnchor alignment, Color color, Vector2 position, Vector2 dimensions, Vector2 anchor = default)
        {
            RectTransform rect = CreateRect(name, parent, dimensions, position, anchor);
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string name, RectTransform parent, string label, Color accent, Font font, Vector2 position, Vector2 dimensions, Vector2 anchor)
        {
            RectTransform rect = CreateRect(name, parent, dimensions, position, anchor);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = SpriteAt("CartoonSciFi_Button_Idle.png");
            image.type = Image.Type.Sliced;
            image.color = accent;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            AddText("Label", rect, label, font, 16, TextAnchor.MiddleCenter, new Color(.01f, .04f, .08f), Vector2.zero, dimensions);
            return button;
        }

        private static Sprite SpriteAt(string filename)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/" + filename);
            if (sprite == null) throw new InvalidOperationException("Progression UI sprite is missing: " + filename);
            return sprite;
        }

        private static void ConfigureSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("No texture importer for " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static Transform FindByName(Scene scene, string name)
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(transform => transform.name == name);
        }

        private static bool HasComponent(GameObject target, string typeName)
        {
            return GetComponentByTypeName(target, typeName) != null;
        }

        private static Component GetComponentByTypeName(GameObject target, string typeName)
        {
            if (target == null) return null;
            Type type = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().FirstOrDefault(candidate => candidate.FullName == typeName);
            return type != null ? target.GetComponent(type) : null;
        }

        private static void ValidateReference(List<string> errors, Component component, string propertyName, UnityEngine.Object expected, string label)
        {
            if (component == null)
            {
                errors.Add("Missing component required for " + label + ".");
                return;
            }
            SerializedProperty property = new SerializedObject(component).FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            {
                errors.Add("" + label + " has no object reference property '" + propertyName + "'.");
                return;
            }
            if (expected == null || property.objectReferenceValue != expected)
                errors.Add(label + " is not wired to the generated target.");
        }

        private static void ValidateReferenceArray(List<string> errors, Component component, string propertyName, IReadOnlyList<UnityEngine.Object> expected, string label)
        {
            if (component == null)
            {
                errors.Add("Missing component required for " + label + ".");
                return;
            }
            SerializedProperty property = new SerializedObject(component).FindProperty(propertyName);
            if (property == null || !property.isArray || property.arraySize != expected.Count)
            {
                errors.Add(label + " does not have the expected generated reference array.");
                return;
            }
            for (int index = 0; index < expected.Count; index++)
            {
                if (expected[index] == null || property.GetArrayElementAtIndex(index).objectReferenceValue != expected[index])
                {
                    errors.Add(label + " has an invalid reference at index " + index + ".");
                    return;
                }
            }
        }

        private static void ValidateEnum(List<string> errors, Component component, string propertyName, int expectedIndex, string label)
        {
            if (component == null)
            {
                errors.Add("Missing component required for " + label + ".");
                return;
            }
            SerializedProperty property = new SerializedObject(component).FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Enum || property.enumValueIndex != expectedIndex)
                errors.Add(label + " is not configured with the expected enum value.");
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Required progression asset is missing: " + path);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            string[] segments = folder.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        private sealed class StationSpec
        {
            public StationSpec(string targetName, string title, string detail, string id, Color accent, string iconFile)
            {
                TargetName = targetName; Title = title; Detail = detail; Id = id; Accent = accent; IconFile = iconFile;
            }
            public string TargetName { get; }
            public string Title { get; }
            public string Detail { get; }
            public string Id { get; }
            public Color Accent { get; }
            public string IconFile { get; }
        }

        private readonly struct CardLayout
        {
            public CardLayout(Vector2 size, Vector2 position)
            {
                Size = size;
                Position = position;
            }

            public Vector2 Size { get; }
            public Vector2 Position { get; }
        }

        private readonly struct StationPlacement
        {
            public StationPlacement(Vector3 interactionPoint, Vector3 beaconPoint, Vector3 radialUp,
                float interactionRadius)
            {
                InteractionPoint = interactionPoint;
                BeaconPoint = beaconPoint;
                RadialUp = radialUp;
                InteractionRadius = interactionRadius;
            }

            public Vector3 InteractionPoint { get; }
            public Vector3 BeaconPoint { get; }
            public Vector3 RadialUp { get; }
            public float InteractionRadius { get; }
        }

        private sealed class ProgressionTestCallback : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!string.Equals(result.ResultState, "Failed", StringComparison.OrdinalIgnoreCase)) return;
                Debug.LogError("Progression test failed: " + result.FullName + "\n" + result.Message + "\n" + result.StackTrace);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                string summary = "Progression contract tests: " + result.PassCount + " passed, " +
                                 result.FailCount + " failed, " + result.SkipCount + " skipped.";
                if (result.FailCount == 0) Debug.Log(summary);
                else Debug.LogError(summary);
            }
        }

        private readonly struct AssetCopy
        {
            public AssetCopy(string source, string destination) { Source = source; Destination = destination; }
            public string Source { get; }
            public string Destination { get; }
        }

        private static readonly AssetCopy[] AssetCopies =
        {
            new AssetCopy("asset packs/visuals/cartoon-ui/sci fi/pop up window.png", "CartoonSciFi_Popup.png"),
            new AssetCopy("asset packs/visuals/cartoon-ui/sci fi/button wide idle.png", "CartoonSciFi_Button_Idle.png"),
            new AssetCopy("asset packs/visuals/cartoon-ui/sci fi/icons info.png", "CartoonSciFi_Icon_Info.png"),
            new AssetCopy("asset packs/visuals/cartoon-ui/sci fi/arrow up.png", "CartoonSciFi_Icon_ArrowUp.png"),
            new AssetCopy("asset packs/visuals/cartoon-ui/sci fi/star filled.png", "CartoonSciFi_Icon_Star.png"),
            new AssetCopy("asset packs/visuals/cartoon-ui/sci fi/icons settings.png", "CartoonSciFi_Icon_Settings.png"),
            new AssetCopy("asset packs/visuals/cartoon-ui/sci fi/lightning filled.png", "CartoonSciFi_Icon_Lightning.png"),
            new AssetCopy("asset packs/visuals/cartoon-ui/sci fi/heart filled.png", "CartoonSciFi_Icon_Heart.png"),
            new AssetCopy("asset packs/visuals/space-expansion-ui/PNG/Blue/Default/crosshair_color_a.png", "SpaceExpansion_Icon_Crosshair.png"),
            new AssetCopy("asset packs/visuals/space-expansion-ui/PNG/Extra/Default/panel_glass_notches.png", "SpaceExpansion_Panel.png"),
        };
    }
}

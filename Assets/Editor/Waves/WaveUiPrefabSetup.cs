using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Waves;
using Player.UI.Waves;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Waves.Editor
{
    /// <summary>
    /// Owns only the generated HUD Canvas/Wave UI subtree. It never rebuilds any of the
    /// PlayerRig's existing HUD children, and may be run repeatedly without duplicate views.
    /// </summary>
    public static class WaveUiPrefabSetup
    {
        private const string PlayerRigPath = "Assets/Prefabs/PlayerRig.prefab";
        private const string GeneratedRootName = "Wave UI";
        private const string DisplaySourcePath = "Assets/Art/Fonts/UI/KenneyFuture.ttf";
        private const string UtilitySourcePath = "Assets/Art/Fonts/UI/KenneyFutureNarrow.ttf";

        private static readonly Color Void = Hex("06131F");
        private static readonly Color Glass = Hex("0B2638");
        private static readonly Color Cyan = Hex("85D8FF");
        private static readonly Color Ice = Hex("F3FBFF");
        private static readonly Color ArenaAmber = Hex("FFB347");
        private static readonly Color ArenaRed = Hex("FF5C63");
        private static readonly Color Muted = Hex("A9C9D8");

        [MenuItem("Tools/Waves/Rebuild Player Rig Wave UI")]
        public static void BuildPlayerRigWaveUi()
        {
            Font displayFont = RequireFont(DisplaySourcePath);
            Font utilityFont = RequireFont(UtilitySourcePath);
            GameObject rigRoot = PrefabUtility.LoadPrefabContents(PlayerRigPath);
            try
            {
                Transform canvas = RequireDirectChild(rigRoot.transform, "HUD Canvas");
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
                }

                Transform previous = canvas.Find(GeneratedRootName);
                if (previous != null)
                {
                    UnityEngine.Object.DestroyImmediate(previous.gameObject);
                }

                RectTransform root = CreateRect(GeneratedRootName, canvas, Vector2.zero, Vector2.zero);
                Stretch(root);
                root.gameObject.AddComponent<CanvasGroup>();

                BuildWaveHud(root, displayFont, utilityFont);
                BuildIntermissionPrompt(root, utilityFont);
                Camera playerCamera = rigRoot.GetComponentInChildren<Camera>(true);
                if (playerCamera == null)
                {
                    throw new InvalidOperationException("PlayerRig has no camera for arena navigation.");
                }
                BuildArenaNavigation(root, displayFont, utilityFont, playerCamera);
                BuildArenaObjective(root, displayFont, utilityFont);
                BuildSealSweep(root);
                BuildGameOver(root, displayFont, utilityFont);

                WaveGameController controller = rigRoot.GetComponent<WaveGameController>();
                if (controller == null)
                {
                    throw new InvalidOperationException("PlayerRig is missing WaveGameController.");
                }
                controller.ConfigureWaveViews(
                    root.GetComponentInChildren<WaveHudView>(true),
                    root.GetComponentInChildren<IntermissionPromptView>(true),
                    root.GetComponentInChildren<ArenaNavigationView>(true),
                    root.GetComponentInChildren<ArenaSealSweepView>(true),
                    root.GetComponentInChildren<ArenaObjectiveView>(true),
                    root.GetComponentInChildren<GameOverMissionSummaryView>(true));
                EditorUtility.SetDirty(controller);

                ValidatePrefabContents(rigRoot, displayFont, utilityFont);
                if (PrefabUtility.SaveAsPrefabAsset(rigRoot, PlayerRigPath) == null)
                {
                    throw new InvalidOperationException("Wave UI setup could not save " + PlayerRigPath + ".");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rigRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PlayerRigPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("Wave UI setup: rebuilt the isolated PlayerRig HUD Canvas/Wave UI subtree.");
        }

        [MenuItem("Tools/Waves/Validate Player Rig Wave UI")]
        public static void ValidatePlayerRigWaveUi()
        {
            Font displayFont = RequireFont(DisplaySourcePath);
            Font utilityFont = RequireFont(UtilitySourcePath);
            GameObject rigRoot = PrefabUtility.LoadPrefabContents(PlayerRigPath);
            try
            {
                ValidatePrefabContents(rigRoot, displayFont, utilityFont);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rigRoot);
            }

            Debug.Log("Wave UI setup: PlayerRig Wave UI validation passed.");
        }

        private static void BuildWaveHud(RectTransform parent, Font display, Font utility)
        {
            RectTransform root = CreateRect("Wave HUD", parent, new Vector2(320f, 150f), new Vector2(-40f, 32f), new Vector2(1f, 0f));
            root.pivot = new Vector2(1f, 0f);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            Text timer = AddText("Timer", root, "--:--", display, 31, Ice, TextAnchor.MiddleRight, new Vector2(65f, 50f), new Vector2(170f, 40f));
            RectTransform track = CreateRect("Timer Track", root, new Vector2(166f, 7f), new Vector2(67f, 24f));
            AddImage(track.gameObject, new Color(Ice.r, Ice.g, Ice.b, .18f), false);
            Image fill = AddImage(CreateRect("Timer Fill", track, new Vector2(166f, 7f), Vector2.zero).gameObject, Cyan, false);
            fill.sprite = BuiltinSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            Text wave = AddText("Wave Number", root, "WAVE --", display, 35, Ice, TextAnchor.MiddleRight, new Vector2(0f, -2f), new Vector2(300f, 46f));
            Text state = AddText("State", root, "INTERMISSION", utility, 17, Cyan, TextAnchor.MiddleRight, new Vector2(0f, -37f), new Vector2(300f, 28f));
            root.gameObject.AddComponent<WaveHudView>().Configure(group, wave, state, timer, fill);
        }

        private static void BuildIntermissionPrompt(RectTransform parent, Font utility)
        {
            RectTransform root = CreateRect("Intermission Prompt", parent, new Vector2(660f, 86f), new Vector2(0f, -150f), new Vector2(.5f, 1f));
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            AddImage(root.gameObject, new Color(Void.r, Void.g, Void.b, .86f), false);
            Text message = AddText("Message", root, "LEAVE PROTECTED AREA TO START NEXT WAVE", utility, 19, Ice, TextAnchor.MiddleCenter, new Vector2(0f, 16f), new Vector2(620f, 30f));
            Text hold = AddText("Hold", root, "HOLD 1.0s", utility, 15, Cyan, TextAnchor.MiddleCenter, new Vector2(0f, -21f), new Vector2(150f, 25f));
            RectTransform track = CreateRect("Hold Track", root, new Vector2(420f, 6f), new Vector2(0f, -35f));
            AddImage(track.gameObject, new Color(Ice.r, Ice.g, Ice.b, .18f), false);
            Image fill = AddImage(CreateRect("Hold Fill", track, new Vector2(420f, 6f), Vector2.zero).gameObject, Cyan, false);
            fill.sprite = BuiltinSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            root.gameObject.AddComponent<IntermissionPromptView>().Configure(group, message, hold, fill);
        }

        private static void BuildArenaNavigation(RectTransform parent, Font display, Font utility, Camera playerCamera)
        {
            RectTransform root = CreateRect("Arena Navigation", parent, Vector2.zero, Vector2.zero);
            Stretch(root, 46f, 46f, 32f, 32f);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            RectTransform labelPanel = CreateRect("Target Readout", root, new Vector2(360f, 72f), new Vector2(0f, -276f), new Vector2(.5f, 1f));
            AddImage(labelPanel.gameObject, new Color(Glass.r, Glass.g, Glass.b, .94f), false);
            Text target = AddText("Target", labelPanel, "ARENA TARGET", display, 22, Ice, TextAnchor.MiddleCenter, new Vector2(0f, 12f), new Vector2(330f, 30f));
            Text distance = AddText("Distance", labelPanel, "---m", utility, 16, Cyan, TextAnchor.MiddleCenter, new Vector2(0f, -17f), new Vector2(180f, 25f));

            RectTransform marker = CreateRect("Edge Marker", root, new Vector2(52f, 62f), Vector2.zero);
            Image markerImage = AddImage(marker.gameObject, ArenaAmber, false);
            markerImage.sprite = BuiltinSprite;
            markerImage.type = Image.Type.Simple;
            markerImage.preserveAspect = false;
            RectTransform arrow = CreateRect("Arrow", marker, new Vector2(48f, 48f), Vector2.zero);
            AddText("Glyph", arrow, "▲", display, 38, Void, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(48f, 48f));

            root.gameObject.AddComponent<ArenaNavigationView>().Configure(group, playerCamera, marker, root, target, distance);
        }

        private static void BuildArenaObjective(RectTransform parent, Font display, Font utility)
        {
            RectTransform root = CreateRect("Arena Objective", parent, new Vector2(620f, 132f), new Vector2(0f, -42f), new Vector2(.5f, 1f));
            root.pivot = new Vector2(.5f, 1f);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            AddImage(root.gameObject, new Color(Void.r, Void.g, Void.b, .88f), false);
            AddRail(root, new Vector2(-300f, 0f), new Vector2(6f, 110f), ArenaAmber);
            Text title = AddText("Title", root, "ARENA 1 // SWARM", utility, 15, ArenaAmber, TextAnchor.MiddleCenter, new Vector2(0f, 43f), new Vector2(560f, 22f));
            Text objective = AddText("Objective", root, "0 DEFEATED  //  -- LEFT", display, 24, Ice, TextAnchor.MiddleCenter, new Vector2(0f, 14f), new Vector2(560f, 32f));
            Text detail = AddText("Detail", root, "CLEAR ALL HOSTILES", utility, 15, Cyan, TextAnchor.MiddleCenter, new Vector2(0f, -14f), new Vector2(560f, 22f));

            RectTransform healthRoot = CreateRect("Boss Health", root, new Vector2(540f, 22f), new Vector2(0f, -43f));
            AddImage(healthRoot.gameObject, new Color(Ice.r, Ice.g, Ice.b, .18f), false);
            RectTransform fillRect = CreateRect("Fill", healthRoot, new Vector2(540f, 22f), Vector2.zero);
            Image healthFill = AddImage(fillRect.gameObject, ArenaRed, false);
            healthFill.sprite = BuiltinSprite;
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            healthFill.fillOrigin = 0;
            healthFill.fillAmount = 1f;
            Text healthText = AddText("Value", healthRoot, "HP --", utility, 14, Ice, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(520f, 20f));
            healthRoot.gameObject.SetActive(false);

            root.gameObject.AddComponent<ArenaObjectiveView>().Configure(
                group, title, objective, detail, healthRoot.gameObject, healthFill, healthText);
        }

        private static void BuildSealSweep(RectTransform parent)
        {
            RectTransform root = CreateRect("Arena Seal Sweep", parent, Vector2.zero, Vector2.zero);
            Stretch(root);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            RectTransform cyan = CreateSweep("Cyan Sweep", root, -86f, Cyan);
            RectTransform amber = CreateSweep("Amber Sweep", root, 0f, ArenaAmber);
            RectTransform red = CreateSweep("Red Sweep", root, 86f, ArenaRed);
            root.gameObject.AddComponent<ArenaSealSweepView>().Configure(group, cyan, amber, red);
        }

        private static void BuildGameOver(RectTransform parent, Font display, Font utility)
        {
            RectTransform root = CreateRect("Game Over", parent, Vector2.zero, Vector2.zero);
            Stretch(root);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            AddImage(root.gameObject, new Color(Void.r, Void.g, Void.b, .89f), true);

            RectTransform panel = CreateRect("Mission Summary", root, new Vector2(680f, 600f), Vector2.zero);
            AddImage(panel.gameObject, Glass, false);
            AddRail(panel, new Vector2(-326f, 0f), new Vector2(8f, 508f), ArenaRed);
            AddText("Eyebrow", panel, "MISSION CONCLUSION", utility, 18, ArenaRed, TextAnchor.MiddleCenter, new Vector2(0f, 235f), new Vector2(560f, 28f));
            AddText("Title", panel, "RUN ENDED", display, 48, Ice, TextAnchor.MiddleCenter, new Vector2(0f, 176f), new Vector2(600f, 60f));
            Text wave = AddText("Wave Reached", panel, "WAVE REACHED  --", utility, 25, Ice, TextAnchor.MiddleLeft, new Vector2(-228f, 83f), new Vector2(460f, 35f));
            Text kills = AddText("Kills", panel, "KILLS  --", utility, 25, Ice, TextAnchor.MiddleLeft, new Vector2(-228f, 28f), new Vector2(460f, 35f));
            Text gold = AddText("Gold Earned", panel, "GOLD EARNED  --", utility, 25, ArenaAmber, TextAnchor.MiddleLeft, new Vector2(-228f, -27f), new Vector2(460f, 35f));
            Text duration = AddText("Duration", panel, "RUN TIME  --:--", utility, 25, Cyan, TextAnchor.MiddleLeft, new Vector2(-228f, -82f), new Vector2(460f, 35f));
            Button restart = CreateButton("Restart", panel, "RESTART", Cyan, display, new Vector2(-118f, -190f));
            Button mainMenu = CreateButton("Main Menu", panel, "MAIN MENU", ArenaRed, display, new Vector2(118f, -190f));
            root.gameObject.AddComponent<GameOverMissionSummaryView>().Configure(group, wave, kills, gold, duration, restart, mainMenu);
        }

        private static RectTransform CreateSweep(string name, RectTransform parent, float y, Color color)
        {
            // ArenaSealSweepView animates the shared x anchor; keeping this local x at zero
            // lets that anchor sweep move the rail across the screen.
            RectTransform sweep = CreateRect(name, parent, new Vector2(290f, 13f), new Vector2(0f, y));
            Image image = AddImage(sweep.gameObject, color, false);
            image.sprite = BuiltinSprite;
            image.type = Image.Type.Sliced;
            return sweep;
        }

        private static Button CreateButton(string name, RectTransform parent, string label, Color accent, Font font, Vector2 position)
        {
            RectTransform root = CreateRect(name, parent, new Vector2(210f, 58f), position);
            Image image = AddImage(root.gameObject, accent, true);
            image.sprite = BuiltinSprite;
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, .9f);
            colors.pressedColor = new Color(.68f, .78f, .84f, 1f);
            button.colors = colors;
            AddText("Label", root, label, font, 18, Void, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(196f, 42f));
            return button;
        }

        private static Text AddText(string name, RectTransform parent, string value, Font font, int size, Color color, TextAnchor alignment, Vector2 position, Vector2 dimensions)
        {
            RectTransform rect = CreateRect(name, parent, dimensions, position);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Image AddImage(GameObject target, Color color, bool raycastTarget)
        {
            Image image = target.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static void AddRail(RectTransform parent, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rail = CreateRect("Accent Rail", parent, size, position);
            Image image = AddImage(rail.gameObject, color, false);
            image.sprite = BuiltinSprite;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position, Vector2? anchor = null)
        {
            GameObject target = new GameObject(name, typeof(RectTransform));
            target.transform.SetParent(parent, false);
            RectTransform rect = target.GetComponent<RectTransform>();
            Vector2 resolvedAnchor = anchor ?? new Vector2(.5f, .5f);
            rect.anchorMin = resolvedAnchor;
            rect.anchorMax = resolvedAnchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static Font RequireFont(string sourcePath)
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null) throw new InvalidOperationException("Wave UI is missing its required font source: " + sourcePath);
            return source;
        }

        private static Transform RequireDirectChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null || child.parent != parent)
            {
                throw new InvalidOperationException("PlayerRig is missing direct child '" + name + "'.");
            }
            return child;
        }

        private static void ValidatePrefabContents(GameObject rigRoot, Font displayFont, Font utilityFont)
        {
            List<string> errors = new List<string>();
            Transform canvas = rigRoot.transform.Find("HUD Canvas");
            if (canvas == null || canvas.parent != rigRoot.transform) errors.Add("PlayerRig is missing direct HUD Canvas.");
            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null) errors.Add("HUD Canvas is missing GraphicRaycaster for Game Over buttons.");
            Transform root = canvas != null ? canvas.Find(GeneratedRootName) : null;
            if (root == null || root.parent != canvas) errors.Add("HUD Canvas is missing direct Wave UI subtree.");
            if (root != null && root.GetComponent<CanvasGroup>() == null) errors.Add("Wave UI root is missing CanvasGroup.");
            if (canvas != null && canvas.Cast<Transform>().Count(child => child.name == GeneratedRootName) != 1) errors.Add("HUD Canvas must contain exactly one Wave UI subtree.");

            ValidateView<WaveHudView>(root, "Wave HUD", new[] { "waveText", "stateText", "timerText", "timerFill" }, errors);
            ValidateView<IntermissionPromptView>(root, "Intermission Prompt", new[] { "messageText", "holdText", "holdFill" }, errors);
            ValidateView<ArenaNavigationView>(root, "Arena Navigation", new[] { "canvasGroup", "navigationCamera", "marker", "markerBounds", "targetLabel", "distanceLabel" }, errors);
            ValidateView<ArenaObjectiveView>(root, "Arena Objective", new[]
            {
                "arenaTitleText", "objectiveText", "detailText", "bossHealthRoot", "bossHealthFill", "bossHealthText"
            }, errors);
            ValidateView<ArenaSealSweepView>(root, "Arena Seal Sweep", new[] { "canvasGroup", "cyanSweep", "amberSweep", "redSweep" }, errors);
            ValidateView<GameOverMissionSummaryView>(root, "Game Over", new[] { "canvasGroup", "waveReachedText", "killsText", "goldEarnedText", "durationText", "restartButton", "mainMenuButton" }, errors);
            ValidateControllerViews(rigRoot.GetComponent<WaveGameController>(), errors);

            if (root != null)
            {
                ValidateWaveHudLayout(root, errors);
                ValidateArenaObjectiveLayout(root, errors);
                foreach (Text text in root.GetComponentsInChildren<Text>(true))
                {
                    if (text.font != displayFont && text.font != utilityFont)
                    {
                        errors.Add("Wave UI text '" + text.name + "' does not use a Kenney UI font.");
                    }
                }
                Transform gameOver = root.Find("Game Over");
                CanvasGroup gameOverGroup = gameOver != null ? gameOver.GetComponent<CanvasGroup>() : null;
                if (gameOverGroup == null || gameOverGroup.alpha > .001f || gameOverGroup.interactable || gameOverGroup.blocksRaycasts)
                {
                    errors.Add("Game Over must be created hidden and non-interactive.");
                }
                Transform marker = root.Find("Arena Navigation/Edge Marker");
                if (marker == null || marker.GetComponent<Image>() == null) errors.Add("Arena navigation requires its edge marker image.");
            }

            if (errors.Count > 0) throw new InvalidOperationException("Wave UI validation failed:\n- " + string.Join("\n- ", errors));
        }

        private static void ValidateWaveHudLayout(Transform root, List<string> errors)
        {
            Transform hud = root.Find("Wave HUD");
            RectTransform hudRect = hud as RectTransform;
            if (hudRect == null)
            {
                return;
            }

            Vector2 bottomRight = new Vector2(1f, 0f);
            if (hudRect.anchorMin != bottomRight || hudRect.anchorMax != bottomRight || hudRect.pivot != bottomRight)
            {
                errors.Add("Wave HUD must be anchored and pivoted to the bottom-right corner.");
            }
            Image background = hud.GetComponent<Image>();
            if (background != null && background.enabled && background.color.a > .001f)
            {
                errors.Add("Wave HUD must not have a background panel image.");
            }

            Text wave = hud.Find("Wave Number")?.GetComponent<Text>();
            Text state = hud.Find("State")?.GetComponent<Text>();
            Text timer = hud.Find("Timer")?.GetComponent<Text>();
            if (wave == null || state == null || timer == null)
            {
                return;
            }
            if (wave.alignment != TextAnchor.MiddleRight || state.alignment != TextAnchor.MiddleRight || timer.alignment != TextAnchor.MiddleRight)
            {
                errors.Add("Wave HUD text must be right-aligned.");
            }
            if (timer.rectTransform.anchoredPosition.y <= wave.rectTransform.anchoredPosition.y ||
                wave.rectTransform.anchoredPosition.y <= state.rectTransform.anchoredPosition.y)
            {
                errors.Add("Wave HUD must stack timer above wave number above state text.");
            }
        }

        private static void ValidateArenaObjectiveLayout(Transform root, List<string> errors)
        {
            RectTransform objective = root.Find("Arena Objective") as RectTransform;
            if (objective == null) return;

            Vector2 topCenter = new Vector2(.5f, 1f);
            if (objective.anchorMin != topCenter || objective.anchorMax != topCenter || objective.pivot != topCenter)
            {
                errors.Add("Arena Objective must be anchored and pivoted to the top-center.");
            }
            if (objective.anchoredPosition.y < -120f)
            {
                errors.Add("Arena Objective must stay in the top HUD safe area.");
            }
        }

        private static void ValidateView<T>(Transform root, string path, IEnumerable<string> referenceProperties, List<string> errors) where T : Component
        {
            Transform viewRoot = root != null ? root.Find(path) : null;
            T component = viewRoot != null ? viewRoot.GetComponent<T>() : null;
            if (component == null)
            {
                errors.Add("Missing " + typeof(T).Name + " at Wave UI/" + path + ".");
                return;
            }

            SerializedObject serialized = new SerializedObject(component);
            foreach (string propertyName in referenceProperties)
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property == null || property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null)
                {
                    errors.Add(typeof(T).Name + " has no valid '" + propertyName + "' reference.");
                }
            }
        }

        private static void ValidateControllerViews(WaveGameController controller, List<string> errors)
        {
            if (controller == null)
            {
                errors.Add("PlayerRig is missing WaveGameController.");
                return;
            }

            SerializedObject serialized = new SerializedObject(controller);
            foreach (string propertyName in new[]
            {
                "waveHud", "intermissionPrompt", "arenaNavigation", "arenaSeal", "arenaObjective", "gameOver"
            })
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                {
                    errors.Add("WaveGameController has no valid '" + propertyName + "' view reference.");
                }
            }
        }

        private static Sprite BuiltinSprite
        {
            get
            {
                Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                if (sprite == null) throw new InvalidOperationException("Unity's built-in UI sprite is unavailable.");
                return sprite;
            }
        }

        private static Color Hex(string value)
        {
            if (!ColorUtility.TryParseHtmlString("#" + value, out Color color)) throw new ArgumentException("Invalid UI colour " + value);
            return color;
        }
    }
}

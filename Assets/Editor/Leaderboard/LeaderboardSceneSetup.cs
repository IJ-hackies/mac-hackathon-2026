using System;
using System.Collections.Generic;
using System.Linq;
using Player.UI.Leaderboard;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LeaderboardEditor
{
    /// <summary>
    /// Builds (or rebuilds) the whole Leaderboard.unity scene from Unity UI primitives, the same
    /// editor-time-codegen-into-a-saved-asset pattern WaveUiPrefabSetup uses for the Wave HUD - no
    /// runtime-instantiated UI. Idempotent: destroys and recreates only its own generated root.
    /// Everything is anchored top-center directly under the Canvas (no nested centered boxes) with
    /// a single downward-growing vertical budget, and the Score/Wave tab panels share that exact
    /// same layout - only their live data differs - so both podiums and tables land in identical
    /// screen positions.
    /// </summary>
    public static class LeaderboardSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Leaderboard.unity";
        private const string DisplaySourcePath = "Assets/Art/Fonts/UI/KenneyFuture.ttf";
        private const string UtilitySourcePath = "Assets/Art/Fonts/UI/KenneyFutureNarrow.ttf";
        private const string GoldSpritePath = "Assets/Art/UI/Medals/Gold01.png";
        private const string SilverSpritePath = "Assets/Art/UI/Medals/Silver01.png";
        private const string BronzeSpritePath = "Assets/Art/UI/Medals/Bronze01.png";
        private const string RowDividerSpritePath = "Assets/Art/Textures/UI/Leaderboard/CartoonSciFi_RowDivider.png";

        // Vertical layout budget, all measured downward (negative Y) from the canvas top edge.
        // Score and Wave panels both use these exact same constants, so their podiums/tables land
        // in identical screen positions - only the live data differs.
        private const float TitleY = -55f;
        private const float TabsY = -130f;
        private const float PanelTop = -190f;
        private const float PodiumHeight = 190f;
        private const float TableTop = PanelTop - PodiumHeight - 20f;
        private const float TableWidth = 1100f;
        private const float TableHeaderHeight = 42f;
        private const float TableRowHeight = 42f;
        private const int RowCount = 10;
        private static readonly float TableHeight = TableHeaderHeight + TableRowHeight * RowCount + 16f;
        private static readonly float PagerY = TableTop - TableHeight - 26f;

        private static readonly Color Void = Hex("06131F");
        private static readonly Color Glass = Hex("0B2638");
        private static readonly Color Cyan = Hex("85D8FF");
        private static readonly Color Ice = Hex("F3FBFF");
        private static readonly Color Amber = Hex("FFB347");
        private static readonly Color Red = Hex("FF5C63");
        private static readonly Color Muted = Hex("A9C9D8");

        [MenuItem("Tools/Leaderboard/Build Leaderboard Scene")]
        public static void BuildScene()
        {
            Font display = RequireFont(DisplaySourcePath);
            Font utility = RequireFont(UtilitySourcePath);
            Sprite gold = RequireSprite(GoldSpritePath);
            Sprite silver = RequireSprite(SilverSpritePath);
            Sprite bronze = RequireSprite(BronzeSpritePath);

            Scene scene = System.IO.File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            Camera cam = cameraGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Void;
            cam.orthographic = false;
            cam.tag = "MainCamera";

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // match height - this layout is width-generous, height-tight

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();

            AddText("Title", canvasRect, "LEADERBOARDS", display, 52, Ice, TextAnchor.MiddleCenter,
                new Vector2(0f, TitleY), new Vector2(1000f, 70f), TopCenter);

            Button scoreTab = CreateButton("Score Tab", canvasRect, "SCORE", Cyan, display,
                new Vector2(-120f, TabsY), TopCenter, new Vector2(220f, 64f));
            Button waveTab = CreateButton("Wave Tab", canvasRect, "WAVE", Muted, display,
                new Vector2(120f, TabsY), TopCenter, new Vector2(220f, 64f));

            GameObject scorePanel = BuildTabPanel("Score Panel", canvasRect, display, utility, gold, silver, bronze,
                out PodiumStand[] scorePodium, out LeaderboardRow[] scoreRows,
                out Button scorePrev, out Button scoreNext, out Text scorePageLabel, "SCORE");
            GameObject wavePanel = BuildTabPanel("Wave Panel", canvasRect, display, utility, gold, silver, bronze,
                out PodiumStand[] wavePodium, out LeaderboardRow[] waveRows,
                out Button wavePrev, out Button waveNext, out Text wavePageLabel, "WAVE");
            wavePanel.SetActive(false);

            Button backButton = CreateButton("Back Button", canvasRect, "BACK TO MAIN MENU", Red, display,
                new Vector2(40f, 40f), BottomLeft, new Vector2(300f, 64f));

            var controllerGo = new GameObject("Leaderboard Scene Controller", typeof(LeaderboardSceneController));
            LeaderboardSceneController controller = controllerGo.GetComponent<LeaderboardSceneController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("scoreTabButton").objectReferenceValue = scoreTab;
            so.FindProperty("waveTabButton").objectReferenceValue = waveTab;
            so.FindProperty("scorePanel").objectReferenceValue = scorePanel;
            so.FindProperty("wavePanel").objectReferenceValue = wavePanel;
            SetArray(so.FindProperty("scorePodium"), scorePodium);
            SetArray(so.FindProperty("scoreRows"), scoreRows);
            so.FindProperty("scorePrevButton").objectReferenceValue = scorePrev;
            so.FindProperty("scoreNextButton").objectReferenceValue = scoreNext;
            so.FindProperty("scorePageLabel").objectReferenceValue = scorePageLabel;
            SetArray(so.FindProperty("wavePodium"), wavePodium);
            SetArray(so.FindProperty("waveRows"), waveRows);
            so.FindProperty("wavePrevButton").objectReferenceValue = wavePrev;
            so.FindProperty("waveNextButton").objectReferenceValue = waveNext;
            so.FindProperty("wavePageLabel").objectReferenceValue = wavePageLabel;
            so.FindProperty("backButton").objectReferenceValue = backButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Could not save " + ScenePath + ".");
            }

            RegisterInBuildSettings();
            Debug.Log("LeaderboardSceneSetup: built " + ScenePath + " and registered it in Build Settings.");
        }

        private static GameObject BuildTabPanel(
            string name, RectTransform parent, Font display, Font utility, Sprite gold, Sprite silver, Sprite bronze,
            out PodiumStand[] podium, out LeaderboardRow[] rows,
            out Button prevButton, out Button nextButton, out Text pageLabel, string valueLabel)
        {
            var panelGo = new GameObject(name, typeof(RectTransform));
            panelGo.transform.SetParent(parent, false);
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            Stretch(panelRect);

            RectTransform podiumRoot = CreateRect("Podium", panelRect, new Vector2(1000f, PodiumHeight),
                new Vector2(0f, PanelTop), TopCenter);
            podium = new[]
            {
                BuildPodiumStand("2nd", podiumRoot, display, utility, silver, -280f, 150f),
                BuildPodiumStand("1st", podiumRoot, display, utility, gold, 0f, 185f),
                BuildPodiumStand("3rd", podiumRoot, display, utility, bronze, 280f, 120f),
            };

            RectTransform tableRoot = CreateRect("Table", panelRect, new Vector2(TableWidth, TableHeight),
                new Vector2(0f, TableTop), TopCenter);
            AddImage(tableRoot.gameObject, new Color(Glass.r, Glass.g, Glass.b, .55f), false);

            float halfWidth = TableWidth * .5f;
            float rankX = -halfWidth + 160f;
            float nameX = -halfWidth + 420f;
            float valueX = halfWidth - 200f;
            float headerY = -TableHeaderHeight * .5f + 5f;
            AddText("Header Rank", tableRoot, "#", utility, 22, Muted, TextAnchor.MiddleLeft,
                new Vector2(rankX, headerY), new Vector2(100f, 34f), TopCenter);
            AddText("Header Name", tableRoot, "USERNAME", utility, 22, Muted, TextAnchor.MiddleLeft,
                new Vector2(nameX, headerY), new Vector2(400f, 34f), TopCenter);
            AddText("Header Value", tableRoot, valueLabel, utility, 22, Muted, TextAnchor.MiddleRight,
                new Vector2(valueX, headerY), new Vector2(180f, 34f), TopCenter);
            AddDivider("Header Divider", tableRoot, TableWidth - 60f, -TableHeaderHeight + 3f);

            rows = new LeaderboardRow[RowCount];
            for (int i = 0; i < rows.Length; i++)
            {
                float rowY = -TableHeaderHeight - 8f - i * TableRowHeight - TableRowHeight * .5f;
                rows[i] = BuildRow(tableRoot, utility, i, rowY, TableWidth, rankX, nameX, valueX);
            }

            RectTransform pager = CreateRect("Pager", panelRect, new Vector2(500f, 56f), new Vector2(0f, PagerY), TopCenter);
            prevButton = CreateButton("Prev", pager, "< PREV", Cyan, display, new Vector2(-170f, 0f), Center, new Vector2(180f, 56f));
            pageLabel = AddText("Page Label", pager, "PAGE 1", utility, 22, Ice, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(160f, 40f));
            nextButton = CreateButton("Next", pager, "NEXT >", Cyan, display, new Vector2(170f, 0f), Center, new Vector2(180f, 56f));

            return panelGo;
        }

        private static PodiumStand BuildPodiumStand(string name, RectTransform parent, Font display, Font utility, Sprite medal, float x, float standHeight)
        {
            // All three stands sit flush with the shared Podium rect's bottom edge (anchor and
            // pivot both BottomCenter, so anchoredPosition.y = 0 IS the parent's bottom edge) -
            // taller/shorter stands then read as a real podium instead of floating independently.
            RectTransform root = CreateRect("Stand " + name, parent, new Vector2(240f, standHeight), new Vector2(x, 0f), BottomCenter);
            AddImage(root.gameObject, new Color(Glass.r, Glass.g, Glass.b, .85f), false);
            RectTransform medalRect = CreateRect("Medal", root, new Vector2(76f, 76f), new Vector2(0f, -18f), TopCenter);
            Image medalImage = AddImage(medalRect.gameObject, Color.white, false);
            medalImage.sprite = medal;
            medalImage.preserveAspect = true;
            Text nameText = AddText("Name", root, "---", display, 20, Ice, TextAnchor.MiddleCenter, new Vector2(0f, -70f), new Vector2(220f, 28f), TopCenter);
            Text valueText = AddText("Value", root, "", utility, 18, Amber, TextAnchor.MiddleCenter, new Vector2(0f, -98f), new Vector2(220f, 26f), TopCenter);
            PodiumStand stand = root.gameObject.AddComponent<PodiumStand>();
            stand.Configure(nameText, valueText);
            return stand;
        }

        private static LeaderboardRow BuildRow(RectTransform tableRoot, Font utility, int index, float y, float tableWidth, float rankX, float nameX, float valueX)
        {
            RectTransform root = CreateRect("Row " + index, tableRoot, new Vector2(tableWidth - 40f, TableRowHeight), new Vector2(0f, y), TopCenter);
            if (index % 2 == 1) AddImage(root.gameObject, new Color(Ice.r, Ice.g, Ice.b, .05f), false);
            Text rank = AddText("Rank", root, "#--", utility, 22, Muted, TextAnchor.MiddleLeft, new Vector2(rankX + 20f, 4f), new Vector2(100f, 32f));
            Text playerName = AddText("Name", root, "---", utility, 22, Ice, TextAnchor.MiddleLeft, new Vector2(nameX + 20f, 4f), new Vector2(400f, 32f));
            Text value = AddText("Value", root, "", utility, 22, Amber, TextAnchor.MiddleRight, new Vector2(valueX + 20f, 4f), new Vector2(180f, 32f));
            AddDivider("Divider", root, tableWidth - 60f, -TableRowHeight + 2f);
            LeaderboardRow row = root.gameObject.AddComponent<LeaderboardRow>();
            row.Configure(rank, playerName, value);
            root.gameObject.SetActive(false);
            return row;
        }

        // Thin Cartoon UI scrollbar-track sprite, 9-sliced and squashed flat, used as a row
        // separator instead of a plain flat-color line - imported at
        // Assets/Art/Textures/UI/Leaderboard/CartoonSciFi_RowDivider.png.
        private static void AddDivider(string name, RectTransform parent, float width, float y)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(width, 4f), new Vector2(0f, y), TopCenter);
            Image image = AddImage(rect.gameObject, new Color(1f, 1f, 1f, .4f), false);
            image.sprite = RowDividerSprite;
            image.type = Image.Type.Sliced;
        }

        private static void RegisterInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void SetArray(SerializedProperty arrayProperty, UnityEngine.Object[] values)
        {
            arrayProperty.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static Button CreateButton(string name, RectTransform parent, string label, Color accent, Font font, Vector2 position, Vector2 anchor, Vector2 size)
        {
            RectTransform root = CreateRect(name, parent, size, position, anchor);
            Image image = AddImage(root.gameObject, accent, true);
            image.sprite = BuiltinSprite;
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, .9f);
            colors.pressedColor = new Color(.68f, .78f, .84f, 1f);
            button.colors = colors;
            AddText("Label", root, label, font, 22, Void, TextAnchor.MiddleCenter, Vector2.zero, size - new Vector2(14f, 14f));
            return button;
        }

        private static Text AddText(string name, RectTransform parent, string value, Font font, int size, Color color, TextAnchor alignment, Vector2 position, Vector2 dimensions, Vector2? anchor = null)
        {
            RectTransform rect = CreateRect(name, parent, dimensions, position, anchor);
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

        // Anchor and pivot always match here - every element in this generator is anchored by one
        // of its own corners/edges (TopCenter, BottomLeft, etc.), never center-anchored-but-edge-
        // positioned, which is what previously let podium/table content land outside its intended
        // parent bounds.
        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position, Vector2? anchor = null)
        {
            GameObject target = new GameObject(name, typeof(RectTransform));
            target.transform.SetParent(parent, false);
            RectTransform rect = target.GetComponent<RectTransform>();
            Vector2 resolvedAnchor = anchor ?? Center;
            rect.anchorMin = resolvedAnchor;
            rect.anchorMax = resolvedAnchor;
            rect.pivot = resolvedAnchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static readonly Vector2 Center = new Vector2(.5f, .5f);
        private static readonly Vector2 TopCenter = new Vector2(.5f, 1f);
        private static readonly Vector2 BottomCenter = new Vector2(.5f, 0f);
        private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);

        private static Font RequireFont(string path)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font == null) throw new InvalidOperationException("Leaderboard scene is missing its required font source: " + path);
            return font;
        }

        private static Sprite RequireSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Leaderboard scene is missing its required medal sprite: " + path);
            return sprite;
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

        private static Sprite RowDividerSprite => RequireSprite(RowDividerSpritePath);

        private static Color Hex(string value)
        {
            if (!ColorUtility.TryParseHtmlString("#" + value, out Color color)) throw new ArgumentException("Invalid UI colour " + value);
            return color;
        }
    }
}

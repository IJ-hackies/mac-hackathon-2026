using System;
using System.Collections.Generic;
using Player.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Player.Editor
{
    public static class SettingsMenuPrefabSetup
    {
        private const string AutoConfigureSessionKey = "Player.SettingsMenu.AutoConfigureScheduled.V3";
        private const string PlayerRigPrefabPath = "Assets/Prefabs/PlayerRig.prefab";
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

        private static readonly Color Void = Hex("06131F");
        private static readonly Color Glass = Hex("0B2638");
        private static readonly Color Panel = Hex("103A50");
        private static readonly Color Ice = Hex("F3FBFF");
        private static readonly Color Cyan = Hex("85D8FF");
        private static readonly Color Muted = Hex("A9C9D8");
        private static readonly Color Solar = Hex("FFD24A");

        [InitializeOnLoadMethod]
        private static void ConfigureAfterScriptReload()
        {
            if (SessionState.GetBool(AutoConfigureSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(AutoConfigureSessionKey, true);
            EditorApplication.delayCall += () =>
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigPrefabPath);
                bool alreadyConfigured = prefab != null &&
                                         prefab.GetComponent<SettingsMenuController>() != null &&
                                         prefab.transform.Find(
                                             "HUD Canvas/Settings Menu/Astronaut Console/Main Page/Controls/Arrow") != null;
                if (alreadyConfigured)
                {
                    return;
                }

                try
                {
                    ConfigureSettingsMenu();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Tools/Player Prototype/Configure Settings Menu %#u")]
        public static void ConfigureSettingsMenu()
        {
            ConfigureTextureImports();

            Font displayFont = RequireAsset<Font>(FontPath);
            Font utilityFont = RequireAsset<Font>(NarrowFontPath);
            Sprite popup = RequireAsset<Sprite>(PopupPath);
            Sprite buttonIdle = RequireAsset<Sprite>(ButtonIdlePath);
            Sprite buttonHover = RequireAsset<Sprite>(ButtonHoverPath);
            Sprite buttonPressed = RequireAsset<Sprite>(ButtonPressedPath);
            Sprite sliderTrack = RequireAsset<Sprite>(SliderTrackPath);
            Sprite sliderFill = RequireAsset<Sprite>(SliderFillPath);
            Sprite sliderHandle = RequireAsset<Sprite>(SliderHandlePath);

            GameObject rigRoot = PrefabUtility.LoadPrefabContents(PlayerRigPrefabPath);
            try
            {
                Transform hud = rigRoot.transform.Find("HUD Canvas");
                if (hud == null)
                {
                    throw new InvalidOperationException("PlayerRig is missing its direct 'HUD Canvas' child.");
                }

                if (hud.GetComponent<GraphicRaycaster>() == null)
                {
                    hud.gameObject.AddComponent<GraphicRaycaster>();
                }

                Transform existing = hud.Find("Settings Menu");
                if (existing != null)
                {
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);
                }

                SettingsMenuController controller = rigRoot.GetComponent<SettingsMenuController>();
                if (controller == null)
                {
                    controller = rigRoot.AddComponent<SettingsMenuController>();
                }

                RectTransform menuRoot = CreateRect("Settings Menu", hud, Vector2.zero, Vector2.zero);
                Stretch(menuRoot);
                Image dimmer = menuRoot.gameObject.AddComponent<Image>();
                dimmer.color = new Color(Void.r, Void.g, Void.b, 0.78f);
                dimmer.raycastTarget = true;

                RectTransform console = CreateRect("Astronaut Console", menuRoot, new Vector2(900f, 700f), Vector2.zero);
                Image consoleFrame = console.gameObject.AddComponent<Image>();
                consoleFrame.sprite = popup;
                consoleFrame.type = Image.Type.Sliced;
                consoleFrame.color = Color.white;
                consoleFrame.raycastTarget = true;

                RectTransform innerGlass = CreateRect("Void Glass", console, new Vector2(836f, 636f), Vector2.zero);
                Image glassImage = innerGlass.gameObject.AddComponent<Image>();
                glassImage.color = new Color(Glass.r, Glass.g, Glass.b, 0.97f);
                glassImage.raycastTarget = false;

                RectTransform accent = CreateRect("Solar Calibration Rail", console, new Vector2(8f, 430f), new Vector2(-370f, -5f));
                Image accentImage = accent.gameObject.AddComponent<Image>();
                accentImage.color = Solar;
                accentImage.raycastTarget = false;

                RectTransform headerPlate = CreateRect("Instrument Header", console, new Vector2(560f, 88f), new Vector2(-100f, 260f));
                Image headerImage = headerPlate.gameObject.AddComponent<Image>();
                headerImage.sprite = buttonIdle;
                headerImage.type = Image.Type.Sliced;
                headerImage.color = Color.white;
                headerImage.raycastTarget = false;

                RectTransform headerBeacon = CreateRect(
                    "Header Beacon",
                    headerPlate,
                    new Vector2(7f, 50f),
                    new Vector2(-245f, 0f));
                Image headerBeaconImage = headerBeacon.gameObject.AddComponent<Image>();
                headerBeaconImage.color = Solar;
                headerBeaconImage.raycastTarget = false;

                CreateText(
                    "Header Eyebrow",
                    headerPlate,
                    "NAUT  //  SUIT CONSOLE",
                    utilityFont,
                    14,
                    Solar,
                    TextAnchor.MiddleLeft,
                    new Vector2(450f, 22f),
                    new Vector2(5f, 18f));

                CreateText(
                    "System Header",
                    headerPlate,
                    "SYSTEM SETTINGS",
                    displayFont,
                    30,
                    Ice,
                    TextAnchor.MiddleLeft,
                    new Vector2(450f, 42f),
                    new Vector2(5f, -13f));

                Button closeButton = CreateButton(
                    "Close",
                    console,
                    "ESC   /   CLOSE",
                    displayFont,
                    18,
                    new Vector2(180f, 88f),
                    new Vector2(290f, 260f),
                    buttonIdle,
                    buttonHover,
                    buttonPressed);

                RectTransform mainPage = CreateRect("Main Page", console, new Vector2(760f, 470f), new Vector2(0f, -70f));
                RectTransform calibrationWell = CreateRect(
                    "Calibration Well",
                    mainPage,
                    new Vector2(720f, 400f),
                    new Vector2(0f, -8f));
                Image calibrationWellImage = calibrationWell.gameObject.AddComponent<Image>();
                calibrationWellImage.color = new Color(Panel.r, Panel.g, Panel.b, 0.7f);
                calibrationWellImage.raycastTarget = false;

                CreateText(
                    "Section Label",
                    mainPage,
                    "SUIT CALIBRATION",
                    utilityFont,
                    18,
                    Cyan,
                    TextAnchor.MiddleLeft,
                    new Vector2(620f, 32f),
                    new Vector2(0f, 175f));

                Slider volumeSlider = CreateSlider(
                    "Volume",
                    mainPage,
                    "MASTER VOLUME",
                    displayFont,
                    utilityFont,
                    sliderTrack,
                    sliderFill,
                    sliderHandle,
                    new Vector2(0f, 94f),
                    out Text volumeValue);

                Slider sensitivitySlider = CreateSlider(
                    "Sensitivity",
                    mainPage,
                    "LOOK SENSITIVITY",
                    displayFont,
                    utilityFont,
                    sliderTrack,
                    sliderFill,
                    sliderHandle,
                    new Vector2(0f, -36f),
                    out Text sensitivityValue);

                Button controlsButton = CreateButton(
                    "Controls",
                    mainPage,
                    "CONTROLS",
                    displayFont,
                    24,
                    new Vector2(620f, 78f),
                    new Vector2(0f, -165f),
                    buttonIdle,
                    buttonHover,
                    buttonPressed);
                CreateText(
                    "Arrow",
                    controlsButton.transform,
                    ">",
                    utilityFont,
                    22,
                    Ice,
                    TextAnchor.MiddleCenter,
                    new Vector2(40f, 40f),
                    new Vector2(270f, 0f));

                CreateText(
                    "Main Footer",
                    mainPage,
                    "NAUT OS  /  CHANGES SAVE LOCALLY",
                    utilityFont,
                    16,
                    Muted,
                    TextAnchor.MiddleCenter,
                    new Vector2(620f, 28f),
                    new Vector2(0f, -220f));

                RectTransform controlsPage = CreateRect("Controls Page", console, new Vector2(760f, 470f), new Vector2(0f, -70f));
                RectTransform controlsWell = CreateRect(
                    "Controls Well",
                    controlsPage,
                    new Vector2(720f, 400f),
                    new Vector2(0f, -8f));
                Image controlsWellImage = controlsWell.gameObject.AddComponent<Image>();
                controlsWellImage.color = new Color(Panel.r, Panel.g, Panel.b, 0.7f);
                controlsWellImage.raycastTarget = false;

                CreateText(
                    "Controls Label",
                    controlsPage,
                    "CONTROL MAP",
                    utilityFont,
                    18,
                    Cyan,
                    TextAnchor.MiddleLeft,
                    new Vector2(620f, 32f),
                    new Vector2(0f, 175f));
                CreateText(
                    "Controls Status",
                    controlsPage,
                    "BINDINGS IN FLIGHT",
                    displayFont,
                    34,
                    Ice,
                    TextAnchor.MiddleCenter,
                    new Vector2(650f, 56f),
                    new Vector2(0f, 73f));
                CreateText(
                    "Controls Note",
                    controlsPage,
                    "The full control map will land here once movement, combat,\nand shared-body inputs are locked.",
                    utilityFont,
                    23,
                    Muted,
                    TextAnchor.MiddleCenter,
                    new Vector2(650f, 92f),
                    new Vector2(0f, -8f));
                CreateText(
                    "Controls Channels",
                    controlsPage,
                    "MOVEMENT   /   AIM   /   COMBAT   /   CO-OP",
                    utilityFont,
                    17,
                    Solar,
                    TextAnchor.MiddleCenter,
                    new Vector2(650f, 32f),
                    new Vector2(0f, -82f));

                Button backButton = CreateButton(
                    "Back",
                    controlsPage,
                    "BACK",
                    displayFont,
                    24,
                    new Vector2(620f, 78f),
                    new Vector2(0f, -165f),
                    buttonIdle,
                    buttonHover,
                    buttonPressed);
                CreateText(
                    "Arrow",
                    backButton.transform,
                    "<",
                    utilityFont,
                    22,
                    Ice,
                    TextAnchor.MiddleCenter,
                    new Vector2(40f, 40f),
                    new Vector2(-270f, 0f));

                controlsPage.gameObject.SetActive(false);
                menuRoot.gameObject.SetActive(false);

                SerializedObject serializedController = new SerializedObject(controller);
                Assign(serializedController, "hudCanvas", hud.GetComponent<Canvas>());
                Assign(serializedController, "menuRoot", menuRoot.gameObject);
                Assign(serializedController, "mainPage", mainPage.gameObject);
                Assign(serializedController, "controlsPage", controlsPage.gameObject);
                Assign(serializedController, "volumeSlider", volumeSlider);
                Assign(serializedController, "volumeValue", volumeValue);
                Assign(serializedController, "sensitivitySlider", sensitivitySlider);
                Assign(serializedController, "sensitivityValue", sensitivityValue);
                Assign(serializedController, "controlsButton", controlsButton);
                Assign(serializedController, "backButton", backButton);
                Assign(serializedController, "closeButton", closeButton);
                Assign(serializedController, "playerController", rigRoot.GetComponentInChildren<global::Player.PlayerController>(true));
                Assign(serializedController, "playerCombat", rigRoot.GetComponentInChildren<global::Player.PlayerCombat>(true));
                Assign(serializedController, "emoteController", rigRoot.GetComponentInChildren<global::Player.PlayerEmoteController>(true));
                Assign(serializedController, "cameraController", rigRoot.GetComponentInChildren<global::Player.ThirdPersonCameraController>(true));
                Assign(serializedController, "crosshairUi", rigRoot.GetComponentInChildren<CrosshairUI>(true));
                serializedController.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(rigRoot, PlayerRigPrefabPath);
                Debug.Log("Configured the PlayerRig settings menu with Cartoon UI and Space Expansion UI assets.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rigRoot);
            }
        }

        public static void ConfigureSettingsMenuBatch()
        {
            ConfigureSettingsMenu();
        }

        [MenuItem("Tools/Player Prototype/Preview Settings Menu %#o")]
        public static void PreviewSettingsMenu()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Enter Play mode before previewing the settings menu.");
                return;
            }

            SettingsMenuController controller =
                UnityEngine.Object.FindFirstObjectByType<SettingsMenuController>();
            if (controller == null)
            {
                Debug.LogWarning("No live SettingsMenuController was found in the active scene.");
                return;
            }

            controller.OpenSettings();
        }

        private static Slider CreateSlider(
            string name,
            Transform parent,
            string label,
            Font displayFont,
            Font utilityFont,
            Sprite trackSprite,
            Sprite fillSprite,
            Sprite handleSprite,
            Vector2 position,
            out Text valueText)
        {
            RectTransform row = CreateRect(name, parent, new Vector2(650f, 112f), position);
            CreateText(
                "Label",
                row,
                label,
                displayFont,
                23,
                Ice,
                TextAnchor.MiddleLeft,
                new Vector2(470f, 38f),
                new Vector2(-75f, 33f));
            valueText = CreateText(
                "Value",
                row,
                "100%",
                utilityFont,
                23,
                Solar,
                TextAnchor.MiddleRight,
                new Vector2(120f, 38f),
                new Vector2(250f, 33f));

            RectTransform track = CreateRect("Track", row, new Vector2(620f, 36f), new Vector2(0f, -19f));
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = trackSprite;
            trackImage.type = Image.Type.Sliced;
            trackImage.raycastTarget = true;

            RectTransform fillArea = CreateRect("Fill Area", track, Vector2.zero, Vector2.zero);
            Stretch(fillArea, 8f, 8f, 8f, 8f);
            RectTransform fill = CreateRect("Fill", fillArea, Vector2.zero, Vector2.zero);
            Stretch(fill);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = fillSprite;
            fillImage.type = Image.Type.Sliced;
            fillImage.raycastTarget = false;

            RectTransform handleArea = CreateRect("Handle Slide Area", track, Vector2.zero, Vector2.zero);
            Stretch(handleArea, 18f, 18f, 0f, 0f);
            RectTransform handle = CreateRect("Handle", handleArea, new Vector2(38f, 38f), Vector2.zero);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = handleSprite;
            handleImage.preserveAspect = true;

            Slider slider = track.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            return slider;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Font font,
            int fontSize,
            Vector2 size,
            Vector2 position,
            Sprite idle,
            Sprite hover,
            Sprite pressed)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = idle;
            image.type = Image.Type.Sliced;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = hover,
                selectedSprite = hover,
                pressedSprite = pressed,
                disabledSprite = idle
            };
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };

            CreateText(
                "Label",
                rect,
                label,
                font,
                fontSize,
                Ice,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.zero,
                true);
            return button;
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
            if (stretch) Stretch(rect, 16f, 16f, 8f, 8f);

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
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
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

        private static void Assign(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Missing serialized property '{propertyName}'.");
            }
            property.objectReferenceValue = value;
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"Required settings-menu asset is missing: {path}");
            }
            return asset;
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
                [SliderHandlePath] = Vector4.zero
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
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static Color Hex(string rgb)
        {
            if (!ColorUtility.TryParseHtmlString($"#{rgb}", out Color color))
            {
                throw new ArgumentException($"Invalid colour '{rgb}'.", nameof(rgb));
            }
            return color;
        }
    }
}

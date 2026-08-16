using Player.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PlayerEditor
{
    /// <summary>
    /// Builds (or rebuilds) the Opening Field Guide overlay as real, hand-editable GameObjects
    /// under the scene's OpeningGuideController, instead of OpeningGuideController constructing it
    /// procedurally the first time it opens at runtime. Same editor-time-codegen-into-the-saved-
    /// asset pattern as WaveUiPrefabSetup/LeaderboardSceneSetup. Idempotent: destroys and rebuilds
    /// only its own "Opening Field Guide" subtree, then re-wires OpeningGuideController.Configure.
    /// Reads its screenshot textures/font/sprites from the already-assigned fields on the scene's
    /// OpeningGuideController component rather than taking them as parameters.
    /// </summary>
    public static class OpeningGuideSceneSetup
    {
        private const string OverlayRootName = "Opening Field Guide";

        private static readonly Color BackdropColor = new Color(0.005f, 0.018f, 0.04f, 0.88f);
        private static readonly Color PanelColor = new Color(0.025f, 0.065f, 0.105f, 0.98f);
        private static readonly Color CardColor = new Color(0.025f, 0.045f, 0.075f, 0.98f);
        private static readonly Color MutedTextColor = new Color(0.65f, 0.76f, 0.84f, 1f);
        private static readonly Color Cyan = new Color(0.2f, 0.78f, 1f, 1f);
        private static readonly Color Blue = new Color(0.37f, 0.36f, 1f, 1f);
        private static readonly Color Green = new Color(0.18f, 0.88f, 0.62f, 1f);
        private static readonly Color Amber = new Color(1f, 0.68f, 0.12f, 1f);

        private static Font _hudFont;

        [MenuItem("Tools/Player Prototype/Rebuild Opening Field Guide")]
        public static void RebuildGuide()
        {
            OpeningGuideController controller =
                Object.FindFirstObjectByType<OpeningGuideController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("OpeningGuideSceneSetup: no OpeningGuideController found in the open scene.");
                return;
            }

            SerializedObject controllerSerialized = new SerializedObject(controller);
            Texture2D skillImage = ReadTexture(controllerSerialized, "skillImage");
            Texture2D baseImage = ReadTexture(controllerSerialized, "baseImage");
            Texture2D specialImage = ReadTexture(controllerSerialized, "specialImage");
            Texture2D outsideImage = ReadTexture(controllerSerialized, "outsideImage");
            Texture2D arenaImage = ReadTexture(controllerSerialized, "arenaImage");
            _hudFont = controllerSerialized.FindProperty("hudFont").objectReferenceValue as Font;
            Sprite panelSprite = controllerSerialized.FindProperty("panelSprite").objectReferenceValue as Sprite;
            Sprite buttonSprite = controllerSerialized.FindProperty("buttonSprite").objectReferenceValue as Sprite;
            if (_hudFont == null) _hudFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Transform existing = controller.transform.Find(OverlayRootName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject overlayRoot = new GameObject(
                OverlayRootName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            overlayRoot.transform.SetParent(controller.transform, false);

            Canvas canvas = overlayRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = overlayRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rootRect = overlayRoot.GetComponent<RectTransform>();
            Stretch(rootRect, Vector2.zero, Vector2.zero);

            Image backdrop = CreateImage("Backdrop", overlayRoot.transform, BackdropColor);
            Stretch(backdrop.rectTransform, Vector2.zero, Vector2.zero);

            Image panel = CreateImage("Mission Panel", overlayRoot.transform, PanelColor, panelSprite);
            RectTransform panelRect = panel.rectTransform;
            Anchor(panelRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1680f, 900f));

            Image accent = CreateImage("Accent", panel.transform, Cyan);
            RectTransform accentRect = accent.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.offsetMin = Vector2.zero;
            accentRect.offsetMax = new Vector2(8f, 0f);

            Text kicker = CreateText(panel.transform, "Kicker", 18, FontStyle.Bold, TextAnchor.UpperLeft, Cyan);
            SetRect(kicker.rectTransform, new Vector2(56f, 834f), new Vector2(700f, 30f));
            kicker.text = "NAUT // FIELD GUIDE";

            Text pageTitle = CreateText(panel.transform, "Page Title", 42, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
            SetRect(pageTitle.rectTransform, new Vector2(56f, 770f), new Vector2(1100f, 56f));

            Text pageSubtitle = CreateText(panel.transform, "Page Subtitle", 20, FontStyle.Normal, TextAnchor.UpperLeft, MutedTextColor);
            SetRect(pageSubtitle.rectTransform, new Vector2(58f, 728f), new Vector2(1100f, 38f));

            Text progressText = CreateText(panel.transform, "Page Progress", 20, FontStyle.Bold, TextAnchor.MiddleRight, MutedTextColor);
            SetRect(progressText.rectTransform, new Vector2(1220f, 785f), new Vector2(360f, 44f));

            GameObject page0 = BuildBasePage(panel.transform, skillImage, baseImage, specialImage);
            GameObject page1 = BuildWavePage(panel.transform, outsideImage);
            GameObject page2 = BuildArenaPage(panel.transform, arenaImage);

            Text footer = CreateText(panel.transform, "Skip Hint", 15, FontStyle.Normal, TextAnchor.MiddleLeft, MutedTextColor);
            SetRect(footer.rectTransform, new Vector2(58f, 34f), new Vector2(650f, 44f));
            footer.text = "CLICK NEXT TO SKIP THROUGH  //  ESC SKIPS GUIDE";

            Button skipButton = CreateButton(panel.transform, "Skip Guide", new Vector2(1340f, 836f), new Vector2(240f, 44f), false, buttonSprite);
            Text skipLabel = skipButton.GetComponentInChildren<Text>();
            skipLabel.text = "SKIP GUIDE";
            skipLabel.fontSize = 16;
            skipLabel.color = MutedTextColor;

            Button nextButton = CreateButton(panel.transform, "Next", new Vector2(1320f, 30f), new Vector2(260f, 64f), true, buttonSprite);
            Text nextLabel = nextButton.GetComponentInChildren<Text>();

            UiSfxWirer.WireAll(overlayRoot);
            overlayRoot.SetActive(false);

            controller.Configure(
                overlayRoot,
                new[] { page0, page1, page2 },
                pageTitle,
                pageSubtitle,
                progressText,
                nextButton,
                nextLabel,
                skipButton);
            EditorUtility.SetDirty(controller);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("OpeningGuideSceneSetup: rebuilt the Opening Field Guide overlay and wired OpeningGuideController.");
        }

        private static GameObject BuildBasePage(Transform panel, Texture skillImage, Texture baseImage, Texture specialImage)
        {
            GameObject page = CreatePageRoot(panel, "Page 1 - Base Stations");
            CreateStationCard(page.transform, "ARCHIVE // BLUE", "UPGRADE YOUR STATS",
                "Prioritise DAMAGE and MAX AMMO early.", skillImage, Blue, -520f);
            CreateStationCard(page.transform, "SUPPLY // GREEN", "RESTOCK FOR THE NEXT WAVE",
                "Buy health packs and ammo refills. Top up before heading out.", baseImage, Green, 0f);
            CreateStationCard(page.transform, "SPECIAL // YELLOW", "UNLOCK SPECIAL SKILLS",
                "Powerful run-changing skills live here. Check the catalog.", specialImage, Amber, 520f);
            return page;
        }

        private static GameObject BuildWavePage(Transform panel, Texture outsideImage)
        {
            GameObject page = CreatePageRoot(panel, "Page 2 - Start The Wave");

            CreateScreenshotFrame(page.transform, "Base Exit", outsideImage,
                new Vector2(-260f, 0f), new Vector2(1010f, 530f), Cyan);

            Image rail = CreateImage("Wave Brief", page.transform, CardColor);
            Anchor(rail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(545f, 0f), new Vector2(470f, 530f));
            AddAccentBar(rail.transform, Cyan);

            Text step = CreateText(rail.transform, "Step", 17, FontStyle.Bold, TextAnchor.UpperLeft, Cyan);
            SetRect(step.rectTransform, new Vector2(34f, 458f), new Vector2(390f, 32f));
            step.text = "DEPLOYMENT // REGULAR WAVE";

            Text body = CreateText(rail.transform, "Body", 25, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
            SetRect(body.rectTransform, new Vector2(34f, 120f), new Vector2(390f, 325f));
            body.text = "Leave the base, then HOLD F to start the first wave.\n\nSurvive until the wave timer reaches zero.\n\nEvery enemy you eliminate pays gold - fight more, earn more, return stronger.";

            return page;
        }

        private static GameObject BuildArenaPage(Transform panel, Texture arenaImage)
        {
            GameObject page = CreatePageRoot(panel, "Page 3 - Arena Contracts");

            CreateScreenshotFrame(page.transform, "Arena Arrow", arenaImage,
                new Vector2(-390f, 0f), new Vector2(720f, 570f), Amber);

            Image rail = CreateImage("Arena Brief", page.transform, CardColor);
            Anchor(rail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(440f, 0f), new Vector2(650f, 570f));
            AddAccentBar(rail.transform, Amber);

            Text step = CreateText(rail.transform, "Step", 17, FontStyle.Bold, TextAnchor.UpperLeft, Amber);
            SetRect(step.rectTransform, new Vector2(38f, 496f), new Vector2(540f, 32f));
            step.text = "EVERY 5 WAVES // MANDATORY CONTRACT";

            Text body = CreateText(rail.transform, "Body", 25, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
            SetRect(body.rectTransform, new Vector2(38f, 125f), new Vector2(550f, 350f));
            body.text = "Follow the HUD arrow to the arena.\n\nOnce combat begins, the arena seals. Defeat every enemy to survive - you cannot leave until the contract is complete.\n\nPrepare health, ammo, and upgrades before entering.";

            Text warning = CreateText(rail.transform, "Warning", 18, FontStyle.Bold, TextAnchor.MiddleLeft, Amber);
            SetRect(warning.rectTransform, new Vector2(38f, 48f), new Vector2(550f, 50f));
            warning.text = "NO EXIT // PREPARE BEFORE ENTRY";

            return page;
        }

        private static void CreateStationCard(
            Transform parent, string routeLabel, string title, string body, Texture texture, Color accentColor, float x)
        {
            Image card = CreateImage(title, parent, CardColor);
            Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(480f, 570f));
            AddAccentBar(card.transform, accentColor);

            Text route = CreateText(card.transform, "Route", 15, FontStyle.Bold, TextAnchor.MiddleLeft, accentColor);
            SetRect(route.rectTransform, new Vector2(24f, 516f), new Vector2(420f, 30f));
            route.text = routeLabel;

            CreateScreenshotFrame(card.transform, "Screenshot", texture,
                new Vector2(0f, 65f), new Vector2(430f, 330f), accentColor);

            Text name = CreateText(card.transform, "Title", 20, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
            SetRect(name.rectTransform, new Vector2(24f, 128f), new Vector2(420f, 54f));
            name.text = title;

            Text detail = CreateText(card.transform, "Detail", 18, FontStyle.Normal, TextAnchor.UpperLeft, MutedTextColor);
            SetRect(detail.rectTransform, new Vector2(24f, 28f), new Vector2(425f, 96f));
            detail.text = body;
        }

        private static void CreateScreenshotFrame(
            Transform parent, string name, Texture texture, Vector2 position, Vector2 size, Color accentColor)
        {
            Image frame = CreateImage(name + " Frame", parent, new Color(0.005f, 0.012f, 0.022f, 1f));
            Anchor(frame.rectTransform, new Vector2(0.5f, 0.5f), position, size);

            Image outline = CreateImage("Outline", frame.transform, new Color(accentColor.r, accentColor.g, accentColor.b, 0.72f));
            Stretch(outline.rectTransform, Vector2.zero, Vector2.zero);

            Image inset = CreateImage("Inset", outline.transform, new Color(0.005f, 0.012f, 0.022f, 1f));
            Stretch(inset.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));

            var rawObject = new GameObject("Image", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            rawObject.transform.SetParent(inset.transform, false);
            RawImage raw = rawObject.GetComponent<RawImage>();
            raw.texture = texture;
            raw.color = texture != null ? Color.white : new Color(0.15f, 0.22f, 0.28f, 1f);
            raw.raycastTarget = false;
            Anchor(raw.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(Mathf.Max(1f, size.x - 12f), Mathf.Max(1f, size.y - 12f)));

            AspectRatioFitter fitter = rawObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = texture != null && texture.height > 0
                ? (float)texture.width / texture.height
                : 16f / 9f;
        }

        private static void AddAccentBar(Transform parent, Color color)
        {
            Image bar = CreateImage("Accent", parent, color);
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(5f, 0f);
        }

        private static GameObject CreatePageRoot(Transform panel, string name)
        {
            var page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(panel, false);
            RectTransform rect = page.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(58f, 116f);
            rect.offsetMax = new Vector2(-58f, -210f);
            return page;
        }

        private static Button CreateButton(Transform parent, string name, Vector2 position, Vector2 size, bool primary, Sprite buttonSprite)
        {
            Image image = CreateImage(name, parent,
                primary ? Cyan : new Color(0.05f, 0.11f, 0.16f, 0.85f),
                primary ? buttonSprite : null);
            SetRect(image.rectTransform, position, size);

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = primary ? new Color(0.8f, 1f, 1f, 1f) : new Color(0.18f, 0.28f, 0.36f, 1f);
            colors.pressedColor = primary ? new Color(0.55f, 0.88f, 1f, 1f) : new Color(0.1f, 0.2f, 0.28f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            Text label = CreateText(button.transform, "Label", primary ? 22 : 16,
                FontStyle.Bold, TextAnchor.MiddleCenter, primary ? new Color(0.02f, 0.08f, 0.12f, 1f) : MutedTextColor);
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        private static Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            return image;
        }

        private static Text CreateText(
            Transform parent, string name, int fontSize, FontStyle style, TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = _hudFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetRect(RectTransform rect, Vector2 bottomLeft, Vector2 size)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = bottomLeft;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static Texture2D ReadTexture(SerializedObject serializedObject, string propertyName) =>
            serializedObject.FindProperty(propertyName).objectReferenceValue as Texture2D;
    }
}

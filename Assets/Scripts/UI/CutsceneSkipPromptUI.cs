using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    /// Self-built, lazily-created "PRESS SPACE TO SKIP" prompt shown bottom-right during any
    /// skippable cutscene (opening singleplayer/tutorial shots, the boss stage1->stage2
    /// transformation). One shared persistent instance - Show()/Hide() toggle it rather than
    /// each cutscene owning its own copy, so simultaneous or back-to-back cutscenes never end up
    /// with two stacked prompts.
    public static class CutsceneSkipPromptUI
    {
        private const string DefaultMessage = "PRESS SPACE TO SKIP";

        private static GameObject _root;
        private static Text _text;

        public static void Show(string message = DefaultMessage)
        {
            EnsureBuilt();
            _text.text = message;
            _root.SetActive(true);
        }

        public static void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        private static void EnsureBuilt()
        {
            if (_root != null) return;

            var canvasGo = new GameObject("CutsceneSkipPromptCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler));
            Object.DontDestroyOnLoad(canvasGo);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Below the scene wipe (32760) so a transition covers it too, above ordinary HUD/menu
            // canvases so it reads over any cutscene framing.
            canvas.sortingOrder = 32000;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var textGo = new GameObject("SkipPrompt", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(canvasGo.transform, false);

            var rect = (RectTransform)textGo.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-40f, 40f);
            rect.sizeDelta = new Vector2(440f, 40f);

            _text = textGo.GetComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 22;
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.LowerRight;
            _text.color = new Color(1f, 0.85f, 0.15f, 1f);
            _text.raycastTarget = false;

            _root = textGo;
            _root.SetActive(false);
        }
    }
}

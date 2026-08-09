using Gameplay.Waves;
using Player.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Leaderboard
{
    /// <summary>Adds the main-menu "best score" readout and top-right Leaderboard button without
    /// editing the sealed MainMenuController - self-built the same way SettingsMenuController
    /// builds its confirm dialog. Add this component alongside MainMenuController on the same
    /// GameObject in MainMenu.unity; it resolves the Canvas itself.
    ///
    /// Both elements reuse the actual CartoonSciFi_Button_Idle sprite the mission-select buttons
    /// use (found at runtime via UiSpriteFinder, already present in this scene) instead of a
    /// procedural flat box, so they read as the same UI kit rather than a bolted-on placeholder.
    /// Clicking Leaderboard navigates to the dedicated Leaderboard scene (built by
    /// Tools > Leaderboard > Build Leaderboard Scene) rather than opening an in-place popup.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuLeaderboardEntryPoint : MonoBehaviour
    {
        [SerializeField] private Canvas menuCanvas;

        private void Awake()
        {
            if (menuCanvas == null) menuCanvas = GetComponentInChildren<Canvas>(true);

            Sprite buttonSprite = UiSpriteFinder.FindSpriteByName(menuCanvas.transform, "CartoonSciFi_Button_Idle");

            BuildBestScoreReadout(buttonSprite);
            BuildLeaderboardButton(buttonSprite);
        }

        // Moved to the top of the screen (was tucked below the mission-select panel, easy to
        // miss) and given the same button-kit background instead of bare text.
        private void BuildBestScoreReadout(Sprite kitSprite)
        {
            Transform parent = menuCanvas != null ? menuCanvas.transform : transform;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("BestScoreReadout", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(340f, 48f);
            rect.anchoredPosition = new Vector2(-32f, -108f);

            var fillImage = go.GetComponent<Image>();
            if (kitSprite != null)
            {
                fillImage.sprite = kitSprite;
                fillImage.type = Image.Type.Sliced;
                fillImage.color = new Color(0.09f, 0.12f, 0.17f, 0.95f);
            }
            else
            {
                fillImage.color = new Color(0.05f, 0.07f, 0.1f, 0.85f);
            }

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = font;
            text.fontSize = 15;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.85f, 0.25f, 1f);
            text.text = LocalScoreRecord.BestScore > 0
                ? $"PERSONAL BEST  {LocalScoreRecord.BestScore} PTS  //  WAVE {LocalScoreRecord.BestWaveReached}"
                : "NO RUNS YET";
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 0f);
            textRect.offsetMax = new Vector2(-10f, 0f);
        }

        private void BuildLeaderboardButton(Sprite kitSprite)
        {
            Transform parent = menuCanvas != null ? menuCanvas.transform : transform;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("LeaderboardButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(240f, 64f);
            rect.anchoredPosition = new Vector2(-32f, -32f);

            var fillImage = go.GetComponent<Image>();
            if (kitSprite != null)
            {
                fillImage.sprite = kitSprite;
                fillImage.type = Image.Type.Sliced;
                fillImage.color = Color.white;
            }
            else
            {
                fillImage.color = new Color(0.09f, 0.12f, 0.17f, 0.95f);
            }

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(go.transform, false);
            var title = titleGo.GetComponent<Text>();
            title.font = font;
            title.fontSize = 18;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            title.text = "LEADERBOARD";
            var titleRect = (RectTransform)titleGo.transform;
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(14f, 0f);
            titleRect.offsetMax = new Vector2(-14f, -8f);

            var subtitleGo = new GameObject("Subtitle", typeof(RectTransform), typeof(Text));
            subtitleGo.transform.SetParent(go.transform, false);
            var subtitle = subtitleGo.GetComponent<Text>();
            subtitle.font = font;
            subtitle.fontSize = 11;
            subtitle.alignment = TextAnchor.MiddleCenter;
            subtitle.color = new Color(0.65f, 0.75f, 0.85f, 0.9f);
            subtitle.text = "VIEW GLOBAL RANKINGS";
            var subtitleRect = (RectTransform)subtitleGo.transform;
            subtitleRect.anchorMin = new Vector2(0f, 0f);
            subtitleRect.anchorMax = new Vector2(1f, 0.5f);
            subtitleRect.offsetMin = new Vector2(14f, 8f);
            subtitleRect.offsetMax = new Vector2(-14f, 0f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = fillImage;
            button.onClick.AddListener(OpenLeaderboard);
            UiSfxWirer.WireAll(go);
        }

        private void OpenLeaderboard()
        {
            SceneTransitionController.LoadScene("Leaderboard");
        }
    }
}

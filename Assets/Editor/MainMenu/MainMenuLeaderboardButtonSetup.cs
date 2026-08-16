using Player.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenuEditor
{
    /// One-shot addition of a top-left "Leaderboards" button to MainMenu.unity's home page.
    /// Idempotent: does nothing if the button is already wired. Clones the existing Tutorial
    /// button (via Object.Instantiate) so it automatically inherits the exact same sprite/
    /// colors/font/size as the rest of the home page, rather than hand-building UI.
    public static class MainMenuLeaderboardButtonSetup
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Tools/MainMenu/Add Leaderboards Button")]
        public static void AddLeaderboardsButton()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                var controller = Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
                if (controller == null)
                {
                    Debug.LogError("MainMenuLeaderboardButtonSetup: no MainMenuController found in MainMenu.unity.");
                    return;
                }

                var so = new SerializedObject(controller);
                var leaderboardButtonProperty = so.FindProperty("leaderboardButton");
                if (leaderboardButtonProperty.objectReferenceValue != null)
                {
                    Debug.Log("MainMenuLeaderboardButtonSetup: Leaderboards button is already wired - nothing to do.");
                    return;
                }

                var tutorialButton = so.FindProperty("tutorialButton").objectReferenceValue as Button;
                if (tutorialButton == null)
                {
                    Debug.LogError("MainMenuLeaderboardButtonSetup: no Tutorial button found to use as a style template.");
                    return;
                }

                var tutorialRect = tutorialButton.GetComponent<RectTransform>();
                var newButtonGo = Object.Instantiate(tutorialButton.gameObject, tutorialRect.parent);
                newButtonGo.name = "LeaderboardButton";
                var newRect = newButtonGo.GetComponent<RectTransform>();

                // Top-left placement, independent of wherever the home page's own button
                // stack sits - anchored/pivoted to the canvas top-left corner with a fixed
                // inset so it reads as a persistent corner action rather than part of the
                // vertical Singleplayer/Tutorial/Settings/Quit stack.
                newRect.anchorMin = new Vector2(0f, 1f);
                newRect.anchorMax = new Vector2(0f, 1f);
                newRect.pivot = new Vector2(0f, 1f);
                newRect.anchoredPosition = new Vector2(40f, -40f);

                var label = newButtonGo.GetComponentInChildren<Text>(true);
                if (label != null) label.text = "LEADERBOARDS";

                var newButton = newButtonGo.GetComponent<Button>();
                newButton.onClick = new Button.ButtonClickedEvent(); // no persistent calls carried over from Tutorial

                leaderboardButtonProperty.objectReferenceValue = newButton;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    Debug.LogError("MainMenuLeaderboardButtonSetup: failed to save MainMenu.unity.");
                    return;
                }
            }
            finally
            {
            }

            Debug.Log("MainMenuLeaderboardButtonSetup: added a top-left Leaderboards button to MainMenu.unity.");
        }
    }
}

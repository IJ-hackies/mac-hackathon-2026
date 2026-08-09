using Player.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenuEditor
{
    /// One-shot setup that adds a Tutorial button and a Quit button to the MainMenu home page,
    /// each cloned from the existing Settings button (same sprite/colors/font/Icon+Label+Detail
    /// layout as every other home button) and stacked below it. The old Multiplayer button this
    /// used to restyle no longer exists upstream, so both buttons are now built from scratch
    /// rather than repurposing an existing one. Idempotent: does nothing to a button already
    /// wired. Run via Tools/Main Menu/Build Tutorial And Quit Buttons.
    public static class MainMenuTutorialAndQuitSetup
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Tools/Main Menu/Build Tutorial And Quit Buttons")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var controller = Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("MainMenuTutorialAndQuitSetup: no MainMenuController found in the scene.");
                return;
            }

            var so = new SerializedObject(controller);
            var tutorialButtonProp = so.FindProperty("tutorialButton");
            var quitButtonProp = so.FindProperty("quitButton");
            var singleplayerButtonProp = so.FindProperty("singleplayerButton");
            var settingsButtonProp = so.FindProperty("settingsButton");

            var singleplayerButton = singleplayerButtonProp.objectReferenceValue as Button;
            var settingsButton = settingsButtonProp.objectReferenceValue as Button;
            if (settingsButton == null)
            {
                Debug.LogError("MainMenuTutorialAndQuitSetup: no settingsButton reference to clone from.");
                return;
            }

            // Derive the vertical gap between home-page rows from the two buttons we know exist,
            // instead of a hardcoded constant, so this keeps working if the layout is retuned.
            float rowSpacing = -120f;
            if (singleplayerButton != null)
            {
                var singleplayerRect = singleplayerButton.GetComponent<RectTransform>();
                var settingsRect = settingsButton.GetComponent<RectTransform>();
                rowSpacing = settingsRect.anchoredPosition.y - singleplayerRect.anchoredPosition.y;
            }

            float nextRowY = settingsButton.GetComponent<RectTransform>().anchoredPosition.y + rowSpacing;

            if (tutorialButtonProp.objectReferenceValue == null)
            {
                Button tutorialButton = BuildHomeButton(
                    settingsButton, "Tutorial", "TUTORIAL", "LEARN HOW TO PLAY THE GAME", nextRowY);
                tutorialButtonProp.objectReferenceValue = tutorialButton;
                nextRowY += rowSpacing;
            }
            else
            {
                Debug.Log("MainMenuTutorialAndQuitSetup: Tutorial button is already wired - nothing to do.");
                nextRowY = ((Button)tutorialButtonProp.objectReferenceValue).GetComponent<RectTransform>()
                    .anchoredPosition.y + rowSpacing;
            }

            if (quitButtonProp.objectReferenceValue == null)
            {
                Button quitButton = BuildHomeButton(
                    settingsButton, "Quit", "QUIT", "EXIT TO DESKTOP", nextRowY);
                quitButtonProp.objectReferenceValue = quitButton;
            }
            else
            {
                Debug.Log("MainMenuTutorialAndQuitSetup: Quit button is already wired - nothing to do.");
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("MainMenuTutorialAndQuitSetup: done.");
        }

        private static Button BuildHomeButton(
            Button template, string objectName, string label, string detail, float anchoredY)
        {
            var go = Object.Instantiate(template.gameObject, template.transform.parent);
            go.name = objectName;

            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0f, anchoredY);

            // No dedicated icon art for these - drop the Icon slot and recenter Label/Detail the
            // same way the (now-removed) icon-less Multiplayer/Tutorial button used to.
            var icon = go.transform.Find("Icon");
            if (icon != null) Object.DestroyImmediate(icon.gameObject);

            var labelText = go.transform.Find("Label")?.GetComponent<Text>();
            if (labelText != null)
            {
                labelText.text = label;
                var labelRect = labelText.GetComponent<RectTransform>();
                labelRect.anchoredPosition = new Vector2(-8f, labelRect.anchoredPosition.y);
            }

            var detailText = go.transform.Find("Detail")?.GetComponent<Text>();
            if (detailText != null)
            {
                detailText.text = detail;
                var detailRect = detailText.GetComponent<RectTransform>();
                detailRect.anchoredPosition = new Vector2(-8f, detailRect.anchoredPosition.y);
            }

            var badge = go.transform.Find("Offline Badge");
            if (badge != null) Object.DestroyImmediate(badge.gameObject);

            var button = go.GetComponent<Button>();
            // No persistent calls carried over from the template - MainMenuController wires
            // LoadTutorial/QuitGame itself at runtime via RegisterListeners.
            button.onClick = new Button.ButtonClickedEvent();
            button.interactable = true;

            return button;
        }
    }
}

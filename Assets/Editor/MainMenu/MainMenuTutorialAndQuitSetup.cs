using Player.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenuEditor
{
    /// One-shot setup that re-skins the former Multiplayer button as the Tutorial button and adds
    /// a Quit button to the MainMenu home page. Idempotent: does nothing to a piece already done.
    /// Run via Tools/Main Menu/Convert Multiplayer To Tutorial And Add Quit, or headless with
    /// -executeMethod MainMenuEditor.MainMenuTutorialAndQuitSetup.Run.
    public static class MainMenuTutorialAndQuitSetup
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Tools/Main Menu/Convert Multiplayer To Tutorial And Add Quit")]
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
            var singleplayerButtonProp = so.FindProperty("singleplayerButton");
            var quitButtonProp = so.FindProperty("quitButton");

            var tutorialButton = tutorialButtonProp.objectReferenceValue as Button;
            if (tutorialButton == null)
            {
                Debug.LogError("MainMenuTutorialAndQuitSetup: tutorialButton reference is missing - " +
                    "expected FormerlySerializedAs to carry over the old Multiplayer button reference.");
                return;
            }

            RestyleAsTutorialButton(tutorialButton);

            if (quitButtonProp.objectReferenceValue == null)
            {
                var singleplayerButton = singleplayerButtonProp.objectReferenceValue as Button;
                if (singleplayerButton == null)
                {
                    Debug.LogError("MainMenuTutorialAndQuitSetup: no singleplayerButton reference to clone from.");
                    return;
                }

                Button quitButton = BuildQuitButton(singleplayerButton, tutorialButton);
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

        private static void RestyleAsTutorialButton(Button tutorialButton)
        {
            tutorialButton.gameObject.name = "Tutorial";
            tutorialButton.interactable = true;

            var image = tutorialButton.GetComponent<Image>();
            if (image != null) image.color = new Color(1f, 1f, 1f, 1f);

            var label = tutorialButton.transform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.text = "TUTORIAL";
                label.color = new Color(0.9529412f, 0.9843137f, 1f, 1f);
            }

            var detail = tutorialButton.transform.Find("Detail")?.GetComponent<Text>();
            if (detail != null)
            {
                detail.text = "LEARN HOW TO PLAY THE GAME";
                detail.color = new Color(0.52156866f, 0.84705883f, 1f, 1f);
            }

            var badge = tutorialButton.transform.Find("Offline Badge");
            if (badge != null) badge.gameObject.SetActive(false);
        }

        private static Button BuildQuitButton(Button template, Button tutorialButton)
        {
            var quitGo = Object.Instantiate(template.gameObject, template.transform.parent);
            quitGo.name = "Quit";

            var tutorialRect = tutorialButton.GetComponent<RectTransform>();
            var quitRect = quitGo.GetComponent<RectTransform>();
            quitRect.anchoredPosition = new Vector2(tutorialRect.anchoredPosition.x, -287f);

            var icon = quitGo.transform.Find("Icon");
            if (icon != null) Object.DestroyImmediate(icon.gameObject);

            var label = quitGo.transform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.text = "QUIT";
                var labelRect = label.GetComponent<RectTransform>();
                labelRect.anchoredPosition = new Vector2(-8f, 15f);
            }

            var detail = quitGo.transform.Find("Detail")?.GetComponent<Text>();
            if (detail != null)
            {
                detail.text = "EXIT TO DESKTOP";
                var detailRect = detail.GetComponent<RectTransform>();
                detailRect.anchoredPosition = new Vector2(-8f, -21f);
            }

            var badge = quitGo.transform.Find("Offline Badge");
            if (badge != null) Object.DestroyImmediate(badge.gameObject);

            var quitButton = quitGo.GetComponent<Button>();
            quitButton.onClick = new Button.ButtonClickedEvent();
            quitButton.interactable = true;

            return quitButton;
        }
    }
}

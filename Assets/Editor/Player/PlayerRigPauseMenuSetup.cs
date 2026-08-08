using Player.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PlayerEditor
{
    /// One-shot addition of a "Return To Main Menu" button to PlayerRig.prefab's shared Escape
    /// pause menu (SettingsMenuController) - since SampleScene and Tutorial both instantiate this
    /// same prefab, this covers both scenes' pause menus at once. Idempotent: does nothing if the
    /// button is already wired. Clones the existing Close button (via Object.Instantiate) rather
    /// than hand-building UI, so it automatically inherits the exact same sprite/colors/font/size
    /// as every other button in this menu, and appends one row further in whatever vertical
    /// spacing Controls->Close already uses, rather than guessing a layout from scratch.
    public static class PlayerRigPauseMenuSetup
    {
        private const string PlayerRigPrefabPath = "Assets/Prefabs/PlayerRig.prefab";

        [MenuItem("Tools/Player Prototype/Add Return To Main Menu Button")]
        public static void AddReturnToMainMenuButton()
        {
            GameObject rigRoot = PrefabUtility.LoadPrefabContents(PlayerRigPrefabPath);
            try
            {
                var settings = rigRoot.GetComponentInChildren<SettingsMenuController>(true);
                if (settings == null)
                {
                    Debug.LogError("PlayerRigPauseMenuSetup: no SettingsMenuController found in the prefab.");
                    return;
                }

                var so = new SerializedObject(settings);
                var mainMenuButtonProperty = so.FindProperty("mainMenuButton");
                if (mainMenuButtonProperty.objectReferenceValue != null)
                {
                    Debug.Log("PlayerRigPauseMenuSetup: Return To Main Menu button is already wired - nothing to do.");
                    return;
                }

                var closeButton = so.FindProperty("closeButton").objectReferenceValue as Button;
                var controlsButton = so.FindProperty("controlsButton").objectReferenceValue as Button;
                if (closeButton == null)
                {
                    Debug.LogError("PlayerRigPauseMenuSetup: no Close button found to use as a style template.");
                    return;
                }

                var closeRect = closeButton.GetComponent<RectTransform>();
                Vector2 rowSpacing = new Vector2(0f, -70f);
                if (controlsButton != null)
                {
                    var controlsRect = controlsButton.GetComponent<RectTransform>();
                    rowSpacing = closeRect.anchoredPosition - controlsRect.anchoredPosition;
                }

                var newButtonGo = Object.Instantiate(closeButton.gameObject, closeRect.parent);
                newButtonGo.name = "MainMenuButton";
                var newRect = newButtonGo.GetComponent<RectTransform>();
                newRect.anchoredPosition = closeRect.anchoredPosition + rowSpacing;

                var label = newButtonGo.GetComponentInChildren<Text>(true);
                if (label != null) label.text = "MAIN MENU";

                var newButton = newButtonGo.GetComponent<Button>();
                newButton.onClick = new Button.ButtonClickedEvent(); // no persistent calls carried over from Close

                mainMenuButtonProperty.objectReferenceValue = newButton;
                so.ApplyModifiedPropertiesWithoutUndo();

                if (PrefabUtility.SaveAsPrefabAsset(rigRoot, PlayerRigPrefabPath) == null)
                {
                    Debug.LogError("PlayerRigPauseMenuSetup: failed to save PlayerRig.prefab.");
                    return;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rigRoot);
            }

            AssetDatabase.ImportAsset(PlayerRigPrefabPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("PlayerRigPauseMenuSetup: added a Return To Main Menu button to PlayerRig.prefab's pause menu.");
        }
    }
}

using System;
using System.Linq;
using Player.UI.Leaderboard;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LeaderboardEditor
{
    /// <summary>Builds Assets/Scenes/Leaderboard.unity from scratch: Canvas, EventSystem, and a
    /// LeaderboardSceneController wired with the project's real CartoonSciFi UI kit sprites and
    /// the medal pack, then registers the scene in Build Settings. Idempotent - re-running clears
    /// and rebuilds the generated hierarchy rather than duplicating it. Requires the medal PNGs
    /// already imported (Tools > Leaderboard > Configure Medal Art is no longer needed - this
    /// tool imports them itself if missing).</summary>
    public static class LeaderboardSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Leaderboard.unity";
        private const string ButtonSpritePath = "Assets/Art/Textures/UI/Settings/CartoonSciFi_Button_Idle.png";
        private const string PopupSpritePath = "Assets/Art/Textures/UI/Settings/CartoonSciFi_Popup.png";
        private const string GoldMedalPath = "Assets/Art/UI/Medals/Gold01.png";
        private const string SilverMedalPath = "Assets/Art/UI/Medals/Silver01.png";
        private const string BronzeMedalPath = "Assets/Art/UI/Medals/Bronze01.png";

        [MenuItem("Tools/Leaderboard/Build Leaderboard Scene")]
        public static void BuildScene()
        {
            Sprite buttonSprite = LoadSprite(ButtonSpritePath);
            Sprite popupSprite = LoadSprite(PopupSpritePath);
            Sprite gold = LoadSprite(GoldMedalPath);
            Sprite silver = LoadSprite(SilverMedalPath);
            Sprite bronze = LoadSprite(BronzeMedalPath);

            Scene scene = System.IO.File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            // Found root cause of "no music in this scene": this camera had no AudioListener.
            // MainMenu's own AudioListener is destroyed on the scene switch (Single load mode),
            // and with zero AudioListeners left in the loaded scene, Unity plays no audio at all
            // regardless of whether MusicManager's AudioSources are correctly running - the
            // PlayMusic() call was firing and succeeding, there was just nothing in this scene
            // able to actually hear it.
            var backgroundGo = new GameObject("Background", typeof(Camera), typeof(AudioListener));
            var camera = backgroundGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.04f, 0.07f, 1f);
            camera.orthographic = true;
            camera.cullingMask = 0;

            var eventSystemGo = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var controllerGo = new GameObject("LeaderboardSceneController", typeof(RectTransform));
            controllerGo.transform.SetParent(canvasGo.transform, false);
            var controllerRect = controllerGo.GetComponent<RectTransform>();
            controllerRect.anchorMin = Vector2.zero;
            controllerRect.anchorMax = Vector2.one;
            controllerRect.offsetMin = Vector2.zero;
            controllerRect.offsetMax = Vector2.zero;
            var controller = controllerGo.AddComponent<LeaderboardSceneController>();

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("buttonSprite").objectReferenceValue = buttonSprite;
            serialized.FindProperty("panelSprite").objectReferenceValue = popupSprite;
            serialized.FindProperty("goldMedalSprite").objectReferenceValue = gold;
            serialized.FindProperty("silverMedalSprite").objectReferenceValue = silver;
            serialized.FindProperty("bronzeMedalSprite").objectReferenceValue = bronze;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Leaderboard Scene Setup: failed to save Leaderboard.unity.");
            }

            RegisterInBuildSettings();

            Debug.Log("Leaderboard Scene Setup: Assets/Scenes/Leaderboard.unity built, wired with the " +
                "CartoonSciFi UI kit and medal art, and registered in Build Settings.");
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"Leaderboard Scene Setup: no sprite found at {path} - " +
                    "that element will fall back to a flat color.");
            }
            return sprite;
        }

        private static void RegisterInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

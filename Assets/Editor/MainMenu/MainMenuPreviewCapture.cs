using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MainMenu.Editor
{
    public static class MainMenuPreviewCapture
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string PreviewPath = "Temp/MainMenuPreview.png";
        [MenuItem("Tools/Main Menu/Capture 1920x1080 Preview")]
        public static void CapturePreview()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(ScenePath);

            try
            {
                Camera camera = null;
                Canvas canvas = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (camera == null) camera = root.GetComponentInChildren<Camera>(true);
                    if (canvas == null) canvas = root.GetComponentInChildren<Canvas>(true);
                }

                if (camera == null || canvas == null)
                {
                    throw new InvalidOperationException("MainMenu preview requires a camera and canvas.");
                }

                RenderMode originalMode = canvas.renderMode;
                Camera originalWorldCamera = canvas.worldCamera;
                float originalPlaneDistance = canvas.planeDistance;
                RenderTexture originalTarget = camera.targetTexture;
                RenderTexture previousActiveTexture = RenderTexture.active;
                var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32)
                {
                    name = "MainMenu Preview Target",
                    antiAliasing = 1
                };
                var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, mipChain: false);

                try
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 5f;
                    camera.targetTexture = target;
                    Canvas.ForceUpdateCanvases();
                    camera.Render();

                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0, recalculateMipMaps: false);
                    texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                    Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath) ?? "Temp");
                    File.WriteAllBytes(PreviewPath, texture.EncodeToPNG());
                }
                finally
                {
                    canvas.renderMode = originalMode;
                    canvas.worldCamera = originalWorldCamera;
                    canvas.planeDistance = originalPlaneDistance;
                    camera.targetTexture = originalTarget;
                    RenderTexture.active = previousActiveTexture;
                    UnityEngine.Object.DestroyImmediate(texture);
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }

            Debug.Log($"Captured main-menu preview: {PreviewPath}");
        }
    }
}

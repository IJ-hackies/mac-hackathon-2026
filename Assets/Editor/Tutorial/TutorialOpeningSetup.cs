using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TutorialEditor
{
    /// One-shot fix for the tutorial scene's dragged-in Planet.prefab instance, plus two capture
    /// commands that frame Tutorial.TutorialOpeningCutscene's opening/overview shots directly
    /// from whatever the Scene view camera is currently looking at. Idempotent: safe to re-run.
    ///
    /// The dragged-in planet's root GameObject is named "Planet Ground" - that's the exact name
    /// Player.PlayerController.TryResolvePlanetCenter() scans the whole scene for (active or
    /// inactive) to decide where radial gravity/ground comes from, so simply having it present
    /// makes the tutorial room's flat ground feel like planet surface. Its "Generated Planet
    /// Vegetation"/"Generated Planet Rocks" children also carry ~17,100 authored
    /// renderers/colliders meant for the one planet scene, not a duplicate copy - see
    /// [world-runtime] in Context/Chunks. Everything else (LandingBase structures, the NAUT
    /// lettering) is comparatively lightweight and stays untouched so the opening shot still has
    /// something to look at.
    public static class TutorialOpeningSetup
    {
        private const string PlanetGroundName = "Planet Ground";
        private const string BackdropName = "Tutorial Planet Backdrop";
        private const string CutsceneName = "TutorialOpeningCutscene";

        [MenuItem("Tools/Tutorial/Strip Dragged-In Planet For Backdrop Use")]
        public static void StripPlanet()
        {
            Transform planetBackdrop = FindOrStripPlanet();
            if (planetBackdrop == null)
            {
                Debug.LogError("TutorialOpeningSetup: no \"" + PlanetGroundName + "\" or \"" + BackdropName +
                    "\" object found in the open scene - drag Planet.prefab in first.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("TutorialOpeningSetup: \"" + BackdropName + "\" is now collider-free and renamed so " +
                "PlayerController stops picking it up as ground, with its 17k-object vegetation/rock " +
                "content removed. Reposition/scale it for framing, then save the scene.");
        }

        [MenuItem("Tools/Tutorial/Capture Opening Shot From Scene View")]
        public static void CaptureOpeningShot()
        {
            CaptureShot("openingShotPosition", "openingShotEulerAngles");
        }

        [MenuItem("Tools/Tutorial/Capture Area Overview From Scene View")]
        public static void CaptureAreaOverview()
        {
            CaptureShot("areaOverviewPosition", "areaOverviewEulerAngles");
        }

        private static void CaptureShot(string positionProperty, string eulerAnglesProperty)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
            {
                Debug.LogError("TutorialOpeningSetup: no active Scene view to capture from - frame the shot " +
                    "in the Scene view first.");
                return;
            }

            Tutorial.TutorialOpeningCutscene cutscene = FindOrCreateCutscene();
            Transform sceneCamera = sceneView.camera.transform;

            var so = new SerializedObject(cutscene);
            so.FindProperty(positionProperty).vector3Value = sceneCamera.position;
            so.FindProperty(eulerAnglesProperty).vector3Value = sceneCamera.eulerAngles;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("TutorialOpeningSetup: captured the current Scene view camera into " +
                positionProperty + "/" + eulerAnglesProperty + ".");
        }

        private static Transform FindOrStripPlanet()
        {
            Transform existingBackdrop = FindInactiveByName(BackdropName);
            if (existingBackdrop != null) return existingBackdrop;

            Transform planetGround = FindInactiveByName(PlanetGroundName);
            if (planetGround == null) return null;

            planetGround.gameObject.name = BackdropName;

            foreach (string childName in new[] { "Generated Planet Vegetation", "Generated Planet Rocks" })
            {
                Transform child = planetGround.Find(childName);
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }

            var instancingRenderer = planetGround.GetComponent<WorldRuntime.SphericalPropInstancingRenderer>();
            if (instancingRenderer != null) Object.DestroyImmediate(instancingRenderer);

            foreach (Collider collider in planetGround.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            return planetGround;
        }

        private static Tutorial.TutorialOpeningCutscene FindOrCreateCutscene()
        {
            var existing = Object.FindFirstObjectByType<Tutorial.TutorialOpeningCutscene>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            var go = new GameObject(CutsceneName);
            return go.AddComponent<Tutorial.TutorialOpeningCutscene>();
        }

        private static Transform FindInactiveByName(string name)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == name);
        }
    }
}

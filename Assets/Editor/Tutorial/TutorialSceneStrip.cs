using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TutorialEditor
{
    /// One-shot cleanup: removes the generated room/gameplay hierarchy from
    /// Assets/Scenes/Tutorial.unity (left over from the deleted auto-builder) while keeping the
    /// space atmosphere and a player reference point, so the scene is ready for hand-built
    /// geometry. Safe to delete this file once the scene has been stripped - it isn't part of
    /// the tutorial's runtime or ongoing authoring workflow.
    public static class TutorialSceneStrip
    {
        private const string ScenePath = "Assets/Scenes/Tutorial.unity";

        // Everything else (the old Structure/Gates/Decorations/Items/Overview/TrainingDummy/
        // TutorialManager/TutorialCanvas/EventSystem hierarchy) gets removed.
        private static readonly string[] KeepRootNames = { "Sun Light", "Planet Ground", "PlayerRig" };

        [MenuItem("Tools/Tutorial/Strip Scene For Manual Build")]
        public static void StripScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int removed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (System.Array.IndexOf(KeepRootNames, root.name) >= 0) continue;
                Object.DestroyImmediate(root);
                removed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"TutorialSceneStrip: removed {removed} generated root object(s) from {ScenePath}. " +
                       $"Kept: {string.Join(", ", KeepRootNames)}.");
        }
    }
}

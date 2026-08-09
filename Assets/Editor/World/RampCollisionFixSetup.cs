using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WorldEditor
{
    /// <summary>
    /// Cleans up every automated collider fix attempted on the LandingBase "Ramp" prop (a
    /// hand-computed BoxCollider, per-part convex MeshColliders, a combined convex MeshCollider,
    /// and a PCA-fitted BoxCollider all turned out wrong in different ways - the last one
    /// produced a degenerate, world-spanning box). Restores the original generated
    /// MeshCollider(s) from Ramp.fbx (bumpy on the ridges, but at least correctly sized/placed)
    /// as a safe baseline, and removes every extra collider object this tool added. From here,
    /// hand-place a BoxCollider in the Editor by eye - see Tools/World/Fix Ramp Collision's
    /// tooltip in the console after running this.
    /// </summary>
    public static class RampCollisionFixSetup
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string RampObjectName = "Ramp";
        private const string ColliderChildName = "RampCollision";

        [MenuItem("Tools/World/Fix Ramp Collision")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("RampCollisionFixSetup: exit Play mode first - scene loading/asset " +
                    "writes aren't allowed while playing.");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            Scene scene = activeScene.path == SampleScenePath
                ? activeScene
                : EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            GameObject ramp = GameObject.Find(RampObjectName);
            if (ramp == null)
            {
                Debug.LogError($"RampCollisionFixSetup: no \"{RampObjectName}\" object found in {SampleScenePath}.");
                return;
            }

            BoxCollider rootBox = ramp.GetComponent<BoxCollider>();
            if (rootBox != null) Object.DestroyImmediate(rootBox);

            Transform staleChild = ramp.transform.Find(ColliderChildName);
            if (staleChild != null) Object.DestroyImmediate(staleChild.gameObject);

            int restoredCount = 0;
            foreach (MeshCollider meshCollider in ramp.GetComponentsInChildren<MeshCollider>(true))
            {
                meshCollider.enabled = true;
                meshCollider.convex = false;
                restoredCount++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"RampCollisionFixSetup: removed the broken auto-generated colliders and restored " +
                $"{restoredCount} original MeshCollider(s) on \"{RampObjectName}\" as a safe baseline. " +
                "Next: select Ramp, Add Component > Box Collider, then drag its face handles in the " +
                "Scene view to fit the ramp by eye.");
        }
    }
}

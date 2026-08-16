using Player;
using UnityEditor;
using UnityEngine;
using Vfx;

namespace PlayerEditor
{
    /// <summary>
    /// One-shot addition of PlayerDustEffects to PlayerRig.prefab. The component builds its own
    /// ParticleSystem/material/texture at runtime (no imported dust asset dependency), so this
    /// tool only needs to attach it once - idempotent, does nothing if already present.
    /// </summary>
    public static class PlayerDustEffectsSetup
    {
        private const string PlayerRigPrefabPath = "Assets/Prefabs/PlayerRig.prefab";

        [MenuItem("Tools/Player Prototype/Add Dust Effects")]
        public static void AddDustEffects()
        {
            GameObject rigRoot = PrefabUtility.LoadPrefabContents(PlayerRigPrefabPath);
            try
            {
                if (rigRoot.GetComponentInChildren<PlayerDustEffects>(true) != null)
                {
                    Debug.Log("PlayerDustEffectsSetup: PlayerDustEffects is already wired - nothing to do.");
                    return;
                }

                PlayerController controller = rigRoot.GetComponentInChildren<PlayerController>(true);
                if (controller == null)
                {
                    Debug.LogError("PlayerDustEffectsSetup: no PlayerController found in the prefab.");
                    return;
                }

                var dustGo = new GameObject("Dust Effects");
                dustGo.transform.SetParent(controller.transform, false);
                PlayerDustEffects dust = dustGo.AddComponent<PlayerDustEffects>();
                SerializedObject so = new SerializedObject(dust);
                so.FindProperty("playerController").objectReferenceValue = controller;
                so.ApplyModifiedPropertiesWithoutUndo();

                if (PrefabUtility.SaveAsPrefabAsset(rigRoot, PlayerRigPrefabPath) == null)
                {
                    Debug.LogError("PlayerDustEffectsSetup: failed to save PlayerRig.prefab.");
                    return;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rigRoot);
            }

            AssetDatabase.ImportAsset(PlayerRigPrefabPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("PlayerDustEffectsSetup: added PlayerDustEffects to PlayerRig.prefab.");
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CharacterEditor
{
    /// Shared clip-lookup helpers for building AnimatorControllers from this project's FBX
    /// packs. Blender exports take names as "<ArmatureName>|<ActionName>" (e.g.
    /// "CharacterArmature|Idle"), and at least one clip has been observed with stray trailing
    /// whitespace/suffixes from the FBX's binary data, so lookups try exact match, then
    /// suffix-after-'|' match, then a trimmed short-name match, all case-insensitive.
    public static class ModelAnimationUtility
    {
        public static List<AnimationClip> LoadSourceClips(GameObject model, out string modelPath)
        {
            modelPath = AssetDatabase.GetAssetPath(model);
            return AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToList();
        }

        public static string ShortClipName(string fullName)
        {
            int pipe = fullName.LastIndexOf('|');
            return pipe >= 0 ? fullName.Substring(pipe + 1) : fullName;
        }

        public static AnimationClip GetClip(List<AnimationClip> sourceClips, string modelPath, string clipName)
        {
            var exact = sourceClips.FirstOrDefault(c =>
                string.Equals(c.name.Trim(), clipName, System.StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            var bySuffix = sourceClips.FirstOrDefault(c =>
                c.name.Trim().EndsWith("|" + clipName, System.StringComparison.OrdinalIgnoreCase));
            if (bySuffix != null) return bySuffix;

            var loose = sourceClips.FirstOrDefault(c =>
                string.Equals(ShortClipName(c.name).Trim(), clipName, System.StringComparison.OrdinalIgnoreCase));
            if (loose != null) return loose;

            Debug.LogWarning($"ModelAnimationUtility: no animation clip matching \"{clipName}\" " +
                              $"found on {modelPath}. Found clips: " +
                              string.Join(", ", sourceClips.Select(c => c.name)));
            return null;
        }

        /// Rebuilds the FBX's explicit clip-split table (ModelImporter.clipAnimations) from the
        /// raw animation takes actually embedded in the source file (importedTakeInfos), forcing
        /// "Loop Time" on the given clip short names. Blender-exported clips import as
        /// non-looping by default.
        ///
        /// Deliberately always rebuilds from importedTakeInfos rather than extending the
        /// existing clipAnimations table: that table is copied verbatim into the .meta and does
        /// NOT get re-derived from the source file on a plain reimport, so if the underlying FBX
        /// is swapped for one with new/renamed takes (as opposed to just re-exported with the
        /// same take names), any clip only present in the new file's raw takes would otherwise
        /// silently never get added - it only ever looked at clips already in the table, not at
        /// what the FBX actually contains. Also drops any stray hand-added entries that don't
        /// correspond to a real take (e.g. a onetime custom sub-clip) - same "always rebuild
        /// clean" philosophy as BuildAnimatorController for the AnimatorController itself.
        public static void ConfigureAnimationLooping(GameObject model, string[] loopingClipShortNames)
        {
            string modelPath = AssetDatabase.GetAssetPath(model);
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) return;

            ModelImporterClipAnimation[] entries = importer.importedTakeInfos
                .Select(take =>
                {
                    bool wantLoop = loopingClipShortNames.Any(name => string.Equals(
                        ShortClipName(take.name).Trim(), name, System.StringComparison.OrdinalIgnoreCase));

                    return new ModelImporterClipAnimation
                    {
                        name = take.name,
                        takeName = take.name,
                        firstFrame = take.startTime * take.sampleRate,
                        lastFrame = take.stopTime * take.sampleRate,
                        loopTime = wantLoop,
                    };
                })
                .ToArray();

            importer.clipAnimations = entries;
            importer.SaveAndReimport();
        }

        /// Finds or creates a physics layer by name in ProjectSettings/TagManager.asset and
        /// returns its index. Shared by PlayerSceneSetup (Player layer) and EnemySceneSetup
        /// (Enemy layer) so both editor scripts agree on the same layer indices without either
        /// one owning layer creation.
        public static int EnsureLayer(string layerName)
        {
            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            var tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    return i;
                }
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layerProp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerProp.stringValue))
                {
                    layerProp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }

            Debug.LogWarning($"ModelAnimationUtility: no free layer slot for \"{layerName}\"; using Default (0).");
            return 0;
        }
    }
}

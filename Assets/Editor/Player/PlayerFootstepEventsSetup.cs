using System.Linq;
using CharacterEditor;
using UnityEditor;
using UnityEngine;

namespace PlayerEditor
{
    /// <summary>
    /// Adds "PlayFootstep" Animation Events to the astronaut's Walk_Gun/Run_Gun clips and the
    /// mech's Walk clip, at each foot's contact point in the loop - two per cycle, at the start
    /// and the midpoint, which is the standard placement for a symmetric walk/run gait. Replaces
    /// PlayerAnimatorRelay's old speed-scaled timer (which approximated footstep cadence and
    /// drifted out of sync with the actual animation) with events fired exactly when the
    /// animation itself plants each foot - see PlayerFootstepAnimationEvents, the receiver these
    /// events call.
    ///
    /// The two event times below (0.0 and 0.5 of the clip) are a starting estimate, not a
    /// measurement of this specific rig's actual contact frames - open Walk_Gun/Run_Gun/Walk in
    /// the Editor's Animation window afterward and drag the event markers to match the feet if
    /// they're off.
    /// </summary>
    public static class PlayerFootstepEventsSetup
    {
        private const string AstronautModelPath = "Assets/Art/Models/Characters/Astronaut_FinnTheFrog.fbx";
        private const string MechModelPath = "Assets/Art/Models/Characters/Mech_FinnTheFrog.fbx";
        private const string FunctionName = "PlayFootstep";
        private static readonly float[] NormalizedContactTimes = { 0f, 0.5f };

        [MenuItem("Tools/Player Prototype/Add Footstep Animation Events")]
        public static void Run()
        {
            int astronautCount = AddFootstepEvents(AstronautModelPath, "Walk_Gun", "Run_Gun");
            int mechCount = AddFootstepEvents(MechModelPath, "Walk");

            Debug.Log($"PlayerFootstepEventsSetup: added footstep events to {astronautCount} astronaut " +
                $"clip(s) and {mechCount} mech clip(s).");
        }

        private static int AddFootstepEvents(string modelPath, params string[] clipShortNames)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError($"PlayerFootstepEventsSetup: no model found at {modelPath}.");
                return 0;
            }

            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"PlayerFootstepEventsSetup: no ModelImporter for {modelPath}.");
                return 0;
            }

            var sourceClips = ModelAnimationUtility.LoadSourceClips(model, out string clipModelPath);
            ModelImporterClipAnimation[] entries = importer.clipAnimations;
            bool changed = false;
            int updatedCount = 0;

            foreach (string shortName in clipShortNames)
            {
                AnimationClip clip = ModelAnimationUtility.GetClip(sourceClips, clipModelPath, shortName);
                if (clip == null) continue;

                int entryIndex = System.Array.FindIndex(entries, e =>
                    string.Equals(ModelAnimationUtility.ShortClipName(e.name).Trim(), shortName,
                        System.StringComparison.OrdinalIgnoreCase));
                if (entryIndex < 0)
                {
                    Debug.LogWarning($"PlayerFootstepEventsSetup: no clipAnimations entry matching " +
                        $"\"{shortName}\" on {modelPath} - run the animator controller setup first.");
                    continue;
                }

                ModelImporterClipAnimation entry = entries[entryIndex];
                entry.events = NormalizedContactTimes
                    .Select(t => new AnimationEvent
                    {
                        time = t * clip.length,
                        functionName = FunctionName,
                    })
                    .ToArray();
                entries[entryIndex] = entry;
                changed = true;
                updatedCount++;
            }

            if (changed)
            {
                importer.clipAnimations = entries;
                importer.SaveAndReimport();
            }

            return updatedCount;
        }
    }
}

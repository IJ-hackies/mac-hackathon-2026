using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LeaderboardEditor
{
    /// <summary>
    /// Copies every RectTransform's layout (anchored position, size, anchors, pivot, rotation,
    /// scale) from "Score Panel" onto "Wave Panel" in the currently open Leaderboard scene, matched
    /// node-by-node by hierarchy path. Both panels are built with identical structure (see
    /// LeaderboardSceneSetup.BuildTabPanel), so this lets hand-tuned positions on one tab be
    /// mirrored onto the other without re-doing the work by hand. Idempotent/safe to re-run;
    /// mismatched structure (extra/missing/renamed children) is reported and skipped rather than
    /// guessed at.
    /// </summary>
    public static class LeaderboardTabLayoutMirror
    {
        [MenuItem("Tools/Leaderboard/Copy Score Tab Layout To Wave Tab")]
        public static void CopyScoreLayoutToWaveTab()
        {
            Transform scorePanel = FindInactive("Score Panel");
            Transform wavePanel = FindInactive("Wave Panel");
            if (scorePanel == null || wavePanel == null)
            {
                Debug.LogError("LeaderboardTabLayoutMirror: could not find both 'Score Panel' and 'Wave Panel' in the open scene.");
                return;
            }

            int copied = 0;
            int skipped = 0;
            CopyRecursive(scorePanel, wavePanel, "Wave Panel", ref copied, ref skipped);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"LeaderboardTabLayoutMirror: copied layout for {copied} node(s)" +
                (skipped > 0 ? $", skipped {skipped} unmatched node(s) - see warnings above." : "."));
        }

        private static void CopyRecursive(Transform score, Transform wave, string wavePath, ref int copied, ref int skipped)
        {
            CopyRectTransform(score, wave);
            copied++;

            int scoreChildCount = score.childCount;
            int waveChildCount = wave.childCount;
            if (scoreChildCount != waveChildCount)
            {
                Debug.LogWarning($"LeaderboardTabLayoutMirror: '{wavePath}' has {waveChildCount} children but its Score Tab " +
                    $"counterpart has {scoreChildCount} - skipping this branch's children.");
                skipped += scoreChildCount;
                return;
            }

            for (int i = 0; i < scoreChildCount; i++)
            {
                Transform scoreChild = score.GetChild(i);
                Transform waveChild = wave.GetChild(i);
                if (scoreChild.name != waveChild.name)
                {
                    Debug.LogWarning($"LeaderboardTabLayoutMirror: child {i} of '{wavePath}' is '{waveChild.name}' but the Score " +
                        $"Tab counterpart is '{scoreChild.name}' - skipping this node.");
                    skipped++;
                    continue;
                }

                CopyRecursive(scoreChild, waveChild, wavePath + "/" + waveChild.name, ref copied, ref skipped);
            }
        }

        // GameObject.Find only matches active GameObjects, and "Wave Panel" starts disabled (only
        // the active tab is shown) - walk every root's full hierarchy, inactive nodes included.
        private static Transform FindInactive(string name)
        {
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = FindInactiveRecursive(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindInactiveRecursive(Transform current, string name)
        {
            if (current.name == name) return current;
            for (int i = 0; i < current.childCount; i++)
            {
                Transform found = FindInactiveRecursive(current.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void CopyRectTransform(Transform score, Transform wave)
        {
            var scoreRect = score as RectTransform;
            var waveRect = wave as RectTransform;
            if (scoreRect == null || waveRect == null) return;

            Undo.RecordObject(waveRect, "Mirror Leaderboard Tab Layout");
            waveRect.anchorMin = scoreRect.anchorMin;
            waveRect.anchorMax = scoreRect.anchorMax;
            waveRect.pivot = scoreRect.pivot;
            waveRect.sizeDelta = scoreRect.sizeDelta;
            waveRect.anchoredPosition = scoreRect.anchoredPosition;
            waveRect.localRotation = scoreRect.localRotation;
            waveRect.localScale = scoreRect.localScale;
            EditorUtility.SetDirty(waveRect);
        }
    }
}

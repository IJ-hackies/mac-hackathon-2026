using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player.Editor
{
    /// <summary>
    /// Keeps every action map PC-only while preserving the generated C# wrapper workflow.
    /// </summary>
    public static class PcInputActionsSetup
    {
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string SessionKey = "Player.PcInputActionsSetup.V3";

        [InitializeOnLoadMethod]
        private static void ConfigureAfterScriptReload()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && NeedsConfiguration())
                {
                    ConfigurePcOnlyBindings();
                }
            };
        }

        [MenuItem("Tools/Player Prototype/Configure PC-Only Input")]
        public static void ConfigurePcOnlyBindings()
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"Missing input-actions asset: {InputActionsPath}");
            }

            foreach (InputActionMap map in asset.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    for (int i = action.bindings.Count - 1; i >= 0; i--)
                    {
                        InputBinding binding = action.bindings[i];
                        if (!binding.isComposite && !IsPcPath(binding.path))
                        {
                            action.ChangeBinding(i).Erase();
                        }
                    }
                }
            }

            for (int i = asset.controlSchemes.Count - 1; i >= 0; i--)
            {
                string schemeName = asset.controlSchemes[i].name;
                if (!string.Equals(schemeName, "Keyboard&Mouse", StringComparison.Ordinal))
                {
                    asset.RemoveControlScheme(schemeName);
                }
            }

            File.WriteAllText(InputActionsPath, asset.ToJson());
            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("Configured all input action maps for keyboard and mouse only.");
        }

        private static bool NeedsConfiguration()
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (asset == null)
            {
                return false;
            }

            foreach (InputActionMap map in asset.actionMaps)
            {
                foreach (InputBinding binding in map.bindings)
                {
                    if (!binding.isComposite && !IsPcPath(binding.path))
                    {
                        return true;
                    }
                }
            }

            return asset.controlSchemes.Count != 1 ||
                   !string.Equals(asset.controlSchemes[0].name, "Keyboard&Mouse", StringComparison.Ordinal);
        }

        private static bool IsPcPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            return path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("<Mouse>/", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("<Pointer>/", StringComparison.OrdinalIgnoreCase);
        }
    }
}

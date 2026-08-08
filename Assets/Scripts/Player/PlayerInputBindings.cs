using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    /// <summary>
    /// Creates the independent input-action copies used by the player components and keeps
    /// their keyboard/mouse binding overrides synchronized. Overrides are persisted through
    /// PlayerPrefs so the Main Menu and in-game settings console share one control map.
    /// </summary>
    public static class PlayerInputBindings
    {
        public sealed class BindingDefinition
        {
            public BindingDefinition(string actionName, string displayName, string bindingId)
            {
                ActionName = actionName;
                DisplayName = displayName;
                BindingId = new Guid(bindingId);
            }

            public string ActionName { get; }
            public string DisplayName { get; }
            public Guid BindingId { get; }
        }

        private const string OverridesPreference = "settings.playerBindingOverrides.v1";

        private static readonly BindingDefinition[] BindingDefinitions =
        {
            new BindingDefinition("Jump", "JUMP", "eb40bb66-4559-4dfa-9a2f-820438abb426"),
            new BindingDefinition("Ability", "DASH / SHIELD", "f2e9ba44-c423-42a7-ad56-f20975884794"),
            new BindingDefinition("Attack", "PRIMARY FIRE", "05f6913d-c316-48b2-a6bb-e225f14c7960"),
            new BindingDefinition("Attack", "PRIMARY FIRE ALT", "b3c1c7f0-bd20-4ee7-a0f1-899b24bca6d7"),
            new BindingDefinition("Attack2", "SECONDARY FIRE", "b2c3d4e5-6f78-4890-abcd-ef0123456789"),
            new BindingDefinition("Melee", "MELEE", "7e1d4f2a-3b5c-4d6e-9f8a-0b1c2d3e4f51"),
            new BindingDefinition("Reload", "RELOAD", "4c0f8e6d-2b3f-5e7a-1b0c-2d3e4f506173"),
            new BindingDefinition("EmoteWheel", "EMOTE WHEEL", "9a3f6b4c-5d7e-6f80-1b0c-2d3e4f506173"),
            new BindingDefinition("Interact", "INTERACT", "1c04ea5f-b012-41d1-a6f7-02e963b52893"),
            new BindingDefinition("Crouch", "CROUCH", "36e52cba-0905-478e-a818-f4bfcb9f3b9a"),
            new BindingDefinition("Previous", "PREVIOUS", "1534dc16-a6aa-499d-9c3a-22b47347b52a"),
            new BindingDefinition("Next", "NEXT", "cbac6039-9c09-46a1-b5f2-4e5124ccb5ed")
        };

        private static readonly HashSet<InputSystem_Actions> LiveActionSets = new HashSet<InputSystem_Actions>();

        public static IReadOnlyList<BindingDefinition> RebindableControls => BindingDefinitions;

        public static event Action BindingsChanged;

        public static InputSystem_Actions CreateActions()
        {
            var actions = new InputSystem_Actions();
            ApplySavedOverrides(actions.asset);
            LiveActionSets.Add(actions);
            return actions;
        }

        public static void ReleaseActions(InputSystem_Actions actions)
        {
            if (actions == null)
            {
                return;
            }

            LiveActionSets.Remove(actions);
            actions.Dispose();
        }

        public static InputAction GetAction(InputSystem_Actions actions, BindingDefinition definition)
        {
            return actions?.asset.FindAction($"Player/{definition.ActionName}", false);
        }

        public static int GetBindingIndex(InputAction action, BindingDefinition definition)
        {
            if (action == null)
            {
                return -1;
            }

            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].id == definition.BindingId)
                {
                    return i;
                }
            }

            return -1;
        }

        public static string GetBindingDisplayName(InputSystem_Actions actions, BindingDefinition definition)
        {
            InputAction action = GetAction(actions, definition);
            int bindingIndex = GetBindingIndex(action, definition);
            return bindingIndex >= 0
                ? action.GetBindingDisplayString(bindingIndex).ToUpperInvariant()
                : "MISSING";
        }

        public static bool IsSupportedPcButton(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                string.Equals(path, "<Keyboard>/escape", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("<Mouse>/", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryFindConflict(
            InputSystem_Actions actions,
            Guid bindingId,
            string candidatePath,
            out string conflictName)
        {
            conflictName = null;
            InputActionMap playerMap = actions?.asset.FindActionMap("Player", false);
            if (playerMap == null || string.IsNullOrWhiteSpace(candidatePath))
            {
                return false;
            }

            foreach (InputBinding binding in playerMap.bindings)
            {
                if (binding.id == bindingId ||
                    string.IsNullOrWhiteSpace(binding.effectivePath) ||
                    !string.Equals(binding.effectivePath, candidatePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                BindingDefinition known = FindDefinition(binding.id);
                conflictName = known != null
                    ? known.DisplayName
                    : NicifyActionName(binding.action);
                return true;
            }

            return false;
        }

        public static void SaveAndApply(InputSystem_Actions source)
        {
            if (source == null)
            {
                return;
            }

            string json = source.asset.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(OverridesPreference, json);
            PlayerPrefs.Save();
            ApplyOverrideJsonToLiveSets(json, source);
            BindingsChanged?.Invoke();
        }

        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(OverridesPreference);
            PlayerPrefs.Save();

            foreach (InputSystem_Actions actionSet in SnapshotLiveSets())
            {
                WithMapsTemporarilyDisabled(actionSet.asset, RemoveConfigurableOverrides);
            }

            BindingsChanged?.Invoke();
        }

        private static void ApplySavedOverrides(InputActionAsset asset)
        {
            string json = PlayerPrefs.GetString(OverridesPreference, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                asset.LoadBindingOverridesFromJson(json, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Saved control bindings were invalid and have been reset: {exception.Message}");
                PlayerPrefs.DeleteKey(OverridesPreference);
                PlayerPrefs.Save();
                asset.RemoveAllBindingOverrides();
            }
        }

        private static void ApplyOverrideJsonToLiveSets(string json, InputSystem_Actions source)
        {
            foreach (InputSystem_Actions actionSet in SnapshotLiveSets())
            {
                // The source already contains the completed override. Re-loading its asset while
                // its interactive rebinding operation is completing can invalidate that operation.
                if (ReferenceEquals(actionSet, source))
                {
                    continue;
                }

                WithMapsTemporarilyDisabled(
                    actionSet.asset,
                    asset => asset.LoadBindingOverridesFromJson(json, true));
            }
        }

        private static void RemoveConfigurableOverrides(InputActionAsset asset)
        {
            foreach (BindingDefinition definition in BindingDefinitions)
            {
                InputAction action = asset.FindAction($"Player/{definition.ActionName}", false);
                int bindingIndex = GetBindingIndex(action, definition);
                if (bindingIndex >= 0)
                {
                    action.RemoveBindingOverride(bindingIndex);
                }
            }
        }

        private static List<InputSystem_Actions> SnapshotLiveSets()
        {
            LiveActionSets.RemoveWhere(actions => actions == null);
            return new List<InputSystem_Actions>(LiveActionSets);
        }

        private static void WithMapsTemporarilyDisabled(InputActionAsset asset, Action<InputActionAsset> operation)
        {
            var enabledMaps = new List<InputActionMap>();
            foreach (InputActionMap map in asset.actionMaps)
            {
                if (!map.enabled)
                {
                    continue;
                }

                enabledMaps.Add(map);
                map.Disable();
            }

            try
            {
                operation(asset);
            }
            finally
            {
                foreach (InputActionMap map in enabledMaps)
                {
                    map.Enable();
                }
            }
        }

        private static BindingDefinition FindDefinition(Guid bindingId)
        {
            foreach (BindingDefinition definition in BindingDefinitions)
            {
                if (definition.BindingId == bindingId)
                {
                    return definition;
                }
            }

            return null;
        }

        private static string NicifyActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return "ANOTHER CONTROL";
            }

            return actionName.Replace("2", " 2").ToUpperInvariant();
        }
    }
}

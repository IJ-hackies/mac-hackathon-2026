using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player.Tests
{
    public sealed class PlayerInputBindingsTests
    {
        private const string OverridesPreference = "settings.playerBindingOverrides.v1";
        private static readonly Guid JumpBindingId =
            new Guid("eb40bb66-4559-4dfa-9a2f-820438abb426");

        private Type _serviceType;
        private MethodInfo _createActions;
        private MethodInfo _releaseActions;
        private MethodInfo _saveAndApply;
        private MethodInfo _resetToDefaults;
        private readonly List<object> _actionSets = new List<object>();
        private bool _hadSavedOverrides;
        private string _savedOverrides;

        [SetUp]
        public void SetUp()
        {
            _hadSavedOverrides = PlayerPrefs.HasKey(OverridesPreference);
            _savedOverrides = PlayerPrefs.GetString(OverridesPreference, string.Empty);
            PlayerPrefs.DeleteKey(OverridesPreference);

            _serviceType = Type.GetType("Player.PlayerInputBindings, Assembly-CSharp");
            Assert.That(_serviceType, Is.Not.Null);
            _createActions = RequireMethod("CreateActions");
            _releaseActions = RequireMethod("ReleaseActions");
            _saveAndApply = RequireMethod("SaveAndApply");
            _resetToDefaults = RequireMethod("ResetToDefaults");
        }

        [TearDown]
        public void TearDown()
        {
            _resetToDefaults?.Invoke(null, null);
            foreach (object actionSet in _actionSets)
            {
                _releaseActions?.Invoke(null, new[] { actionSet });
            }
            _actionSets.Clear();

            if (_hadSavedOverrides)
            {
                PlayerPrefs.SetString(OverridesPreference, _savedOverrides);
            }
            else
            {
                PlayerPrefs.DeleteKey(OverridesPreference);
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void RebindableControls_ContainsTwelvePcRowsAndNoFixedActions()
        {
            PropertyInfo property = _serviceType.GetProperty("RebindableControls");
            Assert.That(property, Is.Not.Null);

            var actionNames = new List<string>();
            foreach (object definition in (IEnumerable)property.GetValue(null))
            {
                actionNames.Add((string)definition.GetType().GetProperty("ActionName")?.GetValue(definition));
            }

            Assert.That(actionNames, Has.Count.EqualTo(12));
            Assert.That(actionNames, Does.Contain("Interact"));
            Assert.That(actionNames, Does.Contain("Crouch"));
            Assert.That(actionNames, Does.Contain("Previous"));
            Assert.That(actionNames, Does.Contain("Next"));
            Assert.That(actionNames, Does.Not.Contain("Move"));
            Assert.That(actionNames, Does.Not.Contain("Look"));
        }

        [Test]
        public void SavedOverride_UpdatesLiveCopiesAndNewCopies_ResetRestoresDefault()
        {
            object source = CreateActionSet();
            object liveCopy = CreateActionSet();
            InputAction sourceJump = GetAsset(source).FindAction("Player/Jump", true);
            int bindingIndex = FindBindingIndex(sourceJump, JumpBindingId);
            Assert.That(bindingIndex, Is.GreaterThanOrEqualTo(0));

            sourceJump.ApplyBindingOverride(bindingIndex, "<Keyboard>/j");
            _saveAndApply.Invoke(null, new[] { source });

            Assert.That(GetJumpPath(liveCopy), Is.EqualTo("<Keyboard>/j"));
            object newCopy = CreateActionSet();
            Assert.That(GetJumpPath(newCopy), Is.EqualTo("<Keyboard>/j"));

            _resetToDefaults.Invoke(null, null);
            Assert.That(GetJumpPath(source), Is.EqualTo("<Keyboard>/space"));
            Assert.That(GetJumpPath(liveCopy), Is.EqualTo("<Keyboard>/space"));
            Assert.That(GetJumpPath(newCopy), Is.EqualTo("<Keyboard>/space"));
            Assert.That(PlayerPrefs.HasKey(OverridesPreference), Is.False);
        }

        [Test]
        public void EscapeIsReservedButKeyboardAndMouseButtonsAreSupported()
        {
            MethodInfo supported = RequireMethod("IsSupportedPcButton");
            Assert.That(supported.Invoke(null, new object[] { "<Keyboard>/escape" }), Is.False);
            Assert.That(supported.Invoke(null, new object[] { "<Keyboard>/f" }), Is.True);
            Assert.That(supported.Invoke(null, new object[] { "<Mouse>/leftButton" }), Is.True);
            Assert.That(supported.Invoke(null, new object[] { "<Gamepad>/buttonSouth" }), Is.False);
        }

        [Test]
        public void GeneratedActions_AllMapsAndControlSchemesArePcOnly()
        {
            InputActionAsset asset = GetAsset(CreateActionSet());
            Assert.That(asset.controlSchemes, Has.Count.EqualTo(1));
            Assert.That(asset.controlSchemes[0].name, Is.EqualTo("Keyboard&Mouse"));

            foreach (InputActionMap map in asset.actionMaps)
            {
                foreach (InputBinding binding in map.bindings)
                {
                    if (binding.isComposite || string.IsNullOrEmpty(binding.path))
                    {
                        continue;
                    }

                    bool isPcPath =
                        binding.path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) ||
                        binding.path.StartsWith("<Mouse>/", StringComparison.OrdinalIgnoreCase) ||
                        binding.path.StartsWith("<Pointer>/", StringComparison.OrdinalIgnoreCase);
                    Assert.That(
                        isPcPath,
                        Is.True,
                        $"{map.name}/{binding.action} still uses non-PC path {binding.path}.");
                }
            }
        }

        private object CreateActionSet()
        {
            object actionSet = _createActions.Invoke(null, null);
            _actionSets.Add(actionSet);
            return actionSet;
        }

        private static InputActionAsset GetAsset(object actionSet)
        {
            return (InputActionAsset)actionSet.GetType().GetProperty("asset")?.GetValue(actionSet);
        }

        private static string GetJumpPath(object actionSet)
        {
            InputAction jump = GetAsset(actionSet).FindAction("Player/Jump", true);
            int bindingIndex = FindBindingIndex(jump, JumpBindingId);
            return jump.bindings[bindingIndex].effectivePath;
        }

        private static int FindBindingIndex(InputAction action, Guid bindingId)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].id == bindingId)
                {
                    return i;
                }
            }
            return -1;
        }

        private MethodInfo RequireMethod(string name)
        {
            MethodInfo method = _serviceType.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, name);
            return method;
        }
    }
}

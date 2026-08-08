using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Player.UI
{
    /// <summary>
    /// Replaces Unity's cross-platform default UI actions with this project's PC-only UI map.
    /// </summary>
    internal sealed class PcUiInputBinding : IDisposable
    {
        private readonly InputSystem_Actions _actions;
        private readonly List<InputActionReference> _references = new List<InputActionReference>();

        public PcUiInputBinding(InputSystemUIInputModule module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            _actions = global::Player.PlayerInputBindings.CreateActions();
            bool wasEnabled = module.enabled;
            module.enabled = false;
            module.actionsAsset = _actions.asset;
            module.move = CreateReference(_actions.UI.Navigate);
            module.submit = CreateReference(_actions.UI.Submit);
            module.cancel = CreateReference(_actions.UI.Cancel);
            module.point = CreateReference(_actions.UI.Point);
            module.leftClick = CreateReference(_actions.UI.Click);
            module.rightClick = CreateReference(_actions.UI.RightClick);
            module.middleClick = CreateReference(_actions.UI.MiddleClick);
            module.scrollWheel = CreateReference(_actions.UI.ScrollWheel);
            module.trackedDevicePosition = CreateReference(_actions.UI.TrackedDevicePosition);
            module.trackedDeviceOrientation = CreateReference(_actions.UI.TrackedDeviceOrientation);
            module.enabled = wasEnabled;
            _actions.UI.Enable();
        }

        public void Dispose()
        {
            _actions.UI.Disable();
            global::Player.PlayerInputBindings.ReleaseActions(_actions);

            foreach (InputActionReference reference in _references)
            {
                if (reference != null)
                {
                    UnityEngine.Object.Destroy(reference);
                }
            }

            _references.Clear();
        }

        private InputActionReference CreateReference(InputAction action)
        {
            InputActionReference reference = InputActionReference.Create(action);
            _references.Add(reference);
            return reference;
        }
    }
}

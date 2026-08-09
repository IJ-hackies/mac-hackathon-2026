using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Player.UI
{
    [DisallowMultipleComponent]
    public sealed class ControlsRebindingUI : MonoBehaviour
    {
        [SerializeField] private Transform rowsRoot;
        [SerializeField] private Button resetButton;
        [SerializeField] private Text statusText;

        private readonly Dictionary<Guid, Row> _rows = new Dictionary<Guid, Row>();
        private InputSystem_Actions _actions;
        private InputActionRebindingExtensions.RebindingOperation _operation;
        private string _previousOverridePath;
        private Button _activeButton;
        private int _escapeConsumedFrame = -1;
        private bool _shuttingDown;

        private sealed class Row
        {
            public PlayerInputBindings.BindingDefinition Definition;
            public Button Button;
            public Text BindingText;
            public UnityEngine.Events.UnityAction Listener;
        }

        public bool IsRebinding => _operation != null;
        public bool BlocksMenuEscape => IsRebinding || _escapeConsumedFrame == Time.frameCount;
        public GameObject FirstSelectable { get; private set; }

        private void OnEnable()
        {
            _shuttingDown = false;
            _actions = PlayerInputBindings.CreateActions();
            ResolveRows();
            RegisterListeners();
            PlayerInputBindings.BindingsChanged += RefreshBindings;
            RefreshBindings();
            SetStatus("SELECT A CONTROL TO REBIND");
        }

        private void OnDisable()
        {
            _shuttingDown = true;
            if (_operation != null)
            {
                _operation.Cancel();
                DisposeOperation();
            }

            PlayerInputBindings.BindingsChanged -= RefreshBindings;
            UnregisterListeners();
            PlayerInputBindings.ReleaseActions(_actions);
            _actions = null;
            _rows.Clear();
            FirstSelectable = null;
        }

        public void RefreshBindings()
        {
            if (_actions == null)
            {
                return;
            }

            foreach (Row row in _rows.Values)
            {
                if (row.BindingText != null)
                {
                    row.BindingText.text = PlayerInputBindings.GetBindingDisplayName(_actions, row.Definition);
                }
            }
        }

        private void ResolveRows()
        {
            _rows.Clear();
            FirstSelectable = null;
            if (rowsRoot == null)
            {
                Debug.LogError("ControlsRebindingUI is missing its rows root.", this);
                return;
            }

            foreach (PlayerInputBindings.BindingDefinition definition in PlayerInputBindings.RebindableControls)
            {
                Transform rowTransform = rowsRoot.Find(definition.BindingId.ToString("N"));
                Button button = rowTransform != null
                    ? rowTransform.GetComponentInChildren<Button>(true)
                    : null;
                Text bindingText = button != null
                    ? button.GetComponentInChildren<Text>(true)
                    : null;

                if (button == null || bindingText == null)
                {
                    Debug.LogError($"ControlsRebindingUI could not resolve row '{definition.DisplayName}'.", this);
                    continue;
                }

                var row = new Row
                {
                    Definition = definition,
                    Button = button,
                    BindingText = bindingText
                };
                _rows.Add(definition.BindingId, row);
                if (FirstSelectable == null)
                {
                    FirstSelectable = button.gameObject;
                }
            }
        }

        private void RegisterListeners()
        {
            foreach (Row row in _rows.Values)
            {
                Row capturedRow = row;
                capturedRow.Listener = () => BeginRebind(capturedRow);
                capturedRow.Button.onClick.AddListener(capturedRow.Listener);
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(ResetToDefaults);
            }
        }

        private void UnregisterListeners()
        {
            foreach (Row row in _rows.Values)
            {
                if (row.Button != null && row.Listener != null)
                {
                    row.Button.onClick.RemoveListener(row.Listener);
                }
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(ResetToDefaults);
            }
        }

        private void BeginRebind(Row row)
        {
            if (_operation != null || _actions == null)
            {
                return;
            }

            InputAction action = PlayerInputBindings.GetAction(_actions, row.Definition);
            int bindingIndex = PlayerInputBindings.GetBindingIndex(action, row.Definition);
            if (action == null || bindingIndex < 0)
            {
                SetStatus($"{row.Definition.DisplayName} BINDING IS MISSING");
                return;
            }

            _previousOverridePath = action.bindings[bindingIndex].overridePath;
            _activeButton = row.Button;
            SetButtonsInteractable(false);
            row.BindingText.text = "PRESS INPUT";
            SetStatus("PRESS A KEY OR MOUSE BUTTON  //  ESC CANCELS");
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            _operation = action.PerformInteractiveRebinding(bindingIndex)
                .WithExpectedControlType<ButtonControl>()
                .WithControlsExcluding("<Gamepad>")
                .WithControlsExcluding("<Joystick>")
                .WithControlsExcluding("<Touchscreen>")
                .WithControlsExcluding("<Pen>")
                .WithControlsExcluding("<XRController>")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnCancel(_ => FinishCancelled())
                .OnComplete(_ => FinishCompleted(row, action, bindingIndex));
            _operation.Start();
        }

        private void FinishCompleted(Row row, InputAction action, int bindingIndex)
        {
            string candidatePath = action.bindings[bindingIndex].effectivePath;
            if (!PlayerInputBindings.IsSupportedPcButton(candidatePath))
            {
                RestorePreviousOverride(action, bindingIndex);
                Audio.AudioManager.Instance.PlaySfx(Audio.SfxId.UiError);
                Finish($"{row.Definition.DisplayName}: KEYBOARD OR MOUSE BUTTONS ONLY");
                return;
            }

            if (PlayerInputBindings.TryFindConflict(
                    _actions,
                    row.Definition.BindingId,
                    candidatePath,
                    out string conflictName))
            {
                RestorePreviousOverride(action, bindingIndex);
                Audio.AudioManager.Instance.PlaySfx(Audio.SfxId.UiError);
                Finish($"INPUT ALREADY USED BY {conflictName}");
                return;
            }

            PlayerInputBindings.SaveAndApply(_actions);
            Audio.AudioManager.Instance.PlaySfx(Audio.SfxId.UiToggle);
            Finish($"{row.Definition.DisplayName} UPDATED");
        }

        private void FinishCancelled()
        {
            _escapeConsumedFrame = Time.frameCount;
            Audio.AudioManager.Instance.PlaySfx(Audio.SfxId.UiClose);
            Finish("REBIND CANCELLED");
        }

        private void Finish(string status)
        {
            DisposeOperation();
            SetButtonsInteractable(true);
            RefreshBindings();
            SetStatus(status);

            if (!_shuttingDown && EventSystem.current != null && _activeButton != null)
            {
                EventSystem.current.SetSelectedGameObject(_activeButton.gameObject);
            }

            _activeButton = null;
            _previousOverridePath = null;
        }

        private void RestorePreviousOverride(InputAction action, int bindingIndex)
        {
            if (string.IsNullOrEmpty(_previousOverridePath))
            {
                action.RemoveBindingOverride(bindingIndex);
            }
            else
            {
                action.ApplyBindingOverride(bindingIndex, _previousOverridePath);
            }
        }

        private void ResetToDefaults()
        {
            if (_operation != null)
            {
                return;
            }

            PlayerInputBindings.ResetToDefaults();
            RefreshBindings();
            SetStatus("DEFAULT CONTROLS RESTORED");
        }

        private void SetButtonsInteractable(bool interactable)
        {
            foreach (Row row in _rows.Values)
            {
                row.Button.interactable = interactable;
            }

            if (resetButton != null)
            {
                resetButton.interactable = interactable;
            }
        }

        private void SetStatus(string value)
        {
            if (statusText != null)
            {
                statusText.text = value;
            }
        }

        private void DisposeOperation()
        {
            if (_operation == null)
            {
                return;
            }

            _operation.Dispose();
            _operation = null;
        }
    }
}

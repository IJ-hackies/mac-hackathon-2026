using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Waves
{
    /// <summary>
    /// Intermission affordance. Input and protected-area checks remain director-owned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IntermissionPromptView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text messageText;
        [SerializeField] private Text holdText;
        [SerializeField] private Image holdFill;
        [SerializeField] private string holdMessage = "HOLD F TO START NEXT WAVE";
        [SerializeField] private string leaveProtectedMessage = "LEAVE PROTECTED AREA TO START NEXT WAVE";

        private string _bindingLabel = "F";
        private bool _startAllowed;
        private bool _isHolding;
        private float _holdProgress;

        public void Configure(CanvasGroup root, Text message, Text hold, Image fill)
        {
            canvasGroup = root;
            messageText = message;
            holdText = hold;
            holdFill = fill;
            SetHoldProgress(0f, false);
        }

        public void SetIntermissionVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }

        /// <summary>True permits the director to expose the F-hold affordance.</summary>
        public void SetStartAllowed(bool allowed)
        {
            _startAllowed = allowed;
            if (messageText != null)
            {
                messageText.text = allowed ? holdMessage : leaveProtectedMessage;
            }

            if (holdText != null) holdText.gameObject.SetActive(allowed);
            if (holdFill != null) holdFill.gameObject.SetActive(allowed);
            if (!allowed) SetHoldProgress(0f, false);
        }

        /// <summary>
        /// Sets the currently rebound start-wave binding. Empty labels safely fall back to F.
        /// The wave director owns resolving the actual Input System display string.
        /// </summary>
        public void SetBindingLabel(string bindingLabel)
        {
            _bindingLabel = string.IsNullOrWhiteSpace(bindingLabel) ? "F" : bindingLabel.Trim().ToUpperInvariant();
            holdMessage = "HOLD " + _bindingLabel + " TO START NEXT WAVE";
            if (_startAllowed && messageText != null) messageText.text = holdMessage;
            SetHoldProgress(_holdProgress, _isHolding);
        }

        public void SetLeaveProtectedAreaMessage()
        {
            SetStartAllowed(false);
        }

        public void SetHoldProgress(float normalizedProgress, bool holding)
        {
            float progress = Mathf.Clamp01(normalizedProgress);
            _holdProgress = progress;
            _isHolding = holding;
            if (holdFill != null) holdFill.fillAmount = progress;
            if (holdText != null)
            {
                holdText.text = holding
                    ? $"{_bindingLabel}  {Mathf.RoundToInt(progress * 100f)}%"
                    : $"HOLD {_bindingLabel}  1.0s";
            }
        }
    }
}

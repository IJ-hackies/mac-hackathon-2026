using System;
using Services.Leaderboards;
using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Waves
{
    /// <summary>
    /// Username-entry popup shown from the game-over summary's "Add To Leaderboard" button.
    /// Every visual piece (panel image, InputField, buttons) is built in the Editor from the same
    /// Cartoon UI sprites as the rest of the menu chrome - this component only drives behavior.
    /// A player only ever submits a username once: on a successful submit it is saved via
    /// CloudUsername against this device's persistent anonymous identity, the popup closes
    /// immediately, and future runs auto-submit under that locked name without prompting again
    /// (see WaveGameController.OnRunEnded).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LeaderboardSubmitPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private InputField usernameField;
        [SerializeField] private Text statusText;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button cancelButton;

        public event Action Submitted;

        private WaveRunSummary _pendingSummary;
        private bool _isSubmitting;

        private void Awake()
        {
            if (usernameField != null)
            {
                usernameField.characterLimit = UsernamePolicy.MaxLength;
                usernameField.onValidateInput += HandleValidateInput;
            }
        }

        private void OnEnable()
        {
            if (submitButton != null) submitButton.onClick.AddListener(HandleSubmit);
            if (cancelButton != null) cancelButton.onClick.AddListener(Hide);
        }

        private void OnDisable()
        {
            if (submitButton != null) submitButton.onClick.RemoveListener(HandleSubmit);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(Hide);
        }

        public void Show(WaveRunSummary summary)
        {
            _pendingSummary = summary;
            SetStatus(string.Empty);
            SetVisible(true);
        }

        public void Hide() => SetVisible(false);

        private char HandleValidateInput(string text, int charIndex, char addedChar) =>
            UsernamePolicy.IsAllowedCharacter(addedChar) ? addedChar : '\0';

        private async void HandleSubmit()
        {
            if (_isSubmitting) return;
            string candidate = usernameField != null ? usernameField.text : string.Empty;

            if (!UsernamePolicy.Validate(candidate, out string reason))
            {
                SetStatus(reason);
                return;
            }

            _isSubmitting = true;
            SetInteractable(false);
            SetStatus("Submitting...");

            try
            {
                await LeaderboardsClient.SubmitAsync(LeaderboardIds.HighestScore, _pendingSummary.Score, candidate);
                await LeaderboardsClient.SubmitAsync(LeaderboardIds.FurthestWave, _pendingSummary.WaveReached, candidate);
                await CloudUsername.SaveAsync(candidate);

                // One submission per identity, ever - close immediately rather than lingering on
                // a success message; WaveGameController hides the Add To Leaderboard button too
                // once Submitted fires, so this run (and every run after) can't submit again.
                Submitted?.Invoke();
                Hide();
            }
            catch (Exception exception)
            {
                // Surfaced to the Console (not just the UI) because a silent failure here is
                // exactly what made "nothing showed up on the leaderboard" hard to diagnose -
                // the real cause (unlinked Cloud project, disabled leaderboard write, network) is
                // in this exception, not in the generic status text below.
                Debug.LogException(exception, this);
                SetStatus("Couldn't reach the leaderboard. Try again.");
            }
            finally
            {
                _isSubmitting = false;
                SetInteractable(true);
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private void SetInteractable(bool interactable)
        {
            if (submitButton != null) submitButton.interactable = interactable;
            if (usernameField != null) usernameField.interactable = interactable;
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }
    }
}

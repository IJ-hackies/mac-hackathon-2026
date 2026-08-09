using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Waves
{
    /// <summary>
    /// Persistent, director-agnostic status readout for the current wave.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveHudView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text waveText;
        [SerializeField] private Text stateText;
        [SerializeField] private Text timerText;
        [SerializeField] private Image timerFill;
        [SerializeField, Min(0f)] private float timerFillLerpSpeed = 8f;

        private float _targetTimerFraction = 1f;
        private float _displayedTimerFraction = 1f;

        public void Configure(CanvasGroup root, Text wave, Text state, Text timer, Image fill)
        {
            canvasGroup = root;
            waveText = wave;
            stateText = state;
            timerText = timer;
            timerFill = fill;
            ApplyTimerFill(_displayedTimerFraction);
        }

        public void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                gameObject.SetActive(visible);
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        public void SetWave(int waveNumber)
        {
            if (waveText != null)
            {
                waveText.text = waveNumber > 0 ? $"WAVE {waveNumber:00}" : "WAVE --";
            }
        }

        public void SetWaveState(string state)
        {
            if (stateText != null)
            {
                stateText.text = state ?? string.Empty;
            }
        }

        /// <summary>Updates a regular-wave countdown. Pass a non-positive duration to hide it.</summary>
        public void SetTimer(float remainingSeconds, float durationSeconds)
        {
            bool shouldShow = durationSeconds > 0f;
            if (timerText != null)
            {
                timerText.gameObject.SetActive(shouldShow);
                timerText.text = shouldShow ? FormatDuration(remainingSeconds) : string.Empty;
            }

            if (timerFill != null)
            {
                timerFill.gameObject.SetActive(shouldShow);
            }

            _targetTimerFraction = shouldShow
                ? Mathf.Clamp01(remainingSeconds / durationSeconds)
                : 0f;
            if (timerFillLerpSpeed <= 0f)
            {
                _displayedTimerFraction = _targetTimerFraction;
                ApplyTimerFill(_displayedTimerFraction);
            }
        }

        public void SetTimerVisible(bool visible)
        {
            if (timerText != null) timerText.gameObject.SetActive(visible);
            if (timerFill != null) timerFill.gameObject.SetActive(visible);
        }

        private void Update()
        {
            if (timerFill == null || Mathf.Approximately(_displayedTimerFraction, _targetTimerFraction))
            {
                return;
            }

            _displayedTimerFraction = Mathf.MoveTowards(
                _displayedTimerFraction,
                _targetTimerFraction,
                timerFillLerpSpeed * Time.unscaledDeltaTime);
            ApplyTimerFill(_displayedTimerFraction);
        }

        private void ApplyTimerFill(float fraction)
        {
            if (timerFill != null)
            {
                timerFill.fillAmount = Mathf.Clamp01(fraction);
            }
        }

        private static string FormatDuration(float seconds)
        {
            int wholeSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{wholeSeconds / 60:00}:{wholeSeconds % 60:00}";
        }
    }
}

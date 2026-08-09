using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Player.UI.Waves
{
    [Serializable]
    public struct WaveRunSummary
    {
        public int WaveReached;
        public int Kills;
        public int GoldEarned;
        public float DurationSeconds;

        public WaveRunSummary(int waveReached, int kills, int goldEarned, float durationSeconds)
        {
            WaveReached = waveReached;
            Kills = kills;
            GoldEarned = goldEarned;
            DurationSeconds = durationSeconds;
        }
    }

    /// <summary>
    /// Full-screen run conclusion. The owner handles input/cursor locking and consumes its public button events.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameOverMissionSummaryView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text waveReachedText;
        [SerializeField] private Text killsText;
        [SerializeField] private Text goldEarnedText;
        [SerializeField] private Text durationText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private UnityEvent restartRequested;
        [SerializeField] private UnityEvent mainMenuRequested;

        public event Action RestartRequested;
        public event Action MainMenuRequested;

        public void Configure(
            CanvasGroup root,
            Text waveReached,
            Text kills,
            Text goldEarned,
            Text duration,
            Button restart,
            Button mainMenu)
        {
            UnsubscribeButtons();
            canvasGroup = root;
            waveReachedText = waveReached;
            killsText = kills;
            goldEarnedText = goldEarned;
            durationText = duration;
            restartButton = restart;
            mainMenuButton = mainMenu;
            SubscribeButtons();
        }

        private void OnEnable()
        {
            SubscribeButtons();
        }

        private void OnDisable()
        {
            UnsubscribeButtons();
        }

        public void Show(WaveRunSummary summary)
        {
            SetVisible(true);
            if (waveReachedText != null) waveReachedText.text = $"WAVE REACHED  {Mathf.Max(0, summary.WaveReached)}";
            if (killsText != null) killsText.text = $"KILLS  {Mathf.Max(0, summary.Kills)}";
            if (goldEarnedText != null) goldEarnedText.text = $"GOLD EARNED  {Mathf.Max(0, summary.GoldEarned)}";
            if (durationText != null) durationText.text = $"RUN TIME  {FormatDuration(summary.DurationSeconds)}";
        }

        public void Hide()
        {
            SetVisible(false);
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

        private void SubscribeButtons()
        {
            if (restartButton != null) restartButton.onClick.AddListener(HandleRestart);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(HandleMainMenu);
        }

        private void UnsubscribeButtons()
        {
            if (restartButton != null) restartButton.onClick.RemoveListener(HandleRestart);
            if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(HandleMainMenu);
        }

        private void HandleRestart()
        {
            restartRequested?.Invoke();
            RestartRequested?.Invoke();
        }

        private void HandleMainMenu()
        {
            mainMenuRequested?.Invoke();
            MainMenuRequested?.Invoke();
        }

        private static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}

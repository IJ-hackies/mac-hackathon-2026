using System;
using System.Collections.Generic;
using Audio;
using Player.UI;
using Services.Leaderboards;
using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Leaderboard
{
    public enum LeaderboardTab { Score, Wave }

    /// <summary>
    /// Drives the Leaderboard scene: tab switching, podium + 10-row-per-page table for each of the
    /// two Unity Dashboard leaderboards, pagination, and the back-to-menu button. Every visual piece
    /// is editor-built (see LeaderboardSceneSetup); this only requests data and refreshes text.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LeaderboardSceneController : MonoBehaviour
    {
        private const int PageSize = 10;

        [Header("Tabs")]
        [SerializeField] private Button scoreTabButton;
        [SerializeField] private Button waveTabButton;
        [SerializeField] private GameObject scorePanel;
        [SerializeField] private GameObject wavePanel;

        [Header("Score Tab")]
        [SerializeField] private PodiumStand[] scorePodium = Array.Empty<PodiumStand>();
        [SerializeField] private LeaderboardRow[] scoreRows = Array.Empty<LeaderboardRow>();
        [SerializeField] private Button scorePrevButton;
        [SerializeField] private Button scoreNextButton;
        [SerializeField] private Text scorePageLabel;

        [Header("Wave Tab")]
        [SerializeField] private PodiumStand[] wavePodium = Array.Empty<PodiumStand>();
        [SerializeField] private LeaderboardRow[] waveRows = Array.Empty<LeaderboardRow>();
        [SerializeField] private Button wavePrevButton;
        [SerializeField] private Button waveNextButton;
        [SerializeField] private Text wavePageLabel;

        [Header("Navigation")]
        [SerializeField] private Button backButton;
        [SerializeField] private MusicManager musicManager;

        private LeaderboardTab _activeTab = LeaderboardTab.Score;
        private int _scorePage;
        private int _wavePage;
        private int _scoreRequestToken;
        private int _waveRequestToken;

        private void Awake()
        {
            if (musicManager == null) musicManager = MusicManager.Instance;
        }

        private void OnEnable()
        {
            if (scoreTabButton != null) scoreTabButton.onClick.AddListener(() => SetTab(LeaderboardTab.Score));
            if (waveTabButton != null) waveTabButton.onClick.AddListener(() => SetTab(LeaderboardTab.Wave));
            if (scorePrevButton != null) scorePrevButton.onClick.AddListener(() => ChangePage(LeaderboardTab.Score, -1));
            if (scoreNextButton != null) scoreNextButton.onClick.AddListener(() => ChangePage(LeaderboardTab.Score, 1));
            if (wavePrevButton != null) wavePrevButton.onClick.AddListener(() => ChangePage(LeaderboardTab.Wave, -1));
            if (waveNextButton != null) waveNextButton.onClick.AddListener(() => ChangePage(LeaderboardTab.Wave, 1));
            if (backButton != null) backButton.onClick.AddListener(HandleBack);
        }

        private void Start()
        {
            if (musicManager != null && musicManager.menuMusic != null) musicManager.PlayMusic(musicManager.menuMusic);
            SetTab(LeaderboardTab.Score);
        }

        private void SetTab(LeaderboardTab tab)
        {
            _activeTab = tab;
            if (scorePanel != null) scorePanel.SetActive(tab == LeaderboardTab.Score);
            if (wavePanel != null) wavePanel.SetActive(tab == LeaderboardTab.Wave);
            RefreshActiveTab();
        }

        private void ChangePage(LeaderboardTab tab, int delta)
        {
            if (tab == LeaderboardTab.Score) _scorePage = Mathf.Max(0, _scorePage + delta);
            else _wavePage = Mathf.Max(0, _wavePage + delta);
            RefreshActiveTab();
        }

        private void RefreshActiveTab()
        {
            if (_activeTab == LeaderboardTab.Score) RequestPage(LeaderboardIds.HighestScore, _scorePage, scorePodium, scoreRows, scorePageLabel, scorePrevButton, scoreNextButton, ++_scoreRequestToken, true);
            else RequestPage(LeaderboardIds.FurthestWave, _wavePage, wavePodium, waveRows, wavePageLabel, wavePrevButton, waveNextButton, ++_waveRequestToken, false);
        }

        private async void RequestPage(
            string leaderboardId, int page, PodiumStand[] podium, LeaderboardRow[] rows, Text pageLabel,
            Button prevButton, Button nextButton, int requestToken, bool isScoreTab)
        {
            if (prevButton != null) prevButton.interactable = page > 0;
            if (pageLabel != null) pageLabel.text = $"PAGE {page + 1}";

            LeaderboardPage result;
            try
            {
                result = await LeaderboardsClient.GetPageAsync(leaderboardId, page * PageSize, PageSize);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (pageLabel != null) pageLabel.text = "UNAVAILABLE";
                return;
            }

            bool stillCurrent = isScoreTab ? requestToken == _scoreRequestToken : requestToken == _waveRequestToken;
            if (!stillCurrent) return;

            ApplyPage(result, podium, rows);
            if (nextButton != null) nextButton.interactable = result.Entries.Count >= PageSize;

            if (page == 0)
            {
                LeaderboardPage top3;
                try
                {
                    top3 = await LeaderboardsClient.GetPageAsync(leaderboardId, 0, 3);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                    return;
                }
                stillCurrent = isScoreTab ? requestToken == _scoreRequestToken : requestToken == _waveRequestToken;
                if (stillCurrent) ApplyPodium(top3, podium);
            }
        }

        private static void ApplyPage(LeaderboardPage page, PodiumStand[] podium, LeaderboardRow[] rows)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;
                if (i < page.Entries.Count)
                {
                    LeaderboardRowData entry = page.Entries[i];
                    rows[i].Refresh(entry.Rank, entry.PlayerName, entry.Value);
                }
                else
                {
                    rows[i].Clear();
                }
            }
        }

        private static void ApplyPodium(LeaderboardPage top3, PodiumStand[] podium)
        {
            for (int i = 0; i < podium.Length; i++)
            {
                if (podium[i] == null) continue;
                if (i < top3.Entries.Count)
                {
                    LeaderboardRowData entry = top3.Entries[i];
                    podium[i].Refresh(entry.PlayerName, entry.Value);
                }
                else
                {
                    podium[i].Clear();
                }
            }
        }

        private void HandleBack()
        {
            SceneTransitionController.LoadScene("MainMenu");
        }
    }
}

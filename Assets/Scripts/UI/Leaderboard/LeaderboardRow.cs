using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Leaderboard
{
    /// <summary>One row of the 10-per-page leaderboard table. Visuals are editor-built; this only refreshes text.</summary>
    [DisallowMultipleComponent]
    public sealed class LeaderboardRow : MonoBehaviour
    {
        [SerializeField] private Text rankText;
        [SerializeField] private Text nameText;
        [SerializeField] private Text valueText;

        public void Configure(Text rank, Text name, Text value)
        {
            rankText = rank;
            nameText = name;
            valueText = value;
        }

        public void Refresh(int rank, string playerName, double value)
        {
            gameObject.SetActive(true);
            if (rankText != null) rankText.text = $"#{rank}";
            if (nameText != null) nameText.text = string.IsNullOrEmpty(playerName) ? "---" : playerName;
            if (valueText != null) valueText.text = Mathf.RoundToInt((float)value).ToString();
        }

        public void Clear() => gameObject.SetActive(false);
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Leaderboard
{
    /// <summary>One of the three top-3 podium stands. The medal image is fixed per stand at build time.</summary>
    [DisallowMultipleComponent]
    public sealed class PodiumStand : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text valueText;

        public void Configure(Text name, Text value)
        {
            nameText = name;
            valueText = value;
        }

        public void Refresh(string playerName, double value)
        {
            if (nameText != null) nameText.text = string.IsNullOrEmpty(playerName) ? "---" : playerName;
            if (valueText != null) valueText.text = Mathf.RoundToInt((float)value).ToString();
        }

        public void Clear()
        {
            if (nameText != null) nameText.text = "---";
            if (valueText != null) valueText.text = "";
        }
    }
}

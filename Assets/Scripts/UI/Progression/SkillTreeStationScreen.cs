using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Progression
{
    [DisallowMultipleComponent]
    public sealed class SkillTreeStationScreen : MonoBehaviour
    {
        [SerializeField] private ProgressionDataAdapter progression;
        [SerializeField] private SkillUpgradeCard[] cards;
        [SerializeField] private Text goldText;

        private void OnEnable()
        {
            if (progression == null) progression = GetComponentInParent<ProgressionDataAdapter>();
            if (progression != null) progression.Refreshed += Refresh;
            Refresh();
        }
        private void OnDisable()
        {
            if (progression != null) progression.Refreshed -= Refresh;
        }
        public void Bind(MonoBehaviour source)
        {
            if (progression == null) progression = GetComponentInParent<ProgressionDataAdapter>();
            if (progression != null) progression.Bind(source);
        }
        public void Refresh()
        {
            if (goldText != null) goldText.text = "G " + (progression != null ? progression.Gold : 0);
            if (cards == null || progression == null) return;
            foreach (SkillUpgradeCard card in cards) if (card != null) card.Refresh(progression);
        }
    }
}

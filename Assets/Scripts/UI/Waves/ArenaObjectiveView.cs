using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Waves
{
    /// <summary>Compact objective copy for both arena contracts.</summary>
    [DisallowMultipleComponent]
    public sealed class ArenaObjectiveView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text arenaTitleText;
        [SerializeField] private Text objectiveText;
        [SerializeField] private Text detailText;
        [SerializeField] private GameObject bossHealthRoot;
        [SerializeField] private Image bossHealthFill;
        [SerializeField] private Text bossHealthText;

        public void Configure(
            CanvasGroup root,
            Text title,
            Text objective,
            Text detail,
            GameObject healthRoot,
            Image healthFill,
            Text healthText)
        {
            canvasGroup = root;
            arenaTitleText = title;
            objectiveText = objective;
            detailText = detail;
            bossHealthRoot = healthRoot;
            bossHealthFill = healthFill;
            bossHealthText = healthText;
        }

        public void SetVisible(bool visible)
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

        public void SetArena1Progress(int defeatedEnemies, int remainingEnemies)
        {
            SetVisible(true);
            SetBossHealthVisible(false);
            if (arenaTitleText != null) arenaTitleText.text = "ARENA 1 // SWARM";
            if (objectiveText != null)
            {
                objectiveText.text = $"{Mathf.Max(0, defeatedEnemies)} DEFEATED  //  {Mathf.Max(0, remainingEnemies)} LEFT";
            }
            if (detailText != null) detailText.text = "CLEAR ALL HOSTILES";
        }

        public void SetArena2Health(string phase, float currentHealth, float maxHealth, string bossName = "BARBARA")
        {
            SetVisible(true);
            SetBossHealthVisible(true);
            if (arenaTitleText != null) arenaTitleText.text = "ARENA 2 // BOSS";
            if (objectiveText != null) objectiveText.text = string.IsNullOrWhiteSpace(bossName) ? "BOSS ENGAGEMENT" : bossName.ToUpperInvariant();
            if (detailText != null) detailText.text = string.IsNullOrWhiteSpace(phase) ? string.Empty : phase.ToUpperInvariant();

            float safeMax = Mathf.Max(0f, maxHealth);
            float safeCurrent = Mathf.Clamp(currentHealth, 0f, safeMax);
            if (bossHealthFill != null)
            {
                bossHealthFill.fillAmount = safeMax > 0f ? safeCurrent / safeMax : 0f;
            }
            if (bossHealthText != null)
            {
                bossHealthText.text = safeMax > 0f
                    ? $"{Mathf.CeilToInt(safeCurrent)} / {Mathf.CeilToInt(safeMax)} HP"
                    : "HP --";
            }
        }

        public void SetObjective(string title, string objective, string detail)
        {
            SetVisible(true);
            SetBossHealthVisible(false);
            if (arenaTitleText != null) arenaTitleText.text = title ?? string.Empty;
            if (objectiveText != null) objectiveText.text = objective ?? string.Empty;
            if (detailText != null) detailText.text = detail ?? string.Empty;
        }

        private void SetBossHealthVisible(bool visible)
        {
            if (bossHealthRoot != null) bossHealthRoot.SetActive(visible);
        }
    }
}

using Combat;
using Enemies;
using UnityEngine;

namespace Gameplay.Waves
{
    /// <summary>
    /// Attach to a boss root and assign the health that represents the final objective.
    /// For Barbara this is the Mech health, never Astronaut stage-one health.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveArenaObjective : MonoBehaviour
    {
        [SerializeField] private Health completionHealth;
        [SerializeField] private WaveDirector director;
        private bool _reported;

        private void Awake()
        {
            if (director == null) director = FindFirstObjectByType<WaveDirector>();
            if (completionHealth == null)
            {
                BossMechAI mech = GetComponentInChildren<BossMechAI>(true);
                if (mech != null) completionHealth = mech.GetComponent<Health>();
            }
        }
        private void OnEnable()
        {
            if (completionHealth != null) completionHealth.Died += ReportComplete;
        }
        private void OnDisable()
        {
            if (completionHealth != null) completionHealth.Died -= ReportComplete;
        }
        public void Configure(WaveDirector targetDirector, Health finalHealth)
        {
            if (completionHealth != null) completionHealth.Died -= ReportComplete;
            director = targetDirector; completionHealth = finalHealth;
            if (isActiveAndEnabled && completionHealth != null) completionHealth.Died += ReportComplete;
        }
        public void ReportComplete()
        {
            if (_reported) return;
            _reported = true;
            if (director != null) director.NotifyArenaBossDefeated();
        }
    }
}

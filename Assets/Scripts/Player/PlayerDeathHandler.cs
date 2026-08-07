using Combat;
using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(Health))]
    public class PlayerDeathHandler : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerCombat combat;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (combat == null) combat = GetComponent<PlayerCombat>();
        }

        private void OnEnable()
        {
            GetComponent<Health>().Died += HandleDeath;
        }

        private void OnDisable()
        {
            GetComponent<Health>().Died -= HandleDeath;
        }

        private void HandleDeath()
        {
            if (controller != null) controller.enabled = false;
            if (combat != null) combat.enabled = false;
        }
    }
}

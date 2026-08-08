using Audio;
using Combat;
using UnityEngine;

namespace Player
{
    /// Small companion to PlayerDeathHandler, following the exact same Health event-subscription
    /// pattern - plays PlayerHitReact or MechHitReact (depending on whether PlayerUltimate is
    /// currently active) whenever the player's Health.Hit fires.
    [RequireComponent(typeof(Health))]
    public class PlayerHitAudio : MonoBehaviour
    {
        [SerializeField] private PlayerUltimate playerUltimate;

        private Health _health;

        private void Awake()
        {
            _health = GetComponent<Health>();
            if (playerUltimate == null) playerUltimate = GetComponent<PlayerUltimate>();
        }

        private void OnEnable()
        {
            if (_health == null) _health = GetComponent<Health>();
            _health.Hit += HandleHit;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Hit -= HandleHit;
        }

        private void HandleHit(DamageType damageType)
        {
            bool ultimateActive = playerUltimate != null && playerUltimate.IsActive;
            AudioManager.Instance.PlaySfx(ultimateActive ? SfxId.MechHitReact : SfxId.PlayerHitReact, transform.position);
        }
    }
}

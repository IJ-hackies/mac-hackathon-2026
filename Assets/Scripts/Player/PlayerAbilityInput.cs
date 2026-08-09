using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    /// Owns the shared Ability action (Shift - the old Sprint binding, repurposed once the
    /// walk/sprint speed split was removed). Routes it to PlayerDash (normal mode, one-shot on
    /// press) or PlayerShield (Ultimate mode, held) depending on PlayerUltimate.IsActive, keeping
    /// both of those components themselves input-agnostic - same separation PlayerCombat already
    /// models between its input handlers and Melee/FireProjectile.
    public class PlayerAbilityInput : MonoBehaviour
    {
        [SerializeField] private PlayerUltimate playerUltimate;
        [SerializeField] private PlayerDash playerDash;
        [SerializeField] private PlayerShield playerShield;

        private InputSystem_Actions _actions;

        private void Awake()
        {
            EnsureRuntimeState();
        }

        private void EnsureRuntimeState()
        {
            if (_actions == null) _actions = PlayerInputBindings.CreateActions();
            if (playerUltimate == null) playerUltimate = GetComponent<PlayerUltimate>();
            if (playerDash == null) playerDash = GetComponent<PlayerDash>();
            if (playerShield == null) playerShield = GetComponent<PlayerShield>();
        }

        private void OnEnable()
        {
            EnsureRuntimeState();
            _actions.Player.Enable();
            _actions.Player.Ability.started += OnAbilityStarted;
            _actions.Player.Ability.canceled += OnAbilityCanceled;
        }

        private void OnDisable()
        {
            if (_actions != null)
            {
                _actions.Player.Ability.started -= OnAbilityStarted;
                _actions.Player.Ability.canceled -= OnAbilityCanceled;
                _actions.Player.Disable();
            }
            playerShield?.SetHeld(false);
        }

        private void OnDestroy()
        {
            PlayerInputBindings.ReleaseActions(_actions);
            _actions = null;
        }

        private void OnAbilityStarted(InputAction.CallbackContext context)
        {
            if (playerUltimate != null && playerUltimate.IsActive)
            {
                playerShield?.SetHeld(true);
            }
            else
            {
                playerDash?.TryDash();
            }
        }

        private void OnAbilityCanceled(InputAction.CallbackContext context)
        {
            playerShield?.SetHeld(false);
        }
    }
}

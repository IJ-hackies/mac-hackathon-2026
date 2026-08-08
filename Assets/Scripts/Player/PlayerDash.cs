using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vfx;

namespace Player
{
    /// Normal-mode ability bound to the shared Ability action (Shift, see PlayerAbilityInput).
    /// Propels the player a short distance in the held Move direction (or current facing if no
    /// input is held) regardless of body/camera facing - "the character does not need to face
    /// towards that direction, they just need to propel in that direction." Input-agnostic
    /// (TryDash is called directly, not wired to input itself) so it stays testable/reusable the
    /// same way PlayerCombat's melee/fire methods are separate from their input wiring.
    public class PlayerDash : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;

        [Header("Dash")]
        [SerializeField] private float dashSpeed = 26f;
        [SerializeField] private float dashDuration = 0.3f;
        [SerializeField] private float dashCooldown = 3f;

        [Header("Visual")]
        [Tooltip("Imported dash burst (e.g. Lana Studio's Burst/Poof_electric).")]
        [SerializeField] private GameObject dashVfxPrefab;
        [SerializeField] private float dashVfxScale = 1f;

        private InputSystem_Actions _actions;
        private float _cooldownEndsAt = -999f;

        public bool IsDashing => playerController != null && playerController.IsDashing;
        public float CooldownDuration => dashCooldown;
        public float CooldownRemaining => Mathf.Max(0f, _cooldownEndsAt - Time.time);
        public bool IsReady => CooldownRemaining <= 0f;

        public event Action DashPerformed;
        public event Action CooldownChanged;

        private void Awake()
        {
            _actions = new InputSystem_Actions();
            if (playerController == null) playerController = GetComponent<PlayerController>();
        }

        // Without this, _actions.Player.Move.ReadValue<Vector2>() in TryDash always reads the
        // C# default (Vector2.zero) - a disabled action map's controls don't update - which made
        // TryDash always fall through to the "no input held" facing fallback, so every dash
        // silently ignored WASD and only ever went wherever the camera/body happened to face.
        private void OnEnable()
        {
            _actions.Player.Enable();
        }

        private void OnDisable()
        {
            _actions.Player.Disable();
        }

        /// Called by PlayerAbilityInput while not in Ultimate mode. Direction comes from
        /// currently-held Move input, camera-relative/tangent-projected the same way
        /// PlayerController's own locomotion is (see GetCameraRelativeTangentDirection), falling
        /// back to the player's current forward if no input is held (an idle dash still fires).
        public bool TryDash()
        {
            if (!IsReady || playerController == null || playerController.IsStaggered || IsDashing)
            {
                return false;
            }

            Vector2 moveInput = _actions.Player.Move.ReadValue<Vector2>();
            Vector3 direction = playerController.GetCameraRelativeTangentDirection(moveInput);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.ProjectOnPlane(transform.forward, transform.up).normalized;
            }

            playerController.Dash(direction, dashSpeed, dashDuration);
            _cooldownEndsAt = Time.time + dashCooldown;
            SpawnDashVfx(direction);

            DashPerformed?.Invoke();
            CooldownChanged?.Invoke();
            return true;
        }

        // Imported burst (e.g. Lana Studio's Burst/Poof_electric) at the launch point, oriented
        // to face the dash direction - a one-shot cue at the departure point rather than a
        // travelling trail, since the player already outruns any trailing effect at this speed.
        private void SpawnDashVfx(Vector3 direction)
        {
            if (dashVfxPrefab == null) return;

            var rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction, Vector3.up)
                : Quaternion.identity;
            var vfx = Instantiate(dashVfxPrefab, transform.position, rotation);
            vfx.transform.localScale = Vector3.one * dashVfxScale;
            ImportedVfxUtility.FixUrpMaterials(vfx);
            ImportedVfxUtility.ForceHierarchyParticleScaling(vfx);

            float maxLifetime = 1f;
            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
            {
                maxLifetime = Mathf.Max(maxLifetime, ps.main.duration + ps.main.startLifetime.constantMax);
            }
            Destroy(vfx, maxLifetime + 0.5f);
        }
    }
}

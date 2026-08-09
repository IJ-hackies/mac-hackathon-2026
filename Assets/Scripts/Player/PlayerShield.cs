using System;
using Audio;
using Combat;
using UnityEngine;
using Vfx;

namespace Player
{
    /// Ultimate-mode ability bound to the shared Ability action (Shift, see PlayerAbilityInput) -
    /// replaces PlayerDash while PlayerUltimate.IsActive. Holding Shift fully mitigates incoming
    /// damage (via Health.IncomingDamageMultiplier) while energy remains, covering the whole
    /// mech in an imported Shield_electric VFX. drainPerSecond is deliberately greater than
    /// regenPerSecond so holding it permanently is impossible - the player must ration shield use
    /// within one ultimate activation, per spec. Energy resets to full on every ActivateUltimate
    /// call (the budget is scoped per ultimate use, not persistent).
    public class PlayerShield : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Transform mechVisualRoot;

        [Header("Energy (seconds of holdable shield)")]
        [SerializeField] private float maxEnergy = 8f;
        [SerializeField] private float drainPerSecond = 1f;
        [Tooltip("Deliberately less than drainPerSecond - net drain while used prevents " +
                 "permanent uptime; balance both together, not in isolation.")]
        [SerializeField] private float regenPerSecond = 0.4f;

        [Header("Visual")]
        [Tooltip("Lana Studio's Shields/Shield_electric - scaled to cover the whole 1.2x mech.")]
        [SerializeField] private GameObject shieldVfxPrefab;
        [SerializeField] private float shieldVfxScale = 0.9f;

        private GameObject _shieldVfxInstance;
        private AudioHandle _shieldLoopHandle;

        public float Energy { get; private set; }
        public float MaxEnergy => maxEnergy;
        public float EnergyFraction => maxEnergy > 0f ? Mathf.Clamp01(Energy / maxEnergy) : 0f;
        public bool IsActive { get; private set; }

        public event Action EnergyChanged;

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
            Energy = maxEnergy;
        }

        private void Update()
        {
            if (IsActive)
            {
                Energy = Mathf.Max(0f, Energy - drainPerSecond * Time.deltaTime);
                EnergyChanged?.Invoke();
                if (Energy <= 0f) SetHeld(false);
            }
            else if (Energy < maxEnergy)
            {
                Energy = Mathf.Min(maxEnergy, Energy + regenPerSecond * Time.deltaTime);
                EnergyChanged?.Invoke();
            }
        }

        /// held=true only actually raises the shield while Energy > 0; called every frame the
        /// Ability action is held/released, so it self-corrects once energy runs out (no separate
        /// "denied" state needed - SetHeld(true) with 0 energy is simply a no-op each frame until
        /// regen brings it back up, but since Update also force-releases on empty, this mainly
        /// matters for the initial press).
        public void SetHeld(bool held)
        {
            bool shouldBeActive = held && Energy > 0f;
            if (shouldBeActive == IsActive) return;

            IsActive = shouldBeActive;
            if (health != null)
            {
                if (IsActive) health.SetIncomingDamageModifier(this, 0f);
                else health.RemoveIncomingDamageModifier(this);
            }

            if (IsActive)
            {
                SpawnShieldVfx();
                if (Application.isPlaying)
                    _shieldLoopHandle = AudioManager.Instance.PlayLoop(SfxId.MechShieldActivate, transform);
            }
            else
            {
                DespawnShieldVfx();
                if (Application.isPlaying && _shieldLoopHandle.IsValid) AudioManager.Instance.StopLoop(_shieldLoopHandle);
            }
        }

        /// Called by PlayerUltimate on every activation - the shield budget is scoped per
        /// ultimate use, not persistent across activations.
        public void ResetEnergy()
        {
            Energy = maxEnergy;
            EnergyChanged?.Invoke();
        }

        private void SpawnShieldVfx()
        {
            if (shieldVfxPrefab == null || _shieldVfxInstance != null) return;

            Transform parent = mechVisualRoot != null ? mechVisualRoot : transform;
            _shieldVfxInstance = Instantiate(shieldVfxPrefab, parent);
            _shieldVfxInstance.transform.localPosition = Vector3.zero;
            _shieldVfxInstance.transform.localRotation = Quaternion.identity;
            _shieldVfxInstance.transform.localScale = Vector3.one * shieldVfxScale;
            ImportedVfxUtility.FixUrpMaterials(_shieldVfxInstance);
            ImportedVfxUtility.ForceHierarchyParticleScaling(_shieldVfxInstance);
        }

        private void DespawnShieldVfx()
        {
            if (_shieldVfxInstance == null) return;
            Destroy(_shieldVfxInstance);
            _shieldVfxInstance = null;
        }

        private void OnDisable()
        {
            IsActive = false;
            health?.RemoveIncomingDamageModifier(this);
            DespawnShieldVfx();
        }
    }
}

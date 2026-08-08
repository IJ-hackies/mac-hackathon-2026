using Player;
using UnityEngine;
using Vfx;

namespace Items
{
    /// Base behavior for a floating power-up pickup: hover at a fixed height, spin slowly
    /// around local up, wear a looping "backlight" aura VFX, play a spawn-in VFX once, and on
    /// player contact play a pickup VFX plus a brief matching backlight burst on the player
    /// before destroying itself. Subclasses only implement ApplyEffect - detection/VFX/
    /// destruction are handled here so every pickup behaves identically except for its payload.
    public abstract class ItemPickup : MonoBehaviour
    {
        [Tooltip("Height above this object's spawn point the model hovers at, roughly player " +
                 "torso height (the player capsule is 2.55 tall).")]
        [SerializeField] private float hoverHeight = 1.4f;
        [SerializeField] private float rotateSpeed = 60f;
        [SerializeField] private float triggerRadius = 1f;

        [Tooltip("Looping aura VFX worn by the floating item, and reused (non-looping) as a " +
                 "brief burst on the player when collected.")]
        [SerializeField] private GameObject backlightPrefab;
        [SerializeField] private GameObject spawnEffectPrefab;
        [SerializeField] private GameObject pickupEffectPrefab;
        [SerializeField] private float playerBacklightDuration = 1.5f;

        private bool _collected;

        /// Subclasses without a finished pickup effect (e.g. ThunderPickup) can override this
        /// to false so the item stays visual-only - visible but not collectible - until their
        /// gameplay effect exists.
        protected virtual bool CollectibleOnContact => true;

        protected virtual void Awake()
        {
            transform.position += transform.up * hoverHeight;

            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = triggerRadius;

            if (spawnEffectPrefab != null)
            {
                GameObject spawnVfx = Instantiate(spawnEffectPrefab, transform.position, transform.rotation);
                ImportedVfxUtility.FixUrpMaterials(spawnVfx);
                ImportedVfxUtility.ForceHierarchyParticleScaling(spawnVfx);
                Destroy(spawnVfx, GetEffectLifetime(spawnVfx));
            }

            if (backlightPrefab != null)
            {
                // Anchored at the ground (pre-hover position), not the hovering model - these
                // Lana Studio backlight effects are authored to rise from a ground-level ring, so
                // parenting at the model's own (elevated) origin left the aura floating starting
                // mid-air instead of reaching down to the floor.
                GameObject aura = Instantiate(backlightPrefab, transform);
                aura.transform.localPosition = Vector3.down * hoverHeight;
                ImportedVfxUtility.FixUrpMaterials(aura);
                ImportedVfxUtility.ForceHierarchyParticleScaling(aura);
            }
        }

        private void Update()
        {
            transform.Rotate(transform.up, rotateSpeed * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_collected || !CollectibleOnContact) return;

            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            _collected = true;
            ApplyEffect(player.gameObject);

            if (pickupEffectPrefab != null)
            {
                GameObject pickupVfx = Instantiate(pickupEffectPrefab, transform.position, transform.rotation);
                ImportedVfxUtility.FixUrpMaterials(pickupVfx);
                ImportedVfxUtility.ForceHierarchyParticleScaling(pickupVfx);
                Destroy(pickupVfx, GetEffectLifetime(pickupVfx));
            }

            if (backlightPrefab != null)
            {
                // At the player's own transform origin (feet/ground level, per PlayerController's
                // feet-origin convention), not offset up to hoverHeight - these backlight effects
                // are authored to rise from a ground-level ring, same reasoning as the floating
                // item's own aura anchor above.
                GameObject burst = Instantiate(
                    backlightPrefab, player.transform.position, player.transform.rotation, player.transform);
                ImportedVfxUtility.FixUrpMaterials(burst);
                ImportedVfxUtility.ForceHierarchyParticleScaling(burst);

                // Parented directly to the player root (unscaled), not under the visual model, so
                // it doesn't automatically inherit the Mech's larger scale the way VFX parented
                // under VisualRoot's own hierarchy does - scale it up explicitly while Ultimate is
                // active so the burst still reads at the right size next to the bigger Mech.
                var ultimate = player.GetComponent<PlayerUltimate>();
                float vfxScale = ultimate != null ? ultimate.VfxScaleMultiplier : 1f;
                if (vfxScale != 1f) burst.transform.localScale *= vfxScale;

                Destroy(burst, playerBacklightDuration);
            }

            Destroy(gameObject);
        }

        private static float GetEffectLifetime(GameObject effect)
        {
            float longest = 1f;
            foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                float lifetime = ps.main.duration + ps.main.startLifetime.constantMax;
                if (lifetime > longest) longest = lifetime;
            }

            return longest;
        }

        /// Applies this pickup's payload to the collecting player. Called once, before the
        /// pickup VFX/backlight burst spawn and before this object is destroyed.
        protected abstract void ApplyEffect(GameObject player);
    }
}

using Audio;
using Combat;
using UnityEngine;

namespace Items
{
    public class HealthPickup : ItemPickup
    {
        private const float HealAmount = 50f;

        protected override void ApplyEffect(GameObject player)
        {
            var health = player.GetComponent<Health>();
            if (health != null) health.Heal(HealAmount);
            AudioManager.Instance.PlaySfx(SfxId.ItemHealthPickup, transform.position);
        }
    }
}

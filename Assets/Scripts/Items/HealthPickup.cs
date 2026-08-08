using Combat;
using UnityEngine;

namespace Items
{
    public class HealthPickup : ItemPickup
    {
        protected override void ApplyEffect(GameObject player)
        {
            var health = player.GetComponent<Health>();
            if (health != null) health.FullyHeal();
        }
    }
}

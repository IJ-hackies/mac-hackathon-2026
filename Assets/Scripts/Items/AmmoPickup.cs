using Player;
using UnityEngine;

namespace Items
{
    public class AmmoPickup : ItemPickup
    {
        protected override void ApplyEffect(GameObject player)
        {
            var ammo = player.GetComponent<PlayerAmmo>();
            if (ammo != null) ammo.RefillFull();
        }
    }
}

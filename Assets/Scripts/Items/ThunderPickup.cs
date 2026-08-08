using Player;
using UnityEngine;

namespace Items
{
    /// Activates the player's timed Ultimate ("Mech mode") - see Player.PlayerUltimate.
    /// CollectibleOnContact defaults to true (ItemPickup base) now that this has a real effect.
    public class ThunderPickup : ItemPickup
    {
        protected override void ApplyEffect(GameObject player)
        {
            var ultimate = player.GetComponent<PlayerUltimate>();
            ultimate?.ActivateUltimate();
        }
    }
}

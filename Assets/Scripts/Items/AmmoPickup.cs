using Audio;
using Player;
using UnityEngine;

namespace Items
{
    public class AmmoPickup : ItemPickup
    {
        protected override void ApplyEffect(GameObject player)
        {
            var ammo = player.GetComponent<PlayerAmmo>();
            if (ammo != null)
            {
                int restoreAmount = ammo.MagazineSize > int.MaxValue / 2
                    ? int.MaxValue
                    : ammo.MagazineSize * 2;
                ammo.RestoreReserve(restoreAmount);
            }
            AudioManager.Instance.PlaySfx(SfxId.ItemAmmoPickup, transform.position);
        }
    }
}

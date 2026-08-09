using UnityEngine;

namespace Tutorial
{
    /// Sits alongside an Items.ItemPickup instance so the tutorial can tell when it's been
    /// collected. ItemPickup destroys its own GameObject on pickup (see Items/ItemPickup.cs); a
    /// sibling component's OnDestroy fires regardless of who called Destroy, so this needs no
    /// changes to the shared pickup script.
    public class TutorialPickupWatcher : MonoBehaviour
    {
        public enum Kind
        {
            Health,
            Ammo,
            Thunder,
        }

        [SerializeField] private Kind kind;
        [SerializeField] private TutorialManager manager;

        public void Configure(TutorialManager owner, Kind itemKind)
        {
            manager = owner;
            kind = itemKind;
        }

        private void OnDestroy()
        {
            if (!Application.isPlaying || manager == null) return;
            manager.NotifyItemCollected(kind);
        }
    }
}

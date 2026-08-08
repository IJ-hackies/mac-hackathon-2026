using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    /// Adds MenuButtonSfx to every Selectable under a root that doesn't already have one, so a
    /// menu controller can wire up hover/click sound for its whole hierarchy with a single call
    /// in Awake, regardless of how the buttons were authored (hand-placed or built by an editor
    /// setup script).
    public static class UiSfxWirer
    {
        public static void WireAll(GameObject root)
        {
            if (root == null) return;

            var selectables = root.GetComponentsInChildren<Selectable>(true);
            foreach (var selectable in selectables)
            {
                if (selectable.GetComponent<MenuButtonSfx>() == null)
                {
                    selectable.gameObject.AddComponent<MenuButtonSfx>();
                }

                if (selectable is Button && selectable.GetComponent<ButtonHoverEffect>() == null)
                {
                    selectable.gameObject.AddComponent<ButtonHoverEffect>();
                }
            }
        }
    }
}

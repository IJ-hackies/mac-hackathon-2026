using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    /// <summary>Finds an already-imported UI kit sprite by asset name from Image components
    /// already present in the loaded scene (e.g. the main menu's own buttons), so self-built
    /// runtime UI can reuse the project's actual CartoonSciFi/SpaceExpansion art instead of a
    /// flat procedural color box - without needing AssetDatabase (unavailable outside the
    /// Editor) or a manually-wired serialized field for every consumer.</summary>
    public static class UiSpriteFinder
    {
        public static Image FindImageBySpriteName(Transform root, string spriteName)
        {
            if (root == null) return null;

            var images = root.GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image.sprite != null && image.sprite.name == spriteName) return image;
            }

            return null;
        }

        public static Sprite FindSpriteByName(Transform root, string spriteName)
        {
            Image image = FindImageBySpriteName(root, spriteName);
            return image != null ? image.sprite : null;
        }
    }
}

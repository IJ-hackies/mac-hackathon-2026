using UnityEngine;

namespace Player.UI
{
    /// <summary>Small procedurally-generated, antialiased sprites (circle, rounded rect) shared by
    /// self-built runtime UI (LeaderboardPopupController, etc.) so ad hoc panels/medals aren't
    /// stuck with hard-edged flat rectangles when no authored sprite is assigned. Textures are
    /// generated once and cached for the process lifetime.</summary>
    public static class RuntimeUiSprites
    {
        private static Sprite _circle;
        private static Sprite _roundedRect;

        public static Sprite Circle => _circle != null ? _circle : _circle = BuildCircle(128);

        /// 16px corner radius baked in, sliced so the panel/button can stretch to any size
        /// without distorting the corners.
        public static Sprite RoundedRect => _roundedRect != null ? _roundedRect : _roundedRect = BuildRoundedRect(64, 16);

        private static Sprite BuildCircle(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float radius = size * 0.5f;
            Vector2 center = new Vector2(radius, radius);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01((radius - distance) / 1.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildRoundedRect(int size, int cornerRadius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            float r = cornerRadius;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;

                    float dx = Mathf.Max(0f, Mathf.Max(r - px, px - (size - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - py, py - (size - r)));
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((r - distance) / 1.5f);
                    // Outside the corner-influence band, pixels are fully inside the rect.
                    bool inCornerBand = px < r || px > size - r;
                    bool inCornerBandY = py < r || py > size - r;
                    if (!(inCornerBand && inCornerBandY)) alpha = 1f;

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            var border = new Vector4(cornerRadius + 2, cornerRadius + 2, cornerRadius + 2, cornerRadius + 2);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, border);
            sprite.name = "RuntimeRoundedRect";
            return sprite;
        }
    }
}

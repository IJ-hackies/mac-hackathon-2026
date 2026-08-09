using UnityEngine;

namespace Combat
{
    /// Floating red damage-number popups (Stardew Valley / Vampire Survivors / Archero style) -
    /// small, solid red, black-outlined, with a quick overshoot "pop" on spawn before rising and
    /// fading. Built entirely at runtime (TextMesh + the engine's built-in Arial font, no
    /// authored prefab) - the outline is faked with 8 offset black copies behind the red text,
    /// since TextMesh has no native outline support.
    public static class DamageNumberSpawner
    {
        private const float RiseHeight = 0.8f;
        private const float Lifetime = 0.75f;
        private const float BounceDuration = 0.12f;
        private const float CharacterSize = 0.22f;
        private const int FontSize = 32;
        private const float OutlineOffset = 0.06f;

        private static readonly Vector2[] OutlineDirections =
        {
            new Vector2(-1f, -1f), new Vector2(0f, -1f), new Vector2(1f, -1f),
            new Vector2(-1f, 0f), new Vector2(1f, 0f),
            new Vector2(-1f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f),
        };

        private static readonly Color DefaultColor = new Color(1f, 0.02f, 0f);

        public static void Spawn(Vector3 worldPosition, float amount, Color? color = null)
        {
            if (amount <= 0f) return;

            // Arial.ttf was removed as a valid GetBuiltinResource path in newer Unity versions -
            // LegacyRuntime.ttf is the current always-available built-in replacement. Guarded with
            // a null-check (rather than letting a future engine change throw) because this is
            // called from projectile/attack onHit callbacks - an uncaught exception here previously
            // aborted whatever hit-resolution code ran after it in the caller (e.g. BossProjectile
            // never finishing its own cleanup), which is what let bullets pass through enemies.
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return;

            var root = new GameObject("DamageNumber");
            Vector3 jitter = new Vector3(Random.Range(-0.15f, 0.15f), 0f, Random.Range(-0.15f, 0.15f));
            root.transform.position = worldPosition + Vector3.up * 0.2f + jitter;
            root.transform.localScale = Vector3.zero;

            string text = Mathf.RoundToInt(amount).ToString();

            var outlineTexts = new TextMesh[OutlineDirections.Length];
            for (int i = 0; i < OutlineDirections.Length; i++)
            {
                var outlineGo = new GameObject("Outline");
                outlineGo.transform.SetParent(root.transform, false);
                outlineGo.transform.localPosition =
                    new Vector3(OutlineDirections[i].x, OutlineDirections[i].y, 0.001f) * OutlineOffset;
                outlineTexts[i] = CreateTextMesh(outlineGo, font, text, Color.black);
            }

            var mainGo = new GameObject("Text");
            mainGo.transform.SetParent(root.transform, false);
            TextMesh mainText = CreateTextMesh(mainGo, font, text, color ?? DefaultColor);

            root.AddComponent<DamageNumberInstance>()
                .Init(mainText, outlineTexts, RiseHeight, Lifetime, BounceDuration);
        }

        private static TextMesh CreateTextMesh(GameObject go, Font font, string text, Color color)
        {
            var textMesh = go.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.font = font;
            textMesh.fontSize = FontSize;
            textMesh.characterSize = CharacterSize;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = color;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.material = font.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return textMesh;
        }
    }

    internal class DamageNumberInstance : MonoBehaviour
    {
        private TextMesh _mainText;
        private TextMesh[] _outlineTexts;
        private float _riseHeight;
        private float _lifetime;
        private float _bounceDuration;
        private float _elapsed;
        private Vector3 _start;
        private Camera _camera;
        private Color _mainColor;

        public void Init(TextMesh mainText, TextMesh[] outlineTexts, float riseHeight, float lifetime, float bounceDuration)
        {
            _mainText = mainText;
            _outlineTexts = outlineTexts;
            _riseHeight = riseHeight;
            _lifetime = lifetime;
            _bounceDuration = bounceDuration;
            _start = transform.position;
            _camera = Camera.main;
            _mainColor = mainText.color;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            if (_camera == null) _camera = Camera.main;
            if (_camera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - _camera.transform.position);
            }

            // Quick overshoot pop on spawn (0 -> ~1.2x -> settle at 1x) - the "satisfying bounce"
            // hit numbers in Archero/Vampire Survivors/etc. have, rather than just fading in flat.
            float scale = _elapsed < _bounceDuration ? EaseOutBack(_elapsed / _bounceDuration) : 1f;
            transform.localScale = Vector3.one * scale;

            // Rises fast at first and settles - most of the travel happens early.
            float riseT = Mathf.Clamp01(_elapsed / _lifetime);
            float eased = 1f - (1f - riseT) * (1f - riseT);
            transform.position = _start + Vector3.up * (_riseHeight * eased);

            // Holds fully solid, then fades out over the final ~45% of its life.
            float fadeStart = _lifetime * 0.55f;
            float alpha = _elapsed <= fadeStart ? 1f : 1f - Mathf.InverseLerp(fadeStart, _lifetime, _elapsed);
            SetAlpha(alpha);

            if (_elapsed >= _lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void SetAlpha(float alpha)
        {
            if (_mainText != null)
            {
                Color color = _mainColor;
                color.a = alpha;
                _mainText.color = color;
            }

            if (_outlineTexts == null) return;
            foreach (var outline in _outlineTexts)
            {
                if (outline == null) continue;
                Color color = Color.black;
                color.a = alpha;
                outline.color = color;
            }
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            const float c3 = overshoot + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + overshoot * x * x;
        }
    }
}

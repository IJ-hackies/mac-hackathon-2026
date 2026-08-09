using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    /// Bottom-center "RELOADING" readout with a looping "." / ".." / "..." tail, shown only
    /// while PlayerAmmo.IsReloading is true. Builds its own Text object at runtime (parented
    /// under the same HUD Canvas as AmmoHudUI) rather than requiring an editor setup tool, so it
    /// works regardless of how a given player rig instance's UI was hand-authored.
    public sealed class ReloadIndicatorUI : MonoBehaviour
    {
        private const string Word = "RELOADING";
        private const float DotIntervalSeconds = 0.35f;
        private static readonly Color GoldYellow = new Color(1f, 0.82f, 0.18f, 1f);

        private global::Player.PlayerAmmo _ammo;
        private Text _label;
        private float _dotTimer;
        private int _dotCount;

        public static void EnsureFor(global::Player.PlayerAmmo ammo)
        {
            if (ammo == null) return;
            if (FindFirstObjectByType<ReloadIndicatorUI>() != null) return;

            AmmoHudUI ammoHud = FindFirstObjectByType<AmmoHudUI>();
            Transform hudCanvas = ammoHud != null ? ammoHud.transform.parent : null;
            if (hudCanvas == null) return;

            var go = new GameObject("ReloadIndicator", typeof(RectTransform));
            // Inactive until fully configured below: AddComponent runs Awake/OnEnable
            // synchronously on an active object, which would fire OnEnable before _label/_ammo
            // are assigned and silently skip the ReloadStarted/Finished subscription.
            go.SetActive(false);
            go.transform.SetParent(hudCanvas, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(400f, 40f);
            rect.anchoredPosition = new Vector2(0f, 90f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var fontAsset = UnityEditor_LoadKenneyFontIfAvailable();
            if (fontAsset != null) font = fontAsset;

            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = 26;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = GoldYellow;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = string.Empty;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(2f, -2f);

            var indicator = go.AddComponent<ReloadIndicatorUI>();
            indicator._label = text;
            indicator.Bind(ammo);
            go.SetActive(true);
        }

        // Loaded via Resources rather than a hard AssetDatabase reference so this class has no
        // editor-only dependency - falls back to the built-in font if the art asset ever moves.
        private static Font UnityEditor_LoadKenneyFontIfAvailable()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/UI/KenneyFuture.ttf");
#else
            return null;
#endif
        }

        private void Bind(global::Player.PlayerAmmo ammo)
        {
            _ammo = ammo;
        }

        private void OnEnable()
        {
            if (_ammo == null) return;
            _ammo.ReloadStarted += HandleReloadStarted;
            _ammo.ReloadFinished += HandleReloadFinished;
            SetVisible(_ammo.IsReloading);
        }

        private void OnDisable()
        {
            if (_ammo == null) return;
            _ammo.ReloadStarted -= HandleReloadStarted;
            _ammo.ReloadFinished -= HandleReloadFinished;
        }

        private void HandleReloadStarted()
        {
            _dotTimer = 0f;
            _dotCount = 1;
            SetVisible(true);
        }

        private void HandleReloadFinished() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            if (_label == null) return;
            _label.enabled = visible;
            if (visible) _label.text = Word + new string('.', _dotCount);
        }

        private void Update()
        {
            if (_label == null || !_label.enabled) return;

            _dotTimer += Time.unscaledDeltaTime;
            if (_dotTimer < DotIntervalSeconds) return;

            _dotTimer -= DotIntervalSeconds;
            _dotCount = _dotCount % 3 + 1;
            _label.text = Word + new string('.', _dotCount);
        }
    }
}

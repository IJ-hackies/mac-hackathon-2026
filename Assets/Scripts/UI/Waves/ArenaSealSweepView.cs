using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Player.UI.Waves
{
    /// <summary>Scaled three-second cyan/amber/red mission-console seal transition.</summary>
    [DisallowMultipleComponent]
    public sealed class ArenaSealSweepView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform cyanSweep;
        [SerializeField] private RectTransform amberSweep;
        [SerializeField] private RectTransform redSweep;
        [SerializeField] private Color cyan = new Color(0.18f, 0.9f, 1f, 1f);
        [SerializeField] private Color amber = new Color(1f, 0.62f, 0.12f, 1f);
        [SerializeField] private Color red = new Color(1f, 0.24f, 0.22f, 1f);
        [SerializeField, Min(0.1f)] private float defaultDuration = 3f;
        [SerializeField] private UnityEvent completed;

        private RectTransform[] _sweeps;
        private float _elapsed;
        private float _duration;
        private bool _playing;

        public event System.Action Completed;

        public void Configure(CanvasGroup root, RectTransform cyan, RectTransform amber, RectTransform red)
        {
            canvasGroup = root;
            cyanSweep = cyan;
            amberSweep = amber;
            redSweep = red;
            _sweeps = null;
        }

        public void Play() => Play(defaultDuration);

        public void Play(float durationSeconds)
        {
            _duration = Mathf.Max(0.01f, durationSeconds);
            _elapsed = 0f;
            _playing = true;
            ApplySignatureColors();
            SetPresentationVisible(true);
            Apply(0f);
        }

        public void Stop()
        {
            _playing = false;
            SetPresentationVisible(false);
        }

        private void Update()
        {
            if (!_playing) return;
            _elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(_elapsed / _duration);
            Apply(normalized);
            if (normalized < 1f) return;

            _playing = false;
            completed?.Invoke();
            Completed?.Invoke();
            SetPresentationVisible(false);
        }

        private void Apply(float normalized)
        {
            RectTransform[] sweeps = Sweeps;
            for (int index = 0; index < sweeps.Length; index++)
            {
                RectTransform sweep = sweeps[index];
                if (sweep == null) continue;
                float local = Mathf.Clamp01(normalized * 1.45f - index * 0.18f);
                float x = Mathf.Lerp(-1f, 1f, local);
                Vector2 anchor = sweep.anchorMin;
                anchor.x = x;
                sweep.anchorMin = anchor;
                anchor = sweep.anchorMax;
                anchor.x = x;
                sweep.anchorMax = anchor;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(Mathf.Min(normalized * 8f, (1f - normalized) * 8f, 1f));
            }
        }

        private RectTransform[] Sweeps => _sweeps ?? (_sweeps = new[] { cyanSweep, amberSweep, redSweep });

        private void ApplySignatureColors()
        {
            SetColor(cyanSweep, cyan);
            SetColor(amberSweep, amber);
            SetColor(redSweep, red);
        }

        private static void SetColor(RectTransform target, Color color)
        {
            if (target == null) return;
            Image image = target.GetComponent<Image>();
            if (image != null) image.color = color;
        }

        private void SetPresentationVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }
    }
}

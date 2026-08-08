using UnityEngine;

namespace Gameplay.Interaction
{
    /// <summary>
    /// Lightweight readability marker for a station. The assigned visual is billboarded toward
    /// the active camera while its local placement keeps it correctly above a planet-aligned
    /// station. Color is applied with property blocks, never by mutating shared materials.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StationMarkerVisual : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer[] colorTargets;
        [SerializeField] private Color baseColor = Color.white;
        [SerializeField] private Vector3 baseScale = Vector3.one;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float bobHeight = 0.12f;
        [SerializeField, Min(0f)] private float bobFrequency = 1.4f;
        [SerializeField, Min(0f)] private float pulseAmount = 0.08f;
        [SerializeField, Min(0f)] private float pulseFrequency = 1.8f;
        [SerializeField] private float phaseOffset;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock _properties;
        private Vector3 _baseLocalPosition;
        private Camera _mainCamera;

        private void Awake()
        {
            if (visualRoot == null) visualRoot = transform;
            _baseLocalPosition = visualRoot.localPosition;
            ApplyColor();
        }

        private void OnEnable()
        {
            if (visualRoot == null) visualRoot = transform;
            _baseLocalPosition = visualRoot.localPosition;
            ApplyColor();
        }

        private void LateUpdate()
        {
            if (visualRoot == null) return;

            float time = Time.unscaledTime + phaseOffset;
            float bob = Mathf.Sin(time * bobFrequency * Mathf.PI * 2f) * bobHeight;
            float pulse = 1f + Mathf.Sin(time * pulseFrequency * Mathf.PI * 2f) * pulseAmount;
            visualRoot.localPosition = _baseLocalPosition + Vector3.up * bob;
            visualRoot.localScale = baseScale * pulse;

            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            Vector3 towardCamera = _mainCamera.transform.position - visualRoot.position;
            if (towardCamera.sqrMagnitude > 0.0001f)
            {
                visualRoot.rotation = Quaternion.LookRotation(towardCamera, _mainCamera.transform.up);
            }
        }

        public void Configure(
            Transform targetVisual,
            Renderer[] targets,
            Color color,
            Vector3 scale)
        {
            visualRoot = targetVisual != null ? targetVisual : transform;
            colorTargets = targets;
            baseColor = color;
            baseScale = scale;
            _baseLocalPosition = visualRoot.localPosition;
            ApplyColor();
        }

        private void ApplyColor()
        {
            if (colorTargets == null) return;
            if (_properties == null) _properties = new MaterialPropertyBlock();
            foreach (Renderer target in colorTargets)
            {
                if (target == null) continue;
                target.GetPropertyBlock(_properties);
                _properties.SetColor(BaseColorId, baseColor);
                _properties.SetColor(ColorId, baseColor);
                target.SetPropertyBlock(_properties);
            }
        }
    }
}

using System.Collections.Generic;
using Gameplay.Areas;
using UnityEngine;

namespace Gameplay.Waves
{
    /// <summary>
    /// Builds a closed, planet-aligned energy fence from an area's authored perimeter poles.
    /// The existing walls remain environment art; this component supplies the runtime lock.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveAreaBarrier : MonoBehaviour
    {
        private const string RuntimeRootName = "Wave Energy Barrier (Runtime)";
        private const string BarrierShaderResourceName = "S_WaveEnergyBarrier";

        [SerializeField] private GameplayArea area;
        [SerializeField] private Color barrierColor = new Color(0.52f, 0.85f, 1f, 1f);
        [SerializeField, Min(2f)] private float height = 8f;
        [SerializeField, Min(0.1f)] private float thickness = 0.55f;
        [SerializeField, Min(0.5f)] private float maximumSegmentLength = 4f;
        [SerializeField] private bool locked;

        private Transform _runtimeRoot;
        private Material _runtimeMaterial;
        private readonly List<Renderer> _renderers = new List<Renderer>();

        public GameplayArea Area => area;
        public bool IsLocked => locked;

        private void Awake()
        {
            if (area == null)
            {
                area = GetComponent<GameplayArea>();
            }

            Rebuild();
            ApplyLockedState();
        }

        private void Update()
        {
            if (!locked || _runtimeMaterial == null)
            {
                return;
            }

            float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.5f;
            if (_runtimeMaterial.HasProperty("_BarrierColor"))
            {
                _runtimeMaterial.SetColor("_BarrierColor", barrierColor);
                _runtimeMaterial.SetFloat("_Pulse", pulse);
            }
            else
            {
                Color fallbackColor = new Color(
                    barrierColor.r,
                    barrierColor.g,
                    barrierColor.b,
                    0.1f + pulse * 0.05f);
                _runtimeMaterial.color = fallbackColor;
                if (_runtimeMaterial.HasProperty("_BaseColor"))
                {
                    _runtimeMaterial.SetColor("_BaseColor", fallbackColor);
                }
            }
        }

        private void OnDestroy()
        {
            DestroyRuntimeResources();
        }

        public void Configure(GameplayArea configuredArea, Color color, float configuredHeight = 8f)
        {
            area = configuredArea;
            barrierColor = color;
            height = Mathf.Max(2f, configuredHeight);
        }

        public void SetLocked(bool shouldLock)
        {
            locked = shouldLock;
            if (_runtimeRoot == null)
            {
                Rebuild();
            }
            ApplyLockedState();
        }

        public void Rebuild()
        {
            DestroyRuntimeRoot();
            DestroyRuntimeResources();
            _renderers.Clear();

            if (area == null || area.PlanetCenter == null || area.PerimeterPoles == null ||
                area.PerimeterPoles.childCount < 3)
            {
                return;
            }

            var polePositions = new List<Vector3>(area.PerimeterPoles.childCount);
            for (int index = 0; index < area.PerimeterPoles.childCount; index++)
            {
                polePositions.Add(area.PerimeterPoles.GetChild(index).position);
            }

            if (!SphericalPerimeterPolygon.TryCreate(
                    polePositions,
                    area.PlanetCenter.position,
                    out SphericalPerimeterPolygon polygon,
                    out string error))
            {
                Debug.LogError($"Cannot build wave barrier for '{area.name}': {error}", area);
                return;
            }

            var rootObject = new GameObject(RuntimeRootName);
            _runtimeRoot = rootObject.transform;
            _runtimeRoot.SetParent(transform, true);
            _runtimeMaterial = CreateBarrierMaterial();

            IReadOnlyList<Vector3> vertices = polygon.OrderedWorldVertices;
            Vector3 center = polygon.PlanetCenter;
            for (int edge = 0; edge < vertices.Count; edge++)
            {
                Vector3 start = vertices[edge];
                Vector3 end = vertices[(edge + 1) % vertices.Count];
                int divisions = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start, end) / maximumSegmentLength));
                for (int segment = 0; segment < divisions; segment++)
                {
                    float t0 = segment / (float)divisions;
                    float t1 = (segment + 1) / (float)divisions;
                    CreatePanel(
                        InterpolateSurfacePoint(start, end, center, t0),
                        InterpolateSurfacePoint(start, end, center, t1),
                        center,
                        edge,
                        segment);
                }
            }
        }

        private void CreatePanel(Vector3 start, Vector3 end, Vector3 center, int edge, int segment)
        {
            Vector3 midpoint = (start + end) * 0.5f;
            Vector3 radialUp = (midpoint - center).normalized;
            Vector3 tangent = Vector3.ProjectOnPlane(end - start, radialUp).normalized;
            if (tangent.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 outward = Vector3.Cross(tangent, radialUp).normalized;
            float length = Vector3.Distance(start, end) + 0.12f;
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = $"Barrier E{edge:00} S{segment:00}";
            panel.transform.SetParent(_runtimeRoot, true);
            panel.transform.SetPositionAndRotation(
                midpoint + radialUp * (height * 0.5f),
                Quaternion.LookRotation(outward, radialUp));
            panel.transform.localScale = new Vector3(length, height, thickness);

            Renderer renderer = panel.GetComponent<Renderer>();
            renderer.sharedMaterial = _runtimeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _renderers.Add(renderer);
        }

        private static Vector3 InterpolateSurfacePoint(Vector3 start, Vector3 end, Vector3 center, float t)
        {
            Vector3 startRadial = start - center;
            Vector3 endRadial = end - center;
            Vector3 direction = Vector3.Slerp(startRadial.normalized, endRadial.normalized, t).normalized;
            float radius = Mathf.Lerp(startRadial.magnitude, endRadial.magnitude, t);
            return center + direction * radius;
        }

        private Material CreateBarrierMaterial()
        {
            Shader shader = Resources.Load<Shader>(BarrierShaderResourceName) ??
                            Shader.Find("Custom/WaveEnergyBarrier") ??
                            Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Unlit/Transparent") ??
                            Shader.Find("Unlit/Texture") ??
                            Shader.Find("Unlit/Color");
            var material = new Material(shader)
            {
                name = $"{area.name} Wave Barrier (Runtime)"
            };

            if (material.HasProperty("_BarrierColor"))
            {
                material.SetColor("_BarrierColor", barrierColor);
                material.SetFloat("_Pulse", 0.5f);
                return material;
            }

            Color fallbackColor = new Color(barrierColor.r, barrierColor.g, barrierColor.b, 0.12f);
            material.color = fallbackColor;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", fallbackColor);
            }
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", 0f);
                if (material.HasProperty("_SrcBlend"))
                {
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                }
                if (material.HasProperty("_DstBlend"))
                {
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                }
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return material;
        }

        private void ApplyLockedState()
        {
            if (_runtimeRoot != null)
            {
                _runtimeRoot.gameObject.SetActive(locked);
            }
        }

        private void DestroyRuntimeRoot()
        {
            if (_runtimeRoot == null)
            {
                Transform existing = transform.Find(RuntimeRootName);
                _runtimeRoot = existing;
            }
            if (_runtimeRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimeRoot.gameObject);
            }
            else
            {
                DestroyImmediate(_runtimeRoot.gameObject);
            }
            _runtimeRoot = null;
        }

        private void DestroyRuntimeResources()
        {
            if (_runtimeMaterial != null)
            {
                if (Application.isPlaying) Destroy(_runtimeMaterial);
                else DestroyImmediate(_runtimeMaterial);
                _runtimeMaterial = null;
            }

        }
    }
}

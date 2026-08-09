using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace WorldRuntime
{
    /// <summary>
    /// Renders compact baked planet dressing, or compatible legacy authoring roots,
    /// as WebGL2-compatible instanced sector batches. Rock collision objects may
    /// remain in the scene without their original rendering components.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SphericalPropInstancingRenderer : MonoBehaviour
    {
        private const int MaximumInstancesPerDraw = 511;
        private const float MinimumDirectionLengthSquared = 0.0001f;

        [Header("Scene Contracts")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private SphereCollider planetRadiusSource;
        [SerializeField]
        [Tooltip("Valid datasets replace only their matching generated scene-object root. " +
                 "Leave empty to preserve the legacy path; multiple datasets allow " +
                 "vegetation and rocks to migrate independently.")]
        private SphericalPropInstanceData[] bakedInstanceDataSets = Array.Empty<SphericalPropInstanceData>();
        [SerializeField] private string[] generatedRootNames =
        {
            "Generated Planet Vegetation",
            "Generated Planet Rocks"
        };

        [Header("Spherical Culling")]
        [SerializeField, Range(0.25f, 0.5f)]
        [Tooltip("Maximum prop distance as a fraction of the planet diameter. " +
                 "The default 0.375 is midway between one quarter and one half.")]
        private float renderDistanceDiameterFraction = 0.375f;

        [SerializeField, Range(5f, 45f)] private float sectorSizeDegrees = 15f;
        [SerializeField, Range(0f, 30f)] private float horizonPaddingDegrees = 8f;
        [SerializeField] private bool useHorizonCulling = true;
        [SerializeField, Min(1f)] private float fallbackPlanetRadius = 150f;

        [Header("Diagnostics")]
        [SerializeField] private bool logInitializationSummary = true;

        /// <summary>
        /// Baked categories assigned to this renderer. Editor bakers should set this
        /// on the SampleScene instance, rather than applying it to Planet.prefab.
        /// </summary>
        public IReadOnlyList<SphericalPropInstanceData> BakedInstanceDataSets =>
            bakedInstanceDataSets;

        /// <summary>
        /// True while one or more cinematic callers have requested that distance
        /// culling be bypassed. Frustum and spherical-horizon culling remain active.
        /// </summary>
        public bool IsFullPlanetVisibilityRequested =>
            _fullPlanetVisibilityRequestCount > 0;

        /// <summary>
        /// Replaces the assigned baked categories. Intended for Edit-mode baking tools;
        /// the caller owns Undo, scene dirtiness, and saving the SampleScene override.
        /// </summary>
        public void SetBakedInstanceDataSets(SphericalPropInstanceData[] dataSets)
        {
            bakedInstanceDataSets = dataSets != null
                ? (SphericalPropInstanceData[])dataSets.Clone()
                : Array.Empty<SphericalPropInstanceData>();
        }

        private readonly Plane[] _frustumPlanes = new Plane[6];
        private readonly List<RendererState> _sourceRenderers = new();
        private readonly List<Sector> _sectors = new();
        private bool _initialized;
        private bool _reportedUnsupportedInstancing;
        private bool _reportedInvalidBakedData;
        private int _fullPlanetVisibilityRequestCount;
        private Vector3 _planetCenter;
        private float _planetRadius;

        /// <summary>
        /// Temporarily bypasses maximum-distance culling so a distant cinematic camera
        /// can render all props on the visible face of the planet. Dispose the returned
        /// request to restore the normal gameplay distance. Requests may be nested.
        /// </summary>
        public IDisposable RequestFullPlanetVisibility()
        {
            _fullPlanetVisibilityRequestCount++;
            return new FullPlanetVisibilityRequest(this);
        }

        private void Awake()
        {
            TryInitialize();
        }

        private void OnEnable()
        {
            if (Application.isPlaying && !_initialized)
            {
                TryInitialize();
            }
        }

        private void LateUpdate()
        {
            if (!_initialized && !TryInitialize())
            {
                return;
            }

            Camera camera = ResolveCamera();
            if (camera == null || !camera.isActiveAndEnabled)
            {
                return;
            }

            try
            {
                RenderVisibleSectors(camera);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "Planet prop instancing failed and restored the source renderers.\n" +
                    exception,
                    this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            RestoreSourceRenderers();
        }

        private bool TryInitialize()
        {
            if (!Application.isPlaying || _initialized)
            {
                return _initialized;
            }

            if (!SystemInfo.supportsInstancing)
            {
                if (!_reportedUnsupportedInstancing)
                {
                    Debug.LogWarning(
                        "GPU instancing is unavailable; planet props will use their source " +
                        "MeshRenderers.",
                        this);
                    _reportedUnsupportedInstancing = true;
                }

                return false;
            }

            if (ResolveCamera() == null)
            {
                return false;
            }

            ResolvePlanetGeometry();
            try
            {
                int capturedCount = BuildSectors();
                if (capturedCount == 0)
                {
                    ClearRuntimeData();
                    return false;
                }

                _initialized = true;
                if (logInitializationSummary)
                {
                    int drawBatchCount = 0;
                    for (int index = 0; index < _sectors.Count; index++)
                    {
                        drawBatchCount += _sectors[index].Batches.Count;
                    }

                    float maximumDistance =
                        _planetRadius * 2f * renderDistanceDiameterFraction;
                    Debug.Log(
                        $"Planet prop instancing captured {capturedCount:N0} prop instances into " +
                        $"{_sectors.Count:N0} spherical sectors and {drawBatchCount:N0} " +
                        $"instanced draw batches. Maximum prop distance is " +
                        $"{maximumDistance:0.#} world units.",
                        this);
                }

                return true;
            }
            catch
            {
                RestoreSourceRenderers();
                throw;
            }
        }

        private int BuildSectors()
        {
            var sectorsByKey = new Dictionary<SectorKey, Sector>();
            var bakedRootNames = new HashSet<string>(StringComparer.Ordinal);
            int capturedCount = 0;

            for (int dataIndex = 0;
                 bakedInstanceDataSets != null && dataIndex < bakedInstanceDataSets.Length;
                 dataIndex++)
            {
                SphericalPropInstanceData data = bakedInstanceDataSets[dataIndex];
                if (data == null ||
                    bakedRootNames.Contains(data.SourceRootName) ||
                    !TryAppendBakedData(data, sectorsByKey, out int dataInstanceCount))
                {
                    continue;
                }

                bakedRootNames.Add(data.SourceRootName);
                DisableSourceRootRenderers(data.SourceRootName);
                capturedCount += dataInstanceCount;
            }

            capturedCount += AppendSceneRootRenderers(sectorsByKey, bakedRootNames);
            foreach (Sector sector in sectorsByKey.Values)
            {
                sector.FinalizeBatches();
                _sectors.Add(sector);
            }

            return capturedCount;
        }

        private void DisableSourceRootRenderers(string rootName)
        {
            GameObject root = FindRoot(gameObject.scene.GetRootGameObjects(), rootName);
            if (root == null)
            {
                return;
            }

            var renderers = new List<MeshRenderer>();
            root.GetComponentsInChildren(false, renderers);
            for (int index = 0; index < renderers.Count; index++)
            {
                MeshRenderer renderer = renderers[index];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                _sourceRenderers.Add(new RendererState(renderer, true));
                renderer.enabled = false;
            }
        }

        private int AppendSceneRootRenderers(
            IDictionary<SectorKey, Sector> sectorsByKey,
            ISet<string> excludedRootNames)
        {
            var rendererBuffer = new List<MeshRenderer>(16000);
            var materialBuffer = new List<Material>(4);
            GameObject[] sceneRoots = gameObject.scene.GetRootGameObjects();
            int capturedCount = 0;

            for (int rootNameIndex = 0;
                 rootNameIndex < generatedRootNames.Length;
                 rootNameIndex++)
            {
                string rootName = generatedRootNames[rootNameIndex];
                if (excludedRootNames.Contains(rootName))
                {
                    continue;
                }

                GameObject generatedRoot = FindRoot(sceneRoots, rootName);
                if (generatedRoot == null)
                {
                    Debug.LogWarning(
                        $"Planet prop instancing could not find scene root '{rootName}'.",
                        this);
                    continue;
                }

                rendererBuffer.Clear();
                generatedRoot.GetComponentsInChildren(false, rendererBuffer);
                for (int rendererIndex = 0;
                     rendererIndex < rendererBuffer.Count;
                     rendererIndex++)
                {
                    MeshRenderer renderer = rendererBuffer[rendererIndex];
                    if (!TryCaptureRenderer(
                            renderer,
                            sectorsByKey,
                            materialBuffer))
                    {
                        continue;
                    }

                    capturedCount++;
                }
            }

            return capturedCount;
        }

        private bool TryAppendBakedData(
            SphericalPropInstanceData data,
            IDictionary<SectorKey, Sector> sectorsByKey,
            out int capturedCount)
        {
            capturedCount = 0;
            if (string.IsNullOrWhiteSpace(data.SourceRootName) ||
                data.PrototypeCount == 0 ||
                data.InstanceCount == 0)
            {
                ReportInvalidBakedData(
                    $"'{data.name}' has no source root, prototypes, or instances.");
                return false;
            }

            IReadOnlyList<SphericalPropInstanceData.Prototype> prototypes = data.Prototypes;
            IReadOnlyList<SphericalPropInstanceData.Instance> instances = data.Instances;
            for (int prototypeIndex = 0; prototypeIndex < prototypes.Count; prototypeIndex++)
            {
                if (!IsValidBakedPrototype(prototypes[prototypeIndex], out string validationError))
                {
                    ReportInvalidBakedData(
                        $"'{data.name}' prototype {prototypeIndex} is invalid: {validationError}");
                    return false;
                }
            }

            for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
            {
                SphericalPropInstanceData.Instance instance = instances[instanceIndex];
                if (instance.PrototypeIndex < 0 ||
                    instance.PrototypeIndex >= prototypes.Count)
                {
                    ReportInvalidBakedData(
                        $"'{data.name}' instance {instanceIndex} references prototype " +
                        $"{instance.PrototypeIndex}, but only {prototypes.Count} exist.");
                    return false;
                }
            }

            for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
            {
                SphericalPropInstanceData.Instance instance = instances[instanceIndex];
                if (AddBakedInstance(
                    prototypes[instance.PrototypeIndex],
                    transform.localToWorldMatrix * instance.BuildLocalMatrix(),
                    sectorsByKey))
                {
                    capturedCount++;
                }
            }

            return capturedCount > 0;
        }

        private static bool IsValidBakedPrototype(
            SphericalPropInstanceData.Prototype prototype,
            out string validationError)
        {
            Mesh mesh = prototype.Mesh;
            if (mesh == null || mesh.subMeshCount <= 0)
            {
                validationError = "it has no mesh or mesh submeshes";
                return false;
            }

            for (int submeshIndex = 0; submeshIndex < mesh.subMeshCount; submeshIndex++)
            {
                Material material = prototype.GetMaterialForSubmesh(submeshIndex);
                if (material == null)
                {
                    validationError = $"submesh {submeshIndex} has no material";
                    return false;
                }

                if (!material.enableInstancing)
                {
                    validationError =
                        $"material '{material.name}' does not have GPU instancing enabled";
                    return false;
                }
            }

            validationError = null;
            return true;
        }

        private bool AddBakedInstance(
            SphericalPropInstanceData.Prototype prototype,
            Matrix4x4 matrix,
            IDictionary<SectorKey, Sector> sectorsByKey)
        {
            Bounds instanceBounds = TransformBounds(prototype.Mesh.bounds, matrix);
            Vector3 fromCenter = instanceBounds.center - _planetCenter;
            if (fromCenter.sqrMagnitude <= MinimumDirectionLengthSquared)
            {
                return false;
            }

            Vector3 direction = fromCenter.normalized;
            SectorKey sectorKey = SectorKey.FromDirection(direction, sectorSizeDegrees);
            if (!sectorsByKey.TryGetValue(sectorKey, out Sector sector))
            {
                sector = new Sector();
                sectorsByKey.Add(sectorKey, sector);
            }

            float radialDistance = Mathf.Max(fromCenter.magnitude, 0.001f);
            float angularExtent = Mathf.Asin(Mathf.Clamp01(
                instanceBounds.extents.magnitude / radialDistance)) * Mathf.Rad2Deg;
            sector.IncludeRenderer(instanceBounds, direction, angularExtent);

            Mesh mesh = prototype.Mesh;
            for (int submeshIndex = 0; submeshIndex < mesh.subMeshCount; submeshIndex++)
            {
                var drawKey = new DrawKey(
                    mesh,
                    prototype.GetMaterialForSubmesh(submeshIndex),
                    submeshIndex,
                    prototype.Layer,
                    prototype.ShadowCastingMode,
                    prototype.ReceiveShadows,
                    prototype.RenderingLayerMask,
                    prototype.LightProbeUsage,
                    prototype.ReflectionProbeUsage);
                sector.AddInstance(drawKey, matrix, instanceBounds);
            }

            return true;
        }

        private void ReportInvalidBakedData(string message)
        {
            if (_reportedInvalidBakedData)
            {
                return;
            }

            Debug.LogWarning(
                $"Planet prop instancing will use its source MeshRenderers because baked " +
                $"instance data {message}",
                this);
            _reportedInvalidBakedData = true;
        }

        private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
        {
            Vector3 localExtents = localBounds.extents;
            Vector3 worldExtents = new Vector3(
                Mathf.Abs(matrix.m00) * localExtents.x +
                Mathf.Abs(matrix.m01) * localExtents.y +
                Mathf.Abs(matrix.m02) * localExtents.z,
                Mathf.Abs(matrix.m10) * localExtents.x +
                Mathf.Abs(matrix.m11) * localExtents.y +
                Mathf.Abs(matrix.m12) * localExtents.z,
                Mathf.Abs(matrix.m20) * localExtents.x +
                Mathf.Abs(matrix.m21) * localExtents.y +
                Mathf.Abs(matrix.m22) * localExtents.z);
            return new Bounds(matrix.MultiplyPoint3x4(localBounds.center), worldExtents * 2f);
        }

        private bool TryCaptureRenderer(
            MeshRenderer renderer,
            IDictionary<SectorKey, Sector> sectorsByKey,
            List<Material> materialBuffer)
        {
            if (renderer == null ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy ||
                renderer.HasPropertyBlock() ||
                renderer.lightmapIndex >= 0)
            {
                return false;
            }

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (mesh == null || mesh.subMeshCount <= 0)
            {
                return false;
            }

            materialBuffer.Clear();
            renderer.GetSharedMaterials(materialBuffer);
            if (materialBuffer.Count == 0)
            {
                return false;
            }

            for (int submeshIndex = 0; submeshIndex < mesh.subMeshCount; submeshIndex++)
            {
                Material material = materialBuffer[
                    Mathf.Min(submeshIndex, materialBuffer.Count - 1)];
                if (material == null || !material.enableInstancing)
                {
                    return false;
                }
            }

            Bounds rendererBounds = renderer.bounds;
            Vector3 fromCenter = rendererBounds.center - _planetCenter;
            if (fromCenter.sqrMagnitude <= MinimumDirectionLengthSquared)
            {
                return false;
            }

            Vector3 direction = fromCenter.normalized;
            SectorKey sectorKey = SectorKey.FromDirection(direction, sectorSizeDegrees);
            if (!sectorsByKey.TryGetValue(sectorKey, out Sector sector))
            {
                sector = new Sector();
                sectorsByKey.Add(sectorKey, sector);
            }

            float radialDistance = Mathf.Max(fromCenter.magnitude, 0.001f);
            float angularExtent = Mathf.Asin(Mathf.Clamp01(
                rendererBounds.extents.magnitude / radialDistance)) * Mathf.Rad2Deg;
            sector.IncludeRenderer(rendererBounds, direction, angularExtent);

            Matrix4x4 matrix = meshFilter.transform.localToWorldMatrix;
            for (int submeshIndex = 0; submeshIndex < mesh.subMeshCount; submeshIndex++)
            {
                Material material = materialBuffer[
                    Mathf.Min(submeshIndex, materialBuffer.Count - 1)];
                var drawKey = new DrawKey(
                    mesh,
                    material,
                    submeshIndex,
                    renderer.gameObject.layer,
                    renderer.shadowCastingMode,
                    renderer.receiveShadows,
                    renderer.renderingLayerMask,
                    renderer.lightProbeUsage,
                    renderer.reflectionProbeUsage);
                sector.AddInstance(drawKey, matrix, rendererBounds);
            }

            _sourceRenderers.Add(new RendererState(renderer, renderer.enabled));
            renderer.enabled = false;
            return true;
        }

        private void RenderVisibleSectors(Camera camera)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            Vector3 cameraPosition = camera.transform.position;
            Vector3 cameraOffset = cameraPosition - _planetCenter;
            float cameraRadius = cameraOffset.magnitude;
            Vector3 cameraDirection = cameraRadius > 0.001f
                ? cameraOffset / cameraRadius
                : Vector3.up;
            float maximumDistance = _planetRadius * 2f * renderDistanceDiameterFraction;
            float maximumDistanceSquared = maximumDistance * maximumDistance;
            float horizonAngle = cameraRadius > _planetRadius
                ? Mathf.Acos(Mathf.Clamp01(_planetRadius / cameraRadius)) * Mathf.Rad2Deg
                : 180f;

            for (int sectorIndex = 0; sectorIndex < _sectors.Count; sectorIndex++)
            {
                Sector sector = _sectors[sectorIndex];
                if ((!IsFullPlanetVisibilityRequested &&
                     sector.Bounds.SqrDistance(cameraPosition) > maximumDistanceSquared) ||
                    !GeometryUtility.TestPlanesAABB(_frustumPlanes, sector.Bounds) ||
                    !IsAboveHorizon(sector, cameraDirection, horizonAngle))
                {
                    continue;
                }

                for (int batchIndex = 0;
                     batchIndex < sector.Batches.Count;
                     batchIndex++)
                {
                    RenderBatch batch = sector.Batches[batchIndex];
                    batch.Parameters.camera = camera;
                    Graphics.RenderMeshInstanced(
                        batch.Parameters,
                        batch.Mesh,
                        batch.SubmeshIndex,
                        batch.Matrices,
                        batch.Matrices.Length,
                        0);
                }
            }
        }

        private bool IsAboveHorizon(
            Sector sector,
            Vector3 cameraDirection,
            float horizonAngle)
        {
            if (!useHorizonCulling || horizonAngle >= 180f)
            {
                return true;
            }

            float sectorAngle = Vector3.Angle(cameraDirection, sector.Direction);
            return sectorAngle <=
                   horizonAngle + sector.AngularRadiusDegrees + horizonPaddingDegrees;
        }

        private Camera ResolveCamera()
        {
            if (targetCamera != null && targetCamera.isActiveAndEnabled)
            {
                return targetCamera;
            }

            targetCamera = Camera.main;
            return targetCamera;
        }

        private void ReleaseFullPlanetVisibility()
        {
            _fullPlanetVisibilityRequestCount =
                Mathf.Max(0, _fullPlanetVisibilityRequestCount - 1);
        }

        private sealed class FullPlanetVisibilityRequest : IDisposable
        {
            private SphericalPropInstancingRenderer _owner;

            public FullPlanetVisibilityRequest(SphericalPropInstancingRenderer owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                SphericalPropInstancingRenderer owner = _owner;
                _owner = null;
                if (owner != null)
                {
                    owner.ReleaseFullPlanetVisibility();
                }
            }
        }

        private void ResolvePlanetGeometry()
        {
            SphereCollider radiusSource = planetRadiusSource;
            if (radiusSource == null)
            {
                radiusSource = GetComponent<SphereCollider>();
            }

            if (radiusSource != null)
            {
                Vector3 scale = radiusSource.transform.lossyScale;
                float maximumScale = Mathf.Max(
                    Mathf.Abs(scale.x),
                    Mathf.Abs(scale.y),
                    Mathf.Abs(scale.z));
                _planetCenter = radiusSource.transform.TransformPoint(radiusSource.center);
                _planetRadius = Mathf.Max(1f, radiusSource.radius * maximumScale);
                return;
            }

            _planetCenter = transform.position;
            _planetRadius = Mathf.Max(1f, fallbackPlanetRadius);
        }

        private void RestoreSourceRenderers()
        {
            for (int index = 0; index < _sourceRenderers.Count; index++)
            {
                RendererState state = _sourceRenderers[index];
                if (state.Renderer != null)
                {
                    state.Renderer.enabled = state.WasEnabled;
                }
            }

            ClearRuntimeData();
        }

        private void ClearRuntimeData()
        {
            _sourceRenderers.Clear();
            _sectors.Clear();
            _initialized = false;
        }

        private static GameObject FindRoot(IReadOnlyList<GameObject> roots, string name)
        {
            for (int index = 0; index < roots.Count; index++)
            {
                if (roots[index].name == name)
                {
                    return roots[index];
                }
            }

            return null;
        }

        private readonly struct RendererState
        {
            public RendererState(MeshRenderer renderer, bool wasEnabled)
            {
                Renderer = renderer;
                WasEnabled = wasEnabled;
            }

            public MeshRenderer Renderer { get; }
            public bool WasEnabled { get; }
        }

        private readonly struct SectorKey : IEquatable<SectorKey>
        {
            private SectorKey(int latitude, int longitude)
            {
                Latitude = latitude;
                Longitude = longitude;
            }

            private int Latitude { get; }
            private int Longitude { get; }

            public static SectorKey FromDirection(Vector3 direction, float sizeDegrees)
            {
                float latitude = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) *
                                 Mathf.Rad2Deg;
                float longitude = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
                return new SectorKey(
                    Mathf.FloorToInt((latitude + 90f) / sizeDegrees),
                    Mathf.FloorToInt((longitude + 180f) / sizeDegrees));
            }

            public bool Equals(SectorKey other)
            {
                return Latitude == other.Latitude && Longitude == other.Longitude;
            }

            public override bool Equals(object obj)
            {
                return obj is SectorKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Latitude * 397) ^ Longitude;
                }
            }
        }

        private readonly struct AngularSample
        {
            public AngularSample(Vector3 direction, float extentDegrees)
            {
                Direction = direction;
                ExtentDegrees = extentDegrees;
            }

            public Vector3 Direction { get; }
            public float ExtentDegrees { get; }
        }

        private readonly struct InstanceDraw
        {
            public InstanceDraw(Matrix4x4 matrix, Bounds bounds)
            {
                Matrix = matrix;
                Bounds = bounds;
            }

            public Matrix4x4 Matrix { get; }
            public Bounds Bounds { get; }
        }

        private readonly struct DrawKey : IEquatable<DrawKey>
        {
            public DrawKey(
                Mesh mesh,
                Material material,
                int submeshIndex,
                int layer,
                ShadowCastingMode shadowCastingMode,
                bool receiveShadows,
                uint renderingLayerMask,
                LightProbeUsage lightProbeUsage,
                ReflectionProbeUsage reflectionProbeUsage)
            {
                Mesh = mesh;
                Material = material;
                SubmeshIndex = submeshIndex;
                Layer = layer;
                ShadowCastingMode = shadowCastingMode;
                ReceiveShadows = receiveShadows;
                RenderingLayerMask = renderingLayerMask;
                LightProbeUsage = lightProbeUsage;
                ReflectionProbeUsage = reflectionProbeUsage;
            }

            public Mesh Mesh { get; }
            public Material Material { get; }
            public int SubmeshIndex { get; }
            public int Layer { get; }
            public ShadowCastingMode ShadowCastingMode { get; }
            public bool ReceiveShadows { get; }
            public uint RenderingLayerMask { get; }
            public LightProbeUsage LightProbeUsage { get; }
            public ReflectionProbeUsage ReflectionProbeUsage { get; }

            public bool Equals(DrawKey other)
            {
                return Mesh == other.Mesh &&
                       Material == other.Material &&
                       SubmeshIndex == other.SubmeshIndex &&
                       Layer == other.Layer &&
                       ShadowCastingMode == other.ShadowCastingMode &&
                       ReceiveShadows == other.ReceiveShadows &&
                       RenderingLayerMask == other.RenderingLayerMask &&
                       LightProbeUsage == other.LightProbeUsage &&
                       ReflectionProbeUsage == other.ReflectionProbeUsage;
            }

            public override bool Equals(object obj)
            {
                return obj is DrawKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = Mesh != null ? Mesh.GetInstanceID() : 0;
                    hashCode = (hashCode * 397) ^
                               (Material != null ? Material.GetInstanceID() : 0);
                    hashCode = (hashCode * 397) ^ SubmeshIndex;
                    hashCode = (hashCode * 397) ^ Layer;
                    hashCode = (hashCode * 397) ^ (int)ShadowCastingMode;
                    hashCode = (hashCode * 397) ^ ReceiveShadows.GetHashCode();
                    hashCode = (hashCode * 397) ^ (int)RenderingLayerMask;
                    hashCode = (hashCode * 397) ^ (int)LightProbeUsage;
                    hashCode = (hashCode * 397) ^ (int)ReflectionProbeUsage;
                    return hashCode;
                }
            }
        }

        private sealed class DrawGroup
        {
            public DrawGroup(DrawKey key)
            {
                Key = key;
            }

            public DrawKey Key { get; }
            public List<InstanceDraw> Instances { get; } = new();
        }

        private sealed class Sector
        {
            private readonly List<AngularSample> _angularSamples = new();
            private readonly Dictionary<DrawKey, DrawGroup> _groups = new();
            private bool _hasBounds;
            private Vector3 _directionSum;

            public Bounds Bounds { get; private set; }
            public Vector3 Direction { get; private set; }
            public float AngularRadiusDegrees { get; private set; }
            public List<RenderBatch> Batches { get; } = new();

            public void IncludeRenderer(
                Bounds bounds,
                Vector3 direction,
                float angularExtentDegrees)
            {
                if (_hasBounds)
                {
                    Bounds combined = Bounds;
                    combined.Encapsulate(bounds);
                    Bounds = combined;
                }
                else
                {
                    Bounds = bounds;
                    _hasBounds = true;
                }

                _directionSum += direction;
                _angularSamples.Add(new AngularSample(direction, angularExtentDegrees));
            }

            public void AddInstance(DrawKey key, Matrix4x4 matrix, Bounds bounds)
            {
                if (!_groups.TryGetValue(key, out DrawGroup group))
                {
                    group = new DrawGroup(key);
                    _groups.Add(key, group);
                }

                group.Instances.Add(new InstanceDraw(matrix, bounds));
            }

            public void FinalizeBatches()
            {
                Direction = _directionSum.sqrMagnitude > MinimumDirectionLengthSquared
                    ? _directionSum.normalized
                    : Vector3.up;
                float maximumAngle = 0f;
                for (int index = 0; index < _angularSamples.Count; index++)
                {
                    AngularSample sample = _angularSamples[index];
                    maximumAngle = Mathf.Max(
                        maximumAngle,
                        Vector3.Angle(Direction, sample.Direction) + sample.ExtentDegrees);
                }

                AngularRadiusDegrees = maximumAngle;
                foreach (DrawGroup group in _groups.Values)
                {
                    CreateBatches(group);
                }

                _angularSamples.Clear();
                _groups.Clear();
            }

            private void CreateBatches(DrawGroup group)
            {
                List<InstanceDraw> instances = group.Instances;
                for (int start = 0;
                     start < instances.Count;
                     start += MaximumInstancesPerDraw)
                {
                    int count = Mathf.Min(MaximumInstancesPerDraw, instances.Count - start);
                    var matrices = new Matrix4x4[count];
                    Bounds batchBounds = instances[start].Bounds;
                    for (int index = 0; index < count; index++)
                    {
                        InstanceDraw instance = instances[start + index];
                        matrices[index] = instance.Matrix;
                        if (index > 0)
                        {
                            batchBounds.Encapsulate(instance.Bounds);
                        }
                    }

                    var parameters = new RenderParams(group.Key.Material)
                    {
                        worldBounds = batchBounds,
                        layer = group.Key.Layer,
                        shadowCastingMode = group.Key.ShadowCastingMode,
                        receiveShadows = group.Key.ReceiveShadows,
                        renderingLayerMask = group.Key.RenderingLayerMask,
                        lightProbeUsage = group.Key.LightProbeUsage,
                        reflectionProbeUsage = group.Key.ReflectionProbeUsage,
                        motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
                    };
                    Batches.Add(new RenderBatch(
                        group.Key.Mesh,
                        group.Key.SubmeshIndex,
                        matrices,
                        parameters));
                }
            }
        }

        private sealed class RenderBatch
        {
            public RenderBatch(
                Mesh mesh,
                int submeshIndex,
                Matrix4x4[] matrices,
                RenderParams parameters)
            {
                Mesh = mesh;
                SubmeshIndex = submeshIndex;
                Matrices = matrices;
                Parameters = parameters;
            }

            public Mesh Mesh { get; }
            public int SubmeshIndex { get; }
            public Matrix4x4[] Matrices { get; }
            public RenderParams Parameters;
        }
    }
}

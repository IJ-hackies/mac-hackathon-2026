using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WorldRuntime;

namespace WorldEditor
{
    /// <summary>
    /// Converts generated planet dressing into compact binary instance assets.
    /// Vegetation source objects are removed after a successful bake. Rock transforms
    /// and MeshColliders remain in the scene while their rendering components are removed.
    /// </summary>
    public static class PlanetPropBakeTool
    {
        public const string VegetationRootName = "Generated Planet Vegetation";
        public const string RockRootName = "Generated Planet Rocks";

        private const string PlanetRootName = "Planet Ground";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string BakeFolder = "Assets/Art/Generated/PlanetProps";
        private const string VegetationAssetPath =
            BakeFolder + "/SampleScene_Vegetation.asset";
        private const string RockAssetPath =
            BakeFolder + "/SampleScene_Rocks.asset";
        private const string BakeRequestFile = "BakePlanetProps.once";
        private const int ExpectedVegetationCount = 16000;
        private const int ExpectedRockCount = 1100;
        private const float MatrixTolerance = 0.0025f;

        [InitializeOnLoadMethod]
        private static void RegisterRequestedBakePoll()
        {
            EditorApplication.update -= RunRequestedBake;
            EditorApplication.update += RunRequestedBake;
        }

        private static void RunRequestedBake()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return;
            }

            string requestPath = Path.Combine(projectRoot, "Temp", BakeRequestFile);
            if (!File.Exists(requestPath))
            {
                return;
            }

            EditorApplication.update -= RunRequestedBake;
            File.Delete(requestPath);
            EditorApplication.delayCall += () =>
            {
                try
                {
                    BakeAllInOpenScene();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Tools/Planet Design/Bake Planet Props for Runtime")]
        public static void BakeAllInOpenScene()
        {
            BakeResult result = BakeAll(SceneManager.GetActiveScene());
            Debug.Log(
                $"Planet prop bake complete: {result.VegetationCount:N0} vegetation and " +
                $"{result.RockCount:N0} rocks now use compact instance data. " +
                $"{result.RockColliderCount:N0} rock colliders remain in the scene.");
        }

        [MenuItem("Tools/Planet Design/Bake Planet Props for Runtime", true)]
        private static bool ValidateBakeAllInOpenScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode &&
                   SceneManager.GetActiveScene().path == SampleScenePath;
        }

        [MenuItem("Tools/Planet Design/Validate Baked Planet Props")]
        public static void ValidateBakedPlanetProps()
        {
            BakeResult result = ValidateBakedScene(SceneManager.GetActiveScene());
            Debug.Log(
                $"Baked planet props are valid: {result.VegetationCount:N0} vegetation, " +
                $"{result.RockCount:N0} rocks, and {result.RockColliderCount:N0} " +
                "preserved rock colliders.");
        }

        [MenuItem("Tools/Planet Design/Validate Baked Planet Props", true)]
        private static bool ValidateBakedPlanetPropsMenu()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode &&
                   SceneManager.GetActiveScene().path == SampleScenePath;
        }

        public static BakeResult BakeAll(Scene scene)
        {
            ValidateSceneForMutation(scene);
            SphericalPropInstancingRenderer renderer = FindRenderer(scene);
            GameObject vegetationRoot = FindRoot(scene, VegetationRootName);
            GameObject rockRoot = FindRoot(scene, RockRootName);

            SphericalPropInstanceData vegetationData = FindAssignedDataSet(
                renderer,
                VegetationRootName);
            SphericalPropInstanceData rockData = FindAssignedDataSet(renderer, RockRootName);

            BakedCategory vegetationBake = null;
            BakedCategory rockBake = null;

            if (vegetationRoot != null)
            {
                vegetationBake = CollectCategory(
                    vegetationRoot,
                    renderer.transform,
                    ExpectedVegetationCount);
            }
            else if (!IsValidDataSet(
                         vegetationData,
                         VegetationRootName,
                         ExpectedVegetationCount,
                         out string vegetationError))
            {
                throw new InvalidOperationException(
                    $"No '{VegetationRootName}' authoring root exists and its current " +
                    $"bake is unusable: {vegetationError}");
            }

            if (rockRoot != null &&
                rockRoot.GetComponentsInChildren<MeshRenderer>(true).Length > 0)
            {
                ValidateRockColliders(rockRoot, ExpectedRockCount);
                rockBake = CollectCategory(
                    rockRoot,
                    renderer.transform,
                    ExpectedRockCount);
            }
            else
            {
                if (!IsValidDataSet(
                        rockData,
                        RockRootName,
                        ExpectedRockCount,
                        out string rockError))
                {
                    throw new InvalidOperationException(
                        $"No rendered '{RockRootName}' authoring root exists and its " +
                        $"current bake is unusable: {rockError}");
                }

                if (rockRoot == null)
                {
                    throw new InvalidOperationException(
                        $"The collider-only '{RockRootName}' root is missing.");
                }

                ValidateRockColliders(rockRoot, ExpectedRockCount);
            }

            EnsureFolder(BakeFolder);
            if (vegetationBake != null)
            {
                vegetationData = WriteDataAsset(
                    VegetationAssetPath,
                    VegetationRootName,
                    vegetationBake);
            }

            if (rockBake != null)
            {
                rockData = WriteDataAsset(RockAssetPath, RockRootName, rockBake);
            }

            AssignDataSets(renderer, vegetationData, rockData);
            AssetDatabase.SaveAssets();

            bool writtenVegetationValid = IsValidDataSet(
                vegetationData,
                VegetationRootName,
                ExpectedVegetationCount,
                out string writtenVegetationError);
            bool writtenRockValid = IsValidDataSet(
                rockData,
                RockRootName,
                ExpectedRockCount,
                out string writtenRockError);
            if (!writtenVegetationValid || !writtenRockValid)
            {
                throw new InvalidOperationException(
                    "The written instance assets failed validation. Vegetation: " +
                    $"{writtenVegetationError}; rocks: {writtenRockError}");
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bake Planet Props for Runtime");
            if (vegetationRoot != null)
            {
                Undo.DestroyObjectImmediate(vegetationRoot);
            }

            if (rockBake != null)
            {
                StripRockRenderingComponents(rockRoot);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    $"Planet prop bake could not save '{scene.path}'.");
            }

            return ValidateBakedScene(scene);
        }

        /// <summary>
        /// Removes one stale category assignment after its authoring root is regenerated.
        /// The other category remains baked and continues using compact data.
        /// </summary>
        public static void ClearBakedCategory(Scene scene, string sourceRootName)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            SphericalPropInstancingRenderer renderer = FindRenderer(scene);
            SphericalPropInstanceData[] retained = renderer.BakedInstanceDataSets
                .Where(data => data != null && data.SourceRootName != sourceRootName)
                .ToArray();
            if (retained.Length == renderer.BakedInstanceDataSets.Count)
            {
                return;
            }

            Undo.RecordObject(renderer, "Clear Stale Planet Prop Bake");
            renderer.SetBakedInstanceDataSets(retained);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static BakeResult ValidateBakedScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.path != SampleScenePath)
            {
                throw new InvalidOperationException(
                    $"Baked prop validation requires the open '{SampleScenePath}'.");
            }

            SphericalPropInstancingRenderer renderer = FindRenderer(scene);
            SphericalPropInstanceData vegetationData = FindAssignedDataSet(
                renderer,
                VegetationRootName);
            SphericalPropInstanceData rockData = FindAssignedDataSet(renderer, RockRootName);

            if (!IsValidDataSet(
                    vegetationData,
                    VegetationRootName,
                    ExpectedVegetationCount,
                    out string vegetationError))
            {
                throw new InvalidOperationException(
                    $"Vegetation bake validation failed: {vegetationError}");
            }

            if (!IsValidDataSet(
                    rockData,
                    RockRootName,
                    ExpectedRockCount,
                    out string rockError))
            {
                throw new InvalidOperationException(
                    $"Rock bake validation failed: {rockError}");
            }

            if (FindRoot(scene, VegetationRootName) != null)
            {
                throw new InvalidOperationException(
                    $"'{VegetationRootName}' still exists in the runtime scene.");
            }

            GameObject rockRoot = FindRoot(scene, RockRootName);
            if (rockRoot == null)
            {
                throw new InvalidOperationException(
                    $"Collider-only '{RockRootName}' is missing.");
            }

            if (rockRoot.GetComponentsInChildren<MeshRenderer>(true).Length != 0 ||
                rockRoot.GetComponentsInChildren<MeshFilter>(true).Length != 0)
            {
                throw new InvalidOperationException(
                    $"'{RockRootName}' still contains rendering components.");
            }

            int colliderCount = ValidateRockColliders(rockRoot, ExpectedRockCount);
            return new BakeResult(
                vegetationData.InstanceCount,
                rockData.InstanceCount,
                colliderCount);
        }

        private static void ValidateSceneForMutation(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Planet prop baking can only run in Edit mode.");
            }

            if (!scene.IsValid() || !scene.isLoaded || scene.path != SampleScenePath)
            {
                throw new InvalidOperationException(
                    $"Open '{SampleScenePath}' as the active scene before baking props.");
            }

            if (scene.isDirty)
            {
                throw new InvalidOperationException(
                    "Save the open SampleScene before baking so unrelated unsaved edits " +
                    "cannot be folded into the generated prop migration.");
            }
        }

        private static BakedCategory CollectCategory(
            GameObject sourceRoot,
            Transform rendererTransform,
            int expectedCount)
        {
            MeshRenderer[] renderers = sourceRoot
                .GetComponentsInChildren<MeshRenderer>(false)
                .OrderBy(GetStableHierarchyPath, StringComparer.Ordinal)
                .ToArray();
            if (renderers.Length != expectedCount)
            {
                throw new InvalidOperationException(
                    $"'{sourceRoot.name}' contains {renderers.Length:N0} active renderers; " +
                    $"expected exactly {expectedCount:N0}.");
            }

            var prototypes = new List<SphericalPropInstanceData.Prototype>();
            var prototypeIndexes = new Dictionary<PrototypeKey, int>();
            var instances = new SphericalPropInstanceData.Instance[renderers.Length];
            var materialBuffer = new List<Material>(4);
            Matrix4x4 worldToRenderer = rendererTransform.worldToLocalMatrix;

            for (int index = 0; index < renderers.Length; index++)
            {
                MeshRenderer meshRenderer = renderers[index];
                ValidateRenderer(meshRenderer, materialBuffer, out Mesh mesh);
                var key = new PrototypeKey(meshRenderer, mesh, materialBuffer);
                if (!prototypeIndexes.TryGetValue(key, out int prototypeIndex))
                {
                    prototypeIndex = prototypes.Count;
                    prototypeIndexes.Add(key, prototypeIndex);
                    prototypes.Add(key.CreatePrototype());
                }

                Matrix4x4 localMatrix =
                    worldToRenderer * meshRenderer.transform.localToWorldMatrix;
                DecomposeMatrix(
                    localMatrix,
                    out Vector3 position,
                    out Quaternion rotation,
                    out Vector3 scale);
                instances[index] = new SphericalPropInstanceData.Instance(
                    prototypeIndex,
                    position,
                    rotation,
                    scale);
            }

            return new BakedCategory(prototypes.ToArray(), instances);
        }

        private static void ValidateRenderer(
            MeshRenderer renderer,
            List<Material> materialBuffer,
            out Mesh mesh)
        {
            if (renderer == null ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy ||
                renderer.HasPropertyBlock() ||
                renderer.lightmapIndex >= 0)
            {
                throw new InvalidOperationException(
                    $"Renderer '{GetStableHierarchyPath(renderer)}' does not satisfy the " +
                    "runtime instancing contract.");
            }

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (mesh == null || mesh.subMeshCount <= 0)
            {
                throw new InvalidOperationException(
                    $"Renderer '{GetStableHierarchyPath(renderer)}' has no usable mesh.");
            }

            materialBuffer.Clear();
            renderer.GetSharedMaterials(materialBuffer);
            if (materialBuffer.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Renderer '{GetStableHierarchyPath(renderer)}' has no materials.");
            }

            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                Material material = materialBuffer[
                    Mathf.Min(submesh, materialBuffer.Count - 1)];
                if (material == null || !material.enableInstancing)
                {
                    throw new InvalidOperationException(
                        $"Renderer '{GetStableHierarchyPath(renderer)}' submesh {submesh} " +
                        "has a missing or non-instanced material.");
                }
            }
        }

        private static void DecomposeMatrix(
            Matrix4x4 matrix,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale)
        {
            position = matrix.GetColumn(3);
            Vector3 right = matrix.GetColumn(0);
            Vector3 up = matrix.GetColumn(1);
            Vector3 forward = matrix.GetColumn(2);
            scale = new Vector3(right.magnitude, up.magnitude, forward.magnitude);
            if (scale.x <= Mathf.Epsilon ||
                scale.y <= Mathf.Epsilon ||
                scale.z <= Mathf.Epsilon ||
                !IsFinite(position) ||
                !IsFinite(scale))
            {
                throw new InvalidOperationException(
                    "A generated prop has an invalid or zero transform scale.");
            }

            if (matrix.determinant < 0f)
            {
                scale.x = -scale.x;
                right = -right;
            }

            rotation = Quaternion.LookRotation(forward / scale.z, up / scale.y);
            if (!IsFinite(rotation))
            {
                throw new InvalidOperationException(
                    "A generated prop has an invalid transform rotation.");
            }

            Matrix4x4 reconstructed = Matrix4x4.TRS(position, rotation, scale);
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    if (Mathf.Abs(matrix[row, column] - reconstructed[row, column]) >
                        MatrixTolerance)
                    {
                        throw new InvalidOperationException(
                            "A generated prop transform contains shear or cannot be " +
                            "represented by compact TRS data.");
                    }
                }
            }
        }

        private static SphericalPropInstanceData WriteDataAsset(
            string assetPath,
            string sourceRootName,
            BakedCategory category)
        {
            SphericalPropInstanceData data =
                AssetDatabase.LoadAssetAtPath<SphericalPropInstanceData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<SphericalPropInstanceData>();
                data.name = Path.GetFileNameWithoutExtension(assetPath);
                data.SetBakedData(sourceRootName, category.Prototypes, category.Instances);
                AssetDatabase.CreateAsset(data, assetPath);
            }
            else
            {
                Undo.RecordObject(data, "Update Planet Prop Instance Data");
                data.SetBakedData(sourceRootName, category.Prototypes, category.Instances);
            }

            EditorUtility.SetDirty(data);
            return data;
        }

        private static void AssignDataSets(
            SphericalPropInstancingRenderer renderer,
            SphericalPropInstanceData vegetationData,
            SphericalPropInstanceData rockData)
        {
            Undo.RecordObject(renderer, "Assign Baked Planet Prop Data");
            renderer.SetBakedInstanceDataSets(new[] { vegetationData, rockData });
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
        }

        private static void StripRockRenderingComponents(GameObject rockRoot)
        {
            MeshRenderer[] renderers = rockRoot.GetComponentsInChildren<MeshRenderer>(true);
            MeshFilter[] filters = rockRoot.GetComponentsInChildren<MeshFilter>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Undo.DestroyObjectImmediate(renderers[index]);
            }

            for (int index = 0; index < filters.Length; index++)
            {
                Undo.DestroyObjectImmediate(filters[index]);
            }
        }

        private static int ValidateRockColliders(GameObject rockRoot, int expectedCount)
        {
            MeshCollider[] colliders = rockRoot.GetComponentsInChildren<MeshCollider>(true);
            if (colliders.Length != expectedCount)
            {
                throw new InvalidOperationException(
                    $"'{RockRootName}' contains {colliders.Length:N0} MeshColliders; " +
                    $"expected exactly {expectedCount:N0}.");
            }

            for (int index = 0; index < colliders.Length; index++)
            {
                MeshCollider collider = colliders[index];
                if (collider == null || collider.sharedMesh == null || !collider.enabled)
                {
                    throw new InvalidOperationException(
                        $"Rock collider {index} is missing, disabled, or has no shared mesh.");
                }
            }

            return colliders.Length;
        }

        private static bool IsValidDataSet(
            SphericalPropInstanceData data,
            string expectedRootName,
            int expectedInstanceCount,
            out string error)
        {
            if (data == null)
            {
                error = "the asset reference is null";
                return false;
            }

            if (data.SourceRootName != expectedRootName)
            {
                error = $"source root is '{data.SourceRootName}'";
                return false;
            }

            if (data.InstanceCount != expectedInstanceCount || data.PrototypeCount == 0)
            {
                error = $"it contains {data.InstanceCount:N0} instances and " +
                        $"{data.PrototypeCount:N0} prototypes";
                return false;
            }

            for (int prototypeIndex = 0;
                 prototypeIndex < data.Prototypes.Count;
                 prototypeIndex++)
            {
                SphericalPropInstanceData.Prototype prototype =
                    data.Prototypes[prototypeIndex];
                Mesh mesh = prototype.Mesh;
                if (mesh == null || mesh.subMeshCount <= 0)
                {
                    error = $"prototype {prototypeIndex} has no usable mesh";
                    return false;
                }

                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    Material material = prototype.GetMaterialForSubmesh(submesh);
                    if (material == null || !material.enableInstancing)
                    {
                        error = $"prototype {prototypeIndex} submesh {submesh} has an " +
                                "invalid material";
                        return false;
                    }
                }
            }

            for (int instanceIndex = 0;
                 instanceIndex < data.Instances.Count;
                 instanceIndex++)
            {
                SphericalPropInstanceData.Instance instance = data.Instances[instanceIndex];
                if (instance.PrototypeIndex < 0 ||
                    instance.PrototypeIndex >= data.PrototypeCount ||
                    !IsFinite(instance.Position) ||
                    !IsFinite(instance.Rotation) ||
                    !IsFinite(instance.Scale))
                {
                    error = $"instance {instanceIndex} is invalid";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static SphericalPropInstanceData FindAssignedDataSet(
            SphericalPropInstancingRenderer renderer,
            string sourceRootName)
        {
            return renderer.BakedInstanceDataSets.FirstOrDefault(
                data => data != null && data.SourceRootName == sourceRootName);
        }

        private static SphericalPropInstancingRenderer FindRenderer(Scene scene)
        {
            GameObject planetRoot = FindRoot(scene, PlanetRootName);
            if (planetRoot == null)
            {
                throw new InvalidOperationException(
                    $"No active-scene root named '{PlanetRootName}' exists.");
            }

            SphericalPropInstancingRenderer renderer =
                planetRoot.GetComponent<SphericalPropInstancingRenderer>();
            if (renderer == null)
            {
                throw new InvalidOperationException(
                    $"'{PlanetRootName}' has no SphericalPropInstancingRenderer.");
            }

            return renderer;
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == rootName);
        }

        private static string GetStableHierarchyPath(Component component)
        {
            return component == null
                ? "<missing>"
                : GetStableHierarchyPath(component.transform);
        }

        private static string GetStableHierarchyPath(Transform transform)
        {
            var segments = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                segments.Push($"{current.GetSiblingIndex():D5}:{current.name}");
                current = current.parent;
            }

            return string.Join("/", segments);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                   IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        public readonly struct BakeResult
        {
            public BakeResult(int vegetationCount, int rockCount, int rockColliderCount)
            {
                VegetationCount = vegetationCount;
                RockCount = rockCount;
                RockColliderCount = rockColliderCount;
            }

            public int VegetationCount { get; }
            public int RockCount { get; }
            public int RockColliderCount { get; }
        }

        private sealed class BakedCategory
        {
            public BakedCategory(
                SphericalPropInstanceData.Prototype[] prototypes,
                SphericalPropInstanceData.Instance[] instances)
            {
                Prototypes = prototypes;
                Instances = instances;
            }

            public SphericalPropInstanceData.Prototype[] Prototypes { get; }
            public SphericalPropInstanceData.Instance[] Instances { get; }
        }

        private readonly struct PrototypeKey : IEquatable<PrototypeKey>
        {
            private readonly Mesh _mesh;
            private readonly Material[] _materials;
            private readonly int _layer;
            private readonly ShadowCastingMode _shadowCastingMode;
            private readonly bool _receiveShadows;
            private readonly uint _renderingLayerMask;
            private readonly LightProbeUsage _lightProbeUsage;
            private readonly ReflectionProbeUsage _reflectionProbeUsage;

            public PrototypeKey(
                MeshRenderer renderer,
                Mesh mesh,
                IReadOnlyList<Material> materials)
            {
                _mesh = mesh;
                _materials = materials.ToArray();
                _layer = renderer.gameObject.layer;
                _shadowCastingMode = renderer.shadowCastingMode;
                _receiveShadows = renderer.receiveShadows;
                _renderingLayerMask = renderer.renderingLayerMask;
                _lightProbeUsage = renderer.lightProbeUsage;
                _reflectionProbeUsage = renderer.reflectionProbeUsage;
            }

            public SphericalPropInstanceData.Prototype CreatePrototype()
            {
                return new SphericalPropInstanceData.Prototype(
                    _mesh,
                    _materials,
                    _layer,
                    _shadowCastingMode,
                    _receiveShadows,
                    _renderingLayerMask,
                    _lightProbeUsage,
                    _reflectionProbeUsage);
            }

            public bool Equals(PrototypeKey other)
            {
                if (_mesh != other._mesh ||
                    _layer != other._layer ||
                    _shadowCastingMode != other._shadowCastingMode ||
                    _receiveShadows != other._receiveShadows ||
                    _renderingLayerMask != other._renderingLayerMask ||
                    _lightProbeUsage != other._lightProbeUsage ||
                    _reflectionProbeUsage != other._reflectionProbeUsage ||
                    _materials.Length != other._materials.Length)
                {
                    return false;
                }

                for (int index = 0; index < _materials.Length; index++)
                {
                    if (_materials[index] != other._materials[index])
                    {
                        return false;
                    }
                }

                return true;
            }

            public override bool Equals(object obj)
            {
                return obj is PrototypeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _mesh != null ? _mesh.GetInstanceID() : 0;
                    hash = (hash * 397) ^ _layer;
                    hash = (hash * 397) ^ (int)_shadowCastingMode;
                    hash = (hash * 397) ^ _receiveShadows.GetHashCode();
                    hash = (hash * 397) ^ (int)_renderingLayerMask;
                    hash = (hash * 397) ^ (int)_lightProbeUsage;
                    hash = (hash * 397) ^ (int)_reflectionProbeUsage;
                    for (int index = 0; index < _materials.Length; index++)
                    {
                        hash = (hash * 397) ^
                               (_materials[index] != null
                                   ? _materials[index].GetInstanceID()
                                   : 0);
                    }

                    return hash;
                }
            }
        }
    }
}

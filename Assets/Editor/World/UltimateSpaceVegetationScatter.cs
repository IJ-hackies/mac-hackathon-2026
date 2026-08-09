using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WorldEditor
{
    /// <summary>
    /// Imports the selected Ultimate Space Kit vegetation and scatters a
    /// replaceable, clustered editor-authored pass over the active planet mesh.
    /// </summary>
    public static class UltimateSpaceVegetationScatter
    {
        private const string PlanetRootName = "Planet Ground";
        private const string GeneratedRootName = "Generated Planet Vegetation";
        private const string RuntimeModelFolder =
            "Assets/Art/Models/Environment/PlanetVegetation";
        private const string RuntimeMaterialFolder =
            "Assets/Art/Materials/PlanetVegetation";
        private const string BaseMaterialPath =
            RuntimeMaterialFolder + "/M_PlanetVegetation.mat";
        private const string OrangeMaterialPath =
            RuntimeMaterialFolder + "/M_PlanetVegetation_Orange.mat";
        private const string LegacyRedMaterialPath =
            RuntimeMaterialFolder + "/M_PlanetVegetation_Red.mat";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string VendorRelativeFolder =
            "asset packs/visuals/Ultimate Space Kit - March 2023/Environment/FBX";

        private const int DefaultVegetationCount = 16000;
        private const int AuthoredSampleCount = DefaultVegetationCount;
        private const int AuthoredSampleSeed = 80;
        private const int BushModelStartIndex = 0;
        private const int GrassModelStartIndex = 3;
        private const int PlantModelStartIndex = 6;
        private const int ModelsPerCategory = 3;
        private const int BushWeight = 1;
        private const int GrassWeight = 8;
        private const int PlantWeight = 1;
        private const int ClusterCount = 64;
        private const int ClusterCenterCandidateCount = 20;
        private const float ClusteredPlacementFraction = 0.25f;
        private const float MinimumClusterRadiusDegrees = 10f;
        private const float MaximumClusterRadiusDegrees = 14f;
        private const float MinimumBushScale = 40f;
        private const float MaximumBushScale = 50f;
        private const float MinimumGrassScale = 60f;
        private const float MaximumGrassScale = 70f;
        private const float MinimumPlantScale = 50f;
        private const float MaximumPlantScale = 60f;
        private const float ModelLocalXRotation = -90f;
        private const float SurfaceEmbed = 0.075f;
        private const int TerrainFitIterations = 3;
        private const float TerrainFitTolerance = 0.001f;
        private const float CastPadding = 10f;

        private static readonly string[] ModelNames =
        {
            "Bush_1", "Bush_2", "Bush_3",
            "Grass_1", "Grass_2", "Grass_3",
            "Plant_1", "Plant_2", "Plant_3"
        };

        private static readonly MaterialVariant[] MaterialVariants =
        {
            new MaterialVariant("Dark Orange", BaseMaterialPath, new Color32(150, 55, 12, 255)),
            new MaterialVariant(
                "Orange",
                OrangeMaterialPath,
                new Color32(232, 119, 25, 255))
        };

        [InitializeOnLoadMethod]
        private static void RunRequestedScatterAfterReload()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return;
            }

            string requestPath = Path.Combine(
                projectRoot,
                "Temp",
                "RegeneratePlanetVegetation.once");
            if (!File.Exists(requestPath))
            {
                return;
            }

            File.Delete(requestPath);
            EditorApplication.delayCall += () =>
            {
                try
                {
                    RegenerateSampleSceneForBatch();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Tools/Planet Design/Regenerate Planet Vegetation %#v")]
        public static void RegenerateInOpenScene()
        {
            Regenerate(
                unchecked(Environment.TickCount * 397) ^ DateTime.Now.Millisecond,
                DefaultVegetationCount,
                DefaultVegetationCount);
        }

        /// <summary>
        /// Batch/reload entry point used to reproduce the checked-in pass
        /// without requiring menu interaction in an already-open editor.
        /// </summary>
        public static void RegenerateSampleSceneForBatch()
        {
            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            Regenerate(AuthoredSampleSeed, AuthoredSampleCount, AuthoredSampleCount);
        }

        [MenuItem("Tools/Planet Design/Regenerate Planet Vegetation %#v", true)]
        private static bool ValidateRegenerateInOpenScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode &&
                   GameObject.Find(PlanetRootName) != null;
        }

        [MenuItem("Tools/Planet Design/Prepare Planet Vegetation Assets")]
        public static void PrepareAssets()
        {
            EnsureFolder(RuntimeModelFolder);
            EnsureFolder(RuntimeMaterialFolder);
            CopyVendorModelsWhenMissing();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            MigrateLegacyOrangeMaterial();

            List<Material> materials = CreateOrUpdateVegetationMaterials();
            foreach (string modelName in ModelNames)
            {
                ConfigureModelImporter(
                    $"{RuntimeModelFolder}/{modelName}.fbx",
                    materials[0]);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>
        /// Replaces this tool's generated root in the active scene. The seed is
        /// explicit so a pleasing pass and its category scales reproduce exactly.
        /// </summary>
        public static void Regenerate(int seed, int minimumCount, int maximumCount)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Planet Vegetation Scatter can only run in Edit mode.");
            }

            if (minimumCount < 0 || maximumCount < minimumCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumCount),
                    "The count range must be non-negative and ordered.");
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException(
                    "Planet Vegetation Scatter requires a loaded active scene.");
            }

            GameObject planet = GameObject.Find(PlanetRootName);
            if (planet == null || planet.scene != scene)
            {
                throw new InvalidOperationException(
                    $"No active-scene object named '{PlanetRootName}' was found.");
            }

            Collider surface = FindSurfaceCollider(planet);
            PrepareAssets();
            List<GameObject> modelPrefabs = LoadModelPrefabs();
            List<Material> materials = LoadVegetationMaterials();

            UnityEngine.Random.State previousRandomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);
            int requestedCount = UnityEngine.Random.Range(minimumCount, maximumCount + 1);
            List<GameObject> modelSelection = BuildModelSelection(
                modelPrefabs,
                requestedCount);
            List<bool> clusteredPlacements = BuildClusteredPlacementSelection(
                requestedCount);
            List<VegetationCluster> clusters = BuildClusters();

            Undo.SetCurrentGroupName("Regenerate Planet Vegetation");
            int undoGroup = Undo.GetCurrentGroup();
            try
            {
                RemoveExistingGeneratedRoot(scene);

                var generatedRoot = new GameObject(GeneratedRootName);
                Undo.RegisterCreatedObjectUndo(generatedRoot, "Create Planet Vegetation Root");
                SceneManager.MoveGameObjectToScene(generatedRoot, scene);
                generatedRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                generatedRoot.transform.localScale = Vector3.one;

                var meshVertexCache = new Dictionary<Mesh, Vector3[]>();
                var missingGeometryWarnings = new HashSet<string>();
                int placedCount = 0;
                int maximumAttempts = Math.Max(requestedCount * 8, 64);

                for (int attempt = 0;
                     attempt < maximumAttempts && placedCount < requestedCount;
                     attempt++)
                {
                    Vector3 direction = GetPlacementDirection(
                        clusteredPlacements[placedCount],
                        clusters);
                    if (!RadialSurfaceSnapWindow.TryGetSurfaceHit(
                            planet.transform.position + direction,
                            planet.transform.position,
                            surface,
                            CastPadding,
                            out RaycastHit hit,
                            out _))
                    {
                        continue;
                    }

                    GameObject source = modelSelection[placedCount];
                    var instance = PrefabUtility.InstantiatePrefab(source, scene) as GameObject;
                    if (instance == null)
                    {
                        continue;
                    }

                    Undo.RegisterCreatedObjectUndo(instance, "Scatter Planet Vegetation");
                    instance.transform.SetParent(generatedRoot.transform, true);

                    Vector3 up = hit.normal.sqrMagnitude > 0.001f
                        ? hit.normal.normalized
                        : direction;
                    Quaternion surfaceAlignment = Quaternion.FromToRotation(Vector3.up, up);
                    Quaternion randomHeading = Quaternion.AngleAxis(
                        UnityEngine.Random.Range(0f, 360f),
                        up);
                    float scale = GetRandomScale(source);
                    Material material = materials[
                        UnityEngine.Random.Range(0, materials.Count)];

                    instance.transform.SetPositionAndRotation(
                        hit.point,
                        randomHeading * surfaceAlignment *
                        Quaternion.Euler(ModelLocalXRotation, 0f, 0f));
                    instance.transform.localScale = Vector3.one * scale;
                    bool foundGeometry = TryGetSurfaceSupportOffset(
                        instance,
                        up,
                        meshVertexCache,
                        out float supportOffset);
                    if (!foundGeometry && missingGeometryWarnings.Add(source.name))
                    {
                        Debug.LogWarning(
                            $"Planet Vegetation Scatter: '{source.name}' has no mesh " +
                            "vertices; its pivot is being used for surface placement.");
                    }

                    instance.transform.position =
                        hit.point + up * supportOffset;
                    for (int fitIteration = 0;
                         fitIteration < TerrainFitIterations;
                         fitIteration++)
                    {
                        float terrainConformingOffset = GetTerrainConformingOffset(
                            instance,
                            planet.transform.position,
                            surface,
                            up,
                            meshVertexCache);
                        instance.transform.position += up * terrainConformingOffset;
                        if (Mathf.Abs(terrainConformingOffset) <= TerrainFitTolerance)
                        {
                            break;
                        }
                    }

                    instance.transform.position -= up * SurfaceEmbed;
                    instance.name = $"{source.name}_{placedCount + 1:000}";
                    ApplyMaterialRecursively(instance, material);
                    SetStaticRecursively(instance);
                    placedCount++;
                }

                if (placedCount != requestedCount)
                {
                    Debug.LogWarning(
                        $"Planet Vegetation Scatter: requested {requestedCount} placements but " +
                        $"only {placedCount} rays hit '{surface.name}'.");
                }

                EditorUtility.SetDirty(generatedRoot);
                PlanetPropBakeTool.ClearBakedCategory(scene, GeneratedRootName);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = generatedRoot;
                Undo.CollapseUndoOperations(undoGroup);

                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        $"Planet Vegetation Scatter could not save '{scene.path}'.");
                }

                SceneView.RepaintAll();
                Debug.Log(
                    $"Planet Vegetation Scatter: placed {placedCount} objects across " +
                    $"'{surface.name}' with seed {seed}; grass " +
                    $"{MinimumGrassScale:0.#}x-{MaximumGrassScale:0.#}x, bushes " +
                    $"{MinimumBushScale:0.#}x-{MaximumBushScale:0.#}x, plants " +
                    $"{MinimumPlantScale:0.#}x-{MaximumPlantScale:0.#}x; " +
                    $"{ClusteredPlacementFraction:P0} use {ClusterCount} mild clusters.");
            }
            finally
            {
                UnityEngine.Random.state = previousRandomState;
            }
        }

        private static void CopyVendorModelsWhenMissing()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException(
                    "Planet Vegetation Scatter could not resolve the project root.");
            }

            string vendorFolder = Path.Combine(
                projectRoot,
                VendorRelativeFolder.Replace('/', Path.DirectorySeparatorChar));
            string runtimeFolder = Path.Combine(
                projectRoot,
                RuntimeModelFolder.Replace('/', Path.DirectorySeparatorChar));

            foreach (string modelName in ModelNames)
            {
                string source = Path.Combine(vendorFolder, modelName + ".fbx");
                string destination = Path.Combine(runtimeFolder, modelName + ".fbx");
                if (!File.Exists(source))
                {
                    throw new FileNotFoundException(
                        $"Planet Vegetation Scatter is missing vendor source '{source}'.",
                        source);
                }

                if (!File.Exists(destination))
                {
                    File.Copy(source, destination, overwrite: false);
                }
            }
        }

        private static List<Material> CreateOrUpdateVegetationMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var materials = new List<Material>(MaterialVariants.Length);
            for (int index = 0; index < MaterialVariants.Length; index++)
            {
                MaterialVariant variant = MaterialVariants[index];
                Material material = AssetDatabase.LoadAssetAtPath<Material>(variant.Path);
                if (material == null)
                {
                    if (shader == null)
                    {
                        throw new InvalidOperationException(
                            $"Universal Render Pipeline/Lit is unavailable and '{variant.Path}' " +
                            "has not been created yet.");
                    }

                    material = new Material(shader)
                    {
                        name = Path.GetFileNameWithoutExtension(variant.Path)
                    };
                    AssetDatabase.CreateAsset(material, variant.Path);
                }
                else if (shader != null)
                {
                    material.shader = shader;
                }

                material.name = Path.GetFileNameWithoutExtension(variant.Path);
                Color color = variant.Color;
                if (shader != null)
                {
                    material.SetTexture("_BaseMap", null);
                    material.SetColor("_BaseColor", color);
                    material.SetColor("_Color", color);
                    material.SetFloat("_Metallic", 0f);
                    material.SetFloat("_Smoothness", 0.08f);
                    material.enableInstancing = true;
                    EditorUtility.SetDirty(material);
                }

                materials.Add(material);
            }

            return materials;
        }

        private static List<Material> LoadVegetationMaterials()
        {
            var materials = new List<Material>(MaterialVariants.Length);
            foreach (MaterialVariant variant in MaterialVariants)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(variant.Path);
                if (material == null)
                {
                    throw new InvalidOperationException(
                        $"Planet Vegetation Scatter could not load '{variant.Path}'.");
                }

                materials.Add(material);
            }

            return materials;
        }

        private static void ConfigureModelImporter(string modelPath, Material material)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"Planet Vegetation Scatter could not import '{modelPath}'.");
            }

            bool requiresReimport = false;
            if (importer.importAnimation)
            {
                importer.importAnimation = false;
                requiresReimport = true;
            }

            if (importer.isReadable)
            {
                importer.isReadable = false;
                requiresReimport = true;
            }

            if (importer.addCollider)
            {
                importer.addCollider = false;
                requiresReimport = true;
            }

            var identifier = new AssetImporter.SourceAssetIdentifier(
                typeof(Material),
                "Atlas");
            IReadOnlyDictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> remaps =
                importer.GetExternalObjectMap();
            if (!remaps.TryGetValue(identifier, out UnityEngine.Object current) ||
                current != material)
            {
                importer.AddRemap(identifier, material);
                requiresReimport = true;
            }

            if (requiresReimport)
            {
                importer.SaveAndReimport();
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null || model.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                throw new InvalidOperationException(
                    $"Planet Vegetation Scatter: '{modelPath}' has no renderable model.");
            }
        }

        private static List<GameObject> LoadModelPrefabs()
        {
            var models = new List<GameObject>(ModelNames.Length);
            foreach (string modelName in ModelNames)
            {
                string path = $"{RuntimeModelFolder}/{modelName}.fbx";
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null)
                {
                    throw new InvalidOperationException(
                        $"Planet Vegetation Scatter could not load '{path}'.");
                }

                models.Add(model);
            }

            return models;
        }

        private static List<GameObject> BuildModelSelection(
            IReadOnlyList<GameObject> models,
            int requestedCount)
        {
            int totalWeight = BushWeight + GrassWeight + PlantWeight;
            int bushCount = Mathf.RoundToInt(
                requestedCount * BushWeight / (float)totalWeight);
            int grassCount = Mathf.RoundToInt(
                requestedCount * GrassWeight / (float)totalWeight);
            int plantCount = requestedCount - bushCount - grassCount;

            var selection = new List<GameObject>(requestedCount);
            AddRandomCategoryModels(
                selection,
                models,
                BushModelStartIndex,
                bushCount);
            AddRandomCategoryModels(
                selection,
                models,
                GrassModelStartIndex,
                grassCount);
            AddRandomCategoryModels(
                selection,
                models,
                PlantModelStartIndex,
                plantCount);

            Shuffle(selection);

            return selection;
        }

        private static void AddRandomCategoryModels(
            ICollection<GameObject> selection,
            IReadOnlyList<GameObject> models,
            int categoryStartIndex,
            int count)
        {
            for (int index = 0; index < count; index++)
            {
                selection.Add(models[
                    categoryStartIndex +
                    UnityEngine.Random.Range(0, ModelsPerCategory)]);
            }
        }

        private static float GetRandomScale(GameObject source)
        {
            if (source.name.StartsWith("Grass_", StringComparison.Ordinal))
            {
                return UnityEngine.Random.Range(MinimumGrassScale, MaximumGrassScale);
            }

            if (source.name.StartsWith("Bush_", StringComparison.Ordinal))
            {
                return UnityEngine.Random.Range(MinimumBushScale, MaximumBushScale);
            }

            if (source.name.StartsWith("Plant_", StringComparison.Ordinal))
            {
                return UnityEngine.Random.Range(MinimumPlantScale, MaximumPlantScale);
            }

            throw new InvalidOperationException(
                $"Planet Vegetation Scatter has no scale range for '{source.name}'.");
        }

        private static List<bool> BuildClusteredPlacementSelection(int requestedCount)
        {
            int clusteredCount = Mathf.RoundToInt(
                requestedCount * ClusteredPlacementFraction);
            var selection = new List<bool>(requestedCount);
            for (int index = 0; index < requestedCount; index++)
            {
                selection.Add(index < clusteredCount);
            }

            Shuffle(selection);
            return selection;
        }

        private static void MigrateLegacyOrangeMaterial()
        {
            Material orange = AssetDatabase.LoadAssetAtPath<Material>(OrangeMaterialPath);
            Material legacyRed = AssetDatabase.LoadAssetAtPath<Material>(
                LegacyRedMaterialPath);
            if (orange != null || legacyRed == null)
            {
                return;
            }

            string error = AssetDatabase.MoveAsset(
                LegacyRedMaterialPath,
                OrangeMaterialPath);
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException(
                    $"Planet Vegetation Scatter could not migrate the orange material: {error}");
            }
        }

        private static List<VegetationCluster> BuildClusters()
        {
            var clusters = new List<VegetationCluster>(ClusterCount);
            for (int clusterIndex = 0; clusterIndex < ClusterCount; clusterIndex++)
            {
                Vector3 bestDirection = UnityEngine.Random.onUnitSphere;
                float bestMinimumDistance = -1f;
                for (int candidateIndex = 0;
                     candidateIndex < ClusterCenterCandidateCount;
                     candidateIndex++)
                {
                    Vector3 candidate = UnityEngine.Random.onUnitSphere;
                    float minimumDistance = float.PositiveInfinity;
                    foreach (VegetationCluster cluster in clusters)
                    {
                        float squaredDistance =
                            (candidate - cluster.Direction).sqrMagnitude;
                        minimumDistance = Mathf.Min(minimumDistance, squaredDistance);
                    }

                    if (minimumDistance > bestMinimumDistance)
                    {
                        bestMinimumDistance = minimumDistance;
                        bestDirection = candidate;
                    }
                }

                clusters.Add(new VegetationCluster(
                    bestDirection,
                    UnityEngine.Random.Range(
                        MinimumClusterRadiusDegrees,
                        MaximumClusterRadiusDegrees)));
            }

            return clusters;
        }

        private static Vector3 GetPlacementDirection(
            bool clustered,
            IReadOnlyList<VegetationCluster> clusters)
        {
            if (!clustered)
            {
                return UnityEngine.Random.onUnitSphere;
            }

            VegetationCluster cluster = clusters[
                UnityEngine.Random.Range(0, clusters.Count)];
            Vector3 tangent = Vector3.ProjectOnPlane(
                UnityEngine.Random.onUnitSphere,
                cluster.Direction);
            if (tangent.sqrMagnitude <= 0.0001f)
            {
                tangent = Vector3.Cross(
                    cluster.Direction,
                    Mathf.Abs(cluster.Direction.y) < 0.9f
                        ? Vector3.up
                        : Vector3.right);
            }

            tangent.Normalize();
            Vector3 rotationAxis = Vector3.Cross(
                cluster.Direction,
                tangent).normalized;
            float angle = cluster.AngularRadiusDegrees *
                          Mathf.Sqrt(UnityEngine.Random.value);
            return Quaternion.AngleAxis(angle, rotationAxis) * cluster.Direction;
        }

        private static void Shuffle<T>(IList<T> values)
        {
            for (int index = values.Count - 1; index > 0; index--)
            {
                int swapIndex = UnityEngine.Random.Range(0, index + 1);
                (values[index], values[swapIndex]) =
                    (values[swapIndex], values[index]);
            }
        }

        private static Collider FindSurfaceCollider(GameObject planet)
        {
            Collider surface = planet
                .GetComponentsInChildren<Collider>(true)
                .FirstOrDefault(candidate =>
                    candidate is MeshCollider &&
                    candidate.enabled &&
                    candidate.gameObject.activeInHierarchy);
            if (surface == null)
            {
                throw new InvalidOperationException(
                    $"'{PlanetRootName}' has no active, enabled MeshCollider.");
            }

            return surface;
        }

        private static void RemoveExistingGeneratedRoot(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == GeneratedRootName)
                {
                    Undo.DestroyObjectImmediate(root);
                }
            }
        }

        private static void SetStaticRecursively(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(
                    child.gameObject,
                    StaticEditorFlags.ReflectionProbeStatic);
            }
        }

        private static bool TryGetSurfaceSupportOffset(
            GameObject root,
            Vector3 up,
            IDictionary<Mesh, Vector3[]> meshVertexCache,
            out float supportOffset)
        {
            Vector3 pivot = root.transform.position;
            float minimumProjection = float.PositiveInfinity;

            foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null)
                {
                    continue;
                }

                Mesh mesh = meshFilter.sharedMesh;
                if (!meshVertexCache.TryGetValue(mesh, out Vector3[] vertices))
                {
                    vertices = mesh.vertices;
                    meshVertexCache.Add(mesh, vertices);
                }

                AccumulateMinimumVertexProjection(
                    vertices,
                    meshFilter.transform.localToWorldMatrix,
                    pivot,
                    up,
                    ref minimumProjection);
            }

            foreach (SkinnedMeshRenderer renderer in
                     root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bakedMesh = new Mesh
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                try
                {
                    renderer.BakeMesh(bakedMesh, false);
                    AccumulateMinimumVertexProjection(
                        bakedMesh.vertices,
                        renderer.transform.localToWorldMatrix,
                        pivot,
                        up,
                        ref minimumProjection);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bakedMesh);
                }
            }

            bool foundGeometry = !float.IsPositiveInfinity(minimumProjection);
            supportOffset = foundGeometry ? -minimumProjection : 0f;
            return foundGeometry;
        }

        private static void AccumulateMinimumVertexProjection(
            IReadOnlyList<Vector3> vertices,
            Matrix4x4 localToWorld,
            Vector3 pivot,
            Vector3 up,
            ref float minimumProjection)
        {
            for (int index = 0; index < vertices.Count; index++)
            {
                Vector3 worldVertex = localToWorld.MultiplyPoint3x4(vertices[index]);
                float projection = Vector3.Dot(worldVertex - pivot, up);
                minimumProjection = Mathf.Min(minimumProjection, projection);
            }
        }

        private static float GetTerrainConformingOffset(
            GameObject root,
            Vector3 center,
            Collider surface,
            Vector3 rootUp,
            IDictionary<Mesh, Vector3[]> meshVertexCache)
        {
            float maximumRequiredOffset = float.NegativeInfinity;

            foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                if (!meshVertexCache.TryGetValue(mesh, out Vector3[] vertices))
                {
                    vertices = mesh.vertices;
                    meshVertexCache.Add(mesh, vertices);
                }

                AccumulateTerrainConformingOffset(
                    vertices,
                    meshFilter.transform.localToWorldMatrix,
                    center,
                    surface,
                    rootUp,
                    ref maximumRequiredOffset);
            }

            foreach (SkinnedMeshRenderer renderer in
                     root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bakedMesh = new Mesh
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                try
                {
                    renderer.BakeMesh(bakedMesh, false);
                    AccumulateTerrainConformingOffset(
                        bakedMesh.vertices,
                        renderer.transform.localToWorldMatrix,
                        center,
                        surface,
                        rootUp,
                        ref maximumRequiredOffset);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bakedMesh);
                }
            }

            return float.IsNegativeInfinity(maximumRequiredOffset)
                ? 0f
                : maximumRequiredOffset;
        }

        private static void AccumulateTerrainConformingOffset(
            IReadOnlyList<Vector3> vertices,
            Matrix4x4 localToWorld,
            Vector3 center,
            Collider surface,
            Vector3 rootUp,
            ref float maximumRequiredOffset)
        {
            for (int index = 0; index < vertices.Count; index++)
            {
                Vector3 worldVertex = localToWorld.MultiplyPoint3x4(vertices[index]);
                if (!RadialSurfaceSnapWindow.TryGetSurfaceHit(
                        worldVertex,
                        center,
                        surface,
                        CastPadding,
                        out RaycastHit surfaceHit,
                        out _))
                {
                    continue;
                }

                Vector3 surfaceNormal = surfaceHit.normal.normalized;
                float movementAlignment = Vector3.Dot(rootUp, surfaceNormal);
                if (movementAlignment <= 0.1f)
                {
                    continue;
                }

                float requiredOffset = Vector3.Dot(
                    surfaceHit.point - worldVertex,
                    surfaceNormal) / movementAlignment;
                maximumRequiredOffset = Mathf.Max(
                    maximumRequiredOffset,
                    requiredOffset);
            }
        }

        private static void ApplyMaterialRecursively(GameObject root, Material material)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] sharedMaterials = renderer.sharedMaterials;
                for (int index = 0; index < sharedMaterials.Length; index++)
                {
                    sharedMaterials[index] = material;
                }

                renderer.sharedMaterials = sharedMaterials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorUtility.SetDirty(renderer);
            }
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

        private readonly struct MaterialVariant
        {
            public MaterialVariant(string displayName, string path, Color color)
            {
                DisplayName = displayName;
                Path = path;
                Color = color;
            }

            public string DisplayName { get; }
            public string Path { get; }
            public Color Color { get; }
        }

        private readonly struct VegetationCluster
        {
            public VegetationCluster(Vector3 direction, float angularRadiusDegrees)
            {
                Direction = direction.normalized;
                AngularRadiusDegrees = angularRadiusDegrees;
            }

            public Vector3 Direction { get; }
            public float AngularRadiusDegrees { get; }
        }
    }
}

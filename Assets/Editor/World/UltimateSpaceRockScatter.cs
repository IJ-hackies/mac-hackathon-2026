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
    /// Authors a replaceable, clustered rock pass over the exact planet mesh
    /// while keeping the three walled gameplay areas clear.
    /// </summary>
    public static class UltimateSpaceRockScatter
    {
        private const string PlanetRootName = "Planet Ground";
        private const string GeneratedRootName = "Generated Planet Rocks";
        private const string RuntimeModelFolder =
            "Assets/Art/Models/Environment/PlanetRocks";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        private const int AuthoredSmallRockCount = 800;
        private const int AuthoredLargeRockCount = 300;
        private const int AuthoredSampleSeed = 80826;
        // These are literal Unity Transform scale values requested by art direction.
        private const float MinimumSmallScale = 100f;
        private const float MaximumSmallScale = 200f;
        private const float MinimumLargeScale = 100f;
        private const float MaximumLargeScale = 200f;
        private const float ModelLocalXRotation = -90f;
        private const float SurfaceEmbed = 0.075f;
        private const float CastPadding = 10f;
        private const int TerrainFitIterations = 3;
        private const float TerrainFitTolerance = 0.001f;

        private const float TargetSmallRocksPerCluster = 14f;
        private const int MinimumSmallRocksPerCluster = 10;
        private const int MaximumSmallRocksPerCluster = 20;
        private const float TargetLargeRocksPerCluster = 2.5f;
        private const int MinimumLargeRocksPerCluster = 1;
        private const int MaximumLargeRocksPerCluster = 3;
        private const float MixedSmallClusterRatio = 0.55f;
        private const float MinimumSmallClusterRadius = 5f;
        private const float MaximumSmallClusterRadius = 12f;
        private const float MinimumLargeClusterRadius = 1.5f;
        private const float MaximumLargeClusterRadius = 4f;
        private const float MinimumClusterCenterSeparationDegrees = 4f;
        private const int ClusterCenterCandidates = 96;
        private const float MinimumRockSpacing = 1.35f;
        private const float ProtectedAreaPadding = 2f;

        private static readonly string[] SmallModelNames =
        {
            "Rock_1", "Rock_2", "Rock_3", "Rock_4"
        };

        private static readonly string[] LargeModelNames =
        {
            "Rock_Large_1", "Rock_Large_2", "Rock_Large_3"
        };

        private static readonly string[] ProtectedAreaNames =
        {
            "LandingBase", "Arena1", "Arena2"
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
                "RegeneratePlanetRocks.once");
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

        [MenuItem("Tools/Planet Design/Regenerate Planet Rocks %#r")]
        public static void RegenerateInOpenScene()
        {
            Regenerate(
                unchecked(Environment.TickCount * 397) ^ DateTime.Now.Millisecond,
                AuthoredSmallRockCount,
                AuthoredLargeRockCount);
        }

        [MenuItem("Tools/Planet Design/Regenerate Planet Rocks %#r", true)]
        private static bool ValidateRegenerateInOpenScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode &&
                   GameObject.Find(PlanetRootName) != null;
        }

        public static void RegenerateSampleSceneForBatch()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || activeScene.path != SampleScenePath)
            {
                EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            }

            Regenerate(
                AuthoredSampleSeed,
                AuthoredSmallRockCount,
                AuthoredLargeRockCount);
        }

        /// <summary>
        /// Replaces this tool's generated root in the active scene. An
        /// explicit seed keeps every authored pass reproducible.
        /// </summary>
        public static void Regenerate(int seed, int smallRockCount, int largeRockCount)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Planet Rock Scatter can only run in Edit mode.");
            }

            if (smallRockCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(smallRockCount),
                    "The small-rock count must be non-negative.");
            }

            if (largeRockCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(largeRockCount),
                    "The large-rock count must be non-negative.");
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException(
                    "Planet Rock Scatter requires a loaded active scene.");
            }

            GameObject planet = FindRoot(scene, PlanetRootName);
            if (planet == null)
            {
                throw new InvalidOperationException(
                    $"No active-scene object named '{PlanetRootName}' was found.");
            }

            Collider surface = FindSurfaceCollider(planet);
            UltimateSpaceRockAssetSetup.PrepareAssets();
            List<GameObject> smallModelPrefabs = LoadModelPrefabs(SmallModelNames);
            List<GameObject> largeModelPrefabs = LoadModelPrefabs(LargeModelNames);
            List<GameObject> allModelPrefabs = smallModelPrefabs
                .Concat(largeModelPrefabs)
                .ToList();
            float protectedAreaClearance = CalculateMaximumRockRadius(
                allModelPrefabs,
                scene) + ProtectedAreaPadding;
            List<ProtectedRegion> protectedRegions = BuildProtectedRegions(
                scene,
                planet.transform.position,
                protectedAreaClearance);

            UnityEngine.Random.State previousRandomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);
            int requestedCount = smallRockCount + largeRockCount;

            Undo.SetCurrentGroupName("Regenerate Planet Rocks");
            int undoGroup = Undo.GetCurrentGroup();
            try
            {
                RemoveExistingGeneratedRoot(scene);

                var generatedRoot = new GameObject(GeneratedRootName);
                Undo.RegisterCreatedObjectUndo(generatedRoot, "Create Planet Rocks Root");
                SceneManager.MoveGameObjectToScene(generatedRoot, scene);
                generatedRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                generatedRoot.transform.localScale = Vector3.one;

                List<ClusterPlan> clusterPlans = CreateClusterPlans(
                    smallRockCount,
                    largeRockCount);
                List<ClusterCenter> centers = GenerateClusterCenters(
                    clusterPlans.Count,
                    planet.transform.position,
                    surface,
                    protectedRegions);

                var meshVertexCache = new Dictionary<Mesh, Vector3[]>();
                var missingGeometryWarnings = new HashSet<string>();
                var placedPositions = new List<Vector3>(requestedCount);
                int placedCount = 0;
                int placedSmallCount = 0;
                int placedLargeCount = 0;

                for (int clusterIndex = 0;
                     clusterIndex < centers.Count && placedCount < requestedCount;
                     clusterIndex++)
                {
                    ClusterCenter center = centers[clusterIndex];
                    ClusterPlan plan = clusterPlans[clusterIndex];
                    var clusterRoot = new GameObject(
                        $"Rock Cluster {clusterIndex + 1:000} ({plan.Label})");
                    Undo.RegisterCreatedObjectUndo(clusterRoot, "Create Rock Cluster");
                    clusterRoot.transform.SetParent(generatedRoot.transform, false);

                    float clusterRadius = plan.SmallCount > 0
                        ? UnityEngine.Random.Range(
                            MinimumSmallClusterRadius,
                            MaximumSmallClusterRadius)
                        : UnityEngine.Random.Range(
                            MinimumLargeClusterRadius,
                            MaximumLargeClusterRadius);
                    int remainingSmall = plan.SmallCount;
                    int remainingLarge = plan.LargeCount;
                    int clusterTarget = remainingSmall + remainingLarge;
                    int clusterPlaced = 0;
                    int clusterAttempts = 0;
                    int maximumClusterAttempts = Math.Max(clusterTarget * 120, 240);

                    while (clusterPlaced < clusterTarget &&
                           clusterAttempts++ < maximumClusterAttempts)
                    {
                        float expansion = 1f +
                            0.35f * clusterAttempts / maximumClusterAttempts;
                        Vector3 direction = SampleDirectionNearCluster(
                            center,
                            clusterRadius * expansion);
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

                        Vector3 hitDirection =
                            (hit.point - planet.transform.position).normalized;
                        if (IsProtected(hitDirection, protectedRegions) ||
                            IsTooClose(hit.point, placedPositions, MinimumRockSpacing))
                        {
                            continue;
                        }

                        bool placeLarge = remainingLarge > 0 &&
                            (remainingSmall == 0 ||
                             UnityEngine.Random.Range(
                                 0,
                                 remainingSmall + remainingLarge) < remainingLarge);
                        List<GameObject> categoryPrefabs = placeLarge
                            ? largeModelPrefabs
                            : smallModelPrefabs;
                        GameObject source = categoryPrefabs[
                            UnityEngine.Random.Range(0, categoryPrefabs.Count)];
                        var instance = PrefabUtility.InstantiatePrefab(source, scene) as GameObject;
                        if (instance == null)
                        {
                            continue;
                        }

                        Undo.RegisterCreatedObjectUndo(instance, "Scatter Planet Rock");
                        instance.transform.SetParent(clusterRoot.transform, true);

                        Vector3 up = hit.normal.sqrMagnitude > 0.001f
                            ? hit.normal.normalized
                            : direction;
                        Quaternion surfaceAlignment =
                            Quaternion.FromToRotation(Vector3.up, up);
                        Quaternion randomHeading = Quaternion.AngleAxis(
                            UnityEngine.Random.Range(0f, 360f),
                            up);
                        float scale = placeLarge
                            ? UnityEngine.Random.Range(MinimumLargeScale, MaximumLargeScale)
                            : UnityEngine.Random.Range(MinimumSmallScale, MaximumSmallScale);

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
                                $"Planet Rock Scatter: '{source.name}' has no mesh vertices; " +
                                "its pivot is being used for surface placement.");
                        }

                        instance.transform.position = hit.point + up * supportOffset;
                        for (int fitIteration = 0;
                             fitIteration < TerrainFitIterations;
                             fitIteration++)
                        {
                            float terrainOffset = GetTerrainConformingOffset(
                                instance,
                                planet.transform.position,
                                surface,
                                up,
                                meshVertexCache);
                            instance.transform.position += up * terrainOffset;
                            if (Mathf.Abs(terrainOffset) <= TerrainFitTolerance)
                            {
                                break;
                            }
                        }

                        instance.transform.position -= up * SurfaceEmbed;
                        instance.name = $"{source.name}_{placedCount + 1:0000}";
                        SetStaticRecursively(instance);
                        placedPositions.Add(instance.transform.position);
                        clusterPlaced++;
                        placedCount++;
                        if (placeLarge)
                        {
                            remainingLarge--;
                            placedLargeCount++;
                        }
                        else
                        {
                            remainingSmall--;
                            placedSmallCount++;
                        }
                    }

                    EditorUtility.DisplayProgressBar(
                        "Planet Rock Scatter",
                        $"Grounding cluster {clusterIndex + 1} of {centers.Count}",
                        (clusterIndex + 1f) / centers.Count);
                }

                if (placedSmallCount != smallRockCount ||
                    placedLargeCount != largeRockCount)
                {
                    throw new InvalidOperationException(
                        $"Planet Rock Scatter: requested {smallRockCount} small and " +
                        $"{largeRockCount} large rocks but placed {placedSmallCount} small " +
                        $"and {placedLargeCount} large. The placement constraints exhausted " +
                        "the available cluster attempts.");
                }

                EditorUtility.SetDirty(generatedRoot);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = generatedRoot;
                Undo.CollapseUndoOperations(undoGroup);

                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        $"Planet Rock Scatter could not save '{scene.path}'.");
                }

                SceneView.RepaintAll();
                Debug.Log(
                    $"Planet Rock Scatter: placed {placedSmallCount} small rocks at " +
                    $"{MinimumSmallScale:0.#}x-{MaximumSmallScale:0.#}x and " +
                    $"{placedLargeCount} large rocks at " +
                    $"{MinimumLargeScale:0.#}x-{MaximumLargeScale:0.#}x in " +
                    $"{centers.Count} clusters across '{surface.name}' with seed {seed}, " +
                    "and no placements inside " +
                    $"{string.Join(", ", ProtectedAreaNames)} (footprint-aware clearance " +
                    $"{protectedAreaClearance:0.##} units).");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                UnityEngine.Random.state = previousRandomState;
            }
        }

        private static List<ClusterCenter> GenerateClusterCenters(
            int count,
            Vector3 planetCenter,
            Collider surface,
            IReadOnlyList<ProtectedRegion> protectedRegions)
        {
            var centers = new List<ClusterCenter>(count);
            for (int centerIndex = 0; centerIndex < count; centerIndex++)
            {
                bool found = false;
                ClusterCenter best = default;
                float bestSeparation = float.NegativeInfinity;

                for (int candidateIndex = 0;
                     candidateIndex < ClusterCenterCandidates;
                     candidateIndex++)
                {
                    Vector3 direction = UnityEngine.Random.onUnitSphere;
                    if (IsProtected(direction, protectedRegions) ||
                        !RadialSurfaceSnapWindow.TryGetSurfaceHit(
                            planetCenter + direction,
                            planetCenter,
                            surface,
                            CastPadding,
                            out RaycastHit hit,
                            out _))
                    {
                        continue;
                    }

                    float separation = 180f;
                    foreach (ClusterCenter existing in centers)
                    {
                        separation = Mathf.Min(
                            separation,
                            Vector3.Angle(direction, existing.Direction));
                    }

                    if (separation > bestSeparation)
                    {
                        bestSeparation = separation;
                        best = new ClusterCenter(direction, hit.point, planetCenter);
                        found = true;
                    }
                }

                if (!found)
                {
                    throw new InvalidOperationException(
                        "Planet Rock Scatter could not find a valid cluster center outside " +
                        "the protected areas.");
                }

                if (centers.Count > 0 &&
                    bestSeparation < MinimumClusterCenterSeparationDegrees)
                {
                    Debug.LogWarning(
                        $"Planet Rock Scatter: cluster {centerIndex + 1} has only " +
                        $"{bestSeparation:0.0} degrees of center separation.");
                }

                centers.Add(best);
            }

            return centers;
        }

        private static List<ClusterPlan> CreateClusterPlans(
            int smallRockCount,
            int largeRockCount)
        {
            int smallClusterCount = CalculateClusterCount(
                smallRockCount,
                TargetSmallRocksPerCluster,
                MinimumSmallRocksPerCluster,
                MaximumSmallRocksPerCluster);
            int largeClusterCount = CalculateClusterCount(
                largeRockCount,
                TargetLargeRocksPerCluster,
                MinimumLargeRocksPerCluster,
                MaximumLargeRocksPerCluster);
            int mixedClusterCount = Math.Min(
                Mathf.RoundToInt(smallClusterCount * MixedSmallClusterRatio),
                largeClusterCount);

            int[] smallCounts = DistributeAcrossClusters(
                smallRockCount,
                smallClusterCount,
                MinimumSmallRocksPerCluster,
                MaximumSmallRocksPerCluster);
            int[] largeCounts = DistributeAcrossClusters(
                largeRockCount,
                largeClusterCount,
                MinimumLargeRocksPerCluster,
                MaximumLargeRocksPerCluster);

            var plans = new List<ClusterPlan>(
                smallClusterCount + largeClusterCount - mixedClusterCount);
            for (int index = 0; index < mixedClusterCount; index++)
            {
                plans.Add(new ClusterPlan(smallCounts[index], largeCounts[index]));
            }

            for (int index = mixedClusterCount; index < smallCounts.Length; index++)
            {
                plans.Add(new ClusterPlan(smallCounts[index], 0));
            }

            for (int index = mixedClusterCount; index < largeCounts.Length; index++)
            {
                plans.Add(new ClusterPlan(0, largeCounts[index]));
            }

            for (int index = plans.Count - 1; index > 0; index--)
            {
                int swapIndex = UnityEngine.Random.Range(0, index + 1);
                ClusterPlan temporary = plans[index];
                plans[index] = plans[swapIndex];
                plans[swapIndex] = temporary;
            }

            return plans;
        }

        private static int CalculateClusterCount(
            int total,
            float targetPerCluster,
            int minimumPerCluster,
            int maximumPerCluster)
        {
            if (total == 0)
            {
                return 0;
            }

            int minimumClusters = Mathf.CeilToInt(total / (float)maximumPerCluster);
            int maximumClusters = total / minimumPerCluster;
            return Mathf.Clamp(
                Mathf.RoundToInt(total / targetPerCluster),
                minimumClusters,
                maximumClusters);
        }

        private static int[] DistributeAcrossClusters(
            int total,
            int clusterCount,
            int minimumPerCluster,
            int maximumPerCluster)
        {
            if (clusterCount == 0)
            {
                return Array.Empty<int>();
            }

            var counts = new int[clusterCount];
            int remaining = total;
            for (int index = 0; index < clusterCount; index++)
            {
                int remainingClusters = clusterCount - index - 1;
                int minimum = Math.Max(
                    minimumPerCluster,
                    remaining - remainingClusters * maximumPerCluster);
                int maximum = Math.Min(
                    maximumPerCluster,
                    remaining - remainingClusters * minimumPerCluster);
                counts[index] = index == clusterCount - 1
                    ? remaining
                    : UnityEngine.Random.Range(minimum, maximum + 1);
                remaining -= counts[index];
            }

            return counts;
        }

        private static Vector3 SampleDirectionNearCluster(
            ClusterCenter center,
            float clusterRadius)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float distance = Mathf.Sqrt(UnityEngine.Random.value) * clusterRadius;
            Vector3 tangentOffset =
                center.TangentX * (Mathf.Cos(angle) * distance) +
                center.TangentY * (Mathf.Sin(angle) * distance);
            return (center.Direction * center.SurfaceRadius + tangentOffset).normalized;
        }

        private static List<ProtectedRegion> BuildProtectedRegions(
            Scene scene,
            Vector3 planetCenter,
            float clearanceWorldUnits)
        {
            var regions = new List<ProtectedRegion>(ProtectedAreaNames.Length);
            foreach (string areaName in ProtectedAreaNames)
            {
                GameObject area = FindRoot(scene, areaName);
                Transform poles = area?.transform.Find("Perimeter/Poles");
                if (poles == null || poles.childCount < 3)
                {
                    throw new InvalidOperationException(
                        $"Planet Rock Scatter requires at least three direct poles at " +
                        $"'{areaName}/Perimeter/Poles'.");
                }

                var polePositions = new List<Vector3>(poles.childCount);
                for (int childIndex = 0; childIndex < poles.childCount; childIndex++)
                {
                    polePositions.Add(poles.GetChild(childIndex).position);
                }

                regions.Add(new ProtectedRegion(
                    areaName,
                    polePositions,
                    planetCenter,
                    clearanceWorldUnits));
            }

            return regions;
        }

        private static bool IsProtected(
            Vector3 direction,
            IReadOnlyList<ProtectedRegion> regions)
        {
            for (int index = 0; index < regions.Count; index++)
            {
                if (regions[index].Contains(direction))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTooClose(
            Vector3 candidate,
            IReadOnlyList<Vector3> existing,
            float minimumSpacing)
        {
            float minimumSquared = minimumSpacing * minimumSpacing;
            for (int index = existing.Count - 1; index >= 0; index--)
            {
                if ((candidate - existing[index]).sqrMagnitude < minimumSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<GameObject> LoadModelPrefabs(
            IReadOnlyList<string> modelNames)
        {
            var models = new List<GameObject>(modelNames.Count);
            for (int modelIndex = 0; modelIndex < modelNames.Count; modelIndex++)
            {
                string path = $"{RuntimeModelFolder}/{modelNames[modelIndex]}.fbx";
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null)
                {
                    throw new InvalidOperationException(
                        $"Planet Rock Scatter could not load '{path}'.");
                }

                models.Add(model);
            }

            return models;
        }

        private static float CalculateMaximumRockRadius(
            IEnumerable<GameObject> models,
            Scene scene)
        {
            float maximumRadius = 0f;
            foreach (GameObject source in models.Distinct())
            {
                var instance = PrefabUtility.InstantiatePrefab(source, scene) as GameObject;
                if (instance == null)
                {
                    continue;
                }

                try
                {
                    instance.transform.SetPositionAndRotation(
                        Vector3.zero,
                        Quaternion.Euler(ModelLocalXRotation, 0f, 0f));
                    instance.transform.localScale = Vector3.one * Mathf.Max(
                        MaximumSmallScale,
                        MaximumLargeScale);

                    foreach (MeshFilter meshFilter in
                             instance.GetComponentsInChildren<MeshFilter>(true))
                    {
                        Mesh mesh = meshFilter.sharedMesh;
                        if (mesh == null)
                        {
                            continue;
                        }

                        Bounds bounds = mesh.bounds;
                        Vector3 min = bounds.min;
                        Vector3 max = bounds.max;
                        for (int x = 0; x <= 1; x++)
                        {
                            for (int y = 0; y <= 1; y++)
                            {
                                for (int z = 0; z <= 1; z++)
                                {
                                    Vector3 localCorner = new Vector3(
                                        x == 0 ? min.x : max.x,
                                        y == 0 ? min.y : max.y,
                                        z == 0 ? min.z : max.z);
                                    Vector3 worldCorner = meshFilter.transform
                                        .TransformPoint(localCorner);
                                    maximumRadius = Mathf.Max(
                                        maximumRadius,
                                        Vector3.Distance(
                                            instance.transform.position,
                                            worldCorner));
                                }
                            }
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            if (maximumRadius <= 0f)
            {
                throw new InvalidOperationException(
                    "Planet Rock Scatter could not calculate rock model bounds.");
            }

            return maximumRadius;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
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

                for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
                {
                    Vector3 worldVertex = meshFilter.transform.localToWorldMatrix
                        .MultiplyPoint3x4(vertices[vertexIndex]);
                    minimumProjection = Mathf.Min(
                        minimumProjection,
                        Vector3.Dot(worldVertex - pivot, up));
                }
            }

            bool foundGeometry = !float.IsPositiveInfinity(minimumProjection);
            supportOffset = foundGeometry ? -minimumProjection : 0f;
            return foundGeometry;
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

                Matrix4x4 localToWorld = meshFilter.transform.localToWorldMatrix;
                for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
                {
                    Vector3 worldVertex = localToWorld.MultiplyPoint3x4(vertices[vertexIndex]);
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

            return float.IsNegativeInfinity(maximumRequiredOffset)
                ? 0f
                : maximumRequiredOffset;
        }

        private readonly struct ClusterPlan
        {
            public ClusterPlan(int smallCount, int largeCount)
            {
                SmallCount = smallCount;
                LargeCount = largeCount;
            }

            public int SmallCount { get; }
            public int LargeCount { get; }

            public string Label => SmallCount > 0 && LargeCount > 0
                ? "Mixed"
                : SmallCount > 0
                    ? "Small"
                    : "Large";
        }

        private readonly struct ClusterCenter
        {
            public ClusterCenter(Vector3 direction, Vector3 surfacePoint, Vector3 center)
            {
                Direction = direction.normalized;
                SurfaceRadius = Vector3.Distance(surfacePoint, center);
                Vector3 reference = Mathf.Abs(Vector3.Dot(Direction, Vector3.up)) > 0.9f
                    ? Vector3.right
                    : Vector3.up;
                TangentX = Vector3.Cross(reference, Direction).normalized;
                TangentY = Vector3.Cross(Direction, TangentX).normalized;
            }

            public Vector3 Direction { get; }
            public float SurfaceRadius { get; }
            public Vector3 TangentX { get; }
            public Vector3 TangentY { get; }
        }

        private sealed class ProtectedRegion
        {
            private readonly string name;
            private readonly Vector3 axis;
            private readonly Vector3 tangentX;
            private readonly Vector3 tangentY;
            private readonly Vector2[] polygon;
            private readonly float clearance;

            public ProtectedRegion(
                string name,
                IReadOnlyList<Vector3> worldPolePositions,
                Vector3 planetCenter,
                float clearanceWorldUnits)
            {
                this.name = name;
                var directions = new Vector3[worldPolePositions.Count];
                Vector3 axisSum = Vector3.zero;
                float radiusSum = 0f;
                for (int index = 0; index < worldPolePositions.Count; index++)
                {
                    Vector3 radial = worldPolePositions[index] - planetCenter;
                    radiusSum += radial.magnitude;
                    directions[index] = radial.normalized;
                    axisSum += directions[index];
                }

                axis = axisSum.normalized;
                Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f
                    ? Vector3.right
                    : Vector3.up;
                tangentX = Vector3.Cross(reference, axis).normalized;
                tangentY = Vector3.Cross(axis, tangentX).normalized;

                polygon = directions
                    .Select(Project)
                    .OrderBy(point => Mathf.Atan2(point.y, point.x))
                    .ToArray();
                float averageRadius = radiusSum / worldPolePositions.Count;
                clearance = Mathf.Tan(clearanceWorldUnits / averageRadius);
            }

            public bool Contains(Vector3 candidateDirection)
            {
                Vector3 direction = candidateDirection.normalized;
                if (Vector3.Dot(direction, axis) <= 0.01f)
                {
                    return false;
                }

                Vector2 point = Project(direction);
                if (IsPointInsidePolygon(point))
                {
                    return true;
                }

                for (int index = 0; index < polygon.Length; index++)
                {
                    Vector2 start = polygon[index];
                    Vector2 end = polygon[(index + 1) % polygon.Length];
                    if (DistanceToSegment(point, start, end) <= clearance)
                    {
                        return true;
                    }
                }

                return false;
            }

            private Vector2 Project(Vector3 direction)
            {
                float denominator = Vector3.Dot(direction, axis);
                if (denominator <= 0.0001f)
                {
                    throw new InvalidOperationException(
                        $"Planet Rock Scatter could not project protected ring '{name}'.");
                }

                return new Vector2(
                    Vector3.Dot(direction, tangentX) / denominator,
                    Vector3.Dot(direction, tangentY) / denominator);
            }

            private bool IsPointInsidePolygon(Vector2 point)
            {
                bool inside = false;
                for (int index = 0, previous = polygon.Length - 1;
                     index < polygon.Length;
                     previous = index++)
                {
                    Vector2 currentPoint = polygon[index];
                    Vector2 previousPoint = polygon[previous];
                    bool crosses = (currentPoint.y > point.y) !=
                                   (previousPoint.y > point.y);
                    if (!crosses)
                    {
                        continue;
                    }

                    float intersectionX =
                        (previousPoint.x - currentPoint.x) *
                        (point.y - currentPoint.y) /
                        (previousPoint.y - currentPoint.y) +
                        currentPoint.x;
                    if (point.x < intersectionX)
                    {
                        inside = !inside;
                    }
                }

                return inside;
            }

            private static float DistanceToSegment(
                Vector2 point,
                Vector2 start,
                Vector2 end)
            {
                Vector2 segment = end - start;
                float lengthSquared = segment.sqrMagnitude;
                if (lengthSquared <= Mathf.Epsilon)
                {
                    return Vector2.Distance(point, start);
                }

                float interpolation = Mathf.Clamp01(
                    Vector2.Dot(point - start, segment) / lengthSquared);
                return Vector2.Distance(point, start + segment * interpolation);
            }
        }
    }
}

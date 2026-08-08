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
    /// replaceable, editor-authored dressing pass over the active planet mesh.
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
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string VendorRelativeFolder =
            "asset packs/visuals/Ultimate Space Kit - March 2023/Environment/FBX";

        // Keep rerolls centered on the approximately 1,200-instance target.
        private const int DefaultMinimumCount = 1100;
        private const int DefaultMaximumCount = 1300;
        private const int AuthoredSampleCount = 1200;
        private const int AuthoredSampleSeed = 80;
        private const float MinimumScale = 65f;
        private const float MaximumScale = 75f;
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

        private static readonly int[] ModelWeights =
        {
            2, 2, 2,
            10, 10, 10,
            2, 2, 2
        };

        private static readonly MaterialVariant[] MaterialVariants =
        {
            new MaterialVariant("Dark Orange", BaseMaterialPath, new Color32(150, 55, 12, 255)),
            new MaterialVariant(
                "Red",
                RuntimeMaterialFolder + "/M_PlanetVegetation_Red.mat",
                new Color32(170, 32, 28, 255))
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
                DefaultMinimumCount,
                DefaultMaximumCount);
        }

        /// <summary>
        /// CI/batch entry point used to author the initial checked-in pass
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
        /// explicit so a pleasing pass can be reproduced exactly.
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
                    Vector3 direction = UnityEngine.Random.onUnitSphere;
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

                    GameObject source = modelPrefabs[
                        UnityEngine.Random.Range(0, modelPrefabs.Count)];
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
                    float scale = UnityEngine.Random.Range(MinimumScale, MaximumScale);
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
                    $"'{surface.name}' with seed {seed} and scale range " +
                    $"{MinimumScale:0.#}x-{MaximumScale:0.#}x.");
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
            var models = new List<GameObject>(ModelWeights.Sum());
            for (int modelIndex = 0; modelIndex < ModelNames.Length; modelIndex++)
            {
                string modelName = ModelNames[modelIndex];
                string path = $"{RuntimeModelFolder}/{modelName}.fbx";
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null)
                {
                    throw new InvalidOperationException(
                        $"Planet Vegetation Scatter could not load '{path}'.");
                }

                for (int weightIndex = 0;
                     weightIndex < ModelWeights[modelIndex];
                     weightIndex++)
                {
                    models.Add(model);
                }
            }

            return models;
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
                    StaticEditorFlags.BatchingStatic |
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
    }
}

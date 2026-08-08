using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WorldEditor
{
    /// <summary>
    /// Authors deterministic Rock_1 dot-matrix lettering at LandingBase/BaseCenter.
    /// The grid follows the planet geodesically and every rock is fitted to the
    /// authored crater mesh rather than an approximate sphere.
    /// </summary>
    public sealed class LandingBaseNautRockArt : EditorWindow
    {
        private const string PlanetRootName = "Planet Ground";
        private const string LandingBaseRootName = "LandingBase";
        private const string BaseCenterPath = "Layout/BaseCenter";
        private const string GeneratedRootName = "Generated NAUT Rock Art";
        private const string RockModelPath =
            "Assets/Art/Models/Environment/PlanetRocks/Rock_1.fbx";

        private const float DefaultRockScale = 100f;
        private const float DefaultHorizontalScaleMultiplier = 1.8f;
        private const float DefaultCellPitch = 2.88f;
        private const int DefaultSeed = 1401;
        private const float ModelLocalXRotation = -90f;
        private const float SurfaceEmbed = 0.075f;
        private const float CastPadding = 10f;
        private const int TerrainFitIterations = 3;
        private const float TerrainFitTolerance = 0.001f;
        private const int LetterHeight = 7;
        private const int LetterWidth = 5;
        private const int LetterGap = 1;

        private static readonly LetterPattern[] Letters =
        {
            new LetterPattern('N', new[]
            {
                "#...#",
                "##..#",
                "#.#.#",
                "#..##",
                "#...#",
                "#...#",
                "#...#"
            }),
            new LetterPattern('A', new[]
            {
                ".###.",
                "#...#",
                "#...#",
                "#####",
                "#...#",
                "#...#",
                "#...#"
            }),
            new LetterPattern('U', new[]
            {
                "#...#",
                "#...#",
                "#...#",
                "#...#",
                "#...#",
                ".#.#.",
                "..#.."
            }),
            new LetterPattern('T', new[]
            {
                "#####",
                "..#..",
                "..#..",
                "..#..",
                "..#..",
                "..#..",
                "..#.."
            })
        };

        [SerializeField] private float rockScale = DefaultRockScale;
        [SerializeField]
        private float horizontalScaleMultiplier = DefaultHorizontalScaleMultiplier;
        [SerializeField] private float cellPitch = DefaultCellPitch;
        [SerializeField] private float headingDegrees;
        [SerializeField] private int seed = DefaultSeed;
        [SerializeField] private bool enableCollision;

        [MenuItem("Tools/Planet Design/Landing Base NAUT Rock Art")]
        public static void Open()
        {
            LandingBaseNautRockArt window = GetWindow<LandingBaseNautRockArt>();
            window.titleContent = new GUIContent("NAUT Rock Art");
            window.minSize = new Vector2(390f, 325f);
            window.Show();
        }

        [MenuItem(
            "Tools/Planet Design/Regenerate Landing Base NAUT Rock Art (Defaults) %#&n",
            false,
            1)]
        public static void RegenerateDefaults()
        {
            Regenerate(
                DefaultRockScale,
                DefaultHorizontalScaleMultiplier,
                DefaultCellPitch,
                headingDegrees: 0f,
                seed: DefaultSeed,
                enableCollision: false);
        }

        [MenuItem(
            "Tools/Planet Design/Regenerate Landing Base NAUT Rock Art (Defaults) %#&n",
            true)]
        private static bool ValidateRegenerateDefaults()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Landing Base Rock Lettering", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Builds a surface-fitted 5x7 NAUT word from Rock_1 instances around " +
                "LandingBase/Layout/BaseCenter. Regeneration replaces only its named " +
                "generated child hierarchy.",
                MessageType.Info);

            rockScale = Mathf.Max(0.01f, EditorGUILayout.FloatField(
                new GUIContent("Rock Scale", "Literal Transform Y scale."),
                rockScale));
            horizontalScaleMultiplier = Mathf.Max(0.01f, EditorGUILayout.FloatField(
                new GUIContent(
                    "Horizontal X/Z Multiplier",
                    "Multiplies the rock's local X and Z scale while preserving Y."),
                horizontalScaleMultiplier));
            cellPitch = Mathf.Max(0.01f, EditorGUILayout.FloatField(
                new GUIContent("Cell Pitch", "World-space spacing between dot-matrix cells."),
                cellPitch));
            headingDegrees = EditorGUILayout.FloatField(
                new GUIContent(
                    "Heading",
                    "Rotates the complete word around BaseCenter's radial direction."),
                headingDegrees);
            seed = EditorGUILayout.IntField(
                new GUIContent("Seed", "Controls deterministic per-rock yaw."),
                seed);
            enableCollision = EditorGUILayout.Toggle(
                new GUIContent(
                    "Enable Collision",
                    "Off by default so aesthetic lettering does not snag the player."),
                enableCollision);

            EditorGUILayout.Space(12f);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Regenerate NAUT Rock Art", GUILayout.Height(36f)))
                {
                    Regenerate(
                        rockScale,
                        horizontalScaleMultiplier,
                        cellPitch,
                        headingDegrees,
                        seed,
                        enableCollision);
                }
            }
        }

        /// <summary>
        /// Replaces LandingBase's generated NAUT child using explicit,
        /// reproducible authoring settings.
        /// </summary>
        public static void Regenerate(
            float scale,
            float pitch,
            float headingDegrees,
            int seed,
            bool enableCollision)
        {
            Regenerate(
                scale,
                DefaultHorizontalScaleMultiplier,
                pitch,
                headingDegrees,
                seed,
                enableCollision);
        }

        /// <summary>
        /// Replaces LandingBase's generated NAUT child with an explicit local
        /// X/Z scale multiplier while preserving the supplied Y scale.
        /// </summary>
        public static void Regenerate(
            float scale,
            float horizontalMultiplier,
            float pitch,
            float headingDegrees,
            int seed,
            bool enableCollision)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Landing Base NAUT Rock Art can only run in Edit mode.");
            }

            if (scale <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scale),
                    "Rock scale must be greater than zero.");
            }

            if (horizontalMultiplier <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(horizontalMultiplier),
                    "Horizontal scale multiplier must be greater than zero.");
            }

            if (pitch <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pitch),
                    "Cell pitch must be greater than zero.");
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException(
                    "Landing Base NAUT Rock Art requires a loaded active scene.");
            }

            GameObject planet = FindSceneRoot(scene, PlanetRootName);
            GameObject landingBase = FindSceneRoot(scene, LandingBaseRootName);
            if (planet == null)
            {
                throw new InvalidOperationException(
                    $"No active-scene root named '{PlanetRootName}' was found.");
            }

            if (landingBase == null)
            {
                throw new InvalidOperationException(
                    $"No active-scene root named '{LandingBaseRootName}' was found.");
            }

            Transform baseCenter = landingBase.transform.Find(BaseCenterPath);
            if (baseCenter == null)
            {
                throw new InvalidOperationException(
                    $"'{LandingBaseRootName}/{BaseCenterPath}' was not found.");
            }

            Collider surface = FindSurfaceCollider(planet);
            Vector3 planetCenter = planet.transform.position;
            if (!RadialSurfaceSnapWindow.TryGetSurfaceHit(
                    baseCenter.position,
                    planetCenter,
                    surface,
                    CastPadding,
                    out RaycastHit anchorHit,
                    out _))
            {
                throw new InvalidOperationException(
                    "BaseCenter could not be projected onto the planet surface.");
            }

            Vector3 anchorDirection = (anchorHit.point - planetCenter).normalized;
            float anchorRadius = Vector3.Distance(anchorHit.point, planetCenter);
            if (anchorRadius <= 0.01f)
            {
                throw new InvalidOperationException(
                    "BaseCenter resolved too close to the planet center.");
            }

            Vector3 horizontal = ProjectBestTangent(baseCenter, anchorDirection);
            horizontal = Quaternion.AngleAxis(headingDegrees, anchorDirection) * horizontal;
            horizontal.Normalize();
            Vector3 vertical = Vector3.Cross(anchorDirection, horizontal).normalized;

            List<CellPlacement> placements = BuildPlacements(
                pitch,
                planetCenter,
                anchorDirection,
                anchorRadius,
                horizontal,
                vertical,
                surface);

            GameObject rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RockModelPath);
            if (rockPrefab == null)
            {
                UltimateSpaceRockAssetSetup.PrepareAssets();
                rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RockModelPath);
                if (rockPrefab == null)
                {
                    throw new InvalidOperationException(
                        $"Rock_1 could not be loaded from '{RockModelPath}'.");
                }
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Regenerate Landing Base NAUT Rock Art");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                RemoveExistingGeneratedRoot(landingBase.transform);

                var generatedRoot = new GameObject(GeneratedRootName);
                Undo.RegisterCreatedObjectUndo(generatedRoot, "Create NAUT Rock Art Root");
                generatedRoot.transform.SetParent(landingBase.transform, false);
                generatedRoot.transform.SetLocalPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                generatedRoot.transform.localScale = Vector3.one;

                var letterRoots = new Dictionary<char, Transform>();
                var meshVertexCache = new Dictionary<Mesh, Vector3[]>();
                var random = new System.Random(seed);

                for (int index = 0; index < placements.Count; index++)
                {
                    CellPlacement placement = placements[index];
                    if (!letterRoots.TryGetValue(placement.Letter, out Transform letterRoot))
                    {
                        var letterObject = new GameObject(placement.Letter.ToString());
                        Undo.RegisterCreatedObjectUndo(letterObject, "Create Rock Art Letter");
                        letterObject.transform.SetParent(generatedRoot.transform, false);
                        letterRoots.Add(placement.Letter, letterObject.transform);
                        letterRoot = letterObject.transform;
                    }

                    GameObject instance = PrefabUtility.InstantiatePrefab(
                        rockPrefab,
                        scene) as GameObject;
                    if (instance == null)
                    {
                        throw new InvalidOperationException(
                            $"Could not instantiate '{RockModelPath}'.");
                    }

                    Undo.RegisterCreatedObjectUndo(instance, "Create NAUT Rock");
                    instance.transform.SetParent(letterRoot, true);

                    Vector3 up = placement.SurfaceHit.normal.sqrMagnitude > 0.001f
                        ? placement.SurfaceHit.normal.normalized
                        : placement.Direction;
                    Quaternion surfaceAlignment = Quaternion.FromToRotation(Vector3.up, up);
                    float yaw = (float)(random.NextDouble() * 360.0);
                    Quaternion randomHeading = Quaternion.AngleAxis(yaw, up);
                    instance.transform.SetPositionAndRotation(
                        placement.SurfaceHit.point,
                        randomHeading * surfaceAlignment *
                        Quaternion.Euler(ModelLocalXRotation, 0f, 0f));
                    instance.transform.localScale = new Vector3(
                        scale * horizontalMultiplier,
                        scale,
                        scale * horizontalMultiplier);

                    float supportOffset = GetSurfaceSupportOffset(
                        instance,
                        up,
                        meshVertexCache);
                    instance.transform.position = placement.SurfaceHit.point + up * supportOffset;

                    for (int fitIteration = 0;
                         fitIteration < TerrainFitIterations;
                         fitIteration++)
                    {
                        float terrainOffset = GetTerrainConformingOffset(
                            instance,
                            planetCenter,
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
                    instance.name =
                        $"Rock_1_{placement.Letter}_{placement.LetterIndex + 1:00}";
                    SetCollisionEnabled(instance, enableCollision);
                    SetStaticRecursively(instance);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);

                    EditorUtility.DisplayProgressBar(
                        "Landing Base NAUT Rock Art",
                        $"Grounding rock {index + 1} of {placements.Count}",
                        (index + 1f) / placements.Count);
                }

                EditorUtility.SetDirty(generatedRoot);
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = generatedRoot;

                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        $"Could not save '{scene.path}'.");
                }

                Undo.CollapseUndoOperations(undoGroup);
                SceneView.RepaintAll();
                Debug.Log(
                    $"Landing Base NAUT Rock Art: placed {placements.Count} Rock_1 " +
                    $"instances at local scale " +
                    $"({scale * horizontalMultiplier:0.##}, {scale:0.##}, " +
                    $"{scale * horizontalMultiplier:0.##}) and {pitch:0.##}-unit pitch " +
                    $"around '{BaseCenterPath}' (heading {headingDegrees:0.##}, " +
                    $"seed {seed}, collision {(enableCollision ? "on" : "off")}).");
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static List<CellPlacement> BuildPlacements(
            float pitch,
            Vector3 planetCenter,
            Vector3 anchorDirection,
            float anchorRadius,
            Vector3 horizontal,
            Vector3 vertical,
            Collider surface)
        {
            int totalColumns = Letters.Length * LetterWidth +
                               (Letters.Length - 1) * LetterGap;
            float centerColumn = (totalColumns - 1) * 0.5f;
            float centerRow = (LetterHeight - 1) * 0.5f;
            var placements = new List<CellPlacement>();

            int letterColumnStart = 0;
            foreach (LetterPattern letter in Letters)
            {
                int letterIndex = 0;
                for (int row = 0; row < LetterHeight; row++)
                {
                    for (int column = 0; column < LetterWidth; column++)
                    {
                        if (letter.Rows[row][column] != '#')
                        {
                            continue;
                        }

                        float x = (letterColumnStart + column - centerColumn) * pitch;
                        float y = (centerRow - row) * pitch;
                        Vector3 tangentOffset = horizontal * x + vertical * y;
                        Vector3 direction = MapTangentOffsetToDirection(
                            anchorDirection,
                            tangentOffset,
                            anchorRadius);

                        if (!RadialSurfaceSnapWindow.TryGetSurfaceHit(
                                planetCenter + direction * anchorRadius,
                                planetCenter,
                                surface,
                                CastPadding,
                                out RaycastHit hit,
                                out _))
                        {
                            throw new InvalidOperationException(
                                $"Could not surface-project {letter.Name} cell " +
                                $"({row}, {column}). The previous art was not changed.");
                        }

                        placements.Add(new CellPlacement(
                            letter.Name,
                            letterIndex++,
                            direction,
                            hit));
                    }
                }

                letterColumnStart += LetterWidth + LetterGap;
            }

            return placements;
        }

        private static Vector3 MapTangentOffsetToDirection(
            Vector3 anchorDirection,
            Vector3 tangentOffset,
            float radius)
        {
            float distance = tangentOffset.magnitude;
            if (distance <= 0.0001f)
            {
                return anchorDirection;
            }

            float angle = distance / radius;
            return (Mathf.Cos(angle) * anchorDirection +
                    Mathf.Sin(angle) * tangentOffset / distance).normalized;
        }

        private static Vector3 ProjectBestTangent(
            Transform baseCenter,
            Vector3 anchorDirection)
        {
            Vector3[] candidates =
            {
                baseCenter.right,
                baseCenter.up,
                baseCenter.forward,
                Vector3.right,
                Vector3.forward
            };

            Vector3 best = Vector3.zero;
            float bestMagnitude = 0f;
            foreach (Vector3 candidate in candidates)
            {
                Vector3 tangent = Vector3.ProjectOnPlane(candidate, anchorDirection);
                if (tangent.sqrMagnitude > bestMagnitude)
                {
                    best = tangent;
                    bestMagnitude = tangent.sqrMagnitude;
                }
            }

            if (bestMagnitude <= 0.0001f)
            {
                throw new InvalidOperationException(
                    "Could not derive a tangent heading from BaseCenter.");
            }

            return best.normalized;
        }

        private static GameObject FindSceneRoot(Scene scene, string name)
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

        private static void RemoveExistingGeneratedRoot(Transform landingBase)
        {
            for (int childIndex = landingBase.childCount - 1; childIndex >= 0; childIndex--)
            {
                Transform child = landingBase.GetChild(childIndex);
                if (child.name == GeneratedRootName)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        private static float GetSurfaceSupportOffset(
            GameObject root,
            Vector3 up,
            IDictionary<Mesh, Vector3[]> meshVertexCache)
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

                Vector3[] vertices = GetMeshVertices(mesh, meshVertexCache);
                Matrix4x4 localToWorld = meshFilter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in vertices)
                {
                    Vector3 worldVertex = localToWorld.MultiplyPoint3x4(vertex);
                    minimumProjection = Mathf.Min(
                        minimumProjection,
                        Vector3.Dot(worldVertex - pivot, up));
                }
            }

            return float.IsPositiveInfinity(minimumProjection)
                ? 0f
                : -minimumProjection;
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

                Vector3[] vertices = GetMeshVertices(mesh, meshVertexCache);
                Matrix4x4 localToWorld = meshFilter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in vertices)
                {
                    Vector3 worldVertex = localToWorld.MultiplyPoint3x4(vertex);
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

        private static Vector3[] GetMeshVertices(
            Mesh mesh,
            IDictionary<Mesh, Vector3[]> meshVertexCache)
        {
            if (!meshVertexCache.TryGetValue(mesh, out Vector3[] vertices))
            {
                vertices = mesh.vertices;
                meshVertexCache.Add(mesh, vertices);
            }

            return vertices;
        }

        private static void SetCollisionEnabled(GameObject root, bool enabled)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = enabled;
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                EditorUtility.SetDirty(collider);
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

        private readonly struct LetterPattern
        {
            public LetterPattern(char name, string[] rows)
            {
                if (rows == null || rows.Length != LetterHeight ||
                    rows.Any(row => row == null || row.Length != LetterWidth))
                {
                    throw new ArgumentException("Letter patterns must be exactly 5x7.");
                }

                Name = name;
                Rows = rows;
            }

            public char Name { get; }
            public string[] Rows { get; }
        }

        private readonly struct CellPlacement
        {
            public CellPlacement(
                char letter,
                int letterIndex,
                Vector3 direction,
                RaycastHit surfaceHit)
            {
                Letter = letter;
                LetterIndex = letterIndex;
                Direction = direction;
                SurfaceHit = surfaceHit;
            }

            public char Letter { get; }
            public int LetterIndex { get; }
            public Vector3 Direction { get; }
            public RaycastHit SurfaceHit { get; }
        }
    }
}

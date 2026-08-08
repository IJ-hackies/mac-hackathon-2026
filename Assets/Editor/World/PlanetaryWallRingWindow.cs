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
    /// Arranges existing scene objects into a closed ring on an authored planet
    /// surface and can generate fitted connector prefabs between the ring nodes.
    /// </summary>
    public sealed class PlanetaryWallRingWindow : EditorWindow
    {
        private const string PlanetRootName = "Planet Ground";
        private const string GeneratedWallMeshFolder = "Assets/Art/Generated/LandingBaseWalls";
        private const float DefaultCastPadding = 10f;
        private const float DirectionEpsilon = 0.0001f;

        private enum HorizontalAxis
        {
            AutoLongest,
            LocalX,
            LocalZ
        }

        [Header("Planet")]
        [SerializeField] private Transform planetCenter;
        [SerializeField] private Collider surfaceCollider;
        [SerializeField] private Transform ringCenterAnchor;

        [Header("Ring")]
        [SerializeField] private HorizontalAxis wallLengthAxis = HorizontalAxis.AutoLongest;
        [SerializeField, Min(0.01f)] private float ringRadius = 15f;
        [SerializeField] private float gap;
        [SerializeField] private float angleOffsetDegrees;
        [SerializeField] private float wallSurfaceOffset;
        [SerializeField] private bool alignWallsToSurfaceNormal;
        [SerializeField] private bool reverseDirection;

        [Header("Curved Wall Sheets")]
        [SerializeField] private Material curvedWallMaterial;
        [SerializeField] private bool closeCurvedWallLoop;
        [SerializeField] private Transform curvedWallOpeningPoleA;
        [SerializeField] private Transform curvedWallOpeningPoleB;
        [SerializeField, Min(0.01f)] private float curvedWallHeight = 4f;
        [SerializeField, Min(0.01f)] private float curvedWallThickness = 0.15f;
        [SerializeField, Min(0f)] private float curvedWallPoleClearance = 0.6f;
        [SerializeField] private float curvedWallBaseOffset = 0.02f;
        [SerializeField, Range(1, 32)] private int curvedWallSegmentsPerSpan = 6;
        [SerializeField, Min(0.001f)] private float curvedWallUvTilesPerUnit = 0.5f;
        [SerializeField] private bool alignCurvedWallsToSurfaceNormal;
        [SerializeField] private bool addCurvedWallCollider = true;

        [Header("Connectors")]
        [SerializeField] private GameObject connectorPrefab;
        [SerializeField] private HorizontalAxis connectorLengthAxis = HorizontalAxis.AutoLongest;
        [SerializeField] private bool scaleConnectorsToFit = true;
        [SerializeField, Min(0f)] private float connectorEndInset;
        [SerializeField] private float connectorSurfaceOffset;
        [SerializeField] private bool alignConnectorsToSurfaceNormal;

        [SerializeField] private Vector2 scrollPosition;

        [MenuItem("Tools/Planet Design/Wall Ring Builder")]
        public static void Open()
        {
            PlanetaryWallRingWindow window = GetWindow<PlanetaryWallRingWindow>();
            window.titleContent = new GUIContent("Wall Ring");
            window.minSize = new Vector2(420f, 590f);
            window.TryResolvePlanetDefaults();
            window.Show();
        }

        private void OnEnable()
        {
            TryResolvePlanetDefaults();
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Planet Surface", EditorStyles.boldLabel);
            planetCenter = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Planet Center", "The transform at the center of radial gravity."),
                planetCenter,
                typeof(Transform),
                true);
            surfaceCollider = (Collider)EditorGUILayout.ObjectField(
                new GUIContent("Surface Collider", "The authored planet collider used for every wall position."),
                surfaceCollider,
                typeof(Collider),
                true);
            ringCenterAnchor = (Transform)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Center Anchor (Optional)",
                    "Only its direction from the planet center is used. Without one, the ring center is inferred from the selected walls; use an anchor for incomplete or uneven rings."),
                ringCenterAnchor,
                typeof(Transform),
                true);

            if (GUILayout.Button("Find Planet Ground And Collider"))
            {
                TryResolvePlanetDefaults(force: true);
            }

            List<Transform> selection = GetTopLevelEditableSelection();
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                selection.Count < 3
                    ? "Select at least three top-level wall objects in the scene."
                    : $"{selection.Count} wall objects selected. The active object anchors the ring's starting angle.",
                selection.Count < 3 ? MessageType.Info : MessageType.None);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Closed Ring Layout", EditorStyles.boldLabel);
            wallLengthAxis = (HorizontalAxis)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "Wall Length Axis",
                    "The prefab's local axis that should run around the ring. Auto chooses the longer rendered X/Z span."),
                wallLengthAxis);
            ringRadius = Mathf.Max(0.01f, EditorGUILayout.FloatField(
                new GUIContent("Ring Radius", "Projected world-space radius around the base axis."),
                ringRadius));
            gap = EditorGUILayout.FloatField(
                new GUIContent("Gap", "Extra space between adjacent wall pieces when fitting the radius."),
                gap);
            angleOffsetDegrees = EditorGUILayout.FloatField(
                new GUIContent("Angle Offset", "Rotates the complete layout around its center axis."),
                angleOffsetDegrees);
            wallSurfaceOffset = EditorGUILayout.FloatField(
                new GUIContent("Surface Offset", "Moves wall pivots outward from the hit surface."),
                wallSurfaceOffset);
            alignWallsToSurfaceNormal = EditorGUILayout.Toggle(
                new GUIContent(
                    "Use Surface Normal",
                    "Tilts each wall to local crater slopes. Radial up usually makes a cleaner structural ring."),
                alignWallsToSurfaceNormal);
            reverseDirection = EditorGUILayout.Toggle(
                new GUIContent("Reverse Direction", "Reverses the order and tangent direction around the ring."),
                reverseDirection);

            using (new EditorGUI.DisabledScope(!CanOperateOnSelection(selection)))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Read Radius From Selection"))
                {
                    ReadRadiusFromSelection(selection);
                }

                if (GUILayout.Button("Fit Radius End-To-End"))
                {
                    FitRadiusToSelection(selection);
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Arrange Selected Walls Into Closed Ring", GUILayout.Height(34f)))
                {
                    ArrangeSelection(selection);
                }
            }

            EditorGUILayout.Space(14f);
            EditorGUILayout.LabelField("Curved Wall Sheets", EditorStyles.boldLabel);
            curvedWallMaterial = (Material)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Wall Material",
                    "Material for the generated sheets. If empty, the active pole's first material is reused."),
                curvedWallMaterial,
                typeof(Material),
                false);
            closeCurvedWallLoop = EditorGUILayout.Toggle(
                new GUIContent(
                    "Close Loop",
                    "When disabled, the explicitly assigned opening-pole pair is left unconnected."),
                closeCurvedWallLoop);
            using (new EditorGUI.DisabledScope(closeCurvedWallLoop))
            {
                curvedWallOpeningPoleA = (Transform)EditorGUILayout.ObjectField(
                    new GUIContent(
                        "Opening Pole A",
                        "One of the two neighboring poles that should have no wall between them."),
                    curvedWallOpeningPoleA,
                    typeof(Transform),
                    true);
                curvedWallOpeningPoleB = (Transform)EditorGUILayout.ObjectField(
                    new GUIContent(
                        "Opening Pole B",
                        "The other neighboring pole that should have no wall between them."),
                    curvedWallOpeningPoleB,
                    typeof(Transform),
                    true);
                using (new EditorGUI.DisabledScope(selection.Count != 2))
                {
                    if (GUILayout.Button("Use Two Selected Poles As Opening"))
                    {
                        curvedWallOpeningPoleA = selection[0];
                        curvedWallOpeningPoleB = selection[1];
                    }
                }
            }
            curvedWallHeight = Mathf.Max(0.01f, EditorGUILayout.FloatField(
                new GUIContent("Height", "Wall height measured outward from the planet surface."),
                curvedWallHeight));
            curvedWallThickness = Mathf.Max(0.01f, EditorGUILayout.FloatField(
                new GUIContent("Thickness", "Depth of the generated solid wall sheet."),
                curvedWallThickness));
            curvedWallPoleClearance = Mathf.Max(0f, EditorGUILayout.FloatField(
                new GUIContent("Pole Clearance", "Space left between each sheet end and the pole center."),
                curvedWallPoleClearance));
            curvedWallBaseOffset = EditorGUILayout.FloatField(
                new GUIContent("Base Offset", "Moves the wall bottom outward from the hit surface."),
                curvedWallBaseOffset);
            curvedWallSegmentsPerSpan = EditorGUILayout.IntSlider(
                new GUIContent("Curve Segments", "More segments produce a smoother arc between each pair of poles."),
                curvedWallSegmentsPerSpan,
                1,
                32);
            curvedWallUvTilesPerUnit = Mathf.Max(0.001f, EditorGUILayout.FloatField(
                new GUIContent("UV Tiles Per Unit", "Controls texture repetition along the generated mesh."),
                curvedWallUvTilesPerUnit));
            alignCurvedWallsToSurfaceNormal = EditorGUILayout.Toggle(
                new GUIContent(
                    "Use Surface Normal",
                    "Makes the wall follow local crater tilt. Radial up produces a cleaner architectural silhouette."),
                alignCurvedWallsToSurfaceNormal);
            addCurvedWallCollider = EditorGUILayout.Toggle(
                new GUIContent("Add Mesh Collider", "Makes the generated static wall block the player."),
                addCurvedWallCollider);

            bool hasOpeningPair = curvedWallOpeningPoleA != null &&
                                  curvedWallOpeningPoleB != null;
            using (new EditorGUI.DisabledScope(
                       !CanOperateOnSelection(selection) ||
                       (!closeCurvedWallLoop && !hasOpeningPair)))
            {
                if (GUILayout.Button("Generate Curved Sheets Between Poles", GUILayout.Height(34f)))
                {
                    GenerateCurvedWallSheets(selection);
                }
            }

            EditorGUILayout.HelpBox(
                "Creates one solid curved wall mesh between neighboring poles. For an entrance, disable Close Loop and assign its exact two poles, or select only those two and capture them before reselecting the complete ring. The scene object is Undoable; its reusable mesh asset is kept under Assets/Art/Generated/LandingBaseWalls.",
                MessageType.Info);

            EditorGUILayout.Space(14f);
            EditorGUILayout.LabelField("Optional Connector Generation", EditorStyles.boldLabel);
            connectorPrefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Connector Prefab",
                    "A project prefab or model inserted between every adjacent wall, including the closing pair."),
                connectorPrefab,
                typeof(GameObject),
                false);
            connectorLengthAxis = (HorizontalAxis)EditorGUILayout.EnumPopup(
                new GUIContent("Connector Length Axis", "The connector axis that spans between wall centers."),
                connectorLengthAxis);
            scaleConnectorsToFit = EditorGUILayout.Toggle(
                new GUIContent("Scale To Fit", "Scales only the selected connector length axis."),
                scaleConnectorsToFit);
            connectorEndInset = Mathf.Max(0f, EditorGUILayout.FloatField(
                new GUIContent("End Inset", "Shortens each connector at both ends to avoid overlapping posts."),
                connectorEndInset));
            connectorSurfaceOffset = EditorGUILayout.FloatField(
                new GUIContent("Surface Offset", "Moves connector pivots outward from the surface."),
                connectorSurfaceOffset);
            alignConnectorsToSurfaceNormal = EditorGUILayout.Toggle(
                new GUIContent("Use Surface Normal", "Tilts each connector to the local crater surface."),
                alignConnectorsToSurfaceNormal);

            using (new EditorGUI.DisabledScope(
                       !CanOperateOnSelection(selection) || connectorPrefab == null))
            {
                if (GUILayout.Button("Generate Closed-Loop Connectors", GUILayout.Height(34f)))
                {
                    GenerateConnectors(selection);
                }
            }

            EditorGUILayout.HelpBox(
                "Connector generation creates a new scene-root group and is fully Undoable. Re-running it creates another group so existing work is never deleted automatically.",
                MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private bool CanOperateOnSelection(IReadOnlyCollection<Transform> selection)
        {
            return planetCenter != null && surfaceCollider != null && selection.Count >= 3;
        }

        private void ReadRadiusFromSelection(List<Transform> selection)
        {
            if (!TryBuildRingFrame(selection, out RingFrame frame, out string error))
            {
                Debug.LogError($"Wall Ring Builder: {error}");
                return;
            }

            ringRadius = selection
                .Select(target => Vector3.ProjectOnPlane(target.position - frame.PlanetCenter, frame.Axis).magnitude)
                .Average();
            Repaint();
        }

        private void FitRadiusToSelection(List<Transform> selection)
        {
            if (!TryBuildRingFrame(selection, out RingFrame frame, out string error))
            {
                Debug.LogError($"Wall Ring Builder: {error}");
                return;
            }

            HorizontalAxis resolvedAxis = ResolveCommonAxis(selection, wallLengthAxis);
            float[] spans = selection
                .Select(target => MeasureRenderedSpan(target, resolvedAxis))
                .Where(span => span > 0.001f)
                .OrderBy(span => span)
                .ToArray();
            if (spans.Length == 0)
            {
                Debug.LogError("Wall Ring Builder: selected walls have no measurable Renderer bounds.");
                return;
            }

            float medianSpan = spans[spans.Length / 2];
            if (spans[^1] > medianSpan * 1.1f || spans[0] < medianSpan * 0.9f)
            {
                Debug.LogWarning(
                    "Wall Ring Builder: selected wall lengths vary by more than 10%. " +
                    "The fitted radius uses the median length, so mixed pieces may leave gaps or overlap.");
            }

            float sideLength = medianSpan + gap;
            if (sideLength <= 0.001f)
            {
                Debug.LogError("Wall Ring Builder: wall width plus Gap must be greater than zero.");
                return;
            }

            ringRadius = sideLength / (2f * Mathf.Tan(Mathf.PI / selection.Count));
            if (ringRadius >= frame.NominalSurfaceRadius)
            {
                ringRadius = frame.NominalSurfaceRadius * 0.98f;
                Debug.LogWarning(
                    "Wall Ring Builder: fitted radius exceeded the planet surface radius and was clamped.");
            }

            Repaint();
        }

        private void ArrangeSelection(List<Transform> selection)
        {
            if (!TryBuildRingFrame(selection, out RingFrame frame, out string error))
            {
                Debug.LogError($"Wall Ring Builder: {error}");
                return;
            }

            if (ringRadius >= frame.NominalSurfaceRadius)
            {
                Debug.LogError(
                    $"Wall Ring Builder: Ring Radius must be less than the local planet radius ({frame.NominalSurfaceRadius:F2}).");
                return;
            }

            HorizontalAxis resolvedAxis = ResolveCommonAxis(selection, wallLengthAxis);
            List<Transform> ordered = SortAroundRing(selection, frame);
            float angularRadius = Mathf.Asin(Mathf.Clamp01(ringRadius / frame.NominalSurfaceRadius));
            float step = Mathf.PI * 2f / ordered.Count;
            float directionSign = reverseDirection ? -1f : 1f;
            float phase = angleOffsetDegrees * Mathf.Deg2Rad;

            var placements = new List<WallPlacement>(ordered.Count);
            for (int index = 0; index < ordered.Count; index++)
            {
                float angle = phase + directionSign * step * index;
                Vector3 ringDirection = GetRingDirection(frame, angularRadius, angle);
                if (!TryGetSurfaceHit(
                        ringDirection,
                        frame.PlanetCenter,
                        surfaceCollider,
                        out RaycastHit hit))
                {
                    Debug.LogError(
                        $"Wall Ring Builder: the planet collider was not hit for wall {index + 1}. Nothing was moved.");
                    return;
                }

                Vector3 up = ResolvePlacementUp(
                    alignWallsToSurfaceNormal,
                    hit.normal,
                    ringDirection);
                Vector3 tangent = GetRingTangent(frame, angle, up) * directionSign;
                placements.Add(new WallPlacement(
                    ordered[index],
                    hit.point + up * wallSurfaceOffset,
                    RotationForAxis(resolvedAxis, tangent, up)));
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Arrange Planetary Wall Ring");
            int undoGroup = Undo.GetCurrentGroup();
            foreach (WallPlacement placement in placements)
            {
                Undo.RecordObject(placement.Target, "Arrange Planetary Wall Ring");
            }

            foreach (WallPlacement placement in placements)
            {
                placement.Target.position = placement.Position;
                placement.Target.rotation = placement.Rotation;
                PrefabUtility.RecordPrefabInstancePropertyModifications(placement.Target);
                EditorUtility.SetDirty(placement.Target);
                EditorSceneManager.MarkSceneDirty(placement.Target.gameObject.scene);
            }

            Undo.CollapseUndoOperations(undoGroup);
            SceneView.RepaintAll();
            Debug.Log(
                $"Wall Ring Builder: arranged {ordered.Count} walls into a closed ring with radius {ringRadius:F2}.");
        }

        private void GenerateCurvedWallSheets(List<Transform> selection)
        {
            if (!TryBuildRingFrame(selection, out RingFrame frame, out string error))
            {
                Debug.LogError($"Wall Ring Builder: {error}");
                return;
            }

            Material material = ResolveCurvedWallMaterial(selection);
            if (material == null)
            {
                Debug.LogError(
                    "Wall Ring Builder: assign a Wall Material or select a pole with a material first.");
                return;
            }

            List<Transform> ordered = SortAroundRing(selection, frame);
            int spanCount = closeCurvedWallLoop ? ordered.Count : ordered.Count - 1;
            var panels = new List<IReadOnlyList<CurvedWallMeshBuilder.Sample>>(spanCount);
            int skippedSpanIndex = -1;
            if (!closeCurvedWallLoop)
            {
                if (curvedWallOpeningPoleA == null || curvedWallOpeningPoleB == null)
                {
                    Debug.LogError(
                        "Wall Ring Builder: assign both Opening Pole A and Opening Pole B. Nothing was generated.");
                    return;
                }

                if (curvedWallOpeningPoleA == curvedWallOpeningPoleB)
                {
                    Debug.LogError(
                        "Wall Ring Builder: the two opening poles must be different. Nothing was generated.");
                    return;
                }

                for (int index = 0; index < ordered.Count; index++)
                {
                    Transform start = ordered[index];
                    Transform end = ordered[(index + 1) % ordered.Count];
                    bool matchesForward = start == curvedWallOpeningPoleA &&
                                          end == curvedWallOpeningPoleB;
                    bool matchesReverse = start == curvedWallOpeningPoleB &&
                                          end == curvedWallOpeningPoleA;
                    if (matchesForward || matchesReverse)
                    {
                        skippedSpanIndex = index;
                        break;
                    }
                }

                if (skippedSpanIndex < 0)
                {
                    Debug.LogError(
                        "Wall Ring Builder: the assigned opening poles must both be selected and adjacent in the ring. Nothing was generated.");
                    return;
                }
            }

            for (int index = 0; index < ordered.Count; index++)
            {
                if (index == skippedSpanIndex)
                {
                    continue;
                }

                Transform start = ordered[index];
                Transform end = ordered[(index + 1) % ordered.Count];
                Vector3 startDirection = start.position - frame.PlanetCenter;
                Vector3 endDirection = end.position - frame.PlanetCenter;
                if (startDirection.sqrMagnitude < DirectionEpsilon ||
                    endDirection.sqrMagnitude < DirectionEpsilon)
                {
                    Debug.LogError(
                        $"Wall Ring Builder: pole pair {index + 1} is too close to the planet center. Nothing was generated.");
                    return;
                }

                startDirection.Normalize();
                endDirection.Normalize();
                float approximateLength = Vector3.Distance(start.position, end.position);
                if (curvedWallPoleClearance * 2f >= approximateLength)
                {
                    Debug.LogError(
                        $"Wall Ring Builder: Pole Clearance is too large for the span between '{start.name}' and '{end.name}'. Nothing was generated.");
                    return;
                }

                float startAngle = GetRingAngle(startDirection, frame);
                float endAngle = GetRingAngle(endDirection, frame);
                float angleDelta = Mathf.Repeat(endAngle - startAngle, Mathf.PI * 2f);
                if (angleDelta < DirectionEpsilon)
                {
                    Debug.LogError(
                        $"Wall Ring Builder: pole pair {index + 1} has no usable arc. Nothing was generated.");
                    return;
                }

                float startAngularRadius = Mathf.Acos(Mathf.Clamp(
                    Vector3.Dot(startDirection, frame.Axis),
                    -1f,
                    1f));
                float endAngularRadius = Mathf.Acos(Mathf.Clamp(
                    Vector3.Dot(endDirection, frame.Axis),
                    -1f,
                    1f));
                float insetFraction = curvedWallPoleClearance / approximateLength;
                var samples = new List<CurvedWallMeshBuilder.Sample>(
                    curvedWallSegmentsPerSpan + 1);

                for (int segment = 0; segment <= curvedWallSegmentsPerSpan; segment++)
                {
                    float spanT = segment / (float)curvedWallSegmentsPerSpan;
                    float t = Mathf.Lerp(insetFraction, 1f - insetFraction, spanT);
                    float angle = startAngle + angleDelta * t;
                    float angularRadius = Mathf.Lerp(
                        startAngularRadius,
                        endAngularRadius,
                        t);
                    Vector3 direction = GetRingDirection(frame, angularRadius, angle);
                    if (!TryGetSurfaceHit(
                            direction,
                            frame.PlanetCenter,
                            surfaceCollider,
                            out RaycastHit hit))
                    {
                        Debug.LogError(
                            $"Wall Ring Builder: the planet surface was not hit while sampling span {index + 1}. Nothing was generated.");
                        return;
                    }

                    Vector3 up = ResolvePlacementUp(
                        alignCurvedWallsToSurfaceNormal,
                        hit.normal,
                        direction);
                    Vector3 localBase = hit.point + up * curvedWallBaseOffset -
                                        frame.PlanetCenter;
                    samples.Add(new CurvedWallMeshBuilder.Sample(localBase, up));
                }

                panels.Add(samples);
            }

            Mesh mesh = CurvedWallMeshBuilder.Build(
                panels,
                curvedWallHeight,
                curvedWallThickness,
                curvedWallUvTilesPerUnit);
            if (mesh.vertexCount == 0)
            {
                DestroyImmediate(mesh);
                Debug.LogError("Wall Ring Builder: curved wall generation produced an empty mesh.");
                return;
            }

            EnsureAssetFolder(GeneratedWallMeshFolder);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{GeneratedWallMeshFolder}/CurvedWallRing.asset");
            try
            {
                AssetDatabase.CreateAsset(mesh, assetPath);
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                if (AssetDatabase.Contains(mesh))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
                else
                {
                    DestroyImmediate(mesh);
                }

                Debug.LogException(exception);
                return;
            }

            Scene targetScene = ordered[0].gameObject.scene;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Generate Curved Planetary Wall Sheets");
            int undoGroup = Undo.GetCurrentGroup();

            var wallRoot = new GameObject("Generated Curved Wall Sheets");
            wallRoot.transform.position = frame.PlanetCenter;
            SceneManager.MoveGameObjectToScene(wallRoot, targetScene);
            Undo.RegisterCreatedObjectUndo(wallRoot, "Create Curved Wall Group");

            var wallObject = new GameObject("Curved Wall Mesh");
            wallObject.transform.SetParent(wallRoot.transform, false);
            MeshFilter meshFilter = wallObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = wallObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            if (addCurvedWallCollider)
            {
                MeshCollider meshCollider = wallObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
                meshCollider.convex = false;
            }

            Undo.RegisterCreatedObjectUndo(wallObject, "Create Curved Wall Mesh");
            EditorSceneManager.MarkSceneDirty(targetScene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = wallRoot;
            SceneView.RepaintAll();
            Debug.Log(
                $"Wall Ring Builder: generated {panels.Count} curved wall span(s) using '{assetPath}'.");
        }

        private void GenerateConnectors(List<Transform> selection)
        {
            if (!EditorUtility.IsPersistent(connectorPrefab))
            {
                Debug.LogError("Wall Ring Builder: Connector Prefab must be a project asset, not a scene object.");
                return;
            }

            if (!TryBuildRingFrame(selection, out RingFrame frame, out string error))
            {
                Debug.LogError($"Wall Ring Builder: {error}");
                return;
            }

            List<Transform> ordered = SortAroundRing(selection, frame);
            Scene targetScene = ordered[0].gameObject.scene;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Generate Planetary Wall Connectors");
            int undoGroup = Undo.GetCurrentGroup();

            var connectorRoot = new GameObject("Generated Wall Connectors");
            SceneManager.MoveGameObjectToScene(connectorRoot, targetScene);
            Undo.RegisterCreatedObjectUndo(connectorRoot, "Create Wall Connector Group");

            int created = 0;
            for (int index = 0; index < ordered.Count; index++)
            {
                Transform start = ordered[index];
                Transform end = ordered[(index + 1) % ordered.Count];
                Vector3 startDirection = (start.position - frame.PlanetCenter).normalized;
                Vector3 endDirection = (end.position - frame.PlanetCenter).normalized;
                Vector3 midpointDirection = startDirection + endDirection;
                if (midpointDirection.sqrMagnitude < DirectionEpsilon)
                {
                    Debug.LogWarning(
                        $"Wall Ring Builder: skipped connector {index + 1} because its endpoints are opposite each other.");
                    continue;
                }

                midpointDirection.Normalize();
                if (!TryGetSurfaceHit(
                        midpointDirection,
                        frame.PlanetCenter,
                        surfaceCollider,
                        out RaycastHit hit))
                {
                    Debug.LogWarning(
                        $"Wall Ring Builder: skipped connector {index + 1} because the planet surface was not hit.");
                    continue;
                }

                Vector3 up = ResolvePlacementUp(
                    alignConnectorsToSurfaceNormal,
                    hit.normal,
                    midpointDirection);
                Vector3 connectionDirection = Vector3.ProjectOnPlane(end.position - start.position, up);
                if (connectionDirection.sqrMagnitude < DirectionEpsilon)
                {
                    continue;
                }

                connectionDirection.Normalize();
                GameObject instance = PrefabUtility.InstantiatePrefab(
                    connectorPrefab,
                    connectorRoot.transform) as GameObject;
                if (instance == null)
                {
                    Debug.LogError(
                        $"Wall Ring Builder: could not instantiate connector asset '{connectorPrefab.name}'.");
                    continue;
                }

                Undo.RegisterCreatedObjectUndo(instance, "Create Wall Connector");
                instance.name = $"{connectorPrefab.name}_{index + 1:D2}";
                HorizontalAxis resolvedAxis = ResolveAxis(instance.transform, connectorLengthAxis);
                instance.transform.position = hit.point + up * connectorSurfaceOffset;
                instance.transform.rotation = RotationForAxis(resolvedAxis, connectionDirection, up);

                if (scaleConnectorsToFit)
                {
                    float targetLength = Mathf.Max(
                        0.01f,
                        Vector3.Distance(start.position, end.position) - connectorEndInset * 2f);
                    float currentLength = MeasureRenderedSpan(instance.transform, resolvedAxis);
                    if (currentLength > 0.001f)
                    {
                        Vector3 localScale = instance.transform.localScale;
                        float factor = targetLength / currentLength;
                        if (resolvedAxis == HorizontalAxis.LocalX)
                        {
                            localScale.x *= factor;
                        }
                        else
                        {
                            localScale.z *= factor;
                        }

                        instance.transform.localScale = localScale;
                    }
                }

                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                created++;
            }

            EditorSceneManager.MarkSceneDirty(targetScene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = connectorRoot;
            SceneView.RepaintAll();
            Debug.Log(
                $"Wall Ring Builder: generated {created} connector(s) under '{connectorRoot.name}'.");
        }

        private bool TryBuildRingFrame(
            IReadOnlyList<Transform> selection,
            out RingFrame frame,
            out string error)
        {
            if (planetCenter == null || surfaceCollider == null)
            {
                frame = default;
                error = "assign a planet center and surface collider first.";
                return false;
            }

            if (!surfaceCollider.enabled || !surfaceCollider.gameObject.activeInHierarchy)
            {
                frame = default;
                error = "the assigned surface collider must be enabled and active.";
                return false;
            }

            int sceneHandle = selection[0].gameObject.scene.handle;
            if (selection.Any(target => target.gameObject.scene.handle != sceneHandle))
            {
                frame = default;
                error = "all selected walls must belong to the same scene.";
                return false;
            }

            Vector3 center = planetCenter.position;
            Vector3 axis;
            if (ringCenterAnchor != null)
            {
                axis = ringCenterAnchor.position - center;
            }
            else
            {
                axis = Vector3.zero;
                foreach (Transform target in selection)
                {
                    Vector3 direction = target.position - center;
                    if (direction.sqrMagnitude >= DirectionEpsilon)
                    {
                        axis += direction.normalized;
                    }
                }
            }

            if (axis.sqrMagnitude < DirectionEpsilon)
            {
                frame = default;
                error = "the ring-center direction could not be inferred. Assign a Center Anchor.";
                return false;
            }

            axis.Normalize();
            Transform active = Selection.activeTransform;
            Transform referenceTarget = active != null && selection.Contains(active)
                ? active
                : selection.OrderBy(GetStableSortKey, StringComparer.Ordinal).First();
            Vector3 reference = referenceTarget.position - center;
            Vector3 basisA = Vector3.ProjectOnPlane(reference, axis);
            if (basisA.sqrMagnitude < DirectionEpsilon)
            {
                basisA = Vector3.ProjectOnPlane(Vector3.forward, axis);
            }
            if (basisA.sqrMagnitude < DirectionEpsilon)
            {
                basisA = Vector3.ProjectOnPlane(Vector3.right, axis);
            }

            basisA.Normalize();
            Vector3 basisB = Vector3.Cross(axis, basisA).normalized;
            float nominalRadius;
            if (TryGetSurfaceHit(axis, center, surfaceCollider, out RaycastHit centerHit))
            {
                nominalRadius = Vector3.Distance(center, centerHit.point);
            }
            else
            {
                nominalRadius = selection.Average(target => Vector3.Distance(center, target.position));
            }

            if (nominalRadius <= 0.01f)
            {
                frame = default;
                error = "the local planet radius could not be measured.";
                return false;
            }

            frame = new RingFrame(center, axis, basisA, basisB, nominalRadius);
            error = null;
            return true;
        }

        private static List<Transform> SortAroundRing(
            IEnumerable<Transform> selection,
            RingFrame frame)
        {
            List<Transform> ordered = selection
                .OrderBy(target =>
                {
                    Vector3 projected = Vector3.ProjectOnPlane(
                        target.position - frame.PlanetCenter,
                        frame.Axis);
                    float angle = Mathf.Atan2(
                        Vector3.Dot(projected, frame.BasisB),
                        Vector3.Dot(projected, frame.BasisA));
                    return angle < 0f ? angle + Mathf.PI * 2f : angle;
                })
                .ThenBy(GetStableSortKey, StringComparer.Ordinal)
                .ToList();

            Transform active = Selection.activeTransform;
            int activeIndex = active != null ? ordered.IndexOf(active) : -1;
            if (activeIndex > 0)
            {
                ordered = ordered
                    .Skip(activeIndex)
                    .Concat(ordered.Take(activeIndex))
                    .ToList();
            }

            return ordered;
        }

        private Material ResolveCurvedWallMaterial(IReadOnlyList<Transform> selection)
        {
            if (curvedWallMaterial != null)
            {
                return curvedWallMaterial;
            }

            Transform active = Selection.activeTransform;
            Transform source = active != null && selection.Contains(active)
                ? active
                : selection[0];
            Renderer renderer = source.GetComponentInChildren<Renderer>(true);
            Material material = renderer != null
                ? renderer.sharedMaterials.FirstOrDefault(candidate => candidate != null)
                : null;
            if (material != null)
            {
                curvedWallMaterial = material;
                Repaint();
            }

            return material;
        }

        private static string GetStableSortKey(Transform target)
        {
            string globalId = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            var hierarchy = new Stack<string>();
            for (Transform current = target; current != null; current = current.parent)
            {
                hierarchy.Push($"{current.GetSiblingIndex():D6}:{current.name}");
            }

            return $"{globalId}|{string.Join("/", hierarchy)}";
        }

        private static Vector3 GetRingDirection(RingFrame frame, float angularRadius, float angle)
        {
            Vector3 around = frame.BasisA * Mathf.Cos(angle) + frame.BasisB * Mathf.Sin(angle);
            return (frame.Axis * Mathf.Cos(angularRadius) + around * Mathf.Sin(angularRadius)).normalized;
        }

        private static float GetRingAngle(Vector3 direction, RingFrame frame)
        {
            Vector3 projected = Vector3.ProjectOnPlane(direction, frame.Axis);
            float angle = Mathf.Atan2(
                Vector3.Dot(projected, frame.BasisB),
                Vector3.Dot(projected, frame.BasisA));
            return angle < 0f ? angle + Mathf.PI * 2f : angle;
        }

        private static Vector3 GetRingTangent(RingFrame frame, float angle, Vector3 up)
        {
            Vector3 tangent = -frame.BasisA * Mathf.Sin(angle) + frame.BasisB * Mathf.Cos(angle);
            tangent = Vector3.ProjectOnPlane(tangent, up);
            return tangent.sqrMagnitude >= DirectionEpsilon ? tangent.normalized : frame.BasisB;
        }

        private static Vector3 ResolvePlacementUp(
            bool useSurfaceNormal,
            Vector3 surfaceNormal,
            Vector3 outwardDirection)
        {
            if (!useSurfaceNormal || surfaceNormal.sqrMagnitude < DirectionEpsilon)
            {
                return outwardDirection;
            }

            Vector3 normal = surfaceNormal.normalized;
            return Vector3.Dot(normal, outwardDirection) < 0f ? -normal : normal;
        }

        private static Quaternion RotationForAxis(
            HorizontalAxis axis,
            Vector3 tangent,
            Vector3 up)
        {
            if (axis == HorizontalAxis.LocalX)
            {
                Vector3 forward = Vector3.Cross(tangent, up).normalized;
                return Quaternion.LookRotation(forward, up);
            }

            return Quaternion.LookRotation(tangent, up);
        }

        private static HorizontalAxis ResolveCommonAxis(
            IReadOnlyList<Transform> targets,
            HorizontalAxis requested)
        {
            if (requested != HorizontalAxis.AutoLongest)
            {
                return requested;
            }

            float xMedian = Median(targets.Select(target => MeasureRenderedSpan(target, HorizontalAxis.LocalX)));
            float zMedian = Median(targets.Select(target => MeasureRenderedSpan(target, HorizontalAxis.LocalZ)));
            return xMedian >= zMedian ? HorizontalAxis.LocalX : HorizontalAxis.LocalZ;
        }

        private static HorizontalAxis ResolveAxis(Transform target, HorizontalAxis requested)
        {
            if (requested != HorizontalAxis.AutoLongest)
            {
                return requested;
            }

            return MeasureRenderedSpan(target, HorizontalAxis.LocalX) >=
                   MeasureRenderedSpan(target, HorizontalAxis.LocalZ)
                ? HorizontalAxis.LocalX
                : HorizontalAxis.LocalZ;
        }

        private static float MeasureRenderedSpan(Transform root, HorizontalAxis axis)
        {
            Vector3 worldAxis = axis == HorizontalAxis.LocalZ ? root.forward : root.right;
            if (worldAxis.sqrMagnitude < DirectionEpsilon)
            {
                return 0f;
            }

            worldAxis.Normalize();
            bool foundRenderer = false;
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Bounds bounds = renderer.localBounds;
                Vector3 center = bounds.center;
                Vector3 extents = bounds.extents;
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 localCorner = center + Vector3.Scale(
                                extents,
                                new Vector3(x, y, z));
                            Vector3 worldCorner = renderer.transform.TransformPoint(localCorner);
                            float projection = Vector3.Dot(worldCorner, worldAxis);
                            minimum = Mathf.Min(minimum, projection);
                            maximum = Mathf.Max(maximum, projection);
                            foundRenderer = true;
                        }
                    }
                }
            }

            return foundRenderer ? maximum - minimum : 0f;
        }

        private static float Median(IEnumerable<float> values)
        {
            float[] ordered = values.Where(value => value > 0.001f).OrderBy(value => value).ToArray();
            return ordered.Length == 0 ? 0f : ordered[ordered.Length / 2];
        }

        private void TryResolvePlanetDefaults(bool force = false)
        {
            if (!force && planetCenter != null && surfaceCollider != null)
            {
                return;
            }

            GameObject planet = GameObject.Find(PlanetRootName);
            if (planet == null)
            {
                if (force)
                {
                    Debug.LogWarning($"Wall Ring Builder: no active object named '{PlanetRootName}' was found.");
                }
                return;
            }

            planetCenter = planet.transform;
            surfaceCollider = planet
                .GetComponentsInChildren<Collider>(true)
                .FirstOrDefault(candidate => candidate.enabled && candidate.gameObject.activeInHierarchy &&
                                             candidate is MeshCollider);
            if (surfaceCollider == null)
            {
                surfaceCollider = planet
                    .GetComponentsInChildren<Collider>(true)
                    .FirstOrDefault(candidate => candidate.enabled && candidate.gameObject.activeInHierarchy);
            }

            Repaint();
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static bool TryGetSurfaceHit(
            Vector3 outwardDirection,
            Vector3 center,
            Collider collider,
            out RaycastHit hit)
        {
            if (outwardDirection.sqrMagnitude < DirectionEpsilon || collider == null)
            {
                hit = default;
                return false;
            }

            outwardDirection.Normalize();
            float outerRadius = GetOuterRadius(collider.bounds, center);
            Vector3 origin = center + outwardDirection * (outerRadius + DefaultCastPadding);
            var ray = new Ray(origin, -outwardDirection);
            return collider.Raycast(ray, out hit, (outerRadius + DefaultCastPadding) * 2f);
        }

        private static float GetOuterRadius(Bounds bounds, Vector3 center)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float radiusSquared = 0f;
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        var corner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        radiusSquared = Mathf.Max(radiusSquared, (corner - center).sqrMagnitude);
                    }
                }
            }

            return Mathf.Sqrt(radiusSquared);
        }

        private static List<Transform> GetTopLevelEditableSelection()
        {
            Transform[] editable = Selection.transforms
                .Where(transform => transform != null &&
                                    transform.gameObject.scene.IsValid() &&
                                    !EditorUtility.IsPersistent(transform) &&
                                    (transform.hideFlags & HideFlags.NotEditable) == 0)
                .Distinct()
                .ToArray();
            return editable
                .Where(candidate => !editable.Any(other => other != candidate && candidate.IsChildOf(other)))
                .ToList();
        }

        private readonly struct RingFrame
        {
            public RingFrame(
                Vector3 planetCenter,
                Vector3 axis,
                Vector3 basisA,
                Vector3 basisB,
                float nominalSurfaceRadius)
            {
                PlanetCenter = planetCenter;
                Axis = axis;
                BasisA = basisA;
                BasisB = basisB;
                NominalSurfaceRadius = nominalSurfaceRadius;
            }

            public Vector3 PlanetCenter { get; }
            public Vector3 Axis { get; }
            public Vector3 BasisA { get; }
            public Vector3 BasisB { get; }
            public float NominalSurfaceRadius { get; }
        }

        private readonly struct WallPlacement
        {
            public WallPlacement(Transform target, Vector3 position, Quaternion rotation)
            {
                Target = target;
                Position = position;
                Rotation = rotation;
            }

            public Transform Target { get; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
        }
    }
}

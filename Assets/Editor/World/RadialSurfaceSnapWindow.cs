using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WorldEditor
{
    /// <summary>
    /// Snaps scene objects to a spherical world's authored collider by casting
    /// from outside the world toward its center. This keeps placement accurate
    /// on craters and other terrain features that an approximate sphere misses.
    /// </summary>
    public sealed class RadialSurfaceSnapWindow : EditorWindow
    {
        private const string PlanetRootName = "Planet Ground";
        private const float DefaultCastPadding = 10f;

        [SerializeField] private Transform planetCenter;
        [SerializeField] private Collider surfaceCollider;
        [SerializeField] private bool alignToSurfaceNormal = true;
        [SerializeField] private bool preserveHeading = true;
        [SerializeField] private float surfaceOffset;
        [SerializeField] private float castPadding = DefaultCastPadding;
        [SerializeField] private bool drawPreview = true;

        [MenuItem("Tools/Planet Design/Radial Surface Snap")]
        public static void Open()
        {
            RadialSurfaceSnapWindow window = GetWindow<RadialSurfaceSnapWindow>();
            window.titleContent = new GUIContent("Radial Snap");
            window.minSize = new Vector2(360f, 310f);
            window.TryResolveDefaults();
            window.Show();
        }

        [MenuItem("Tools/Planet Design/Snap Selection To Planet")]
        public static void SnapSelectionToPlanet()
        {
            if (!TryFindPlanet(out Transform center, out Collider collider, out string error))
            {
                Debug.LogError($"Radial Surface Snap: {error}");
                return;
            }

            SnapSelectedTransforms(
                center,
                collider,
                alignToNormal: true,
                keepHeading: true,
                offset: 0f,
                castPadding: DefaultCastPadding);
        }

        [MenuItem("Tools/Planet Design/Snap Selection To Planet", true)]
        private static bool ValidateSnapSelectionToPlanet()
        {
            return Selection.transforms.Any(IsEditableSceneTransform);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DuringSceneGui;
            TryResolveDefaults();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGui;
        }

        private void OnSelectionChange()
        {
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Planet Surface", EditorStyles.boldLabel);

            planetCenter = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Planet Center", "The transform at the center of radial gravity."),
                planetCenter,
                typeof(Transform),
                true);
            surfaceCollider = (Collider)EditorGUILayout.ObjectField(
                new GUIContent("Surface Collider", "The authored planet collider to place objects on."),
                surfaceCollider,
                typeof(Collider),
                true);

            if (GUILayout.Button("Find Planet Ground And Collider"))
            {
                TryResolveDefaults(force: true);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
            alignToSurfaceNormal = EditorGUILayout.Toggle(
                new GUIContent(
                    "Use Surface Normal",
                    "Aligns props to crater slopes. Disable to use purely radial up."),
                alignToSurfaceNormal);
            preserveHeading = EditorGUILayout.Toggle(
                new GUIContent(
                    "Preserve Heading",
                    "Keeps each object's existing rotation around its up axis."),
                preserveHeading);
            surfaceOffset = EditorGUILayout.FloatField(
                new GUIContent(
                    "Surface Offset",
                    "Moves the object pivot away from the surface along its chosen up direction."),
                surfaceOffset);
            castPadding = Mathf.Max(0.01f, EditorGUILayout.FloatField(
                new GUIContent(
                    "Cast Padding",
                    "Extra distance beyond the collider bounds where the inward ray begins."),
                castPadding));
            drawPreview = EditorGUILayout.Toggle(
                new GUIContent("Draw Preview", "Draws the selected objects' radial cast paths."),
                drawPreview);

            EditorGUILayout.Space(10f);
            int editableSelectionCount = GetTopLevelEditableSelection().Count;
            EditorGUILayout.HelpBox(
                editableSelectionCount == 0
                    ? "Select one or more scene objects to snap. Selecting a parent and its child snaps only the parent."
                    : $"Ready to snap {editableSelectionCount} selected object(s).",
                editableSelectionCount == 0 ? MessageType.Info : MessageType.None);

            using (new EditorGUI.DisabledScope(
                       planetCenter == null || surfaceCollider == null || editableSelectionCount == 0))
            {
                if (GUILayout.Button("Snap Selection To Surface", GUILayout.Height(34f)))
                {
                    SnapSelectedTransforms(
                        planetCenter,
                        surfaceCollider,
                        alignToSurfaceNormal,
                        preserveHeading,
                        surfaceOffset,
                        castPadding);
                }
            }

            if (planetCenter != null && surfaceCollider != null &&
                !surfaceCollider.transform.IsChildOf(planetCenter) &&
                surfaceCollider.transform != planetCenter)
            {
                EditorGUILayout.HelpBox(
                    "The surface collider is not part of the selected planet hierarchy. This is allowed, but verify the references before snapping.",
                    MessageType.Warning);
            }
        }

        private void DuringSceneGui(SceneView sceneView)
        {
            if (!drawPreview || planetCenter == null || surfaceCollider == null)
            {
                return;
            }

            Color oldColor = Handles.color;
            Handles.color = new Color(0.15f, 0.85f, 1f, 0.7f);

            foreach (Transform target in GetTopLevelEditableSelection())
            {
                if (!TryGetSurfaceHit(
                        target.position,
                        planetCenter.position,
                        surfaceCollider,
                        castPadding,
                        out RaycastHit hit,
                        out Vector3 rayOrigin))
                {
                    continue;
                }

                Handles.DrawDottedLine(target.position, hit.point, 4f);
                Handles.DrawWireDisc(hit.point, hit.normal, HandleUtility.GetHandleSize(hit.point) * 0.08f);
                Handles.DrawLine(rayOrigin, hit.point);
            }

            Handles.color = oldColor;
        }

        private void TryResolveDefaults(bool force = false)
        {
            if (!force && planetCenter != null && surfaceCollider != null)
            {
                return;
            }

            if (TryFindPlanet(out Transform center, out Collider collider, out string error))
            {
                planetCenter = center;
                surfaceCollider = collider;
                Repaint();
                SceneView.RepaintAll();
            }
            else if (force)
            {
                Debug.LogWarning($"Radial Surface Snap: {error}");
            }
        }

        private static bool TryFindPlanet(
            out Transform center,
            out Collider collider,
            out string error)
        {
            GameObject planet = GameObject.Find(PlanetRootName);
            if (planet == null)
            {
                center = null;
                collider = null;
                error = $"No active scene object named '{PlanetRootName}' was found.";
                return false;
            }

            center = planet.transform;
            collider = planet
                .GetComponentsInChildren<Collider>(true)
                .FirstOrDefault(candidate => candidate.enabled && candidate.gameObject.activeInHierarchy &&
                                             candidate is MeshCollider);
            if (collider == null)
            {
                collider = planet
                    .GetComponentsInChildren<Collider>(true)
                    .FirstOrDefault(candidate => candidate.enabled && candidate.gameObject.activeInHierarchy);
            }

            if (collider == null)
            {
                error = $"'{PlanetRootName}' has no enabled collider in its active hierarchy.";
                return false;
            }

            error = null;
            return true;
        }

        private static void SnapSelectedTransforms(
            Transform center,
            Collider collider,
            bool alignToNormal,
            bool keepHeading,
            float offset,
            float castPadding)
        {
            if (center == null)
            {
                Debug.LogError("Radial Surface Snap: assign a planet center first.");
                return;
            }

            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                Debug.LogError("Radial Surface Snap: assign an active, enabled surface collider first.");
                return;
            }

            List<Transform> selection = GetTopLevelEditableSelection();
            if (selection.Count == 0)
            {
                Debug.LogWarning("Radial Surface Snap: select at least one editable scene object.");
                return;
            }

            int snapped = 0;
            var failed = new List<string>();
            Undo.SetCurrentGroupName("Snap Objects To Planet Surface");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (Transform target in selection)
            {
                if (target == center || collider.transform == target || collider.transform.IsChildOf(target))
                {
                    failed.Add($"{target.name} (belongs to the planet surface)");
                    continue;
                }

                if (!TryGetSurfaceHit(
                        target.position,
                        center.position,
                        collider,
                        castPadding,
                        out RaycastHit hit,
                        out _))
                {
                    failed.Add(target.name);
                    continue;
                }

                Vector3 targetUp = alignToNormal ? hit.normal.normalized :
                    (hit.point - center.position).normalized;
                if (targetUp.sqrMagnitude < 0.99f)
                {
                    failed.Add(target.name);
                    continue;
                }

                Undo.RecordObject(target, "Snap To Planet Surface");
                target.position = hit.point + targetUp * offset;
                target.rotation = keepHeading
                    ? Quaternion.FromToRotation(target.up, targetUp) * target.rotation
                    : Quaternion.FromToRotation(Vector3.up, targetUp);

                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                EditorUtility.SetDirty(target);
                if (target.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                }

                snapped++;
            }

            Undo.CollapseUndoOperations(undoGroup);
            SceneView.RepaintAll();

            if (failed.Count == 0)
            {
                Debug.Log($"Radial Surface Snap: snapped {snapped} object(s) to '{collider.name}'.");
            }
            else
            {
                Debug.LogWarning(
                    $"Radial Surface Snap: snapped {snapped} object(s); could not snap {failed.Count}: " +
                    string.Join(", ", failed));
            }
        }

        internal static bool TryGetSurfaceHit(
            Vector3 objectPosition,
            Vector3 center,
            Collider collider,
            float castPadding,
            out RaycastHit hit,
            out Vector3 rayOrigin)
        {
            Vector3 radialDirection = objectPosition - center;
            if (radialDirection.sqrMagnitude < 0.0001f)
            {
                hit = default;
                rayOrigin = default;
                return false;
            }

            radialDirection.Normalize();
            float outerRadius = GetOuterRadius(collider.bounds, center);
            float padding = Mathf.Max(0.01f, castPadding);
            rayOrigin = center + radialDirection * (outerRadius + padding);
            var ray = new Ray(rayOrigin, -radialDirection);
            return collider.Raycast(ray, out hit, (outerRadius + padding) * 2f);
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
                .Where(IsEditableSceneTransform)
                .Distinct()
                .ToArray();

            return editable
                .Where(candidate => !editable.Any(other => other != candidate && candidate.IsChildOf(other)))
                .ToList();
        }

        private static bool IsEditableSceneTransform(Transform transform)
        {
            return transform != null &&
                   transform.gameObject.scene.IsValid() &&
                   !EditorUtility.IsPersistent(transform) &&
                   (transform.hideFlags & HideFlags.NotEditable) == 0;
        }
    }
}

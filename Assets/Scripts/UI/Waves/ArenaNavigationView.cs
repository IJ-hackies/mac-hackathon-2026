using UnityEngine;
using UnityEngine.UI;

namespace Player.UI.Waves
{
    /// <summary>
    /// Presents an arena objective and a screen-edge arrow using the great-circle tangent from player to target.
    /// It does not decide target selection, entry range, or any gameplay state.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class ArenaNavigationView : MonoBehaviour
    {
        private const float AntipodeEnterDot = -0.98480775f; // 170 degrees.
        private const float AntipodeExitDot = -0.9659258f;   // 165 degrees.

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Camera navigationCamera;
        [SerializeField] private Transform player;
        [SerializeField] private Transform planetCenter;
        [SerializeField] private Transform target;
        [SerializeField] private RectTransform marker;
        [SerializeField] private RectTransform markerBounds;
        [SerializeField] private Text targetLabel;
        [SerializeField] private Text distanceLabel;
        [SerializeField, Min(0f)] private float edgePadding = 48f;

        private string _targetLabel = "ARENA";
        private bool _isActive;
        private bool _hasManualGeometry;
        private Vector3 _manualPlayerPosition;
        private Vector3 _manualTargetPosition;
        private Vector3 _manualPlanetCenter;
        private bool _usingAntipodeRoute;
        private bool _hasPreviousTangent;
        private Vector3 _previousTangent;
        private Vector3 _previousUp;

        public void Configure(
            CanvasGroup root,
            Camera camera,
            RectTransform edgeMarker,
            RectTransform bounds,
            Text arenaLabel,
            Text distance)
        {
            canvasGroup = root;
            navigationCamera = camera;
            marker = edgeMarker;
            markerBounds = bounds;
            targetLabel = arenaLabel;
            distanceLabel = distance;
        }

        public void SetTarget(Transform arenaTarget, string label, Transform trackedPlayer, Transform center)
        {
            ResetRouteState();
            target = arenaTarget;
            _targetLabel = string.IsNullOrWhiteSpace(label) ? "ARENA" : label;
            player = trackedPlayer;
            planetCenter = center;
            _hasManualGeometry = false;
            SetActive(arenaTarget != null && trackedPlayer != null && center != null);
        }

        /// <summary>Test-friendly geometry entry point; positions may be supplied without scene transforms.</summary>
        public void SetNavigationGeometry(Vector3 playerPosition, Vector3 targetPosition, Vector3 centerPosition, string label)
        {
            if (!_hasManualGeometry) ResetRouteState();
            _manualPlayerPosition = playerPosition;
            _manualTargetPosition = targetPosition;
            _manualPlanetCenter = centerPosition;
            _targetLabel = string.IsNullOrWhiteSpace(label) ? "ARENA" : label;
            _hasManualGeometry = true;
            SetActive(true);
            RefreshMarker();
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            if (!active) ResetRouteState();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = active ? 1f : 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            else if (marker != null)
            {
                marker.gameObject.SetActive(active);
            }
        }

        private void LateUpdate()
        {
            if (_isActive) RefreshMarker();
        }

        private void RefreshMarker()
        {
            if (!TryGetGeometry(out Vector3 playerPosition, out Vector3 targetPosition, out Vector3 centerPosition))
            {
                SetActive(false);
                return;
            }

            Vector3 radial = playerPosition - centerPosition;
            Vector3 targetRadial = targetPosition - centerPosition;
            float radius = radial.magnitude;
            if (radius < 0.001f || targetRadial.sqrMagnitude < 0.000001f) return;

            Vector3 up = radial / radius;
            Vector3 targetDirection = targetRadial.normalized;
            float dot = Mathf.Clamp(Vector3.Dot(up, targetDirection), -1f, 1f);
            Vector3 tangent = ResolveTravelTangent(up, targetDirection, dot);

            if (targetLabel != null) targetLabel.text = _targetLabel;
            if (distanceLabel != null) distanceLabel.text = $"{radius * Mathf.Acos(dot):0}m";
            PlaceMarker(tangent, up);
        }

        private Vector3 ResolveTravelTangent(Vector3 up, Vector3 targetDirection, float dot)
        {
            Vector3 transportedTangent = Vector3.zero;
            if (_hasPreviousTangent)
            {
                transportedTangent = Quaternion.FromToRotation(_previousUp, up) * _previousTangent;
                transportedTangent = Vector3.ProjectOnPlane(transportedTangent, up);
            }

            bool stabilizeAntipode = _usingAntipodeRoute
                ? dot <= AntipodeExitDot
                : dot <= AntipodeEnterDot;
            Vector3 tangent = stabilizeAntipode
                ? transportedTangent
                : targetDirection - up * dot;

            if (tangent.sqrMagnitude < 0.000001f)
            {
                tangent = CameraTangentForward(up);
            }

            if (tangent.sqrMagnitude < 0.000001f)
            {
                tangent = Vector3.Cross(
                    up,
                    Mathf.Abs(Vector3.Dot(up, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right);
            }

            tangent.Normalize();
            _usingAntipodeRoute = stabilizeAntipode;
            _hasPreviousTangent = true;
            _previousTangent = tangent;
            _previousUp = up;
            return tangent;
        }

        private void PlaceMarker(Vector3 tangent, Vector3 up)
        {
            if (marker == null || navigationCamera == null) return;

            Vector3 cameraForward = CameraTangentForward(up);
            Vector3 cameraRight = Vector3.Cross(up, cameraForward);
            Vector2 screenDirection = new Vector2(
                Vector3.Dot(tangent, cameraRight),
                Vector3.Dot(tangent, cameraForward));
            if (screenDirection.sqrMagnitude < 0.0001f) screenDirection = Vector2.up;
            screenDirection.Normalize();

            RectTransform bounds = markerBounds != null ? markerBounds : marker.parent as RectTransform;
            if (bounds == null) return;
            Rect rect = bounds.rect;
            Vector2 limits = new Vector2(
                Mathf.Max(0f, rect.width * 0.5f - edgePadding),
                Mathf.Max(0f, rect.height * 0.5f - edgePadding));
            float scale = Mathf.Min(
                limits.x / Mathf.Max(Mathf.Abs(screenDirection.x), 0.0001f),
                limits.y / Mathf.Max(Mathf.Abs(screenDirection.y), 0.0001f));
            marker.anchoredPosition = screenDirection * scale;
            marker.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg - 90f);
        }

        private Vector3 CameraTangentForward(Vector3 up)
        {
            Vector3 forward = Vector3.ProjectOnPlane(
                navigationCamera != null ? navigationCamera.transform.forward : Vector3.forward,
                up);
            if (forward.sqrMagnitude < 0.000001f && navigationCamera != null)
            {
                forward = Vector3.ProjectOnPlane(navigationCamera.transform.up, up);
            }
            return forward.sqrMagnitude >= 0.000001f ? forward.normalized : Vector3.zero;
        }

        private void ResetRouteState()
        {
            _usingAntipodeRoute = false;
            _hasPreviousTangent = false;
            _previousTangent = Vector3.zero;
            _previousUp = Vector3.zero;
        }

        private bool TryGetGeometry(out Vector3 playerPosition, out Vector3 targetPosition, out Vector3 centerPosition)
        {
            if (_hasManualGeometry)
            {
                playerPosition = _manualPlayerPosition;
                targetPosition = _manualTargetPosition;
                centerPosition = _manualPlanetCenter;
                return true;
            }

            if (player == null || target == null || planetCenter == null)
            {
                playerPosition = targetPosition = centerPosition = default;
                return false;
            }

            playerPosition = player.position;
            targetPosition = target.position;
            centerPosition = planetCenter.position;
            return true;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Areas
{
    [DisallowMultipleComponent]
    public sealed class GameplayArea : MonoBehaviour
    {
        private const string DefaultPerimeterPath = "Perimeter/Poles";
        private const string DefaultEntrancePath = "Perimeter/Entrance";
        private const string DefaultPlanetName = "Planet Ground";

        [Header("Identity")]
        [SerializeField] private GameplayAreaId areaId;
        [SerializeField] private int priority;

        [Header("Perimeter")]
        [SerializeField] private Transform planetCenter;
        [SerializeField] private Transform perimeterPoles;
        [SerializeField] private Transform entrance;
        [SerializeField, Min(0f)] private float exitPaddingWorldUnits = 1.5f;

        private SphericalPerimeterPolygon _perimeter;
        private string _validationError;

        public GameplayAreaId AreaId => areaId;
        public int Priority => priority;
        public Transform PlanetCenter => planetCenter;
        public Transform PerimeterPoles => perimeterPoles;
        public Transform Entrance => entrance;
        public float ExitPaddingWorldUnits => exitPaddingWorldUnits;
        public bool IsValid => _perimeter != null;
        public string ValidationError => _validationError;

        private void Reset()
        {
            ResolveDefaults();
            RebuildPerimeter();
        }

        private void Awake()
        {
            RebuildPerimeter();
        }

        private void OnEnable()
        {
            if (_perimeter == null)
            {
                RebuildPerimeter();
            }
        }

        private void OnValidate()
        {
            exitPaddingWorldUnits = Mathf.Max(0f, exitPaddingWorldUnits);
            if (planetCenter != null && perimeterPoles != null)
            {
                RebuildPerimeter();
            }
        }

        public void Configure(
            GameplayAreaId id,
            Transform planetCenterTransform,
            Transform perimeterPoleRoot,
            float exitPadding,
            int overlapPriority = 0,
            Transform entranceTransform = null)
        {
            areaId = id;
            planetCenter = planetCenterTransform;
            perimeterPoles = perimeterPoleRoot;
            entrance = entranceTransform;
            exitPaddingWorldUnits = Mathf.Max(0f, exitPadding);
            priority = overlapPriority;
            RebuildPerimeter();
        }

        public bool RebuildPerimeter(bool logError = false)
        {
            _perimeter = null;
            if (planetCenter == null)
            {
                _validationError = "Planet Center is not assigned.";
                return ReportInvalid(logError);
            }

            if (perimeterPoles == null)
            {
                _validationError = "Perimeter Poles is not assigned.";
                return ReportInvalid(logError);
            }

            if (perimeterPoles.childCount < 3)
            {
                _validationError =
                    $"'{perimeterPoles.name}' needs at least three direct pole children.";
                return ReportInvalid(logError);
            }

            var polePositions = new List<Vector3>(perimeterPoles.childCount);
            for (int childIndex = 0; childIndex < perimeterPoles.childCount; childIndex++)
            {
                polePositions.Add(perimeterPoles.GetChild(childIndex).position);
            }

            if (!SphericalPerimeterPolygon.TryCreate(
                    polePositions,
                    planetCenter.position,
                    out _perimeter,
                    out _validationError))
            {
                return ReportInvalid(logError);
            }

            _validationError = null;
            return true;
        }

        public bool Contains(Vector3 worldPosition)
        {
            return _perimeter != null && _perimeter.ContainsWorldPosition(worldPosition);
        }

        public bool ContainsWithExitPadding(Vector3 worldPosition)
        {
            return _perimeter != null &&
                   _perimeter.ContainsWorldPosition(worldPosition, exitPaddingWorldUnits);
        }

        private void ResolveDefaults()
        {
            perimeterPoles = transform.Find(DefaultPerimeterPath);
            entrance = transform.Find(DefaultEntrancePath);
            GameObject planet = GameObject.Find(DefaultPlanetName);
            planetCenter = planet != null ? planet.transform : null;

            if (name == nameof(GameplayAreaId.LandingBase))
            {
                areaId = GameplayAreaId.LandingBase;
            }
            else if (name == nameof(GameplayAreaId.Arena1))
            {
                areaId = GameplayAreaId.Arena1;
            }
            else if (name == nameof(GameplayAreaId.Arena2))
            {
                areaId = GameplayAreaId.Arena2;
            }
        }

        private bool ReportInvalid(bool logError)
        {
            if (logError)
            {
                Debug.LogError(
                    $"Gameplay area '{name}' is invalid: {_validationError}",
                    this);
            }

            return false;
        }

        private void OnDrawGizmos()
        {
            if (planetCenter == null || perimeterPoles == null || perimeterPoles.childCount < 3)
            {
                return;
            }

            RebuildPerimeter();
            if (_perimeter == null)
            {
                return;
            }

            Gizmos.color = GetGizmoColor(areaId);
            IReadOnlyList<Vector3> vertices = _perimeter.OrderedWorldVertices;
            for (int index = 0; index < vertices.Count; index++)
            {
                Vector3 current = vertices[index];
                Vector3 next = vertices[(index + 1) % vertices.Count];
                Gizmos.DrawLine(current, next);
                Gizmos.DrawSphere(current, 0.2f);
            }
        }

        private static Color GetGizmoColor(GameplayAreaId id)
        {
            switch (id)
            {
                case GameplayAreaId.LandingBase:
                    return new Color(0.15f, 0.8f, 1f, 0.9f);
                case GameplayAreaId.Arena1:
                    return new Color(1f, 0.55f, 0.1f, 0.9f);
                case GameplayAreaId.Arena2:
                    return new Color(0.9f, 0.1f, 0.2f, 0.9f);
                default:
                    return Color.white;
            }
        }
    }
}

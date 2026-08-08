using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Areas
{
    /// <summary>
    /// Projects a compact pole ring on a spherical world into a stable tangent plane.
    /// Membership depends only on radial direction, so jumping does not leave an area.
    /// </summary>
    public sealed class SphericalPerimeterPolygon
    {
        private const float DirectionEpsilon = 0.000001f;
        private const float ProjectionEpsilon = 0.0001f;
        private const float BoundaryEpsilon = 0.00001f;

        private readonly Vector3 _planetCenter;
        private readonly Vector3 _axis;
        private readonly Vector3 _tangentX;
        private readonly Vector3 _tangentY;
        private readonly Vector2[] _polygon;
        private readonly Vector3[] _orderedWorldVertices;
        private readonly float _averageSurfaceRadius;

        private SphericalPerimeterPolygon(
            Vector3 planetCenter,
            Vector3 axis,
            Vector3 tangentX,
            Vector3 tangentY,
            Vector2[] polygon,
            Vector3[] orderedWorldVertices,
            float averageSurfaceRadius)
        {
            _planetCenter = planetCenter;
            _axis = axis;
            _tangentX = tangentX;
            _tangentY = tangentY;
            _polygon = polygon;
            _orderedWorldVertices = orderedWorldVertices;
            _averageSurfaceRadius = averageSurfaceRadius;
        }

        public Vector3 PlanetCenter => _planetCenter;
        public Vector3 CenterDirection => _axis;
        public float AverageSurfaceRadius => _averageSurfaceRadius;
        public IReadOnlyList<Vector3> OrderedWorldVertices => _orderedWorldVertices;

        public static bool TryCreate(
            IReadOnlyList<Vector3> worldVertices,
            Vector3 planetCenter,
            out SphericalPerimeterPolygon perimeter,
            out string error)
        {
            perimeter = null;
            if (worldVertices == null || worldVertices.Count < 3)
            {
                error = "A gameplay-area perimeter requires at least three pole positions.";
                return false;
            }

            var directions = new Vector3[worldVertices.Count];
            Vector3 axisSum = Vector3.zero;
            float radiusSum = 0f;
            for (int index = 0; index < worldVertices.Count; index++)
            {
                Vector3 radial = worldVertices[index] - planetCenter;
                float radius = radial.magnitude;
                if (radius <= DirectionEpsilon)
                {
                    error = $"Perimeter pole {index} is too close to the planet center.";
                    return false;
                }

                directions[index] = radial / radius;
                axisSum += directions[index];
                radiusSum += radius;
            }

            if (axisSum.sqrMagnitude <= DirectionEpsilon)
            {
                error = "The perimeter does not resolve to one compact planetary region.";
                return false;
            }

            Vector3 axis = axisSum.normalized;
            Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f
                ? Vector3.right
                : Vector3.up;
            Vector3 tangentX = Vector3.Cross(reference, axis).normalized;
            Vector3 tangentY = Vector3.Cross(axis, tangentX).normalized;

            var vertices = new ProjectedVertex[worldVertices.Count];
            for (int index = 0; index < directions.Length; index++)
            {
                float denominator = Vector3.Dot(directions[index], axis);
                if (denominator <= ProjectionEpsilon)
                {
                    error = $"Perimeter pole {index} lies outside the area's projectable hemisphere.";
                    return false;
                }

                Vector2 projected = Project(
                    directions[index],
                    axis,
                    tangentX,
                    tangentY,
                    denominator);
                vertices[index] = new ProjectedVertex(
                    projected,
                    worldVertices[index],
                    Mathf.Atan2(projected.y, projected.x));
            }

            Array.Sort(vertices, (left, right) => left.Angle.CompareTo(right.Angle));
            var polygon = new Vector2[vertices.Length];
            var orderedWorldVertices = new Vector3[vertices.Length];
            for (int index = 0; index < vertices.Length; index++)
            {
                polygon[index] = vertices[index].Projected;
                orderedWorldVertices[index] = vertices[index].WorldPosition;
            }

            if (Mathf.Abs(SignedDoubleArea(polygon)) <= DirectionEpsilon)
            {
                error = "The perimeter poles collapse to a degenerate polygon.";
                return false;
            }

            perimeter = new SphericalPerimeterPolygon(
                planetCenter,
                axis,
                tangentX,
                tangentY,
                polygon,
                orderedWorldVertices,
                radiusSum / worldVertices.Count);
            error = null;
            return true;
        }

        public bool ContainsWorldPosition(Vector3 worldPosition, float outwardPaddingWorldUnits = 0f)
        {
            Vector3 radial = worldPosition - _planetCenter;
            if (radial.sqrMagnitude <= DirectionEpsilon)
            {
                return false;
            }

            return ContainsDirection(radial.normalized, outwardPaddingWorldUnits);
        }

        public bool ContainsDirection(Vector3 direction, float outwardPaddingWorldUnits = 0f)
        {
            if (direction.sqrMagnitude <= DirectionEpsilon)
            {
                return false;
            }

            direction.Normalize();
            float denominator = Vector3.Dot(direction, _axis);
            if (denominator <= ProjectionEpsilon)
            {
                return false;
            }

            Vector2 point = Project(
                direction,
                _axis,
                _tangentX,
                _tangentY,
                denominator);
            if (IsPointInsidePolygon(point))
            {
                return true;
            }

            float padding = Mathf.Max(0f, outwardPaddingWorldUnits);
            float angularPadding = Mathf.Clamp(
                padding / _averageSurfaceRadius,
                0f,
                Mathf.PI * 0.49f);
            float projectedPadding = Mathf.Tan(angularPadding) + BoundaryEpsilon;
            for (int index = 0; index < _polygon.Length; index++)
            {
                Vector2 start = _polygon[index];
                Vector2 end = _polygon[(index + 1) % _polygon.Length];
                if (DistanceToSegment(point, start, end) <= projectedPadding)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPointInsidePolygon(Vector2 point)
        {
            bool inside = false;
            for (int index = 0, previous = _polygon.Length - 1;
                 index < _polygon.Length;
                 previous = index++)
            {
                Vector2 currentPoint = _polygon[index];
                Vector2 previousPoint = _polygon[previous];
                bool crosses = (currentPoint.y > point.y) != (previousPoint.y > point.y);
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

        private static Vector2 Project(
            Vector3 direction,
            Vector3 axis,
            Vector3 tangentX,
            Vector3 tangentY,
            float denominator)
        {
            return new Vector2(
                Vector3.Dot(direction, tangentX) / denominator,
                Vector3.Dot(direction, tangentY) / denominator);
        }

        private static float SignedDoubleArea(IReadOnlyList<Vector2> polygon)
        {
            float area = 0f;
            for (int index = 0; index < polygon.Count; index++)
            {
                Vector2 current = polygon[index];
                Vector2 next = polygon[(index + 1) % polygon.Count];
                area += current.x * next.y - next.x * current.y;
            }

            return area;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= DirectionEpsilon)
            {
                return Vector2.Distance(point, start);
            }

            float interpolation = Mathf.Clamp01(
                Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * interpolation);
        }

        private readonly struct ProjectedVertex
        {
            public ProjectedVertex(Vector2 projected, Vector3 worldPosition, float angle)
            {
                Projected = projected;
                WorldPosition = worldPosition;
                Angle = angle;
            }

            public Vector2 Projected { get; }
            public Vector3 WorldPosition { get; }
            public float Angle { get; }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Areas.Tests
{
    internal static class GameplayAreaTestFactory
    {
        public static List<Vector3> CreateRing(
            Vector3 planetCenter,
            Vector3 centerDirection,
            float planetRadius,
            float angularRadiusDegrees,
            int vertexCount = 8)
        {
            centerDirection.Normalize();
            Vector3 reference = Mathf.Abs(Vector3.Dot(centerDirection, Vector3.up)) > 0.9f
                ? Vector3.right
                : Vector3.up;
            Vector3 tangentX = Vector3.Cross(reference, centerDirection).normalized;
            Vector3 tangentY = Vector3.Cross(centerDirection, tangentX).normalized;
            float angularRadius = angularRadiusDegrees * Mathf.Deg2Rad;
            var positions = new List<Vector3>(vertexCount);
            for (int index = 0; index < vertexCount; index++)
            {
                float angle = index * Mathf.PI * 2f / vertexCount;
                Vector3 tangent = tangentX * Mathf.Cos(angle) + tangentY * Mathf.Sin(angle);
                Vector3 direction =
                    centerDirection * Mathf.Cos(angularRadius) +
                    tangent * Mathf.Sin(angularRadius);
                positions.Add(planetCenter + direction.normalized * planetRadius);
            }

            return positions;
        }

        public static Vector3 DirectionOffset(
            Vector3 centerDirection,
            float angularOffsetDegrees)
        {
            centerDirection.Normalize();
            Vector3 reference = Mathf.Abs(Vector3.Dot(centerDirection, Vector3.up)) > 0.9f
                ? Vector3.right
                : Vector3.up;
            Vector3 tangent = Vector3.Cross(reference, centerDirection).normalized;
            float angle = angularOffsetDegrees * Mathf.Deg2Rad;
            return (centerDirection * Mathf.Cos(angle) + tangent * Mathf.Sin(angle)).normalized;
        }

        public static GameplayArea CreateArea(
            Transform planetCenter,
            GameplayAreaId id,
            float angularRadiusDegrees = 10f,
            float exitPadding = 1.5f,
            int priority = 0)
        {
            var root = new GameObject(id.ToString());
            var perimeter = new GameObject("Poles").transform;
            perimeter.SetParent(root.transform);
            List<Vector3> positions = CreateRing(
                planetCenter.position,
                Vector3.up,
                100f,
                angularRadiusDegrees);
            for (int index = 0; index < positions.Count; index++)
            {
                var pole = new GameObject($"Pole {index:00}").transform;
                pole.SetParent(perimeter);
                pole.position = positions[index];
            }

            GameplayArea area = root.AddComponent<GameplayArea>();
            area.Configure(id, planetCenter, perimeter, exitPadding, priority);
            return area;
        }
    }
}

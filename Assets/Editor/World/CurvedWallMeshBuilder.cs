using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldEditor
{
    /// <summary>
    /// Builds a closed solid wall mesh from one or more sampled surface paths.
    /// Input positions are local to the generated wall root.
    /// </summary>
    internal static class CurvedWallMeshBuilder
    {
        internal readonly struct Sample
        {
            public Sample(Vector3 basePosition, Vector3 up)
            {
                BasePosition = basePosition;
                Up = up.normalized;
            }

            public Vector3 BasePosition { get; }
            public Vector3 Up { get; }
        }

        public static Mesh Build(
            IReadOnlyList<IReadOnlyList<Sample>> panels,
            float height,
            float thickness,
            float uvTilesPerUnit)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            foreach (IReadOnlyList<Sample> panel in panels)
            {
                if (panel == null || panel.Count < 2)
                {
                    continue;
                }

                AddPanel(
                    panel,
                    height,
                    thickness,
                    uvTilesPerUnit,
                    vertices,
                    normals,
                    uvs,
                    triangles);
            }

            var mesh = new Mesh
            {
                name = "Curved Wall Ring",
                indexFormat = vertices.Count > ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddPanel(
            IReadOnlyList<Sample> samples,
            float height,
            float thickness,
            float uvTilesPerUnit,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles)
        {
            int count = samples.Count;
            float halfThickness = thickness * 0.5f;
            var tangents = new Vector3[count];
            var sides = new Vector3[count];
            var distances = new float[count];

            for (int index = 0; index < count; index++)
            {
                Vector3 before = samples[Mathf.Max(0, index - 1)].BasePosition;
                Vector3 after = samples[Mathf.Min(count - 1, index + 1)].BasePosition;
                Vector3 tangent = Vector3.ProjectOnPlane(after - before, samples[index].Up);
                if (tangent.sqrMagnitude < 0.000001f)
                {
                    tangent = index > 0 ? tangents[index - 1] : Vector3.forward;
                }

                tangent.Normalize();
                Vector3 side = Vector3.Cross(samples[index].Up, tangent).normalized;
                if (index > 0 && Vector3.Dot(side, sides[index - 1]) < 0f)
                {
                    side = -side;
                }

                tangents[index] = tangent;
                sides[index] = side;
                if (index > 0)
                {
                    distances[index] = distances[index - 1] +
                                       Vector3.Distance(
                                           samples[index - 1].BasePosition,
                                           samples[index].BasePosition);
                }
            }

            int stripStart = vertices.Count;
            for (int index = 0; index < count; index++)
            {
                Sample sample = samples[index];
                Vector3 sideOffset = sides[index] * halfThickness;
                Vector3 top = sample.BasePosition + sample.Up * height;
                Vector3 frontBottom = sample.BasePosition + sideOffset;
                Vector3 frontTop = top + sideOffset;
                Vector3 backBottom = sample.BasePosition - sideOffset;
                Vector3 backTop = top - sideOffset;
                float u = distances[index] * uvTilesPerUnit;
                float heightV = height * uvTilesPerUnit;
                float thicknessV = thickness * uvTilesPerUnit;

                AddVertex(vertices, normals, uvs, frontBottom, sides[index], new Vector2(u, 0f));
                AddVertex(vertices, normals, uvs, frontTop, sides[index], new Vector2(u, heightV));
                AddVertex(vertices, normals, uvs, backBottom, -sides[index], new Vector2(u, 0f));
                AddVertex(vertices, normals, uvs, backTop, -sides[index], new Vector2(u, heightV));
                AddVertex(vertices, normals, uvs, frontTop, sample.Up, new Vector2(u, 0f));
                AddVertex(vertices, normals, uvs, backTop, sample.Up, new Vector2(u, thicknessV));
                AddVertex(vertices, normals, uvs, frontBottom, -sample.Up, new Vector2(u, 0f));
                AddVertex(vertices, normals, uvs, backBottom, -sample.Up, new Vector2(u, thicknessV));
            }

            for (int index = 0; index < count - 1; index++)
            {
                int current = stripStart + index * 8;
                int next = current + 8;
                AddQuad(triangles, current, current + 1, next + 1, next);
                AddQuad(triangles, current + 2, next + 2, next + 3, current + 3);
                AddQuad(triangles, current + 4, current + 5, next + 5, next + 4);
                AddQuad(triangles, current + 6, next + 6, next + 7, current + 7);
            }

            AddCap(
                samples[0],
                sides[0],
                -tangents[0],
                height,
                halfThickness,
                uvTilesPerUnit,
                false,
                vertices,
                normals,
                uvs,
                triangles);
            AddCap(
                samples[count - 1],
                sides[count - 1],
                tangents[count - 1],
                height,
                halfThickness,
                uvTilesPerUnit,
                true,
                vertices,
                normals,
                uvs,
                triangles);
        }

        private static void AddCap(
            Sample sample,
            Vector3 side,
            Vector3 normal,
            float height,
            float halfThickness,
            float uvTilesPerUnit,
            bool reverseWinding,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles)
        {
            int start = vertices.Count;
            Vector3 sideOffset = side * halfThickness;
            Vector3 top = sample.BasePosition + sample.Up * height;
            float width = halfThickness * 2f * uvTilesPerUnit;
            float heightV = height * uvTilesPerUnit;

            AddVertex(vertices, normals, uvs, sample.BasePosition + sideOffset, normal, new Vector2(0f, 0f));
            AddVertex(vertices, normals, uvs, sample.BasePosition - sideOffset, normal, new Vector2(width, 0f));
            AddVertex(vertices, normals, uvs, top - sideOffset, normal, new Vector2(width, heightV));
            AddVertex(vertices, normals, uvs, top + sideOffset, normal, new Vector2(0f, heightV));

            if (reverseWinding)
            {
                AddQuad(triangles, start, start + 3, start + 2, start + 1);
            }
            else
            {
                AddQuad(triangles, start, start + 1, start + 2, start + 3);
            }
        }

        private static void AddVertex(
            ICollection<Vector3> vertices,
            ICollection<Vector3> normals,
            ICollection<Vector2> uvs,
            Vector3 position,
            Vector3 normal,
            Vector2 uv)
        {
            vertices.Add(position);
            normals.Add(normal);
            uvs.Add(uv);
        }

        private static void AddQuad(ICollection<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);
        }
    }
}

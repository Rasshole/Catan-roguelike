using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Beach band hugging the outer edges of a hex tile cluster (top-down visible cap).</summary>
    internal static class ColonistCoastSandMesh
    {
        public const float BeachWidthFactor = 0.25f;

        public static Mesh Create(BoardState board, float hexScale, float beachWidth)
        {
            var tileSet = new HashSet<HexCoord>(board.Tiles.Keys);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float y = ThinDiskMesh.TopY;

            foreach (var coord in tileSet)
            {
                var (cx, cz) = HexMath.ToWorldPosition(coord, hexScale);
                var center = new Vector3(cx, y, cz);

                for (int direction = 0; direction < 6; direction++)
                {
                    if (tileSet.Contains(HexMath.GetNeighbor(coord, direction)))
                        continue;

                    int corner0 = direction;
                    int corner1 = (direction + 1) % 6;
                    var (x0, z0) = HexMath.VertexToWorldPosition(coord, corner0, hexScale);
                    var (x1, z1) = HexMath.VertexToWorldPosition(coord, corner1, hexScale);
                    var inner0 = new Vector3(x0, y, z0);
                    var inner1 = new Vector3(x1, y, z1);
                    var outer0 = OffsetOutward(inner0, center, beachWidth);
                    var outer1 = OffsetOutward(inner1, center, beachWidth);
                    AddQuadUp(vertices, triangles, inner0, inner1, outer1, outer0);
                }
            }

            var mesh = new Mesh { name = "ColonistCoastSand" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static float ComputeOuterExtentRadius(BoardState board, float hexScale, float beachWidth)
        {
            float maxR = 0f;
            var tileSet = new HashSet<HexCoord>(board.Tiles.Keys);
            foreach (var coord in tileSet)
            {
                var (cx, cz) = HexMath.ToWorldPosition(coord, hexScale);
                var center = new Vector3(cx, 0f, cz);

                for (int direction = 0; direction < 6; direction++)
                {
                    if (tileSet.Contains(HexMath.GetNeighbor(coord, direction)))
                        continue;

                    for (int corner = direction; corner <= direction + 1; corner++)
                    {
                        int cornerIndex = corner % 6;
                        var (vx, vz) = HexMath.VertexToWorldPosition(coord, cornerIndex, hexScale);
                        var inner = new Vector3(vx, 0f, vz);
                        var outer = OffsetOutward(new Vector3(vx, 0f, vz), new Vector3(cx, 0f, cz), beachWidth);
                        maxR = Mathf.Max(maxR, outer.magnitude);
                        maxR = Mathf.Max(maxR, inner.magnitude);
                    }
                }
            }

            return maxR > 0f ? maxR : TableCameraFraming.ComputeBoardBoundingRadius(board, hexScale);
        }

        private static Vector3 OffsetOutward(Vector3 pointOnCap, Vector3 hexCenter, float distance)
        {
            var flat = pointOnCap - hexCenter;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-8f)
                return pointOnCap;
            return pointOnCap + flat.normalized * distance;
        }

        private static void AddQuadUp(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d)
        {
            int i0 = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.Add(i0);
            triangles.Add(i0 + 2);
            triangles.Add(i0 + 1);
            triangles.Add(i0);
            triangles.Add(i0 + 3);
            triangles.Add(i0 + 2);
        }

    }
}

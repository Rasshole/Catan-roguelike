using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Beach band hugging the outer edges of a hex tile cluster (top-down visible cap).</summary>
    public static class ColonistCoastSandMesh
    {
        public const float BeachWidthFactor = 0.25f;

        public static int LastBuiltQuadCount { get; private set; }

        public static Mesh Create(BoardState board, float hexScale, float beachWidth)
        {
            var tileSet = new HashSet<HexCoord>(board.Tiles.Keys);
            var boardCenter = ComputeBoardCenterXZ(board, hexScale);
            float y = ThinDiskMesh.TopY;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var innerIndex = new Dictionary<long, int>();
            var outerIndex = new Dictionary<long, int>();
            int quadCount = 0;

            foreach (var coord in tileSet)
            {
                for (int direction = 0; direction < 6; direction++)
                {
                    if (tileSet.Contains(HexMath.GetNeighbor(coord, direction)))
                        continue;

                    int corner0 = direction;
                    int corner1 = (direction + 1) % 6;
                    var inner0 = CornerVertex(coord, corner0, hexScale, y);
                    var inner1 = CornerVertex(coord, corner1, hexScale, y);
                    int i0 = GetOrAddVertex(innerIndex, vertices, inner0);
                    int i1 = GetOrAddVertex(innerIndex, vertices, inner1);
                    var outer0 = OffsetAlongBoardNormal(inner0, boardCenter, beachWidth);
                    var outer1 = OffsetAlongBoardNormal(inner1, boardCenter, beachWidth);
                    int o0 = GetOrAddVertex(outerIndex, vertices, outer0);
                    int o1 = GetOrAddVertex(outerIndex, vertices, outer1);
                    AddQuadUp(triangles, i0, i1, o1, o0);
                    quadCount++;
                }
            }

            LastBuiltQuadCount = quadCount;
            var mesh = new Mesh { name = "ColonistCoastSand" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Vector2 ComputeBoardCenterXZ(BoardState board, float hexScale)
        {
            if (board.Tiles.Count == 0)
                return Vector2.zero;

            float sumX = 0f;
            float sumZ = 0f;
            foreach (var coord in board.Tiles.Keys)
            {
                var (x, z) = HexMath.ToWorldPosition(coord, hexScale);
                sumX += x;
                sumZ += z;
            }

            float inv = 1f / board.Tiles.Count;
            return new Vector2(sumX * inv, sumZ * inv);
        }

        public static Vector2 ComputeCentroidXZ(IReadOnlyList<Vector3> vertices)
        {
            if (vertices == null || vertices.Count == 0)
                return Vector2.zero;

            float sumX = 0f;
            float sumZ = 0f;
            for (int i = 0; i < vertices.Count; i++)
            {
                sumX += vertices[i].x;
                sumZ += vertices[i].z;
            }

            float inv = 1f / vertices.Count;
            return new Vector2(sumX * inv, sumZ * inv);
        }

        public static int CountCoastalEdges(BoardState board)
        {
            var tileSet = new HashSet<HexCoord>(board.Tiles.Keys);
            int count = 0;
            foreach (var coord in tileSet)
            {
                for (int direction = 0; direction < 6; direction++)
                {
                    if (!tileSet.Contains(HexMath.GetNeighbor(coord, direction)))
                        count++;
                }
            }

            return count;
        }

        public static float ComputeOuterExtentRadius(BoardState board, float hexScale, float beachWidth)
        {
            float maxR = 0f;
            var boardCenter = ComputeBoardCenterXZ(board, hexScale);
            var tileSet = new HashSet<HexCoord>(board.Tiles.Keys);
            foreach (var coord in tileSet)
            {
                for (int direction = 0; direction < 6; direction++)
                {
                    if (tileSet.Contains(HexMath.GetNeighbor(coord, direction)))
                        continue;

                    for (int corner = direction; corner <= direction + 1; corner++)
                    {
                        int cornerIndex = corner % 6;
                        var inner = CornerVertex(coord, cornerIndex, hexScale, 0f);
                        var outer = OffsetAlongBoardNormal(
                            CornerVertex(coord, cornerIndex, hexScale, ThinDiskMesh.TopY),
                            boardCenter,
                            beachWidth);
                        maxR = Mathf.Max(maxR, new Vector2(outer.x, outer.z).magnitude);
                        maxR = Mathf.Max(maxR, new Vector2(inner.x, inner.z).magnitude);
                    }
                }
            }

            return maxR > 0f
                ? maxR
                : TableCameraFraming.ComputeBoardBoundingRadius(board, hexScale);
        }

        private static Vector3 CornerVertex(HexCoord coord, int cornerIndex, float hexScale, float y)
        {
            var (x, z) = HexMath.VertexToWorldPosition(coord, cornerIndex, hexScale);
            return new Vector3(x, y, z);
        }

        private static Vector3 OffsetAlongBoardNormal(Vector3 inner, Vector2 boardCenter, float distance)
        {
            var flat = new Vector2(inner.x - boardCenter.x, inner.z - boardCenter.y);
            if (flat.sqrMagnitude < 1e-8f)
                flat = Vector2.right;
            flat.Normalize();
            return new Vector3(inner.x + flat.x * distance, inner.y, inner.z + flat.y * distance);
        }

        private static int GetOrAddVertex(Dictionary<long, int> map, List<Vector3> vertices, Vector3 vertex)
        {
            long key = QuantizeKey(vertex);
            if (map.TryGetValue(key, out int index))
                return index;

            index = vertices.Count;
            vertices.Add(vertex);
            map[key] = index;
            return index;
        }

        private static long QuantizeKey(Vector3 vertex) =>
            ((long)Mathf.RoundToInt(vertex.x * 10000f) << 32)
            | (uint)Mathf.RoundToInt(vertex.z * 10000f);

        private static void AddQuadUp(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);
            triangles.Add(a);
            triangles.Add(d);
            triangles.Add(c);
        }
    }
}

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

        public static float LastTargetBeachWidth { get; private set; }

        public static Mesh Create(BoardState board, float hexScale, float beachWidth)
        {
            LastTargetBeachWidth = beachWidth;
            var tileSet = new HashSet<HexCoord>(board.Tiles.Keys);
            float y = ThinDiskMesh.TopY;

            var coastalEdges = new List<CoastalEdge>();
            var cornerNormals = new Dictionary<long, List<Vector2>>();

            foreach (var coord in tileSet)
            {
                var (cx, cz) = HexMath.ToWorldPosition(coord, hexScale);
                var hexCenter = new Vector2(cx, cz);

                for (int direction = 0; direction < 6; direction++)
                {
                    if (tileSet.Contains(HexMath.GetNeighbor(coord, direction)))
                        continue;

                    int corner0 = direction;
                    int corner1 = (direction + 1) % 6;
                    var inner0 = CornerVertex(coord, corner0, hexScale, y);
                    var inner1 = CornerVertex(coord, corner1, hexScale, y);
                    var outward = ComputeEdgeOutwardNormal(inner0, inner1, hexCenter);
                    coastalEdges.Add(new CoastalEdge(inner0, inner1, outward));
                    AccumulateCornerNormal(cornerNormals, inner0, outward);
                    AccumulateCornerNormal(cornerNormals, inner1, outward);
                }
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var innerIndex = new Dictionary<long, int>();
            var outerIndex = new Dictionary<long, int>();

            foreach (var edge in coastalEdges)
            {
                int i0 = GetOrAddVertex(innerIndex, vertices, edge.Inner0);
                int i1 = GetOrAddVertex(innerIndex, vertices, edge.Inner1);
                var outer0 = MiterOffset(edge.Inner0, cornerNormals[QuantizeKey(edge.Inner0)], beachWidth);
                var outer1 = MiterOffset(edge.Inner1, cornerNormals[QuantizeKey(edge.Inner1)], beachWidth);
                int o0 = GetOrAddVertex(outerIndex, vertices, outer0);
                int o1 = GetOrAddVertex(outerIndex, vertices, outer1);
                AddQuadUp(triangles, i0, i1, o1, o0);
            }

            LastBuiltQuadCount = coastalEdges.Count;
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

        public static bool TryMeasureEdgeQuadWidth(
            Vector3 inner0,
            Vector3 inner1,
            Vector3 outer0,
            Vector3 outer1,
            out float width)
        {
            var innerMid = new Vector2((inner0.x + inner1.x) * 0.5f, (inner0.z + inner1.z) * 0.5f);
            var outerMid = new Vector2((outer0.x + outer1.x) * 0.5f, (outer0.z + outer1.z) * 0.5f);
            width = Vector2.Distance(innerMid, outerMid);
            return width > 0.001f;
        }

        public static float ComputeOuterExtentRadius(BoardState board, float hexScale, float beachWidth)
        {
            float maxR = 0f;
            var tileSet = new HashSet<HexCoord>(board.Tiles.Keys);
            var cornerNormals = new Dictionary<long, List<Vector2>>();

            foreach (var coord in tileSet)
            {
                var (cx, cz) = HexMath.ToWorldPosition(coord, hexScale);
                var hexCenter = new Vector2(cx, cz);
                for (int direction = 0; direction < 6; direction++)
                {
                    if (tileSet.Contains(HexMath.GetNeighbor(coord, direction)))
                        continue;

                    int corner0 = direction;
                    int corner1 = (direction + 1) % 6;
                    var inner0 = CornerVertex(coord, corner0, hexScale, 0f);
                    var inner1 = CornerVertex(coord, corner1, hexScale, 0f);
                    var outward = ComputeEdgeOutwardNormal(
                        CornerVertex(coord, corner0, hexScale, ThinDiskMesh.TopY),
                        CornerVertex(coord, corner1, hexScale, ThinDiskMesh.TopY),
                        hexCenter);
                    AccumulateCornerNormal(cornerNormals, inner0, outward);
                    AccumulateCornerNormal(cornerNormals, inner1, outward);
                    maxR = Mathf.Max(maxR, new Vector2(inner0.x, inner0.z).magnitude);
                    maxR = Mathf.Max(maxR, new Vector2(inner1.x, inner1.z).magnitude);
                }
            }

            foreach (var coord in tileSet)
            {
                for (int corner = 0; corner < 6; corner++)
                {
                    var inner = CornerVertex(coord, corner, hexScale, ThinDiskMesh.TopY);
                    long key = QuantizeKey(inner);
                    if (!cornerNormals.TryGetValue(key, out var normals))
                        continue;
                    var outer = MiterOffset(inner, normals, beachWidth);
                    maxR = Mathf.Max(maxR, new Vector2(outer.x, outer.z).magnitude);
                }
            }

            return maxR > 0f
                ? maxR
                : TableCameraFraming.ComputeBoardBoundingRadius(board, hexScale);
        }

        private readonly struct CoastalEdge
        {
            public readonly Vector3 Inner0;
            public readonly Vector3 Inner1;
            public readonly Vector2 Outward;

            public CoastalEdge(Vector3 inner0, Vector3 inner1, Vector2 outward)
            {
                Inner0 = inner0;
                Inner1 = inner1;
                Outward = outward;
            }
        }

        private static Vector3 CornerVertex(HexCoord coord, int cornerIndex, float hexScale, float y)
        {
            var (x, z) = HexMath.VertexToWorldPosition(coord, cornerIndex, hexScale);
            return new Vector3(x, y, z);
        }

        private static Vector2 ComputeEdgeOutwardNormal(Vector3 inner0, Vector3 inner1, Vector2 hexCenter)
        {
            var edge = new Vector2(inner1.x - inner0.x, inner1.z - inner0.z);
            if (edge.sqrMagnitude < 1e-8f)
                return Vector2.right;
            edge.Normalize();
            var n1 = new Vector2(-edge.y, edge.x);
            var n2 = new Vector2(edge.y, -edge.x);
            var mid = new Vector2((inner0.x + inner1.x) * 0.5f, (inner0.z + inner1.z) * 0.5f);
            var toMid = mid - hexCenter;
            return Vector2.Dot(n1, toMid) >= Vector2.Dot(n2, toMid) ? n1 : n2;
        }

        private static void AccumulateCornerNormal(Dictionary<long, List<Vector2>> map, Vector3 inner, Vector2 outward)
        {
            long key = QuantizeKey(inner);
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<Vector2>(2);
                map[key] = list;
            }

            list.Add(outward);
        }

        private static Vector3 MiterOffset(Vector3 inner, List<Vector2> normals, float beachWidth)
        {
            if (normals == null || normals.Count == 0)
                return inner;

            Vector2 sum = Vector2.zero;
            for (int i = 0; i < normals.Count; i++)
                sum += normals[i];
            if (sum.sqrMagnitude < 1e-8f)
                sum = normals[0];
            sum.Normalize();

            Vector2 reference = normals[0];
            float denom = Vector2.Dot(sum, reference);
            if (Mathf.Abs(denom) < 1e-4f)
                denom = 1f;
            float miterLength = beachWidth / denom;
            return new Vector3(inner.x + sum.x * miterLength, inner.y, inner.z + sum.y * miterLength);
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

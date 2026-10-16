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

        public static Mesh Create(BoardState board, float hexScale, float beachWidth, float meshLocalY = 0f)
        {
            LastTargetBeachWidth = beachWidth;
            var tileSet = CollectLandCoords(board);
            float y = meshLocalY;

            var coastalEdges = new List<CoastalEdge>();

            foreach (var coord in tileSet)
            {
                var (hexCx, hexCz) = HexMath.ToWorldPosition(coord, hexScale);
                var hexCenter = new Vector2(hexCx, hexCz);
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
                }
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var innerIndex = new Dictionary<long, int>();

            foreach (var edge in coastalEdges)
            {
                int i0 = GetOrAddVertex(innerIndex, vertices, edge.Inner0);
                int i1 = GetOrAddVertex(innerIndex, vertices, edge.Inner1);
                var outer0 = OffsetEdgePoint(edge.Inner0, edge.Outward, beachWidth);
                var outer1 = OffsetEdgePoint(edge.Inner1, edge.Outward, beachWidth);
                int o0 = vertices.Count;
                vertices.Add(outer0);
                int o1 = vertices.Count;
                vertices.Add(outer1);
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

        private static HashSet<HexCoord> CollectLandCoords(BoardState board)
        {
            var tileSet = new HashSet<HexCoord>();
            foreach (var kvp in board.Tiles)
            {
                if (kvp.Value.IsFishTile)
                    continue;
                tileSet.Add(kvp.Key);
            }

            return tileSet;
        }

        public static Vector2 ComputeBoardCenterXZ(BoardState board, float hexScale)
        {
            if (board.Tiles.Count == 0)
                return Vector2.zero;

            float sumX = 0f;
            float sumZ = 0f;
            int count = 0;
            foreach (var kvp in board.Tiles)
            {
                if (kvp.Value.IsFishTile)
                    continue;
                var (x, z) = HexMath.ToWorldPosition(kvp.Key, hexScale);
                sumX += x;
                sumZ += z;
                count++;
            }

            if (count == 0)
                return Vector2.zero;

            float inv = 1f / count;
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
            var tileSet = CollectLandCoords(board);
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
            var tileSet = CollectLandCoords(board);

            foreach (var coord in tileSet)
            {
                var (hexCx, hexCz) = HexMath.ToWorldPosition(coord, hexScale);
                var hexCenter = new Vector2(hexCx, hexCz);
                for (int direction = 0; direction < 6; direction++)
                {
                    if (tileSet.Contains(HexMath.GetNeighbor(coord, direction)))
                        continue;

                    int corner0 = direction;
                    int corner1 = (direction + 1) % 6;
                    var inner0 = CornerVertex(coord, corner0, hexScale, 0f);
                    var inner1 = CornerVertex(coord, corner1, hexScale, 0f);
                    maxR = Mathf.Max(maxR, new Vector2(inner0.x, inner0.z).magnitude);
                    maxR = Mathf.Max(maxR, new Vector2(inner1.x, inner1.z).magnitude);
                    var outward = ComputeEdgeOutwardNormal(inner0, inner1, hexCenter);
                    var outer0 = OffsetEdgePoint(inner0, outward, beachWidth);
                    var outer1 = OffsetEdgePoint(inner1, outward, beachWidth);
                    maxR = Mathf.Max(maxR, new Vector2(outer0.x, outer0.z).magnitude);
                    maxR = Mathf.Max(maxR, new Vector2(outer1.x, outer1.z).magnitude);
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

        private static Vector3 OffsetEdgePoint(Vector3 inner, Vector2 outward, float beachWidth) =>
            new Vector3(inner.x + outward.x * beachWidth, inner.y, inner.z + outward.y * beachWidth);

        public static bool TryMeasureMeshWidthAtCoastalEdgeMidpoint(
            Mesh mesh,
            BoardState board,
            HexCoord coord,
            int direction,
            float hexScale,
            float beachWidth,
            out float width)
        {
            width = 0f;
            if (mesh == null || board == null)
                return false;

            var tileSet = CollectLandCoords(board);
            if (!tileSet.Contains(coord) || tileSet.Contains(HexMath.GetNeighbor(coord, direction)))
                return false;

            int corner0 = direction;
            int corner1 = (direction + 1) % 6;
            var (hexCx, hexCz) = HexMath.ToWorldPosition(coord, hexScale);
            var hexCenter = new Vector2(hexCx, hexCz);
            var inner0 = CornerVertex(coord, corner0, hexScale, 0f);
            var inner1 = CornerVertex(coord, corner1, hexScale, 0f);
            var outward = ComputeEdgeOutwardNormal(inner0, inner1, hexCenter);
            var innerMid = new Vector3(
                (inner0.x + inner1.x) * 0.5f,
                0f,
                (inner0.z + inner1.z) * 0.5f);
            var outerMid = OffsetEdgePoint(innerMid, outward, beachWidth);
            width = Vector2.Distance(
                new Vector2(innerMid.x, innerMid.z),
                new Vector2(outerMid.x, outerMid.z));
            return width > 0.001f;
        }

        public static void AppendOuterFramingSamples(
            BoardState board,
            float hexScale,
            float beachWidth,
            float sampleY,
            List<Vector3> samples)
        {
            if (board == null || samples == null || beachWidth <= 0f)
                return;

            var tileSet = CollectLandCoords(board);

            foreach (var coord in tileSet)
            {
                var (hexCx, hexCz) = HexMath.ToWorldPosition(coord, hexScale);
                var hexCenter = new Vector2(hexCx, hexCz);
                for (int direction = 0; direction < 6; direction++)
                {
                    if (tileSet.Contains(HexMath.GetNeighbor(coord, direction)))
                        continue;

                    int corner0 = direction;
                    int corner1 = (direction + 1) % 6;
                    var inner0 = CornerVertex(coord, corner0, hexScale, sampleY);
                    var inner1 = CornerVertex(coord, corner1, hexScale, sampleY);
                    var outward = ComputeEdgeOutwardNormal(inner0, inner1, hexCenter);
                    samples.Add(inner0);
                    samples.Add(inner1);
                    samples.Add(OffsetEdgePoint(inner0, outward, beachWidth));
                    samples.Add(OffsetEdgePoint(inner1, outward, beachWidth));
                    var innerMid = new Vector3(
                        (inner0.x + inner1.x) * 0.5f,
                        sampleY,
                        (inner0.z + inner1.z) * 0.5f);
                    samples.Add(OffsetEdgePoint(innerMid, outward, beachWidth));
                }
            }
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

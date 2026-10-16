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
        private const float MiterLimitFactor = 2.5f;

        public static int LastBuiltQuadCount { get; private set; }

        public static int LastOutlineVertexCount { get; private set; }

        public static float LastTargetBeachWidth { get; private set; }

        private readonly struct QuantizedVertex : System.IEquatable<QuantizedVertex>
        {
            public readonly int X;
            public readonly int Z;

            public QuantizedVertex(int x, int z)
            {
                X = x;
                Z = z;
            }

            public bool Equals(QuantizedVertex other) => X == other.X && Z == other.Z;

            public override bool Equals(object obj) => obj is QuantizedVertex other && Equals(other);

            public override int GetHashCode() => (X * 397) ^ Z;
        }

        public static Mesh Create(BoardState board, float hexScale, float beachWidth, float meshLocalY = 0f)
        {
            LastTargetBeachWidth = beachWidth;
            if (!TryBuildLandOutlinePolygon(board, hexScale, meshLocalY, out var inner))
            {
                LastBuiltQuadCount = 0;
                LastOutlineVertexCount = 0;
                return new Mesh { name = "ColonistCoastSand_Empty" };
            }

            LastOutlineVertexCount = inner.Count;

            var outer = OffsetOutlinePolygonMiter(inner, beachWidth);
            var vertices = new List<Vector3>(inner.Count + outer.Count);
            var triangles = new List<int>(inner.Count * 6);
            vertices.AddRange(inner);
            int outerBase = vertices.Count;
            vertices.AddRange(outer);

            int n = inner.Count;
            for (int i = 0; i < n; i++)
            {
                int i0 = i;
                int i1 = (i + 1) % n;
                int o0 = outerBase + i0;
                int o1 = outerBase + i1;
                AddQuadUp(triangles, i0, i1, o1, o0);
            }

            LastBuiltQuadCount = n;
            var mesh = new Mesh { name = "ColonistCoastSand" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static bool TryBuildLandOutlinePolygon(
            BoardState board,
            float hexScale,
            float y,
            out List<Vector3> innerPolygon)
        {
            innerPolygon = null;
            var tileSet = CollectLandCoords(board);
            if (tileSet.Count == 0)
                return false;

            var adjacency = new Dictionary<QuantizedVertex, List<QuantizedVertex>>();
            var positions = new Dictionary<QuantizedVertex, Vector2>();

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
                    var k0 = QuantizeKey(inner0);
                    var k1 = QuantizeKey(inner1);
                    positions[k0] = new Vector2(inner0.x, inner0.z);
                    positions[k1] = new Vector2(inner1.x, inner1.z);
                    AddAdjacency(adjacency, k0, k1);
                    AddAdjacency(adjacency, k1, k0);
                }
            }

            if (adjacency.Count == 0)
                return false;

            var startKey = default(QuantizedVertex);
            float minX = float.PositiveInfinity;
            foreach (var kvp in positions)
            {
                if (kvp.Value.x < minX)
                {
                    minX = kvp.Value.x;
                    startKey = kvp.Key;
                }
            }

            var landCentroid = ComputeBoardCenterXZ(board, hexScale);
            List<QuantizedVertex> loopKeys = null;
            if (!TryWalkBoundaryLoop(adjacency, positions, startKey, landCentroid, out loopKeys) || loopKeys.Count < 3)
            {
                loopKeys = null;
                foreach (var candidate in positions.Keys)
                {
                    if (TryWalkBoundaryLoop(adjacency, positions, candidate, landCentroid, out loopKeys)
                        && loopKeys.Count >= 3)
                        break;
                }

                if (loopKeys == null || loopKeys.Count < 3)
                    return false;
            }

            innerPolygon = new List<Vector3>(loopKeys.Count);
            for (int i = 0; i < loopKeys.Count; i++)
            {
                var p = positions[loopKeys[i]];
                innerPolygon.Add(new Vector3(p.x, y, p.y));
            }

            return innerPolygon.Count >= 3;
        }

        public static List<Vector3> OffsetOutlinePolygonMiter(IReadOnlyList<Vector3> inner, float beachWidth)
        {
            int n = inner.Count;
            var outer = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = ToXZ(inner[(i - 1 + n) % n]);
                Vector2 curr = ToXZ(inner[i]);
                Vector2 next = ToXZ(inner[(i + 1) % n]);
                Vector2 edgeIn = (curr - prev).normalized;
                Vector2 edgeOut = (next - curr).normalized;
                Vector2 nIn = OutwardNormal(edgeIn);
                Vector2 nOut = OutwardNormal(edgeOut);
                Vector2 tangent = nIn + nOut;
                if (tangent.sqrMagnitude < 1e-8f)
                    tangent = nIn;
                tangent.Normalize();
                float denom = Vector2.Dot(tangent, nIn);
                if (Mathf.Abs(denom) < 1e-4f)
                    denom = 1f;
                float miter = beachWidth / Mathf.Max(0.25f, Mathf.Abs(denom));
                miter = Mathf.Min(miter, beachWidth * MiterLimitFactor);
                Vector2 o = curr + tangent * miter;
                outer.Add(new Vector3(o.x, inner[i].y, o.y));
            }

            return outer;
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
            if (!TryBuildLandOutlinePolygon(board, hexScale, 0f, out var inner))
                return TableCameraFraming.ComputeBoardBoundingRadius(board, hexScale);

            var outer = OffsetOutlinePolygonMiter(inner, beachWidth);
            float maxR = 0f;
            for (int i = 0; i < outer.Count; i++)
            {
                var p = outer[i];
                maxR = Mathf.Max(maxR, new Vector2(p.x, p.z).magnitude);
            }

            for (int i = 0; i < inner.Count; i++)
            {
                var p = inner[i];
                maxR = Mathf.Max(maxR, new Vector2(p.x, p.z).magnitude);
            }

            return maxR > 0f
                ? maxR
                : TableCameraFraming.ComputeBoardBoundingRadius(board, hexScale);
        }

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

            if (!TryBuildLandOutlinePolygon(board, hexScale, sampleY, out var inner))
                return;

            for (int i = 0; i < inner.Count; i++)
                samples.Add(inner[i]);

            var outer = OffsetOutlinePolygonMiter(inner, beachWidth);
            for (int i = 0; i < outer.Count; i++)
                samples.Add(outer[i]);
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

        private static void AddAdjacency(Dictionary<QuantizedVertex, List<QuantizedVertex>> map, QuantizedVertex a, QuantizedVertex b)
        {
            if (!map.TryGetValue(a, out var list))
            {
                list = new List<QuantizedVertex>(2);
                map[a] = list;
            }

            if (!list.Contains(b))
                list.Add(b);
        }

        private static bool TryWalkBoundaryLoop(
            Dictionary<QuantizedVertex, List<QuantizedVertex>> adjacency,
            Dictionary<QuantizedVertex, Vector2> positions,
            QuantizedVertex startKey,
            Vector2 landCentroid,
            out List<QuantizedVertex> loopKeys)
        {
            loopKeys = new List<QuantizedVertex>(adjacency.Count);
            QuantizedVertex? prev = null;
            var curr = startKey;
            int guard = 0;
            while (guard++ < adjacency.Count + 4)
            {
                loopKeys.Add(curr);
                if (!adjacency.TryGetValue(curr, out var neighbors) || neighbors.Count == 0)
                    return false;

                QuantizedVertex? next = null;
                var currPos = positions[curr];
                if (!prev.HasValue)
                {
                    float bestCross = float.NegativeInfinity;
                    for (int i = 0; i < neighbors.Count; i++)
                    {
                        var nPos = positions[neighbors[i]];
                        float cross = CrossZ(nPos - currPos, landCentroid - currPos);
                        if (cross > bestCross)
                        {
                            bestCross = cross;
                            next = neighbors[i];
                        }
                    }
                }
                else
                {
                    var prevPos = positions[prev.Value];
                    Vector2 inDir = (currPos - prevPos).normalized;
                    float bestTurn = float.PositiveInfinity;
                    for (int i = 0; i < neighbors.Count; i++)
                    {
                        if (neighbors[i].Equals(prev.Value))
                            continue;

                        var nPos = positions[neighbors[i]];
                        Vector2 outDir = (nPos - currPos).normalized;
                        float turn = SignedTurnRadians(inDir, outDir);
                        if (turn < bestTurn)
                        {
                            bestTurn = turn;
                            next = neighbors[i];
                        }
                    }
                }

                if (!next.HasValue)
                    return false;

                if (next.Value.Equals(startKey) && loopKeys.Count > 2)
                    return loopKeys.Count >= 3;

                prev = curr;
                curr = next.Value;
            }

            return false;
        }

        private static float SignedTurnRadians(Vector2 inDir, Vector2 outDir)
        {
            float cross = CrossZ(inDir, outDir);
            float dot = Vector2.Dot(inDir, outDir);
            float angle = Mathf.Atan2(cross, dot);
            if (angle < 0f)
                angle += Mathf.PI * 2f;
            return angle;
        }

        private static float CrossZ(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static Vector2 OutwardNormal(Vector2 edgeDir)
        {
            var n = new Vector2(edgeDir.y, -edgeDir.x);
            if (n.sqrMagnitude < 1e-8f)
                return Vector2.right;
            return n.normalized;
        }

        private static Vector2 ToXZ(Vector3 v) => new Vector2(v.x, v.z);

        private static QuantizedVertex QuantizeKey(Vector3 vertex) =>
            new QuantizedVertex(
                Mathf.RoundToInt(vertex.x * 10000f),
                Mathf.RoundToInt(vertex.z * 10000f));

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

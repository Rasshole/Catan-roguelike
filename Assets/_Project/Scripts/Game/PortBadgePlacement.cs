using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Game
{
    /// <summary>Places port rate badges in open water off the coast edge (XZ).</summary>
    public static class PortBadgePlacement
    {
        public const float SeaClearanceBeyondDisc = 0.05f;

        public static bool TryComputeBadgeCenter(
            BoardState board,
            Vertex portVertex,
            float hexScale,
            float discRadius,
            out Vector2 badgeCenter,
            out Vector2 seaOutward)
        {
            badgeCenter = default;
            seaOutward = default;
            if (board == null)
                return false;

            portVertex = VertexGraph.Canonicalize(portVertex);
            if (!TryGetLandCentroid(board, portVertex, hexScale, out Vector2 landCentroid))
                return false;

            if (!TryGetCoastEdgeFrame(board, portVertex, hexScale, landCentroid, out Vector2 edgeMid, out Vector2 seaDir))
                return false;

            float push = discRadius + SeaClearanceBeyondDisc;
            badgeCenter = edgeMid + seaDir * push;
            seaOutward = seaDir;
            return true;
        }

        public static bool DiscIntersectsAnyLandHex(
            Vector2 discCenter,
            float discRadius,
            BoardState board,
            float hexScale)
        {
            if (board == null)
                return false;

            foreach (var hex in board.Tiles.Keys)
            {
                if (DiscIntersectsHexPolygon(discCenter, discRadius, hex, hexScale))
                    return true;
            }

            return false;
        }

        public static bool DiscIntersectsHexPolygon(
            Vector2 discCenter,
            float discRadius,
            HexCoord hex,
            float hexScale)
        {
            Span<Vector2> corners = stackalloc Vector2[HexPrismMesh.SideCount];
            FillHexCorners(hex, hexScale, corners);
            if (PointInConvexPolygon(discCenter, corners))
                return true;

            for (int i = 0; i < corners.Length; i++)
            {
                int next = (i + 1) % corners.Length;
                if (DistancePointToSegment(discCenter, corners[i], corners[next]) < discRadius)
                    return true;
                if ((corners[i] - discCenter).sqrMagnitude < discRadius * discRadius)
                    return true;
            }

            return false;
        }

        public static void FillHexCorners(HexCoord hex, float hexScale, Span<Vector2> corners)
        {
            for (int i = 0; i < HexPrismMesh.SideCount; i++)
            {
                var (x, z) = HexMath.VertexToWorldPosition(hex, i, hexScale);
                corners[i] = new Vector2(x, z);
            }
        }

        private static bool TryGetLandCentroid(
            BoardState board,
            Vertex portVertex,
            float hexScale,
            out Vector2 centroid)
        {
            centroid = default;
            int count = 0;
            Vector2 sum = Vector2.zero;
            foreach (var hex in VertexGraph.GetHexesForVertex(portVertex))
            {
                if (!board.Tiles.ContainsKey(hex))
                    continue;
                var (x, z) = HexMath.ToWorldPosition(hex, hexScale);
                sum += new Vector2(x, z);
                count++;
            }

            if (count == 0)
                return false;

            centroid = sum / count;
            return true;
        }

        private static bool TryGetCoastEdgeFrame(
            BoardState board,
            Vertex portVertex,
            float hexScale,
            Vector2 landCentroid,
            out Vector2 edgeMid,
            out Vector2 seaDir)
        {
            edgeMid = default;
            seaDir = default;
            Vector2 midSum = Vector2.zero;
            Vector2 seaSum = Vector2.zero;
            int count = 0;

            foreach (var adjacent in VertexGraph.GetAdjacentVertices(portVertex))
            {
                if (!IsBoardEdgeVertex(board, adjacent))
                    continue;

                var edge = VertexGraph.GetEdgeBetween(portVertex, adjacent);
                if (GetHexesForEdge(board, edge).Count != 1)
                    continue;

                var (mx, mz) = HexMath.EdgeMidpoint(edge, hexScale);
                Vector2 mid = new Vector2(mx, mz);
                Vector2 toSea = mid - landCentroid;
                if (toSea.sqrMagnitude < 1e-8f)
                    continue;

                var (ax, az) = HexMath.VertexToWorldPosition(edge.A, hexScale);
                var (bx, bz) = HexMath.VertexToWorldPosition(edge.B, hexScale);
                Vector2 along = new Vector2(bx - ax, bz - az);
                if (along.sqrMagnitude < 1e-8f)
                    continue;
                along.Normalize();
                Vector2 perpA = new Vector2(-along.y, along.x);
                Vector2 perpB = -perpA;
                Vector2 sea = Vector2.Dot(perpA, toSea) >= Vector2.Dot(perpB, toSea) ? perpA : perpB;
                if (Vector2.Dot(sea, toSea) < 0f)
                    sea = -sea;

                midSum += mid;
                seaSum += sea;
                count++;
            }

            if (count == 0)
                return false;

            edgeMid = midSum / count;
            seaDir = seaSum.normalized;
            if (seaDir.sqrMagnitude < 1e-8f)
                return false;

            return true;
        }

        private static bool IsBoardEdgeVertex(BoardState board, Vertex vertex)
        {
            int onBoard = 0;
            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (board.Tiles.ContainsKey(hex))
                    onBoard++;
            }

            return onBoard > 0 && onBoard <= 2;
        }

        public static int GetHexesForEdgeCount(BoardState board, Edge edge)
        {
            var edgeCanon = new Edge(VertexGraph.Canonicalize(edge.A), VertexGraph.Canonicalize(edge.B));
            int count = 0;
            foreach (var kv in board.Tiles)
            {
                var hex = kv.Key;
                for (int i = 0; i < 6; i++)
                {
                    if (HexMath.GetEdge(hex, i).Equals(edgeCanon))
                        count++;
                }
            }

            return count;
        }

        private static List<HexCoord> GetHexesForEdge(BoardState board, Edge edge)
        {
            var edgeCanon = new Edge(VertexGraph.Canonicalize(edge.A), VertexGraph.Canonicalize(edge.B));
            var hexes = new List<HexCoord>(2);
            foreach (var kv in board.Tiles)
            {
                var hex = kv.Key;
                for (int i = 0; i < 6; i++)
                {
                    if (HexMath.GetEdge(hex, i).Equals(edgeCanon))
                        hexes.Add(hex);
                }
            }

            return hexes;
        }

        private static bool PointInConvexPolygon(Vector2 point, ReadOnlySpan<Vector2> polygon)
        {
            bool? sign = null;
            for (int i = 0; i < polygon.Length; i++)
            {
                int next = (i + 1) % polygon.Length;
                float cross = Cross(polygon[next] - polygon[i], point - polygon[i]);
                if (Mathf.Abs(cross) < 1e-6f)
                    continue;
                bool positive = cross > 0f;
                sign ??= positive;
                if (sign != positive)
                    return false;
            }

            return true;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-8f)
                return (point - a).magnitude;

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lenSq);
            Vector2 closest = a + ab * t;
            return (point - closest).magnitude;
        }
    }
}

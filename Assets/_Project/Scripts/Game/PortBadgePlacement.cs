using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using UnityEngine;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Game
{
    /// <summary>Places port rate badges in open water off the coast (XZ).</summary>
    public static class PortBadgePlacement
    {
        public const float SeaClearanceBeyondDisc = 0.05f;
        private const float SeaFishGroundClearance = 0.24f;
        private const float HexInradiusPushFactor = 0.12f;
        private const float OutwardStepInradiusFactor = 0.06f;
        private const int MaxOutwardIterations = 32;
        public const float BadgeStackFramingPadFactor = 1.1f;

        public static bool TryComputeBadgeCenter(
            BoardState board,
            Vertex portVertex,
            float hexScale,
            float discRadius,
            out Vector2 badgeCenter,
            out Vector2 seaOutward) =>
            TryComputeBadgeCenter(
                board,
                portVertex,
                hexScale,
                discRadius,
                useColonistShortPier: false,
                out badgeCenter,
                out seaOutward);

        public static bool TryComputeBadgeCenter(
            BoardState board,
            Vertex portVertex,
            float hexScale,
            float discRadius,
            bool useColonistShortPier,
            out Vector2 badgeCenter,
            out Vector2 seaOutward)
        {
            badgeCenter = default;
            seaOutward = default;
            if (board == null || board.Tiles.Count == 0)
                return false;

            portVertex = VertexGraph.Canonicalize(portVertex);
            var (vx, vz) = HexMath.VertexToWorldPosition(portVertex, hexScale);
            Vector2 vertexPos = new Vector2(vx, vz);

            if (useColonistShortPier
                && SeaFishHarborLayout.TryComputeShortPierBadgeCenter(
                    board, portVertex, hexScale, discRadius, out badgeCenter))
            {
                seaOutward = ComputeSeawardDirection(board, portVertex, hexScale, vertexPos);
                return true;
            }

            if (!TouchesLand(board, portVertex))
                return false;

            Vector2 coastAnchor = GetCoastAnchor(board, portVertex, hexScale, vertexPos);
            Vector2 seaDir = ComputeSeawardDirection(board, portVertex, hexScale, vertexPos);
            if (seaDir.sqrMagnitude < 1e-8f)
                return false;

            float hexInradius = hexScale * HexMath.InnerRadius;
            float initialPush = discRadius + SeaClearanceBeyondDisc + hexInradius * HexInradiusPushFactor;
            badgeCenter = coastAnchor + seaDir * initialPush;

            float step = hexInradius * OutwardStepInradiusFactor;
            for (int i = 0; i < MaxOutwardIterations; i++)
            {
                if (!DiscIntersectsBlockedHex(badgeCenter, discRadius, board, hexScale, blockSeaFishGrounds: true))
                {
                    seaOutward = seaDir;
                    return true;
                }

                badgeCenter += seaDir * step;
            }

            seaOutward = seaDir;
            return !DiscIntersectsBlockedHex(badgeCenter, discRadius, board, hexScale, blockSeaFishGrounds: true);
        }

        public static float ComputeMaxBadgeOuterDistanceFromCenter(
            BoardState board,
            float hexScale,
            float discRadius,
            Vector3 boardCenterWorld)
        {
            if (board == null)
                return 0f;

            var center = new Vector2(boardCenterWorld.x, boardCenterWorld.z);
            float stackPad = discRadius * BadgeStackFramingPadFactor;
            float max = 0f;
            foreach (var port in PortAccess.DiscoverPorts(board))
            {
                if (!TryComputeBadgeCenter(board, port.Vertex, hexScale, discRadius, out Vector2 badgeCenter, out _))
                    continue;

                float dist = Vector2.Distance(center, badgeCenter) + discRadius + stackPad;
                max = Mathf.Max(max, dist);
            }

            return max;
        }

        public static bool DiscIntersectsAnyLandHex(
            Vector2 discCenter,
            float discRadius,
            BoardState board,
            float hexScale) =>
            DiscIntersectsBlockedHex(discCenter, discRadius, board, hexScale, blockSeaFishGrounds: false);

        public static bool DiscIntersectsBlockedHex(
            Vector2 discCenter,
            float discRadius,
            BoardState board,
            float hexScale) =>
            DiscIntersectsBlockedHex(discCenter, discRadius, board, hexScale, blockSeaFishGrounds: true);

        public static bool DiscIntersectsBlockedHex(
            Vector2 discCenter,
            float discRadius,
            BoardState board,
            float hexScale,
            bool blockSeaFishGrounds)
        {
            if (board == null)
                return false;

            foreach (var kvp in board.Tiles)
            {
                if (DiscIntersectsHexPolygon(discCenter, discRadius, kvp.Key, hexScale))
                    return true;
            }

            if (!blockSeaFishGrounds)
                return false;

            foreach (var kvp in board.SeaFishGrounds)
            {
                float radius = discRadius + SeaFishGroundClearance;
                if (SeaFishHarborLayout.DiscIntersectsHexPolygon(discCenter, radius, kvp.Key, hexScale))
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
            var corners = new Vector2[HexPrismMesh.SideCount];
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

        public static void FillHexCorners(HexCoord hex, float hexScale, Vector2[] corners)
        {
            for (int i = 0; i < HexPrismMesh.SideCount; i++)
            {
                var (x, z) = HexMath.VertexToWorldPosition(hex, i, hexScale);
                corners[i] = new Vector2(x, z);
            }
        }

        private static bool TouchesLand(BoardState board, Vertex portVertex)
        {
            foreach (var hex in VertexGraph.GetHexesForVertex(portVertex))
            {
                if (board.Tiles.TryGetValue(hex, out var tile) && IsLandTile(tile))
                    return true;
            }

            return false;
        }

        private static bool IsLandTile(HexTileData tile) =>
            !tile.IsFishTile || tile.IsDesert;

        private static Vector2 GetCoastAnchor(
            BoardState board,
            Vertex portVertex,
            float hexScale,
            Vector2 vertexPos)
        {
            Vector2 midSum = vertexPos;
            int midCount = 1;
            foreach (var adjacent in VertexGraph.GetAdjacentVertices(portVertex))
            {
                var edge = VertexGraph.GetEdgeBetween(portVertex, adjacent);
                if (CountHexesForEdge(board, edge) != 1)
                    continue;

                var (mx, mz) = HexMath.EdgeMidpoint(edge, hexScale);
                midSum += new Vector2(mx, mz);
                midCount++;
            }

            return midSum / midCount;
        }

        private static Vector2 ComputeSeawardDirection(
            BoardState board,
            Vertex portVertex,
            float hexScale,
            Vector2 vertexPos)
        {
            Vector2 boardCenter = ComputeBoardCenter(board, hexScale);
            Vector2 fromCenter = vertexPos - boardCenter;
            if (fromCenter.sqrMagnitude > 1e-8f)
                return fromCenter.normalized;

            Vector2 awayFromLand = Vector2.zero;
            int count = 0;
            foreach (var hex in VertexGraph.GetHexesForVertex(portVertex))
            {
                if (!board.Tiles.ContainsKey(hex))
                    continue;
                var (hx, hz) = HexMath.ToWorldPosition(hex, hexScale);
                Vector2 toVertex = vertexPos - new Vector2(hx, hz);
                if (toVertex.sqrMagnitude < 1e-8f)
                    continue;
                awayFromLand += toVertex.normalized;
                count++;
            }

            if (count > 0 && awayFromLand.sqrMagnitude > 1e-8f)
                return awayFromLand.normalized;

            return Vector2.up;
        }

        private static Vector2 ComputeBoardCenter(BoardState board, float hexScale)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (var kvp in board.Tiles)
            {
                if (!IsLandTile(kvp.Value))
                    continue;

                var (x, z) = HexMath.ToWorldPosition(kvp.Key, hexScale);
                sum += new Vector2(x, z);
                count++;
            }

            return count > 0 ? sum / count : Vector2.zero;
        }

        private static int CountHexesForEdge(BoardState board, Edge edge)
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

        private static bool PointInConvexPolygon(Vector2 point, Vector2[] polygon)
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

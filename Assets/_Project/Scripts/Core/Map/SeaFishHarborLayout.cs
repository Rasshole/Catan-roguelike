using System;
using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Shop;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Core.Map
{
    /// <summary>Places sea fishing grounds clear of short-pier port badges (ports win conflicts).</summary>
    public static class SeaFishHarborLayout
    {
        public const float DefaultHexScale = 1.2f;
        public const float DefaultBadgeDiscRadius = 0.42f;
        public const float ShortPierLength = 0.36f;
        public const float SeaClearanceBeyondDisc = 0.05f;
        private const float HexInradiusPushFactor = 0.12f;
        private const float SeaFishGroundClearance = 0.24f;
        private const int HexCornerCount = 6;

        private static readonly int[] SeaFishDisplayTokens = { 2, 3, 3, 4, 4, 5 };

        public static void PlaceSeaFishGrounds(
            BoardState board,
            IReadOnlyList<PortDefinition> ports,
            int? seed,
            float hexScale = DefaultHexScale,
            float discRadius = DefaultBadgeDiscRadius)
        {
            if (board == null)
                return;

            board.SeaFishGrounds.Clear();

            var land = new HashSet<HexCoord>(board.Tiles.Keys);
            var candidates = CollectSeaCandidates(land);
            var rng = seed.HasValue ? new Random(seed.Value ^ 0xF15E) : new Random();
            int fishCount = Math.Min(6, candidates.Count);
            var fishValues = new[] { 1, 1, 2, 2, 3, 0 };

            for (int i = 0; i < fishCount; i++)
            {
                int value = fishValues[i % fishValues.Length];
                HexCoord? placed = null;
                int attempts = candidates.Count;
                while (attempts-- > 0 && candidates.Count > 0)
                {
                    int pick = rng.Next(candidates.Count);
                    var candidate = candidates[pick];
                    candidates.RemoveAt(pick);

                    if (!ConflictsWithPortBadges(board, ports, candidate, hexScale, discRadius))
                    {
                        placed = candidate;
                        break;
                    }
                }

                if (!placed.HasValue)
                {
                    foreach (var fallback in CollectSeaCandidates(land))
                    {
                        if (board.SeaFishGrounds.ContainsKey(fallback))
                            continue;
                        if (!ConflictsWithPortBadges(board, ports, fallback, hexScale, discRadius))
                        {
                            placed = fallback;
                            break;
                        }
                    }
                }

                if (!placed.HasValue)
                    continue;

                land.Add(placed.Value);
                board.SeaFishGrounds[placed.Value] = new HexTileData(placed.Value, ResourceType.Wheat, isCoastal: true)
                {
                    IsFishTile = true,
                    FishValue = value,
                    NumberToken = SeaFishDisplayTokens[i % SeaFishDisplayTokens.Length]
                };
            }
        }

        private static bool ConflictsWithPortBadges(
            BoardState board,
            IReadOnlyList<PortDefinition> ports,
            HexCoord fishCoord,
            float hexScale,
            float discRadius)
        {
            if (ports == null || ports.Count == 0)
                return false;

            foreach (var port in ports)
            {
                if (!TryComputeShortPierBadgeCenter(board, port.Vertex, hexScale, discRadius, out MapPlaneXz center))
                    continue;

                float checkRadius = discRadius + SeaFishGroundClearance;
                if (DiscIntersectsHexPolygon(center, checkRadius, fishCoord, hexScale))
                    return true;
            }

            return false;
        }

        public static bool TryComputeShortPierBadgeCenter(
            BoardState board,
            Vertex portVertex,
            float hexScale,
            float discRadius,
            out MapPlaneXz badgeCenter)
        {
            badgeCenter = MapPlaneXz.Zero;
            if (board == null || board.Tiles.Count == 0)
                return false;

            portVertex = VertexGraph.Canonicalize(portVertex);
            var (vx, vz) = HexMath.VertexToWorldPosition(portVertex, hexScale);
            var vertexPos = new MapPlaneXz(vx, vz);

            if (!TouchesLand(board, portVertex))
                return false;

            MapPlaneXz coastAnchor = GetCoastAnchor(board, portVertex, hexScale, vertexPos);
            MapPlaneXz seaDir = ComputeSeawardDirection(board, portVertex, hexScale, vertexPos);
            if (seaDir.SqrMagnitude < 1e-8f)
                return false;

            float hexInradius = hexScale * HexMath.InnerRadius;
            float pierEnd = ShortPierLength + discRadius + SeaClearanceBeyondDisc;
            float initialPush = discRadius + SeaClearanceBeyondDisc + hexInradius * HexInradiusPushFactor;
            float outward = Math.Max(initialPush, pierEnd);
            outward = Math.Min(outward, pierEnd + hexInradius * 0.08f);
            badgeCenter = coastAnchor + seaDir * outward;

            if (DiscIntersectsLandHex(badgeCenter, discRadius, board, hexScale))
            {
                outward = pierEnd;
                badgeCenter = coastAnchor + seaDir * outward;
            }

            return !DiscIntersectsLandHex(badgeCenter, discRadius, board, hexScale);
        }

        private static bool DiscIntersectsLandHex(
            MapPlaneXz discCenter,
            float discRadius,
            BoardState board,
            float hexScale)
        {
            foreach (var kvp in board.Tiles)
            {
                if (DiscIntersectsHexPolygon(discCenter, discRadius, kvp.Key, hexScale))
                    return true;
            }

            return false;
        }

        public static bool DiscIntersectsHexPolygon(
            MapPlaneXz discCenter,
            float discRadius,
            HexCoord hex,
            float hexScale)
        {
            var corners = new MapPlaneXz[HexCornerCount];
            for (int i = 0; i < HexCornerCount; i++)
            {
                var (x, z) = HexMath.VertexToWorldPosition(hex, i, hexScale);
                corners[i] = new MapPlaneXz(x, z);
            }

            if (PointInConvexPolygon(discCenter, corners))
                return true;

            for (int i = 0; i < corners.Length; i++)
            {
                int next = (i + 1) % corners.Length;
                if (DistancePointToSegment(discCenter, corners[i], corners[next]) < discRadius)
                    return true;
                if ((corners[i] - discCenter).SqrMagnitude < discRadius * discRadius)
                    return true;
            }

            return false;
        }

        private static List<HexCoord> CollectSeaCandidates(HashSet<HexCoord> land)
        {
            var candidates = new List<HexCoord>();
            foreach (var coord in land)
            {
                for (int d = 0; d < 6; d++)
                {
                    var sea = HexMath.GetNeighbor(coord, d);
                    if (!land.Contains(sea) && !candidates.Contains(sea))
                        candidates.Add(sea);
                }
            }

            candidates.Sort((a, b) =>
            {
                int cmp = a.Q.CompareTo(b.Q);
                return cmp != 0 ? cmp : a.R.CompareTo(b.R);
            });
            return candidates;
        }

        private static bool TouchesLand(BoardState board, Vertex portVertex)
        {
            foreach (var hex in VertexGraph.GetHexesForVertex(portVertex))
            {
                if (board.Tiles.ContainsKey(hex))
                    return true;
            }

            return false;
        }

        private static MapPlaneXz GetCoastAnchor(
            BoardState board,
            Vertex portVertex,
            float hexScale,
            MapPlaneXz vertexPos)
        {
            MapPlaneXz midSum = vertexPos;
            int midCount = 1;
            foreach (var adjacent in VertexGraph.GetAdjacentVertices(portVertex))
            {
                var edge = VertexGraph.GetEdgeBetween(portVertex, adjacent);
                if (CountHexesForEdge(board, edge) != 1)
                    continue;

                var (mx, mz) = HexMath.EdgeMidpoint(edge, hexScale);
                midSum += new MapPlaneXz(mx, mz);
                midCount++;
            }

            return midSum / midCount;
        }

        private static MapPlaneXz ComputeSeawardDirection(
            BoardState board,
            Vertex portVertex,
            float hexScale,
            MapPlaneXz vertexPos)
        {
            MapPlaneXz boardCenter = ComputeBoardCenter(board, hexScale);
            MapPlaneXz fromCenter = vertexPos - boardCenter;
            if (fromCenter.SqrMagnitude > 1e-8f)
                return fromCenter.Normalized;

            MapPlaneXz awayFromLand = MapPlaneXz.Zero;
            int count = 0;
            foreach (var hex in VertexGraph.GetHexesForVertex(portVertex))
            {
                if (!board.Tiles.ContainsKey(hex))
                    continue;
                var (hx, hz) = HexMath.ToWorldPosition(hex, hexScale);
                MapPlaneXz toVertex = vertexPos - new MapPlaneXz(hx, hz);
                if (toVertex.SqrMagnitude < 1e-8f)
                    continue;
                awayFromLand += toVertex.Normalized;
                count++;
            }

            if (count > 0 && awayFromLand.SqrMagnitude > 1e-8f)
                return awayFromLand.Normalized;

            return MapPlaneXz.UnitZ;
        }

        private static MapPlaneXz ComputeBoardCenter(BoardState board, float hexScale)
        {
            MapPlaneXz sum = MapPlaneXz.Zero;
            int count = 0;
            foreach (var kvp in board.Tiles)
            {
                var (x, z) = HexMath.ToWorldPosition(kvp.Key, hexScale);
                sum += new MapPlaneXz(x, z);
                count++;
            }

            return count > 0 ? sum / count : MapPlaneXz.Zero;
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

        private static bool PointInConvexPolygon(MapPlaneXz point, MapPlaneXz[] polygon)
        {
            bool? sign = null;
            for (int i = 0; i < polygon.Length; i++)
            {
                int next = (i + 1) % polygon.Length;
                float cross = Cross(polygon[next] - polygon[i], point - polygon[i]);
                if (Math.Abs(cross) < 1e-6f)
                    continue;
                bool positive = cross > 0f;
                sign ??= positive;
                if (sign != positive)
                    return false;
            }

            return true;
        }

        private static float Cross(MapPlaneXz a, MapPlaneXz b) => a.X * b.Z - a.Z * b.X;

        private static float DistancePointToSegment(MapPlaneXz point, MapPlaneXz a, MapPlaneXz b)
        {
            MapPlaneXz ab = b - a;
            float lenSq = ab.SqrMagnitude;
            if (lenSq < 1e-8f)
                return (point - a).Magnitude;

            float t = MapPlaneXz.Clamp01(MapPlaneXz.Dot(point - a, ab) / lenSq);
            MapPlaneXz closest = a + ab * t;
            return (point - closest).Magnitude;
        }
    }
}

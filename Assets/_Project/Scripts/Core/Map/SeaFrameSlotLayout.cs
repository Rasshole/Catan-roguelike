using System;
using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Shop;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Core.Map
{
    /// <summary>
    /// Coastal sea-ring slots: each port and each fishing ground owns one distinct sea hex.
    /// </summary>
    public static class SeaFrameSlotLayout
    {
        private const int HexCornerCount = 6;
        private static readonly int[] SeaFishDisplayTokens = { 2, 3, 3, 4, 4, 5 };

        public static void AssignPortSeaSlots(BoardState board, IReadOnlyList<PortDefinition> ports)
        {
            if (board == null)
                return;

            board.PortVertexToSeaSlot.Clear();
            var land = new HashSet<HexCoord>(board.Tiles.Keys);
            var usedSea = new HashSet<HexCoord>();

            foreach (var port in ports)
            {
                var vertex = VertexGraph.Canonicalize(port.Vertex);
                if (!TryPickSeaSlotForPort(board, vertex, land, usedSea, out HexCoord sea))
                    continue;

                board.PortVertexToSeaSlot[vertex] = sea;
                usedSea.Add(sea);
            }
        }

        public static void AssignFishSeaGrounds(BoardState board, int? seed)
        {
            if (board == null)
                return;

            board.SeaFishGrounds.Clear();
            var land = new HashSet<HexCoord>(board.Tiles.Keys);
            var ring = CollectCoastalSeaRing(land);
            var usedSea = new HashSet<HexCoord>(board.PortVertexToSeaSlot.Values);

            var fishCandidates = new List<HexCoord>();
            foreach (var coord in ring)
            {
                if (!usedSea.Contains(coord))
                    fishCandidates.Add(coord);
            }

            var rng = seed.HasValue ? new Random(seed.Value ^ 0xF15E) : new Random();
            int fishCount = Math.Min(6, fishCandidates.Count);
            var fishValues = new[] { 1, 1, 2, 2, 3, 0 };

            for (int i = 0; i < fishCount; i++)
            {
                int pick = rng.Next(fishCandidates.Count);
                var coord = fishCandidates[pick];
                fishCandidates.RemoveAt(pick);

                board.SeaFishGrounds[coord] = new HexTileData(coord, ResourceType.Wheat, isCoastal: true)
                {
                    IsFishTile = true,
                    FishValue = fishValues[i % fishValues.Length],
                    NumberToken = SeaFishDisplayTokens[i % SeaFishDisplayTokens.Length]
                };
            }
        }

        public static void AssignPortAndFishSlots(
            BoardState board,
            IReadOnlyList<PortDefinition> ports,
            int? seed)
        {
            AssignPortSeaSlots(board, ports);
            AssignFishSeaGrounds(board, seed);
        }

        public static bool TryGetBadgeCenter(
            BoardState board,
            Vertex portVertex,
            float hexScale,
            out MapPlaneXz center)
        {
            center = MapPlaneXz.Zero;
            portVertex = VertexGraph.Canonicalize(portVertex);
            if (!board.PortVertexToSeaSlot.TryGetValue(portVertex, out HexCoord sea))
                return false;

            var (x, z) = HexMath.ToWorldPosition(sea, hexScale);
            center = new MapPlaneXz(x, z);
            return true;
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

        public static IReadOnlyList<HexCoord> CollectCoastalSeaRing(HashSet<HexCoord> land)
        {
            var ring = new List<HexCoord>();
            foreach (var coord in land)
            {
                for (int d = 0; d < 6; d++)
                {
                    var sea = HexMath.GetNeighbor(coord, d);
                    if (!land.Contains(sea) && !ring.Contains(sea))
                        ring.Add(sea);
                }
            }

            ring.Sort((a, b) =>
            {
                int cmp = a.Q.CompareTo(b.Q);
                return cmp != 0 ? cmp : a.R.CompareTo(b.R);
            });
            return ring;
        }

        private static bool TryPickSeaSlotForPort(
            BoardState board,
            Vertex portVertex,
            HashSet<HexCoord> land,
            HashSet<HexCoord> usedSea,
            out HexCoord sea)
        {
            sea = default;
            var candidates = CollectSeaCandidatesForPort(board, portVertex, land);
            if (candidates.Count == 0)
                return false;

            MapPlaneXz boardCenter = ComputeBoardCenter(board);
            HexCoord? best = null;
            float bestDist = float.NegativeInfinity;

            foreach (var candidate in candidates)
            {
                if (usedSea.Contains(candidate))
                    continue;

                var (cx, cz) = HexMath.ToWorldPosition(candidate, 1f);
                float dist = (new MapPlaneXz(cx, cz) - boardCenter).SqrMagnitude;
                if (dist > bestDist)
                {
                    bestDist = dist;
                    best = candidate;
                }
            }

            if (!best.HasValue)
            {
                foreach (var candidate in candidates)
                {
                    if (!usedSea.Contains(candidate))
                    {
                        sea = candidate;
                        return true;
                    }
                }

                return false;
            }

            sea = best.Value;
            return true;
        }

        private static List<HexCoord> CollectSeaCandidatesForPort(
            BoardState board,
            Vertex portVertex,
            HashSet<HexCoord> land)
        {
            var set = new HashSet<HexCoord>();

            foreach (var hex in VertexGraph.GetHexesForVertex(portVertex))
            {
                if (!land.Contains(hex))
                    set.Add(hex);
            }

            foreach (var hex in VertexGraph.GetHexesForVertex(portVertex))
            {
                if (!land.Contains(hex))
                    continue;

                for (int d = 0; d < 6; d++)
                {
                    var neighbor = HexMath.GetNeighbor(hex, d);
                    if (!land.Contains(neighbor))
                        set.Add(neighbor);
                }
            }

            var list = new List<HexCoord>(set);
            list.Sort((a, b) =>
            {
                int cmp = a.Q.CompareTo(b.Q);
                return cmp != 0 ? cmp : a.R.CompareTo(b.R);
            });
            return list;
        }

        private static MapPlaneXz ComputeBoardCenter(BoardState board)
        {
            MapPlaneXz sum = MapPlaneXz.Zero;
            int count = 0;
            foreach (var coord in board.Tiles.Keys)
            {
                var (x, z) = HexMath.ToWorldPosition(coord, 1f);
                sum += new MapPlaneXz(x, z);
                count++;
            }

            return count > 0 ? sum / count : MapPlaneXz.Zero;
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

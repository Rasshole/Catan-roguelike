using System;
using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Shop;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core.Map
{
    /// <summary>
    /// Coastal sea-ring slots: each port and each fishing ground owns one distinct sea hex.
    /// </summary>
    public static class SeaFrameSlotLayout
    {
        private const int HexCornerCount = 6;
        public const float DefaultBadgeDiscRadius = 0.42f;
        public const float BadgeLandPolygonClearance = 0.03f;
        /// <summary>Extra radius so clearance matches drawn hex outline + badge rim (world units).</summary>
        public const float BadgeRenderedLandInset = 0.055f;
        public const float FishShoalLandClearance = 0.05f;
        public const float FishChevronBandThicknessFactor = 0.53f; // ~70% of v107 band; token + fish still nest in the coast corner
        public const float FishChevronBandCenterPushFactor = 0.90f;
        private const float FishChevronTokenBiasFactor = 0.11f;
        private const float BadgeClearanceEpsilon = 0.004f;
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

            for (int i = fishCandidates.Count - 1; i >= 0; i--)
            {
                if (!TryGetFishShoalPlacement(board, fishCandidates[i], 1f, out _))
                    fishCandidates.RemoveAt(i);
            }

            int fishCount = Math.Min(6, fishCandidates.Count);
            var fishValues = new[] { 1, 1, 2, 2, 3, 0 };
            var picked = PickFishCoordsMaxMinAngularGap(board, fishCandidates, fishCount);

            for (int i = 0; i < picked.Count; i++)
            {
                var coord = picked[i];
                board.SeaFishGrounds[coord] = new HexTileData(coord, ResourceType.Wheat, isCoastal: true)
                {
                    IsFishTile = true,
                    FishValue = fishValues[i % fishValues.Length],
                    NumberToken = SeaFishDisplayTokens[i % SeaFishDisplayTokens.Length]
                };
            }
        }

        private static List<HexCoord> PickFishCoordsMaxMinAngularGap(
            BoardState board,
            List<HexCoord> candidates,
            int fishCount)
        {
            var result = new List<HexCoord>(fishCount);
            if (fishCount <= 0 || candidates.Count == 0)
                return result;

            MapPlaneXz center = ComputeBoardCenter(board);
            var angles = new Dictionary<HexCoord, float>(candidates.Count);
            foreach (var coord in candidates)
            {
                var (x, z) = HexMath.ToWorldPosition(coord, 1f);
                angles[coord] = (float)Math.Atan2(z - center.Z, x - center.X);
            }

            var orderedCandidates = new List<HexCoord>(candidates);
            orderedCandidates.Sort(CompareHexCoord);

            HexCoord first = orderedCandidates[0];
            float firstAngle = angles[first];
            foreach (var coord in orderedCandidates)
            {
                float angle = angles[coord];
                if (angle < firstAngle - 1e-6f
                    || (Math.Abs(angle - firstAngle) < 1e-6f && CompareHexCoord(coord, first) < 0))
                {
                    first = coord;
                    firstAngle = angle;
                }
            }

            result.Add(first);
            var remaining = new HashSet<HexCoord>(candidates);
            remaining.Remove(first);

            while (result.Count < fishCount && remaining.Count > 0)
            {
                var pool = new List<HexCoord>(remaining);
                pool.Sort(CompareHexCoord);

                HexCoord? best = null;
                float bestMinGap = float.NegativeInfinity;

                foreach (var coord in pool)
                {
                    float minGap = float.MaxValue;
                    foreach (var picked in result)
                        minGap = Math.Min(minGap, AngularDistance(angles[coord], angles[picked]));

                    bool better = !best.HasValue
                        || minGap > bestMinGap + 1e-6f
                        || (Math.Abs(minGap - bestMinGap) < 1e-6f && CompareHexCoord(coord, best.Value) < 0);
                    if (!better)
                        continue;

                    bestMinGap = minGap;
                    best = coord;
                }

                if (!best.HasValue)
                    break;

                result.Add(best.Value);
                remaining.Remove(best.Value);
            }

            return result;
        }

        private static float AngularDistance(float a, float b)
        {
            float d = Math.Abs(a - b);
            if (d > Math.PI)
                d = (float)(2.0 * Math.PI) - d;
            return d;
        }

        private static int CompareHexCoord(HexCoord a, HexCoord b)
        {
            int cmp = a.Q.CompareTo(b.Q);
            return cmp != 0 ? cmp : a.R.CompareTo(b.R);
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
            float badgeRadius,
            out MapPlaneXz center)
        {
            center = MapPlaneXz.Zero;
            portVertex = VertexGraph.Canonicalize(portVertex);
            if (!board.PortVertexToSeaSlot.TryGetValue(portVertex, out HexCoord sea))
                return false;

            if (!TryGetHarborCoastEdge(board, portVertex, sea, hexScale, out MapPlaneXz coastMid, out _, out _))
            {
                var (x, z) = HexMath.ToWorldPosition(sea, hexScale);
                center = new MapPlaneXz(x, z);
                return true;
            }

            var (sx, sz) = HexMath.ToWorldPosition(sea, hexScale);
            var seaCenter = new MapPlaneXz(sx, sz);
            MapPlaneXz outward = (seaCenter - coastMid).Normalized;
            if (outward.SqrMagnitude < 1e-6f)
            {
                MapPlaneXz boardCenter = ComputeBoardCenter(board);
                outward = (seaCenter - boardCenter).Normalized;
            }

            const float pushStep = 0.006f;
            float minPush = BadgeLandPolygonClearance;
            float maxPush = badgeRadius + BadgeRenderedLandInset + 0.55f;
            for (float push = minPush; push <= maxPush; push += pushStep)
            {
                center = coastMid + outward * push;
                if (DiscClearOfAllLandHexes(board, center, badgeRadius, BadgeLandPolygonClearance, hexScale))
                    return true;
            }

            center = coastMid + outward * maxPush;
            return DiscClearOfAllLandHexes(board, center, badgeRadius, BadgeLandPolygonClearance, hexScale);
        }

        public static bool TryGetFishShoalPlacement(
            BoardState board,
            HexCoord seaCoord,
            float hexScale,
            out FishShoalPlacement placement)
        {
            placement = default;
            if (board == null || board.Tiles.ContainsKey(seaCoord))
                return false;

            var (seaX, seaZ) = HexMath.ToWorldPosition(seaCoord, hexScale);
            var seaCenter = new MapPlaneXz(seaX, seaZ);

            for (int corner = 0; corner < 6; corner++)
            {
                var landA = HexMath.GetNeighbor(seaCoord, (corner + 4) % 6);
                var landB = HexMath.GetNeighbor(seaCoord, (corner + 5) % 6);
                if (!board.Tiles.ContainsKey(landA) || !board.Tiles.ContainsKey(landB))
                    continue;

                var inlandVertex = VertexGraph.Canonicalize(new Vertex(seaCoord, corner));
                var (tipX, tipZ) = HexMath.VertexToWorldPosition(inlandVertex, hexScale);
                var inlandTip = new MapPlaneXz(tipX, tipZ);

                int dirA = DirectionToNeighbor(landA, seaCoord);
                int dirB = DirectionToNeighbor(landB, seaCoord);
                if (dirA < 0 || dirB < 0)
                    continue;

                var edgeA = HexMath.GetEdgeFacing(landA, dirA);
                var edgeB = HexMath.GetEdgeFacing(landB, dirB);
                var outerLeft = VertexGraph.Canonicalize(OtherVertexOnEdge(edgeA, inlandVertex));
                var outerRight = VertexGraph.Canonicalize(OtherVertexOnEdge(edgeB, inlandVertex));

                var (olx, olz) = HexMath.VertexToWorldPosition(outerLeft, hexScale);
                var (orx, orz) = HexMath.VertexToWorldPosition(outerRight, hexScale);
                var outerLeftXz = new MapPlaneXz(olx, olz);
                var outerRightXz = new MapPlaneXz(orx, orz);

                // Inner notch P1–C–P2 on land/sea coast edges; outer band offset seaward by t.
                var p1 = outerLeftXz;
                var p2 = outerRightXz;
                var c = inlandTip;
                MapPlaneXz outward = (seaCenter - c).Normalized;
                float t = FishChevronBandThicknessFactor * hexScale;
                var p1Prime = p1 + outward * t;
                var p2Prime = p2 + outward * t;
                var cPrime = c + outward * (t * FishChevronBandCenterPushFactor);
                var tokenCenter = (c + cPrime) / 2f + outward * (t * FishChevronTokenBiasFactor);

                placement = new FishShoalPlacement(
                    c,
                    p1,
                    p2,
                    p1Prime,
                    p2Prime,
                    cPrime,
                    tokenCenter,
                    outward,
                    inlandVertex,
                    outerLeft,
                    outerRight);
                return true;
            }

            return false;
        }

        private static MapPlaneXz LerpMapPlane(MapPlaneXz from, MapPlaneXz to, float t) =>
            from + (to - from) * t;

        private static int DirectionToNeighbor(HexCoord from, HexCoord to)
        {
            for (int d = 0; d < 6; d++)
            {
                if (HexMath.GetNeighbor(from, d).Equals(to))
                    return d;
            }

            return -1;
        }

        private static Vertex OtherVertexOnEdge(Edge edge, Vertex onEdge)
        {
            onEdge = VertexGraph.Canonicalize(onEdge);
            var a = VertexGraph.Canonicalize(edge.A);
            var b = VertexGraph.Canonicalize(edge.B);
            if (a.Equals(onEdge))
                return b;
            if (b.Equals(onEdge))
                return a;
            return onEdge;
        }

        public static void AppendFishShoalFramingSamples(
            BoardState board,
            float hexScale,
            IList<MapPlaneXz> xzSamples)
        {
            if (board == null || xzSamples == null)
                return;

            foreach (var sea in board.SeaFishGrounds.Keys)
            {
                if (!TryGetFishShoalPlacement(board, sea, hexScale, out FishShoalPlacement p))
                    continue;

                xzSamples.Add(p.InlandTip);
                xzSamples.Add(p.CoastArmLeft);
                xzSamples.Add(p.CoastArmRight);
                xzSamples.Add(p.SeaFlatLeft);
                xzSamples.Add(p.SeaFlatRight);
            }
        }

        /// <summary>Minimum XZ distance from <paramref name="point"/> to any land hex edge (0 if inside a land hex).</summary>
        public static float MinDistanceToLandHexPolygons(
            BoardState board,
            MapPlaneXz point,
            float hexScale)
        {
            float min = float.MaxValue;
            foreach (var coord in board.Tiles.Keys)
                min = Math.Min(min, DistancePointToLandHexPolygon(point, coord, hexScale));

            return min;
        }

        private static float DistancePointToLandHexPolygon(
            MapPlaneXz point,
            HexCoord hex,
            float hexScale)
        {
            var corners = new MapPlaneXz[HexCornerCount];
            for (int i = 0; i < HexCornerCount; i++)
            {
                var (x, z) = HexMath.VertexToWorldPosition(hex, i, hexScale);
                corners[i] = new MapPlaneXz(x, z);
            }

            if (PointInConvexPolygon(point, corners))
                return 0f;

            float min = float.MaxValue;
            for (int i = 0; i < corners.Length; i++)
            {
                int next = (i + 1) % corners.Length;
                min = Math.Min(min, DistancePointToSegment(point, corners[i], corners[next]));
            }

            return min;
        }

        public static bool DiscClearOfAllLandHexes(
            BoardState board,
            MapPlaneXz center,
            float badgeRadius,
            float polygonClearance,
            float hexScale)
        {
            float probeRadius = badgeRadius + BadgeRenderedLandInset + polygonClearance + BadgeClearanceEpsilon;
            foreach (var landCoord in board.Tiles.Keys)
            {
                if (DiscIntersectsHexPolygon(center, probeRadius, landCoord, hexScale))
                    return false;
            }

            return true;
        }

        public static bool TryGetHarborCoastEdge(
            BoardState board,
            Vertex portVertex,
            HexCoord seaCoord,
            float hexScale,
            out MapPlaneXz coastMid,
            out MapPlaneXz coastVertexA,
            out MapPlaneXz coastVertexB)
        {
            coastMid = MapPlaneXz.Zero;
            coastVertexA = MapPlaneXz.Zero;
            coastVertexB = MapPlaneXz.Zero;
            portVertex = VertexGraph.Canonicalize(portVertex);

            foreach (var hex in VertexGraph.GetHexesForVertex(portVertex))
            {
                if (!board.Tiles.ContainsKey(hex))
                    continue;

                for (int d = 0; d < 6; d++)
                {
                    if (HexMath.GetNeighbor(hex, d) != seaCoord)
                        continue;

                    var edge = HexMath.GetEdgeFacing(hex, d);
                    var (mx, mz) = HexMath.EdgeMidpoint(edge, hexScale);
                    var (ax, az) = HexMath.VertexToWorldPosition(edge.A, hexScale);
                    var (bx, bz) = HexMath.VertexToWorldPosition(edge.B, hexScale);
                    coastMid = new MapPlaneXz(mx, mz);
                    coastVertexA = new MapPlaneXz(ax, az);
                    coastVertexB = new MapPlaneXz(bx, bz);
                    return true;
                }
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
            portVertex = VertexGraph.Canonicalize(portVertex);
            var anchored = new List<HexCoord>();

            foreach (var hex in VertexGraph.GetHexesForVertex(portVertex))
            {
                if (!land.Contains(hex))
                    continue;

                for (int d = 0; d < 6; d++)
                {
                    var neighbor = HexMath.GetNeighbor(hex, d);
                    if (land.Contains(neighbor))
                        continue;

                    var edge = HexMath.GetEdgeFacing(hex, d);
                    if (!EdgeContainsVertex(edge, portVertex))
                        continue;

                    if (!usedSea.Contains(neighbor))
                        anchored.Add(neighbor);
                }
            }

            if (anchored.Count > 0)
            {
                sea = anchored[0];
                return true;
            }

            foreach (var candidate in CollectSeaCandidatesForPort(board, portVertex, land))
            {
                if (!usedSea.Contains(candidate))
                {
                    sea = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool EdgeContainsVertex(Edge edge, Vertex vertex)
        {
            vertex = VertexGraph.Canonicalize(vertex);
            return VertexGraph.Canonicalize(edge.A).Equals(vertex)
                || VertexGraph.Canonicalize(edge.B).Equals(vertex);
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

using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Moves sea fishing grounds so port badges can sit on short piers (ports win conflicts).</summary>
    public static class SeaFishPortRelocator
    {
        private const float BadgeDiscRadiusFallback = 0.42f;

        public static void Resolve(BoardState board, float hexScale)
        {
            if (board == null)
                return;

            var ports = PortAccess.DiscoverPorts(board);
            if (ports.Count == 0)
                return;

            float discRadius = PortMarkerVisuals.BadgeDiscWorldRadius > 0.01f
                ? PortMarkerVisuals.BadgeDiscWorldRadius
                : BadgeDiscRadiusFallback;

            var fishCoords = new List<HexCoord>();
            var fishValues = new List<int>();
            foreach (var kvp in board.Tiles)
            {
                if (kvp.Value.IsFishTile && !kvp.Value.IsDesert)
                {
                    fishCoords.Add(kvp.Key);
                    fishValues.Add(kvp.Value.FishValue);
                }
            }

            if (fishCoords.Count == 0)
                return;

            foreach (var coord in fishCoords)
                board.Tiles.Remove(coord);

            var candidates = CollectSeaCandidates(board);
            var rng = new System.Random(board.Tiles.Count * 17 + fishCoords.Count);

            for (int i = 0; i < fishCoords.Count; i++)
            {
                int value = fishValues[i];
                HexCoord? placed = null;
                int attempts = candidates.Count;
                while (attempts-- > 0 && candidates.Count > 0)
                {
                    int pick = rng.Next(candidates.Count);
                    var candidate = candidates[pick];
                    candidates.RemoveAt(pick);

                    board.Tiles[candidate] = CreateSeaFishTile(candidate, value);
                    if (!PortBadgeConflictsWithSeaFish(board, ports, hexScale, discRadius))
                    {
                        placed = candidate;
                        break;
                    }

                    board.Tiles.Remove(candidate);
                }

                if (!placed.HasValue)
                {
                    foreach (var fallback in CollectSeaCandidates(board))
                    {
                        if (board.Tiles.ContainsKey(fallback))
                            continue;
                        board.Tiles[fallback] = CreateSeaFishTile(fallback, value);
                        if (!PortBadgeConflictsWithSeaFish(board, ports, hexScale, discRadius))
                        {
                            placed = fallback;
                            break;
                        }

                        board.Tiles.Remove(fallback);
                    }
                }

                if (!placed.HasValue)
                    Debug.LogWarning("SeaFishPortRelocator: could not place a fishing ground away from port badges.");
            }
        }

        private static bool PortBadgeConflictsWithSeaFish(
            BoardState board,
            IReadOnlyList<PortDefinition> ports,
            float hexScale,
            float discRadius)
        {
            foreach (var port in ports)
            {
                if (!PortBadgePlacement.TryComputeBadgeCenter(
                        board, port.Vertex, hexScale, discRadius, out Vector2 center, out _))
                    continue;

                foreach (var kvp in board.Tiles)
                {
                    if (!kvp.Value.IsFishTile || kvp.Value.IsDesert)
                        continue;

                    if (PortBadgePlacement.DiscIntersectsHexPolygon(center, discRadius, kvp.Key, hexScale))
                        return true;
                }
            }

            return false;
        }

        private static List<HexCoord> CollectSeaCandidates(BoardState board)
        {
            var land = new HashSet<HexCoord>();
            foreach (var kvp in board.Tiles)
            {
                if (!kvp.Value.IsFishTile || kvp.Value.IsDesert)
                    land.Add(kvp.Key);
            }

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

        private static HexTileData CreateSeaFishTile(HexCoord coord, int fishValue) =>
            new(coord, ResourceType.Wheat, isCoastal: true)
            {
                IsFishTile = true,
                FishValue = fishValue
            };
    }
}

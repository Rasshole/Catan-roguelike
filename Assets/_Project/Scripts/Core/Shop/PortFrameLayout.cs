using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Shop
{
    /// <summary>Even harbor spacing around the island frame (classic 9 on 19-hex base).</summary>
    public static class PortFrameLayout
    {
        public static List<PortDefinition> DiscoverFramePorts(
            BoardState board,
            int specificTarget,
            int genericTarget)
        {
            var edgeVertices = CollectSortedCoastVertices(board);
            int total = specificTarget + genericTarget;
            var picked = PickEvenlySpacedVertices(edgeVertices, total);
            if (picked.Count < total)
                picked = FillRemainingSpaced(edgeVertices, picked, total);

            var ports = new List<PortDefinition>();
            var usedResources = new HashSet<ResourceType>();
            foreach (var vertex in picked)
            {
                ResourceType? specific = TryPickSpecificResource(board, vertex, usedResources);
                if (specific.HasValue)
                {
                    ports.Add(new PortDefinition(vertex, specific));
                    usedResources.Add(specific.Value);
                }
                else
                {
                    ports.Add(new PortDefinition(vertex, null));
                }
            }

            int landTiles = board.Tiles.Values.Count(IsLandTile);
            bool allowDuplicateResources = landTiles <= 13;
            EnsureSpecificTargets(board, edgeVertices, ports, specificTarget, usedResources, allowDuplicateResources);

            while (ports.Count(p => p.IsGeneric) < genericTarget)
            {
                foreach (var vertex in edgeVertices)
                {
                    if (ports.Any(p => p.Vertex.Equals(vertex)))
                        continue;
                    if (PortSpacingRules.ConflictsWithAny(vertex, ports.Select(p => p.Vertex).ToList()))
                        continue;
                    ports.Add(new PortDefinition(vertex, null));
                    if (ports.Count(p => p.IsGeneric) >= genericTarget)
                        break;
                }
            }

            while (ports.Count(p => !p.IsGeneric) > specificTarget)
            {
                int idx = ports.FindIndex(p => !p.IsGeneric);
                if (idx < 0)
                    break;
                ports[idx] = new PortDefinition(ports[idx].Vertex, null);
            }

            while (ports.Count > total)
                ports.RemoveAt(ports.Count - 1);

            while (ports.Count < total && edgeVertices.Count > ports.Count)
            {
                foreach (var vertex in edgeVertices)
                {
                    if (ports.Any(p => p.Vertex.Equals(vertex)))
                        continue;
                    if (PortSpacingRules.ConflictsWithAny(vertex, ports.Select(p => p.Vertex).ToList()))
                        continue;
                    ports.Add(new PortDefinition(vertex, null));
                    break;
                }
            }

            return ports;
        }

        private static List<HexMath.Vertex> CollectSortedCoastVertices(BoardState board)
        {
            var seen = new HashSet<HexMath.Vertex>();
            var list = new List<(HexMath.Vertex vertex, float angle)>();

            foreach (var hex in board.Tiles.Values)
            {
                if (!IsLandTile(hex) || !hex.IsCoastal)
                    continue;

                for (int c = 0; c < 6; c++)
                {
                    var vertex = VertexGraph.Canonicalize(new HexMath.Vertex(hex.Coord, c));
                    if (!seen.Add(vertex))
                        continue;
                    if (!IsBoardEdgeVertex(board, vertex))
                        continue;

                    var (x, z) = HexMath.VertexToWorldPosition(vertex, 1f);
                    float angle = (float)Math.Atan2(z, x);
                    list.Add((vertex, angle));
                }
            }

            return list.OrderBy(t => t.angle).Select(t => t.vertex).ToList();
        }

        private static List<HexMath.Vertex> PickEvenlySpacedVertices(
            List<HexMath.Vertex> sorted,
            int count)
        {
            var picked = new List<HexMath.Vertex>();
            if (sorted.Count == 0 || count <= 0)
                return picked;

            for (int i = 0; i < count; i++)
            {
                int start = (int)Math.Floor((i + 0.5) * sorted.Count / (double)count) % sorted.Count;
                for (int attempt = 0; attempt < sorted.Count; attempt++)
                {
                    int idx = (start + attempt) % sorted.Count;
                    var candidate = sorted[idx];
                    if (!PortSpacingRules.ConflictsWithAny(candidate, picked))
                    {
                        picked.Add(candidate);
                        break;
                    }
                }
            }

            return FillRemainingSpaced(sorted, picked, count);
        }

        private static List<HexMath.Vertex> FillRemainingSpaced(
            List<HexMath.Vertex> sorted,
            List<HexMath.Vertex> picked,
            int total)
        {
            foreach (var vertex in sorted)
            {
                if (picked.Count >= total)
                    break;
                if (picked.Contains(vertex))
                    continue;
                if (PortSpacingRules.ConflictsWithAny(vertex, picked))
                    continue;
                picked.Add(vertex);
            }

            return picked;
        }

        private static ResourceType? TryPickSpecificResource(
            BoardState board,
            HexMath.Vertex vertex,
            HashSet<ResourceType> used,
            bool allowDuplicates = false)
        {
            foreach (var resource in ResourceTypes.AllProductive)
            {
                if (!allowDuplicates && used.Contains(resource))
                    continue;
                if (VertexTouchesCoastalResource(board, vertex, resource))
                    return resource;
            }

            return null;
        }

        private static void EnsureSpecificTargets(
            BoardState board,
            List<HexMath.Vertex> edgeVertices,
            List<PortDefinition> ports,
            int specificTarget,
            HashSet<ResourceType> usedResources,
            bool allowDuplicateResources)
        {
            PromoteGenericsToSpecific(board, ports, specificTarget, usedResources, allowDuplicateResources);

            while (ports.Count(p => !p.IsGeneric) < specificTarget)
            {
                int genericIdx = ports.FindIndex(p => p.IsGeneric);
                if (genericIdx < 0)
                    break;

                var placedVertices = ports.Select(p => p.Vertex).ToList();
                bool upgraded = false;

                foreach (var vertex in edgeVertices)
                {
                    if (ports.Any(p => p.Vertex.Equals(vertex)))
                        continue;
                    if (PortSpacingRules.ConflictsWithAny(vertex, placedVertices))
                        continue;

                    var resource = TryPickSpecificResource(board, vertex, usedResources, allowDuplicateResources);
                    if (!resource.HasValue)
                        continue;

                    ports[genericIdx] = new PortDefinition(vertex, resource);
                    if (!usedResources.Contains(resource.Value))
                        usedResources.Add(resource.Value);
                    upgraded = true;
                    break;
                }

                if (!upgraded)
                    break;
            }
        }

        private static void PromoteGenericsToSpecific(
            BoardState board,
            List<PortDefinition> ports,
            int specificTarget,
            HashSet<ResourceType> used,
            bool allowDuplicateResources)
        {
            for (int i = 0; i < ports.Count && ports.Count(p => !p.IsGeneric) < specificTarget; i++)
            {
                if (!ports[i].IsGeneric)
                    continue;

                var resource = TryPickSpecificResource(board, ports[i].Vertex, used, allowDuplicateResources);
                if (!resource.HasValue)
                    continue;

                ports[i] = new PortDefinition(ports[i].Vertex, resource);
                if (!used.Contains(resource.Value))
                    used.Add(resource.Value);
            }
        }

        private static bool VertexTouchesCoastalResource(BoardState board, HexMath.Vertex vertex, ResourceType resource)
        {
            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (!board.Tiles.TryGetValue(hex, out var tile))
                    continue;
                if (!IsLandTile(tile))
                    continue;
                if (tile.IsCoastal && tile.Resource == resource)
                    return true;
            }

            return false;
        }

        private static bool IsLandTile(HexTileData tile) => !tile.IsFishTile || tile.IsDesert;

        private static bool IsBoardEdgeVertex(BoardState board, HexMath.Vertex vertex)
        {
            int onBoard = 0;
            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (board.Tiles.TryGetValue(hex, out var tile) && IsLandTile(tile))
                    onBoard++;
            }

            return onBoard <= 2;
        }
    }
}

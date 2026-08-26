using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Victory
{
    public static class RouteCalculator
    {
        public static int LongestRoadLength(BoardState board, PlayerId player)
        {
            var playerEdges = board.Roads
                .Where(kv => kv.Value == player && !board.DisabledRoads.Contains(kv.Key))
                .Select(kv => kv.Key)
                .ToList();

            if (playerEdges.Count == 0) return 0;

            var adjacency = BuildAdjacency(playerEdges);
            int longest = 0;

            foreach (var start in adjacency.Keys)
            {
                longest = System.Math.Max(
                    longest,
                    DfsLongest(start, adjacency, new HashSet<HexMath.Vertex>(), board, player));
            }

            return longest;
        }

        private static Dictionary<HexMath.Vertex, List<HexMath.Vertex>> BuildAdjacency(List<HexMath.Edge> edges)
        {
            var adj = new Dictionary<HexMath.Vertex, List<HexMath.Vertex>>();

            void Add(HexMath.Vertex a, HexMath.Vertex b)
            {
                if (!adj.ContainsKey(a)) adj[a] = new List<HexMath.Vertex>();
                adj[a].Add(b);
            }

            foreach (var edge in edges)
            {
                var a = VertexGraph.Canonicalize(edge.A);
                var b = VertexGraph.Canonicalize(edge.B);
                Add(a, b);
                Add(b, a);
            }

            return adj;
        }

        /// <summary>
        /// Classic Catan: an opponent settlement or city splits the chain.
        /// You may arrive at that vertex (the road still counts) but cannot
        /// continue through it. Own buildings do not interrupt.
        /// </summary>
        private static int DfsLongest(
            HexMath.Vertex current,
            Dictionary<HexMath.Vertex, List<HexMath.Vertex>> adjacency,
            HashSet<HexMath.Vertex> visited,
            BoardState board,
            PlayerId player)
        {
            visited.Add(current);

            // Arrived via a road onto an enemy building: stop. The caller already
            // counted that last edge. Starting DFS here still explores outbound roads.
            if (visited.Count > 1 && IsOpponentBuilding(board, current, player))
            {
                visited.Remove(current);
                return 0;
            }

            int best = 0;

            if (adjacency.TryGetValue(current, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (visited.Contains(neighbor)) continue;
                    best = System.Math.Max(
                        best,
                        1 + DfsLongest(neighbor, adjacency, visited, board, player));
                }
            }

            visited.Remove(current);
            return best;
        }

        private static bool IsOpponentBuilding(BoardState board, HexMath.Vertex vertex, PlayerId player)
        {
            vertex = VertexGraph.Canonicalize(vertex);
            return board.VertexBuildings.TryGetValue(vertex, out var building)
                && building.owner != player;
        }

        public static PlayerId? GetLongestRoadOwner(BoardState board)
        {
            int human = LongestRoadLength(board, PlayerId.Human);
            int ai = LongestRoadLength(board, PlayerId.Ai);

            if (human >= 5 && human > ai) return PlayerId.Human;
            if (ai >= 5 && ai > human) return PlayerId.Ai;
            return null;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Victory
{
    public static class RouteCalculator
    {
        /// <summary>
        /// v145 Seafarers Longest Trade Route: the longest line of your roads and ships. A road and a ship only
        /// join at your own settlement or city; a rival's building cuts the line. Without ships this is exactly
        /// <see cref="LongestRoadLength"/>.
        /// </summary>
        public static int LongestTradeRouteLength(BoardState board, PlayerId player)
        {
            if (!board.Ships.Values.Contains(player))
                return LongestRoadLength(board, player);

            var pieces = new List<(HexMath.Vertex a, HexMath.Vertex b, bool ship)>();
            foreach (var kv in board.Roads)
                if (kv.Value == player && !board.DisabledRoads.Contains(kv.Key))
                    pieces.Add((VertexGraph.Canonicalize(kv.Key.A), VertexGraph.Canonicalize(kv.Key.B), false));
            foreach (var kv in board.Ships)
                if (kv.Value == player)
                    pieces.Add((VertexGraph.Canonicalize(kv.Key.A), VertexGraph.Canonicalize(kv.Key.B), true));

            var at = new Dictionary<HexMath.Vertex, List<int>>();
            for (int i = 0; i < pieces.Count; i++)
            {
                foreach (var v in new[] { pieces[i].a, pieces[i].b })
                {
                    if (!at.TryGetValue(v, out var list))
                        at[v] = list = new List<int>();
                    list.Add(i);
                }
            }

            bool Own(HexMath.Vertex v) => board.VertexBuildings.TryGetValue(v, out var b) && b.owner == player;
            bool Rival(HexMath.Vertex v) => board.VertexBuildings.TryGetValue(v, out var b) && b.owner != player;

            var used = new bool[pieces.Count];
            int Walk(int piece, HexMath.Vertex end)
            {
                // Arrived at `end` over `piece`; extend through the next unused piece if the joint allows it.
                if (Rival(end))
                    return 0;
                int best = 0;
                foreach (var next in at[end])
                {
                    if (used[next])
                        continue;
                    if (pieces[next].ship != pieces[piece].ship && !Own(end))
                        continue;
                    used[next] = true;
                    var far = pieces[next].a.Equals(end) ? pieces[next].b : pieces[next].a;
                    best = System.Math.Max(best, 1 + Walk(next, far));
                    used[next] = false;
                }

                return best;
            }

            int longest = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                used[i] = true;
                longest = System.Math.Max(longest, 1 + Walk(i, pieces[i].b));
                longest = System.Math.Max(longest, 1 + Walk(i, pieces[i].a));
                used[i] = false;
            }

            return longest;
        }

        public static int LongestRoadLength(BoardState board, PlayerId player)
        {
            var playerEdges = board.Roads
                .Where(kv => kv.Value == player && !board.DisabledRoads.Contains(kv.Key))
                .Select(kv => kv.Key)
                .ToList();

            if (playerEdges.Count == 0) return 0;

            var adjacency = BuildAdjacency(playerEdges);
            var components = GetComponents(playerEdges, adjacency, board, player);

            int longest = 0;
            foreach (var component in components)
            {
                longest = System.Math.Max(
                    longest,
                    LongestInComponent(component, adjacency, board, player));
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
        /// Roads in the same component connect without passing through an opponent
        /// settlement or city. Enemy vertices are endpoints, not bridges.
        /// </summary>
        private static List<List<HexMath.Edge>> GetComponents(
            List<HexMath.Edge> playerEdges,
            Dictionary<HexMath.Vertex, List<HexMath.Vertex>> adjacency,
            BoardState board,
            PlayerId player)
        {
            var edgeSet = new HashSet<HexMath.Edge>(playerEdges);
            var visited = new HashSet<HexMath.Edge>();
            var components = new List<List<HexMath.Edge>>();

            foreach (var start in playerEdges)
            {
                if (visited.Contains(start)) continue;

                var component = new List<HexMath.Edge>();
                ExploreComponent(start, edgeSet, visited, component, adjacency, board, player);
                components.Add(component);
            }

            return components;
        }

        private static void ExploreComponent(
            HexMath.Edge edge,
            HashSet<HexMath.Edge> edgeSet,
            HashSet<HexMath.Edge> visited,
            List<HexMath.Edge> component,
            Dictionary<HexMath.Vertex, List<HexMath.Vertex>> adjacency,
            BoardState board,
            PlayerId player)
        {
            if (!visited.Add(edge)) return;
            component.Add(edge);

            TryExpandFromVertex(edge.A, edgeSet, visited, component, adjacency, board, player);
            TryExpandFromVertex(edge.B, edgeSet, visited, component, adjacency, board, player);
        }

        private static void TryExpandFromVertex(
            HexMath.Vertex vertex,
            HashSet<HexMath.Edge> edgeSet,
            HashSet<HexMath.Edge> visited,
            List<HexMath.Edge> component,
            Dictionary<HexMath.Vertex, List<HexMath.Vertex>> adjacency,
            BoardState board,
            PlayerId player)
        {
            vertex = VertexGraph.Canonicalize(vertex);
            if (IsOpponentBuilding(board, vertex, player)) return;
            if (!adjacency.TryGetValue(vertex, out var neighbors)) return;

            foreach (var neighbor in neighbors)
            {
                var nextEdge = VertexGraph.GetEdgeBetween(vertex, neighbor);
                if (!edgeSet.Contains(nextEdge) || visited.Contains(nextEdge)) continue;
                ExploreComponent(nextEdge, edgeSet, visited, component, adjacency, board, player);
            }
        }

        private static int LongestInComponent(
            List<HexMath.Edge> component,
            Dictionary<HexMath.Vertex, List<HexMath.Vertex>> adjacency,
            BoardState board,
            PlayerId player)
        {
            if (component.Count == 0) return 0;

            var degrees = new Dictionary<HexMath.Vertex, int>();
            foreach (var edge in component)
            {
                IncrementDegree(degrees, edge.A);
                IncrementDegree(degrees, edge.B);
            }

            int maxDegree = 0;
            foreach (var degree in degrees.Values)
                maxDegree = System.Math.Max(maxDegree, degree);

            // Paths and cycles (max degree 2) use every road in the component.
            if (maxDegree <= 2)
                return component.Count;

            var allowedEdges = new HashSet<HexMath.Edge>(component);
            int best = 0;
            foreach (var start in degrees.Keys)
            {
                best = System.Math.Max(
                    best,
                    DfsLongest(start, adjacency, new HashSet<HexMath.Vertex>(), board, player, allowedEdges));
            }

            return best;
        }

        private static void IncrementDegree(Dictionary<HexMath.Vertex, int> degrees, HexMath.Vertex vertex)
        {
            vertex = VertexGraph.Canonicalize(vertex);
            degrees.TryGetValue(vertex, out int count);
            degrees[vertex] = count + 1;
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
            PlayerId player,
            HashSet<HexMath.Edge> allowedEdges)
        {
            current = VertexGraph.Canonicalize(current);
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

                    var edge = VertexGraph.GetEdgeBetween(current, neighbor);
                    if (!allowedEdges.Contains(edge)) continue;

                    best = System.Math.Max(
                        best,
                        1 + DfsLongest(neighbor, adjacency, visited, board, player, allowedEdges));
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

            if (human >= BalanceConfig.LongestRouteMinimum && human > ai) return PlayerId.Human;
            if (ai >= BalanceConfig.LongestRouteMinimum && ai > human) return PlayerId.Ai;
            return null;
        }
    }
}

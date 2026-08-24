using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core.Map
{
    public sealed class PlacementValidator
    {
        public bool CanPlaceSettlement(BoardState board, Vertex vertex, PlayerId player, bool setupPhase = false)
        {
            vertex = VertexGraph.Canonicalize(vertex);

            if (board.VertexBuildings.ContainsKey(vertex))
                return false;

            if (!IsVertexOnBoard(board, vertex))
                return false;

            // Catan distance rule: no settlement within 2 edges (adjacent vertices forbidden)
            foreach (var existing in board.VertexBuildings.Keys)
            {
                if (VertexGraph.VertexDistance(vertex, existing) < 2)
                    return false;
            }

            if (setupPhase)
                return true;

            return IsConnectedToNetwork(board, vertex, player);
        }

        public bool CanPlaceRoad(BoardState board, Edge edge, PlayerId player, bool setupPhase = false)
        {
            edge = NormalizeEdge(edge);
            if (board.Roads.ContainsKey(edge))
                return false;

            if (!IsEdgeOnBoard(board, edge))
                return false;

            if (setupPhase)
            {
                // During setup, road must touch player's just-placed settlement
                return TouchesPlayerBuilding(board, edge.A, player)
                    || TouchesPlayerBuilding(board, edge.B, player);
            }

            return IsRoadConnected(board, edge, player);
        }

        public bool CanUpgradeToCity(BoardState board, Vertex vertex, PlayerId player)
        {
            vertex = VertexGraph.Canonicalize(vertex);
            if (!board.VertexBuildings.TryGetValue(vertex, out var building))
                return false;
            return building.owner == player && building.type == BuildingType.Settlement;
        }

        private static bool IsVertexOnBoard(BoardState board, Vertex vertex)
        {
            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (board.TryGetTile(hex, out _))
                    return true;
            }
            return false;
        }

        private static bool IsEdgeOnBoard(BoardState board, Edge edge)
        {
            return IsVertexOnBoard(board, edge.A) && IsVertexOnBoard(board, edge.B);
        }

        private static bool TouchesPlayerBuilding(BoardState board, Vertex vertex, PlayerId player)
        {
            vertex = VertexGraph.Canonicalize(vertex);
            return board.VertexBuildings.TryGetValue(vertex, out var b)
                && b.owner == player;
        }

        private static bool IsConnectedToNetwork(BoardState board, Vertex vertex, PlayerId player)
        {
            vertex = VertexGraph.Canonicalize(vertex);

            if (TouchesPlayerBuilding(board, vertex, player))
                return true;

            foreach (var adjacent in VertexGraph.GetAdjacentVertices(vertex))
            {
                var edge = VertexGraph.GetEdgeBetween(vertex, adjacent);
                if (board.Roads.TryGetValue(edge, out var owner) && owner == player
                    && !board.DisabledRoads.Contains(edge))
                    return true;
            }

            return false;
        }

        private static bool IsRoadConnected(BoardState board, Edge edge, PlayerId player)
        {
            if (board.DisabledRoads.Contains(edge))
                return false;

            return TouchesPlayerBuilding(board, edge.A, player)
                || TouchesPlayerBuilding(board, edge.B, player)
                || TouchesPlayerRoad(board, edge.A, player, edge)
                || TouchesPlayerRoad(board, edge.B, player, edge);
        }

        private static bool TouchesPlayerRoad(BoardState board, Vertex vertex, PlayerId player, Edge exclude)
        {
            vertex = VertexGraph.Canonicalize(vertex);
            foreach (var adjacent in VertexGraph.GetAdjacentVertices(vertex))
            {
                var edge = VertexGraph.GetEdgeBetween(vertex, adjacent);
                if (edge.Equals(exclude)) continue;
                if (board.Roads.TryGetValue(edge, out var owner) && owner == player
                    && !board.DisabledRoads.Contains(edge))
                    return true;
            }
            return false;
        }

        public IEnumerable<Vertex> GetValidSettlementSpots(BoardState board, PlayerId player, bool setupPhase) =>
            GetAllVertices(board).Where(v => CanPlaceSettlement(board, v, player, setupPhase));

        public IEnumerable<Edge> GetValidRoadSpots(BoardState board, PlayerId player, bool setupPhase) =>
            GetAllEdges(board).Where(e => CanPlaceRoad(board, e, player, setupPhase));

        private static IEnumerable<Vertex> GetAllVertices(BoardState board)
        {
            var seen = new HashSet<Vertex>();
            foreach (var hex in board.Tiles.Keys)
            {
                for (int c = 0; c < 6; c++)
                {
                    var v = VertexGraph.Canonicalize(new Vertex(hex, c));
                    if (seen.Add(v))
                        yield return v;
                }
            }
        }

        private static IEnumerable<Edge> GetAllEdges(BoardState board)
        {
            var seen = new HashSet<Edge>();
            foreach (var v in GetAllVertices(board))
            {
                foreach (var adj in VertexGraph.GetAdjacentVertices(v))
                {
                    var edge = NormalizeEdge(new Edge(v, adj));
                    if (seen.Add(edge))
                        yield return edge;
                }
            }
        }

        private static Edge NormalizeEdge(Edge edge) =>
            new Edge(VertexGraph.Canonicalize(edge.A), VertexGraph.Canonicalize(edge.B));

        public bool CanAffordRoad(BoardState board, PlayerId player, ResourceBundle inventory) =>
            inventory.CanAfford(BalanceConfig.GetRoadCost(board, player));

        public bool CanAffordSettlement(BoardState board, PlayerId player, ResourceBundle inventory) =>
            inventory.CanAfford(BalanceConfig.GetSettlementCost(board, player));

        public bool CanAffordCity(BoardState board, PlayerId player, ResourceBundle inventory) =>
            inventory.CanAfford(BalanceConfig.GetCityCost(board, player));
    }
}

using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core.Map
{
    /// <summary>
    /// v145 Seafarers: where ships may go and which ship may sail. Follows the official rules:
    /// a ship sits on an edge bordering at least one sea hex; it joins your own settlement/city or your own
    /// ship (never a road directly — roads and ships only meet at your own settlement or city); a rival's
    /// building cuts the line; the pirate blocks the six edges of its hex.
    /// </summary>
    public static class ShipRules
    {
        public static Edge Normalize(Edge e) => new(VertexGraph.Canonicalize(e.A), VertexGraph.Canonicalize(e.B));

        /// <summary>The (up to) two hexes that share this edge.</summary>
        public static IEnumerable<HexCoord> EdgeHexes(Edge e)
        {
            var b = new HashSet<HexCoord>(VertexGraph.GetHexesForVertex(e.B));
            return VertexGraph.GetHexesForVertex(e.A).Where(b.Contains);
        }

        /// <summary>The six edges around a hex.</summary>
        public static IEnumerable<Edge> HexEdges(HexCoord hex)
        {
            for (int i = 0; i < 6; i++)
                yield return Normalize(HexMath.GetEdge(hex, i));
        }

        public static bool IsShipEdge(BoardState board, Edge e) =>
            board.IsSeafarers && EdgeHexes(e).Any(board.SeaHexes.Contains);

        /// <summary>True when at least one of the edge's hexes is land (roads need land).</summary>
        public static bool BordersLand(BoardState board, Edge e) => EdgeHexes(e).Any(h => board.Tiles.ContainsKey(h));

        public static bool IsPirateBlocked(BoardState board, Edge e) =>
            board.PirateHex.HasValue && EdgeHexes(e).Contains(board.PirateHex.Value);

        public static bool HasOwnBuilding(BoardState board, Vertex v, PlayerId player) =>
            board.VertexBuildings.TryGetValue(VertexGraph.Canonicalize(v), out var b) && b.owner == player;

        public static bool HasRivalBuilding(BoardState board, Vertex v, PlayerId player) =>
            board.VertexBuildings.TryGetValue(VertexGraph.Canonicalize(v), out var b) && b.owner != player;

        /// <summary>Own ships touching <paramref name="v"/>, other than <paramref name="exclude"/>.</summary>
        public static IEnumerable<Edge> ShipsAt(BoardState board, Vertex v, PlayerId player, Edge? exclude = null)
        {
            v = VertexGraph.Canonicalize(v);
            foreach (var adj in VertexGraph.GetAdjacentVertices(v))
            {
                var e = VertexGraph.GetEdgeBetween(v, adj);
                if (exclude.HasValue && e.Equals(exclude.Value))
                    continue;
                if (board.Ships.TryGetValue(e, out var owner) && owner == player)
                    yield return e;
            }
        }

        /// <summary>
        /// Can <paramref name="player"/> put a ship on <paramref name="edge"/>? <paramref name="ignore"/> is a ship
        /// treated as absent (the one being moved).
        /// </summary>
        public static bool CanPlaceShip(BoardState board, Edge edge, PlayerId player, Edge? ignore = null)
        {
            edge = Normalize(edge);
            if (!IsShipEdge(board, edge) || IsPirateBlocked(board, edge))
                return false;
            if (board.Roads.ContainsKey(edge))
                return false;
            if (board.Ships.ContainsKey(edge) && !(ignore.HasValue && ignore.Value.Equals(edge)))
                return false;
            return IsShipConnected(board, edge, player, ignore);
        }

        private static bool IsShipConnected(BoardState board, Edge edge, PlayerId player, Edge? ignore)
        {
            foreach (var v in new[] { edge.A, edge.B })
            {
                if (HasOwnBuilding(board, v, player))
                    return true;
                if (HasRivalBuilding(board, v, player))
                    continue;
                if (ShipsAt(board, v, player, edge).Any(s => !(ignore.HasValue && s.Equals(ignore.Value))))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The "open" end of a shipping line: a ship with an end that holds neither your own building nor
        /// another of your ships. Ships of a closed route (both ends anchored) cannot move.
        /// </summary>
        public static bool IsOpenShip(BoardState board, Edge ship, PlayerId player)
        {
            ship = Normalize(ship);
            if (!board.Ships.TryGetValue(ship, out var owner) || owner != player)
                return false;
            foreach (var v in new[] { ship.A, ship.B })
            {
                if (HasOwnBuilding(board, v, player))
                    continue;
                if (!ShipsAt(board, v, player, ship).Any())
                    return true;
            }

            return false;
        }

        /// <summary>Candidate edges next to any of the given vertices.</summary>
        public static IEnumerable<Edge> EdgesAround(IEnumerable<Vertex> vertices)
        {
            var seen = new HashSet<Edge>();
            foreach (var v in vertices)
            foreach (var adj in VertexGraph.GetAdjacentVertices(v))
            {
                var e = VertexGraph.GetEdgeBetween(v, adj);
                if (seen.Add(e))
                    yield return e;
            }
        }

        /// <summary>Every vertex the player can build a ship from: own buildings and own ship ends.</summary>
        public static IEnumerable<Vertex> ShipNetworkVertices(BoardState board, PlayerId player)
        {
            var set = new HashSet<Vertex>();
            foreach (var kv in board.VertexBuildings)
                if (kv.Value.owner == player)
                    set.Add(VertexGraph.Canonicalize(kv.Key));
            foreach (var kv in board.Ships)
            {
                if (kv.Value != player)
                    continue;
                set.Add(VertexGraph.Canonicalize(kv.Key.A));
                set.Add(VertexGraph.Canonicalize(kv.Key.B));
            }

            return set;
        }

        public static IEnumerable<Edge> ValidShipSpots(BoardState board, PlayerId player, Edge? ignore = null) =>
            EdgesAround(ShipNetworkVertices(board, player)).Where(e => CanPlaceShip(board, e, player, ignore))
                .OrderBy(e => e.A.Hex.Q).ThenBy(e => e.A.Hex.R).ThenBy(e => e.A.CornerIndex)
                .ThenBy(e => e.B.Hex.Q).ThenBy(e => e.B.Hex.R).ThenBy(e => e.B.CornerIndex);
    }
}

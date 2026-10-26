using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core.Match
{
    /// <summary>
    /// v145 Seafarers AI: ships head for free settlement spots across the water (nearest good spot first), the open
    /// ship sails closer when that helps, Road Building mixes roads and ships, and the pirate is used when it robs
    /// more than the robber would.
    /// </summary>
    public static partial class MatchAi
    {
        /// <summary>How far (in sea edges) a ship looks for a spot to settle.</summary>
        public const int ShipLookahead = 6;

        /// <summary>
        /// Worth of a ship on <paramref name="e"/>: the best free settlement spot across the water (off the home
        /// island) reachable over open sea edges from its ends, discounted by the sea edges still to cross (0 when
        /// nothing is in reach). <paramref name="ignore"/> is a ship about to sail (its edge counts as open).
        /// </summary>
        public static int ShipScore(MatchController match, Edge e, int seat, Edge? ignore = null)
        {
            var map = ShipWorthMap(match, seat, ignore);
            map.TryGetValue(VertexGraph.Canonicalize(e.A), out int a);
            map.TryGetValue(VertexGraph.Canonicalize(e.B), out int b);
            return System.Math.Max(a, b);
        }

        /// <summary>AI weighting of a spot across the water (a fresh island: new numbers, no rivals nearby).</summary>
        private const int IsletWorthMul = 3, IsletWorthBonus = 6;

        private static object _worthKeyMatch;
        private static string _worthKey;
        private static Dictionary<Vertex, int> _worthMap;

        /// <summary>
        /// For every corner within <see cref="ShipLookahead"/> sea edges of a free spot across the water, the best
        /// discounted spot worth (one reverse search per spot; cached per board layout).
        /// </summary>
        private static Dictionary<Vertex, int> ShipWorthMap(MatchController match, int seat, Edge? ignore)
        {
            var state = match.State;
            var board = state.Board;
            int shipHash = 0;
            foreach (var kv in board.Ships)
                shipHash = unchecked(shipHash + kv.Key.GetHashCode() * 31 + (int)kv.Value);
            string key = $"{seat}|{ignore}|{board.Roads.Count}|{board.VertexBuildings.Count}|{shipHash}|{board.Ships.Count}";
            if (ReferenceEquals(_worthKeyMatch, match) && key == _worthKey && _worthMap != null)
                return _worthMap;

            var owner = MatchSeats.ToPlayerId(seat);
            var home = board.SetupLandHexes;
            var map = new Dictionary<Vertex, int>();
            var seenSpots = new HashSet<Vertex>();
            foreach (var hex in board.Tiles.Keys)
            {
                if (home.Count > 0 && home.Contains(hex))
                    continue;
                for (int c = 0; c < 6; c++)
                {
                    var spot = VertexGraph.Canonicalize(new Vertex(hex, c));
                    if (!seenSpots.Add(spot))
                        continue;
                    if (home.Count > 0 && VertexGraph.GetHexesForVertex(spot).Any(home.Contains))
                        continue;
                    if (!match.Placement.CanPlaceSettlement(board, spot, owner, true))
                        continue;
                    int worth = IsletWorthMul * VertexScore(state, spot, seat) + IsletWorthBonus;
                    if (worth <= 0)
                        continue;
                    var dist = new Dictionary<Vertex, int> { [spot] = 0 };
                    var queue = new Queue<Vertex>();
                    queue.Enqueue(spot);
                    while (queue.Count > 0)
                    {
                        var v = queue.Dequeue();
                        int d = dist[v];
                        int w = worth / (d + 1);
                        if (!map.TryGetValue(v, out int had) || w > had)
                            map[v] = w;
                        if (d >= ShipLookahead || d > 0 && ShipRules.HasRivalBuilding(board, v, owner))
                            continue;
                        foreach (var next in VertexGraph.GetAdjacentVertices(v))
                        {
                            var n = VertexGraph.Canonicalize(next);
                            if (dist.ContainsKey(n))
                                continue;
                            var edge = ShipRules.Normalize(new Edge(v, n));
                            if (!ShipRules.IsShipEdge(board, edge) || board.Roads.ContainsKey(edge)
                                || board.Ships.ContainsKey(edge) && !(ignore.HasValue && edge.Equals(ShipRules.Normalize(ignore.Value))))
                                continue;
                            dist[n] = d + 1;
                            queue.Enqueue(n);
                        }
                    }
                }
            }

            _worthKeyMatch = match;
            _worthKey = key;
            _worthMap = map;
            return map;
        }

        /// <summary>The best ship spot to build on (null when no ship leads anywhere).</summary>
        public static Edge? BestShip(MatchController match, int seat, out int score)
        {
            score = 0;
            if (!match.SeafarersOn || match.CountShips(seat) >= MatchController.MaxShips)
                return null;
            Edge? best = null;
            foreach (var e in match.ValidShips(seat))
            {
                int s = ShipScore(match, e, seat);
                if (s > score)
                {
                    score = s;
                    best = e;
                }
            }

            return best;
        }

        /// <summary>
        /// Sail the open ship when another spot is clearly closer to a settlement spot (strictly better, so it never
        /// sails back next turn).
        /// </summary>
        public static MatchCommand ChooseShipMove(MatchController match, int seat)
        {
            if (!match.SeafarersOn)
                return null;
            MatchCommand best = null;
            int bestGain = 2;
            foreach (var from in match.MovableShips(seat))
            {
                int here = ShipScore(match, from, seat, from);
                foreach (var to in match.ShipMoveTargets(seat, from))
                {
                    int gain = ShipScore(match, to, seat, from) - here;
                    if (gain > bestGain)
                    {
                        bestGain = gain;
                        best = MatchCommand.MoveShipTo(seat, from, to);
                    }
                }
            }

            return best;
        }

        /// <summary>Road Building on a sea map: the better of the best road and the best ship.</summary>
        private static MatchCommand ChooseFreePiece(MatchController match, int seat)
        {
            var roads = match.ValidRoads(seat).ToList();
            var ship = BestShip(match, seat, out int shipScore);
            if (roads.Count > 0)
            {
                var road = Best(roads, e => RoadScore(match, e, seat));
                if (!ship.HasValue || RoadScore(match, road, seat) >= shipScore)
                    return MatchCommand.AtEdge(MatchCommandType.BuildRoad, seat, road);
            }

            if (ship.HasValue)
                return MatchCommand.Ship(seat, ship.Value);
            var any = match.ValidShips(seat).FirstOrDefault();
            return match.ValidShips(seat).Any() ? MatchCommand.Ship(seat, any) : null;
        }

        /// <summary>
        /// v145: the pirate instead of the robber when a sea hex robs a ship owner and the robber's best land hex
        /// hurts nobody (or the pirate frees one of our own blocked ships).
        /// </summary>
        private const int PirateBlocksLeader = 20;

        private static MatchCommand ChoosePirate(MatchController match, int seat, MatchCommand robber, int robberScore)
        {
            if (!match.SeafarersOn)
                return null;
            var state = match.State;
            var owner = MatchSeats.ToPlayerId(seat);
            int leader = Leader(state, seat);
            bool ownBlocked = state.Board.PirateHex.HasValue
                              && ShipRules.HexEdges(state.Board.PirateHex.Value).Any(e => state.Board.Ships.TryGetValue(e, out var o) && o == owner);
            HexCoord? bestHex = null;
            int bestScore = int.MinValue;
            foreach (var hex in match.PirateTargets())
            {
                int score = 0;
                foreach (var e in ShipRules.HexEdges(hex))
                {
                    if (!state.Board.Ships.TryGetValue(e, out var o))
                        continue;
                    int s = MatchSeats.ToSeat(o);
                    if (s == seat)
                        score -= 50;
                    else
                        score += 4 + (s == leader ? 6 : 0) + (state.Seats[s].CardCount > 0 ? 2 : 0)
                                 + (s == leader && ShipRules.IsOpenShip(state.Board, e, o) ? PirateBlocksLeader : 0);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestHex = hex;
                }
            }

            if (!bestHex.HasValue)
                return null;
            var victims = match.PirateVictims(seat, bestHex.Value).ToList();
            // ... or it parks on the leader's open ship, stopping their sail toward a new island.
            bool worth = victims.Count > 0 && (robberScore <= 0 || bestScore >= PirateBlocksLeader)
                         || ownBlocked && robberScore <= 0 && bestScore >= 0;
            if (!worth)
                return null;
            int victim = victims.OrderByDescending(s => s == leader).ThenByDescending(s => state.Seats[s].PublicVictoryPoints)
                .ThenByDescending(s => state.Seats[s].CardCount).ThenBy(s => s).DefaultIfEmpty(-1).First();
            return MatchCommand.Pirate(seat, bestHex.Value, victim);
        }
    }
}

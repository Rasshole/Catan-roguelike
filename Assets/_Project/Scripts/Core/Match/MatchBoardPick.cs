using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core.Match
{
    /// <summary>What a board click is for in the match: <see cref="Auto"/> picks the nearest affordable build.</summary>
    public enum MatchBuildMode
    {
        Auto,
        Settlement,
        City,
        Road,
        /// <summary>v145 Seafarers: build a ship on a sea edge (also the free pieces of Road Building).</summary>
        Ship,
        /// <summary>v145 Seafarers: move your open ship; first pick the ship, then where it goes.</summary>
        MoveShip
    }

    /// <summary>
    /// v135: turns a board click (world x/z on the board plane) into the matching <see cref="MatchCommand"/> for the
    /// acting seat, so pass-and-play players place pieces by clicking the board. Pure C#: the hot-seat HUD raycasts
    /// and hands the point in; the command still goes through <see cref="MatchController.Apply"/>.
    /// </summary>
    public static class MatchBoardPick
    {
        public const float DefaultRadius = 0.35f;
        /// <summary>v136: robber click on a hex touching 2+ robbable opponents — the HUD must ask whom to rob.</summary>
        public const int VictimPending = -2;

        public static MatchCommand Pick(MatchController match, MatchBuildMode mode, float x, float z, float scale,
            float radius = DefaultRadius) => Pick(match, mode, x, z, scale, null, radius);

        /// <summary>
        /// v145: <paramref name="moveFrom"/> is the ship picked for <see cref="MatchBuildMode.MoveShip"/> (null until
        /// one is picked: see <see cref="PickMovableShip"/>).
        /// </summary>
        public static MatchCommand Pick(MatchController match, MatchBuildMode mode, float x, float z, float scale,
            Edge? moveFrom, float radius = DefaultRadius)
        {
            var state = match.State;
            int seat = state.ActingSeat;
            float max = radius * scale;
            switch (state.Phase)
            {
                case MatchPhase.SetupSettlement:
                    return At(MatchCommandType.PlaceSetupSettlement, seat, Nearest(match.ValidSetupSettlements(), x, z, scale, max));
                case MatchPhase.SetupRoad:
                    return At(MatchCommandType.PlaceSetupRoad, seat, Nearest(match.ValidSetupRoads(), x, z, scale, max));
                case MatchPhase.RoadBuilding:
                    return FreePieceShips(match, seat, mode)
                        ? At(MatchCommandType.BuildShip, seat, Nearest(match.ValidShips(seat), x, z, scale, max))
                        : At(MatchCommandType.BuildRoad, seat, Nearest(match.ValidRoads(seat), x, z, scale, max));
                case MatchPhase.MoveRobber:
                    return PickRobber(match, seat, x, z, scale);
                case MatchPhase.Main when mode == MatchBuildMode.MoveShip:
                {
                    if (!moveFrom.HasValue)
                        return null;
                    var to = Nearest(match.ShipMoveTargets(seat, moveFrom.Value), x, z, scale, max);
                    return to.HasValue ? MatchCommand.MoveShipTo(seat, moveFrom.Value, to.Value) : null;
                }
                case MatchPhase.Main when mode == MatchBuildMode.Ship:
                    return state.ActiveTrade == null
                        ? At(MatchCommandType.BuildShip, seat, Nearest(Targets(match, mode).edges, x, z, scale, max))
                        : null;
                case MatchPhase.Main:
                    return PickBuild(match, seat, mode, x, z, scale, max);
                default:
                    return null;
            }
        }

        /// <summary>v145: Road Building places ships when asked to, or when no road fits anywhere.</summary>
        public static bool FreePieceShips(MatchController match, int seat, MatchBuildMode mode) =>
            match.SeafarersOn && (mode == MatchBuildMode.Ship
                                  || !match.ValidRoads(seat).Any() && match.ValidShips(seat).Any());

        /// <summary>v145: the acting seat's movable ship nearest the click (step one of a ship move), if any.</summary>
        public static Edge? PickMovableShip(MatchController match, float x, float z, float scale,
            float radius = DefaultRadius)
        {
            var state = match.State;
            if (state.Phase != MatchPhase.Main || state.ActiveTrade != null)
                return null;
            return Nearest(match.MovableShips(state.ActingSeat), x, z, scale, radius * scale);
        }

        /// <summary>The spots a click can hit right now (for highlighting): vertices and edges.</summary>
        public static (List<Vertex> vertices, List<Edge> edges, bool cities) Targets(MatchController match, MatchBuildMode mode)
        {
            var state = match.State;
            int seat = state.ActingSeat;
            var hand = state.Seats[seat].Hand;
            switch (state.Phase)
            {
                case MatchPhase.SetupSettlement:
                    return (match.ValidSetupSettlements().ToList(), new List<Edge>(), false);
                case MatchPhase.SetupRoad:
                    return (new List<Vertex>(), match.ValidSetupRoads().ToList(), false);
                case MatchPhase.RoadBuilding:
                    return (new List<Vertex>(),
                        (FreePieceShips(match, seat, mode) ? match.ValidShips(seat) : match.ValidRoads(seat)).ToList(), false);
                case MatchPhase.Main when state.ActiveTrade == null && mode == MatchBuildMode.Ship:
                {
                    var e = new List<Edge>();
                    if (match.SeafarersOn && hand.CanAfford(match.ShipCostFor(seat))
                                          && match.CountShips(seat) < MatchController.MaxShips)
                        e.AddRange(match.ValidShips(seat));
                    return (new List<Vertex>(), e, false);
                }
                case MatchPhase.Main when state.ActiveTrade == null && mode == MatchBuildMode.MoveShip:
                    return (new List<Vertex>(), match.MovableShips(seat).ToList(), false);
                case MatchPhase.Main when state.ActiveTrade == null:
                {
                    var v = new List<Vertex>();
                    var e = new List<Edge>();
                    bool cities = false;
                    if ((mode == MatchBuildMode.Auto || mode == MatchBuildMode.Settlement)
                        && hand.CanAfford(match.SettlementCostFor(seat))
                        && match.CountSettlements(seat) < BuildingPieceLimits.MaxSettlements)
                        v.AddRange(match.ValidSettlements(seat));
                    if ((mode == MatchBuildMode.Auto || mode == MatchBuildMode.City) && hand.CanAfford(match.CityCostFor(seat))
                        && match.CountCities(seat) < BuildingPieceLimits.MaxCities)
                    {
                        v.AddRange(match.UpgradableSettlements(seat));
                        cities = mode == MatchBuildMode.City || v.Count > 0 && !hand.CanAfford(match.SettlementCostFor(seat));
                    }

                    if ((mode == MatchBuildMode.Auto || mode == MatchBuildMode.Road) && hand.CanAfford(match.RoadCostFor(seat))
                        && match.CountRoads(seat) < BuildingPieceLimits.MaxRoads)
                        e.AddRange(match.ValidRoads(seat));
                    return (v, e, cities);
                }
                default:
                    return (new List<Vertex>(), new List<Edge>(), false);
            }
        }

        private static MatchCommand PickBuild(MatchController match, int seat, MatchBuildMode mode, float x, float z,
            float scale, float max)
        {
            var (vertices, edges, _) = Targets(match, mode);
            var upgradable = new HashSet<Vertex>(match.UpgradableSettlements(seat));
            var v = Nearest(vertices, x, z, scale, max);
            var e = Nearest(edges, x, z, scale, max);
            float dv = v.HasValue ? DistV(v.Value, x, z, scale) : float.MaxValue;
            float de = e.HasValue ? DistE(e.Value, x, z, scale) : float.MaxValue;
            if (v.HasValue && dv <= de)
                return MatchCommand.AtVertex(upgradable.Contains(v.Value) ? MatchCommandType.BuildCity : MatchCommandType.BuildSettlement,
                    seat, v.Value);
            return e.HasValue ? MatchCommand.AtEdge(MatchCommandType.BuildRoad, seat, e.Value) : null;
        }

        private static MatchCommand PickRobber(MatchController match, int seat, float x, float z, float scale)
        {
            var state = match.State;
            HexCoord? best = null;
            float bestD = float.MaxValue;
            foreach (var coord in state.Board.Tiles.Keys)
            {
                if (state.Board.RobberTile == coord || state.Board.LakeHex == coord)
                    continue;
                var (tx, tz) = HexMath.ToWorldPosition(coord, scale);
                float d = (x - tx) * (x - tx) + (z - tz) * (z - tz);
                if (d < bestD)
                {
                    bestD = d;
                    best = coord;
                }
            }

            // v145 Seafarers: a click on open sea moves the pirate instead.
            bool pirate = false;
            if (match.SeafarersOn)
            {
                foreach (var coord in match.PirateTargets())
                {
                    var (tx, tz) = HexMath.ToWorldPosition(coord, scale);
                    float d = (x - tx) * (x - tx) + (z - tz) * (z - tz);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = coord;
                        pirate = true;
                    }
                }
            }

            // Clicks beyond a tile's reach (in the sea) don't count.
            if (!best.HasValue || bestD > scale * scale * 1.1f)
                return null;
            if (pirate)
            {
                var pv = match.PirateVictims(seat, best.Value).ToList();
                return MatchCommand.Pirate(seat, best.Value, pv.Count == 0 ? -1 : pv.Count == 1 ? pv[0] : VictimPending);
            }

            // v136: one possible victim is robbed directly; with two or more the player chooses (victim picker):
            // the command comes back with Victim = VictimPending and the HUD asks before applying it.
            var victims = match.RobberVictims(seat, best.Value).ToList();
            int victim = victims.Count == 0 ? -1 : victims.Count == 1 ? victims[0] : VictimPending;
            return MatchCommand.Robber(seat, best.Value, victim);
        }

        private static MatchCommand At(MatchCommandType type, int seat, Vertex? v) =>
            v.HasValue ? MatchCommand.AtVertex(type, seat, v.Value) : null;

        private static MatchCommand At(MatchCommandType type, int seat, Edge? e) =>
            e.HasValue ? MatchCommand.AtEdge(type, seat, e.Value) : null;

        public static Vertex? Nearest(IEnumerable<Vertex> candidates, float x, float z, float scale, float max)
        {
            Vertex? best = null;
            float bestD = max * max;
            foreach (var v in candidates)
            {
                float d = DistV(v, x, z, scale);
                if (d <= bestD)
                {
                    bestD = d;
                    best = v;
                }
            }

            return best;
        }

        public static Edge? Nearest(IEnumerable<Edge> candidates, float x, float z, float scale, float max)
        {
            Edge? best = null;
            float bestD = max * max;
            foreach (var e in candidates)
            {
                float d = DistE(e, x, z, scale);
                if (d <= bestD)
                {
                    bestD = d;
                    best = e;
                }
            }

            return best;
        }

        private static float DistV(Vertex v, float x, float z, float scale)
        {
            var (vx, vz) = HexMath.VertexToWorldPosition(v, scale);
            return (x - vx) * (x - vx) + (z - vz) * (z - vz);
        }

        private static float DistE(Edge e, float x, float z, float scale)
        {
            var (mx, mz) = HexMath.EdgeMidpoint(e, scale);
            return (x - mx) * (x - mx) + (z - mz) * (z - mz);
        }
    }
}

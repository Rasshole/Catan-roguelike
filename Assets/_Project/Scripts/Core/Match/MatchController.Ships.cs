using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core.Match
{
    /// <summary>
    /// v145 Seafarers: ships and the pirate on maps with playable sea (<see cref="BoardState.SeaHexes"/>).
    /// Classic maps have no sea hexes, so none of this changes them.
    /// </summary>
    public sealed partial class MatchController
    {
        public static readonly ResourceBundle ShipCost = new() { Wood = 1, Sheep = 1 };
        public const int MaxShips = 15;

        /// <summary>True on a sea map: ships, ship moves and the pirate are in play.</summary>
        public bool SeafarersOn => State.Board.IsSeafarers;

        public int CountShips(int seat) => State.Board.CountShips(MatchSeats.ToPlayerId(seat));

        /// <summary>Ships share the Road Crew counter with roads (every third piece is free).</summary>
        public ResourceBundle ShipCostFor(int seat) => IsNextRoadCrewPieceFree(seat) ? default : ShipCost;

        public IEnumerable<Edge> ValidShips(int seat)
        {
            if (!SeafarersOn || CountShips(seat) >= MaxShips)
                return Enumerable.Empty<Edge>();
            return ShipRules.ValidShipSpots(State.Board, MatchSeats.ToPlayerId(seat)).ToList();
        }

        /// <summary>Ships the current seat may move right now (open end, not built this turn, not under the pirate).</summary>
        public IEnumerable<Edge> MovableShips(int seat)
        {
            if (!SeafarersOn || State.ShipMovedThisTurn || seat != State.CurrentSeat || State.Phase != MatchPhase.Main)
                return Enumerable.Empty<Edge>();
            var owner = MatchSeats.ToPlayerId(seat);
            return State.Board.Ships.Where(kv => kv.Value == owner).Select(kv => kv.Key)
                .Where(e => !State.ShipsBuiltThisTurn.Contains(e) && !ShipRules.IsPirateBlocked(State.Board, e)
                            && ShipRules.IsOpenShip(State.Board, e, owner))
                .Where(e => ShipMoveTargets(seat, e).Any())
                .OrderBy(e => e.A.Hex.Q).ThenBy(e => e.A.Hex.R).ThenBy(e => e.A.CornerIndex)
                .ThenBy(e => e.B.Hex.Q).ThenBy(e => e.B.Hex.R).ThenBy(e => e.B.CornerIndex)
                .ToList();
        }

        /// <summary>Where the ship on <paramref name="from"/> could sail to (its own edge excluded).</summary>
        public IEnumerable<Edge> ShipMoveTargets(int seat, Edge from)
        {
            from = NormalizeEdge(from);
            return ShipRules.ValidShipSpots(State.Board, MatchSeats.ToPlayerId(seat), from).Where(e => !e.Equals(from)).ToList();
        }

        /// <summary>v145 Road Building: room for at least one more free road or ship.</summary>
        private bool AnyFreePieceSpot(int seat) =>
            (CountRoads(seat) < BuildingPieceLimits.MaxRoads && ValidRoads(seat).Any())
            || (SeafarersOn && CountShips(seat) < MaxShips && ValidShips(seat).Any());

        private int FreePiecesInSupply(int seat) =>
            BuildingPieceLimits.MaxRoads - CountRoads(seat) + (SeafarersOn ? MaxShips - CountShips(seat) : 0);

        /// <summary>Seats the pirate on <paramref name="hex"/> could steal from: ships on its edges (same protections as the robber).</summary>
        public IEnumerable<int> PirateVictims(int seat, HexCoord hex)
        {
            var victims = new SortedSet<int>();
            foreach (var e in ShipRules.HexEdges(hex))
            {
                if (State.Board.Ships.TryGetValue(e, out var owner))
                {
                    int s = MatchSeats.ToSeat(owner);
                    if (s != seat && CanBeRobbed(s))
                        victims.Add(s);
                }
            }

            return victims;
        }

        /// <summary>Sea hexes the pirate may move to now.</summary>
        public IEnumerable<HexCoord> PirateTargets() =>
            State.Board.SeaHexes.Where(h => State.Board.PirateHex != h)
                .OrderBy(h => h.Q).ThenBy(h => h.R).ToList();

        private MatchCommandResult BuildShip(MatchCommand c)
        {
            bool free = State.Phase == MatchPhase.RoadBuilding;
            if (State.Phase != MatchPhase.Main && !free)
                return MatchCommandResult.Rejected("Roll first.");
            if (State.ActiveTrade != null)
                return MatchCommandResult.Rejected("Close the trade offer first.");
            if (!SeafarersOn)
                return MatchCommandResult.Rejected("There is no sea for ships on this map.");
            if (CountShips(c.Seat) >= MaxShips)
                return MatchCommandResult.Rejected("No ships left.");
            var e = NormalizeEdge(c.Edge);
            if (!ShipRules.CanPlaceShip(State.Board, e, State.Current.Owner))
                return MatchCommandResult.Rejected(ShipRules.IsPirateBlocked(State.Board, e)
                    ? "The pirate blocks that edge."
                    : "Invalid ship.");
            if (!free && !TryPay(ShipCostFor(c.Seat)))
                return MatchCommandResult.Rejected("Can't afford a ship.");
            State.Board.Ships[e] = State.Current.Owner;
            State.ShipsBuiltThisTurn.Add(e);
            if (free)
            {
                State.FreeRoadsLeft--;
                if (State.FreeRoadsLeft <= 0 || !AnyFreePieceSpot(c.Seat))
                    FinishRoadBuilding();
            }

            AfterBuild();
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult MoveShip(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Main)
                return MatchCommandResult.Rejected("Roll first.");
            if (State.ActiveTrade != null)
                return MatchCommandResult.Rejected("Close the trade offer first.");
            if (!SeafarersOn)
                return MatchCommandResult.Rejected("There is no sea for ships on this map.");
            if (State.ShipMovedThisTurn)
                return MatchCommandResult.Rejected("You already moved a ship this turn.");
            var owner = State.Current.Owner;
            var from = NormalizeEdge(c.Edge);
            if (!State.Board.Ships.TryGetValue(from, out var o) || o != owner)
                return MatchCommandResult.Rejected("That is not your ship.");
            if (State.ShipsBuiltThisTurn.Contains(from))
                return MatchCommandResult.Rejected("A ship built this turn can't move yet.");
            if (ShipRules.IsPirateBlocked(State.Board, from))
                return MatchCommandResult.Rejected("The pirate blocks that ship.");
            if (!ShipRules.IsOpenShip(State.Board, from, owner))
                return MatchCommandResult.Rejected("Only the open end of a shipping line can move.");
            var to = NormalizeEdge(c.ToEdge);
            if (to.Equals(from) || !ShipRules.CanPlaceShip(State.Board, to, owner, from))
                return MatchCommandResult.Rejected("The ship can't sail there.");

            State.Board.Ships.Remove(from);
            State.Board.Ships[to] = owner;
            State.ShipMovedThisTurn = true;
            AppendLog($"{State.Current.Name} moved a ship.");
            AfterBuild();
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult MovePirate(MatchCommand c)
        {
            if (State.Phase != MatchPhase.MoveRobber)
                return MatchCommandResult.Rejected("No robber or pirate to move.");
            if (!SeafarersOn)
                return MatchCommandResult.Rejected("There is no pirate on this map.");
            var hex = c.Hex;
            if (!State.Board.SeaHexes.Contains(hex))
                return MatchCommandResult.Rejected("The pirate must go on a sea hex.");
            if (State.Board.PirateHex == hex)
                return MatchCommandResult.Rejected("The pirate must move to a different hex.");
            var victims = PirateVictims(c.Seat, hex).ToList();
            if (c.Victim >= 0 && !victims.Contains(c.Victim))
                return MatchCommandResult.Rejected("That player can't be robbed there.");
            if (c.Victim < 0 && victims.Count > 0)
                return MatchCommandResult.Rejected("Choose a player to steal from.");

            State.Board.PirateHex = hex;
            AppendLog($"{State.Current.Name} moved the pirate.");
            if (c.Victim >= 0)
                StealOne(c.Seat, c.Victim, "pirate");

            State.Phase = State.ResumePhase;
            State.ResumePhase = MatchPhase.Main;
            return MatchCommandResult.Accepted;
        }

        /// <summary>v145: a setup settlement must be on the home island when the map names one.</summary>
        private bool IsOnSetupLand(Vertex v)
        {
            var home = State.Board.SetupLandHexes;
            if (home.Count == 0)
                return true;
            bool any = false;
            foreach (var hex in VertexGraph.GetHexesForVertex(v))
            {
                if (!State.Board.Tiles.ContainsKey(hex))
                    continue;
                if (!home.Contains(hex))
                    return false;
                any = true;
            }

            return any;
        }
    }
}

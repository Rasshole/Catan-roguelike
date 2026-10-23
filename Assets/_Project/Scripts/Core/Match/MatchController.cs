using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Victory;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core.Match
{
    /// <summary>
    /// v134 Phase 1: authoritative rules for an N-player (2–5 seats) classic match on the roguelike board.
    /// Every state change goes through <see cref="Apply"/> with a serializable <see cref="MatchCommand"/>; accepted
    /// commands are appended to <see cref="CommandLog"/>. Same config + same log ⇒ same <see cref="MatchState.ComputeHash"/>.
    /// Humans and AIs act the same way: AI seats just get their commands from <see cref="MatchAi"/>.
    /// </summary>
    public sealed class MatchController
    {
        public static readonly ResourceBundle RoadCost = new() { Wood = 1, Brick = 1 };
        public static readonly ResourceBundle SettlementCost = new() { Wood = 1, Brick = 1, Wheat = 1, Sheep = 1 };
        public static readonly ResourceBundle CityCost = new() { Wheat = 2, Stone = 3 };
        public const int BankTradeRatio = 4;
        public const int DiscardLimit = 7;
        public const int LongestRoadMinimum = 5;
        public const int LongestRoadPoints = 2;

        private readonly PlacementValidator _placement = new();
        private readonly List<MatchCommand> _log = new();

        public MatchState State { get; }
        public IReadOnlyList<MatchCommand> CommandLog => _log;
        public PlacementValidator Placement => _placement;

        public event Action<MatchCommand> CommandApplied;

        public MatchController(MatchConfig config)
        {
            config.Validate();
            var board = MapPresets.CreateBoard(config.MapSize, config.Seed);
            State = new MatchState(config, board) { Rng = new MatchRng(config.Seed, 0x5EA7) };
            for (int i = 0; i < config.SeatCount; i++)
            {
                State.Seats.Add(new MatchSeatState
                {
                    Index = i,
                    Kind = config.Seats[i].Kind,
                    Name = string.IsNullOrEmpty(config.Seats[i].Name) ? $"Player {i + 1}" : config.Seats[i].Name,
                    Hand = ResourceBundle.Zero,
                    ExtraTimeCharges = TurnClock.StartingCharges
                });
            }

            // Snake setup order 0..N-1, N-1..0.
            for (int i = 0; i < config.SeatCount; i++) State.SetupOrder.Add(i);
            for (int i = config.SeatCount - 1; i >= 0; i--) State.SetupOrder.Add(i);
            State.Phase = MatchPhase.SetupSettlement;
            State.CurrentSeat = State.SetupOrder[0];
            AppendLog($"{State.Current.Name} places a settlement.");
        }

        /// <summary>Rebuild a match from its config and an ordered command log (replay / reconnect).</summary>
        public static MatchController Replay(MatchConfig config, IEnumerable<MatchCommand> log)
        {
            var controller = new MatchController(config);
            foreach (var command in log)
            {
                var result = controller.Apply(command);
                if (!result.Ok)
                    throw new InvalidOperationException($"Replay diverged at {command}: {result.Error}");
            }

            return controller;
        }

        public bool IsSetup => State.Phase == MatchPhase.SetupSettlement || State.Phase == MatchPhase.SetupRoad;
        public bool IsOver => State.Phase == MatchPhase.GameOver;

        public MatchCommandResult Apply(MatchCommand command)
        {
            if (command == null)
                return MatchCommandResult.Rejected("No command.");
            if (IsOver)
                return MatchCommandResult.Rejected("The match is over.");
            if (command.Seat < 0 || command.Seat >= State.Seats.Count)
                return MatchCommandResult.Rejected("Unknown seat.");
            if (command.Seat != State.CurrentSeat)
                return MatchCommandResult.Rejected($"Not seat {command.Seat}'s turn (seat {State.CurrentSeat} acts).");

            var result = command.Type switch
            {
                MatchCommandType.PlaceSetupSettlement => SetupSettlement(command),
                MatchCommandType.PlaceSetupRoad => SetupRoad(command),
                MatchCommandType.RollDice => Roll(command),
                MatchCommandType.MoveRobber => MoveRobber(command),
                MatchCommandType.BuildRoad => BuildRoad(command),
                MatchCommandType.BuildSettlement => BuildSettlement(command),
                MatchCommandType.BuildCity => BuildCity(command),
                MatchCommandType.BankTrade => BankTrade(command),
                MatchCommandType.UseExtraTime => UseExtraTime(command),
                MatchCommandType.EndTurn => EndTurn(command),
                _ => MatchCommandResult.Rejected("Unknown command.")
            };

            if (result.Ok)
            {
                _log.Add(command);
                CommandApplied?.Invoke(command);
            }

            return result;
        }

        // ---------- queries ----------

        public IEnumerable<Vertex> ValidSetupSettlements() =>
            _placement.GetValidSettlementSpots(State.Board, State.Current.Owner, true);

        public IEnumerable<Edge> ValidSetupRoads()
        {
            if (!State.LastSetupSettlement.HasValue)
                return Enumerable.Empty<Edge>();
            var v = State.LastSetupSettlement.Value;
            return VertexGraph.GetAdjacentVertices(v)
                .Select(a => NormalizeEdge(new Edge(v, a)))
                .Where(e => _placement.CanPlaceRoad(State.Board, e, State.Current.Owner, true));
        }

        // Network-local queries: only vertices/edges touching the seat's own pieces can be valid, so scan those
        // instead of the whole board (same rules as PlacementValidator, much cheaper on 30-37 tile maps).
        public IEnumerable<Vertex> ValidSettlements(int seat)
        {
            var owner = MatchSeats.ToPlayerId(seat);
            var result = new List<Vertex>();
            foreach (var v in NetworkVertices(owner, roadsOnly: true))
                if (_placement.CanPlaceSettlement(State.Board, v, owner, false))
                    result.Add(v);
            return result;
        }

        public IEnumerable<Edge> ValidRoads(int seat)
        {
            var owner = MatchSeats.ToPlayerId(seat);
            var seen = new HashSet<Edge>();
            var result = new List<Edge>();
            foreach (var v in NetworkVertices(owner, roadsOnly: false))
            foreach (var adj in VertexGraph.GetAdjacentVertices(v))
            {
                var e = NormalizeEdge(new Edge(v, adj));
                if (seen.Add(e) && _placement.CanPlaceRoad(State.Board, e, owner, false))
                    result.Add(e);
            }

            return result;
        }

        private List<Vertex> NetworkVertices(PlayerId owner, bool roadsOnly)
        {
            var seen = new HashSet<Vertex>();
            var list = new List<Vertex>();
            foreach (var kv in State.Board.Roads)
            {
                if (kv.Value != owner || State.Board.DisabledRoads.Contains(kv.Key))
                    continue;
                foreach (var end in new[] { kv.Key.A, kv.Key.B })
                {
                    var c = VertexGraph.Canonicalize(end);
                    if (seen.Add(c))
                        list.Add(c);
                }
            }

            if (!roadsOnly)
                foreach (var kv in State.Board.VertexBuildings)
                    if (kv.Value.owner == owner && seen.Add(kv.Key))
                        list.Add(kv.Key);
            return list;
        }

        public IEnumerable<Vertex> UpgradableSettlements(int seat)
        {
            var owner = MatchSeats.ToPlayerId(seat);
            return State.Board.VertexBuildings
                .Where(kv => kv.Value.owner == owner && kv.Value.type == BuildingType.Settlement)
                .Select(kv => kv.Key);
        }

        public IEnumerable<int> RobberVictims(int seat, HexCoord hex)
        {
            var victims = new SortedSet<int>();
            for (int c = 0; c < 6; c++)
            {
                var v = VertexGraph.Canonicalize(new Vertex(hex, c));
                if (State.Board.VertexBuildings.TryGetValue(v, out var b))
                {
                    int s = MatchSeats.ToSeat(b.owner);
                    if (s != seat && State.Seats[s].CardCount > 0)
                        victims.Add(s);
                }
            }

            return victims;
        }

        public int CountRoads(int seat) => State.Board.CountRoads(MatchSeats.ToPlayerId(seat));
        public int CountSettlements(int seat) => State.Board.CountBuildings(MatchSeats.ToPlayerId(seat), BuildingType.Settlement);
        public int CountCities(int seat) => State.Board.CountBuildings(MatchSeats.ToPlayerId(seat), BuildingType.City);

        // ---------- setup ----------

        private MatchCommandResult SetupSettlement(MatchCommand c)
        {
            if (State.Phase != MatchPhase.SetupSettlement)
                return MatchCommandResult.Rejected("Not placing a setup settlement now.");
            var v = VertexGraph.Canonicalize(c.Vertex);
            if (!_placement.CanPlaceSettlement(State.Board, v, State.Current.Owner, true))
                return MatchCommandResult.Rejected("Invalid settlement spot.");

            State.Board.VertexBuildings[v] = (BuildingType.Settlement, State.Current.Owner);
            State.LastSetupSettlement = v;
            bool secondRound = State.SetupStep >= State.Seats.Count;
            if (secondRound)
            {
                var hand = State.Current.Hand;
                foreach (var hex in VertexGraph.GetHexesForVertex(v))
                {
                    if (State.Board.TryGetTile(hex, out var tile) && !tile.IsDesert)
                        hand.Add(tile.Resource, 1);
                }

                State.Current.Hand = hand;
            }

            State.Phase = MatchPhase.SetupRoad;
            RefreshVictoryPoints();
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult SetupRoad(MatchCommand c)
        {
            if (State.Phase != MatchPhase.SetupRoad)
                return MatchCommandResult.Rejected("Not placing a setup road now.");
            var e = NormalizeEdge(c.Edge);
            if (!ValidSetupRoads().Contains(e))
                return MatchCommandResult.Rejected("Setup road must touch the settlement you just placed.");

            State.Board.Roads[e] = State.Current.Owner;
            State.LastSetupSettlement = null;
            State.SetupStep++;
            if (State.SetupStep >= State.SetupOrder.Count)
            {
                State.CurrentSeat = 0;
                State.TurnNumber = 1;
                State.Phase = MatchPhase.Roll;
                AppendLog($"Setup complete. {State.Current.Name} rolls first.");
            }
            else
            {
                State.CurrentSeat = State.SetupOrder[State.SetupStep];
                State.Phase = MatchPhase.SetupSettlement;
            }

            RefreshVictoryPoints();
            return MatchCommandResult.Accepted;
        }

        // ---------- turn ----------

        private MatchCommandResult Roll(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Roll)
                return MatchCommandResult.Rejected("Already rolled.");
            int roll = State.Rng.RollDie() + State.Rng.RollDie();
            State.LastRoll = roll;
            AppendLog($"{State.Current.Name} rolled {roll}.");
            if (roll == 7)
            {
                foreach (var seat in State.Seats)
                    AutoDiscard(seat);
                State.Phase = MatchPhase.MoveRobber;
            }
            else
            {
                Produce(roll);
                State.Phase = MatchPhase.Main;
            }

            return MatchCommandResult.Accepted;
        }

        private void Produce(int roll)
        {
            var gains = new ResourceBundle[State.Seats.Count];
            foreach (var tile in State.Board.Tiles.Values)
            {
                if (tile.IsDesert || tile.NumberToken != roll || tile.HasRobber)
                    continue;
                for (int corner = 0; corner < 6; corner++)
                {
                    var v = VertexGraph.Canonicalize(new Vertex(tile.Coord, corner));
                    if (!State.Board.VertexBuildings.TryGetValue(v, out var b))
                        continue;
                    gains[MatchSeats.ToSeat(b.owner)].Add(tile.Resource, b.type == BuildingType.City ? 2 : 1);
                }
            }

            for (int i = 0; i < gains.Length; i++)
            {
                if (gains[i].Total == 0) continue;
                var hand = State.Seats[i].Hand;
                hand.Add(gains[i]);
                State.Seats[i].Hand = hand;
            }
        }

        /// <summary>Phase 1 policy: over 7 cards, discard half (rounded down) from the largest piles.</summary>
        private void AutoDiscard(MatchSeatState seat)
        {
            int total = seat.CardCount;
            if (total <= DiscardLimit)
                return;
            int toDiscard = total / 2;
            var hand = seat.Hand;
            for (int i = 0; i < toDiscard; i++)
            {
                var largest = AllResources.OrderByDescending(r => hand[r]).ThenBy(r => (int)r).First();
                hand.Add(largest, -1);
            }

            seat.Hand = hand;
            AppendLog($"{seat.Name} discarded {toDiscard}.");
        }

        private MatchCommandResult MoveRobber(MatchCommand c)
        {
            if (State.Phase != MatchPhase.MoveRobber)
                return MatchCommandResult.Rejected("No robber to move.");
            var hex = c.Hex;
            if (!State.Board.TryGetTile(hex, out _))
                return MatchCommandResult.Rejected("Robber must go on a land hex.");
            if (State.Board.RobberTile == hex)
                return MatchCommandResult.Rejected("Robber must move to a different hex.");
            var victims = RobberVictims(c.Seat, hex).ToList();
            if (c.Victim >= 0 && !victims.Contains(c.Victim))
                return MatchCommandResult.Rejected("That player can't be robbed there.");
            if (c.Victim < 0 && victims.Count > 0)
                return MatchCommandResult.Rejected("Choose a player to steal from.");

            State.Board.PlaceRobber(hex);
            if (c.Victim >= 0)
            {
                var victim = State.Seats[c.Victim];
                var vHand = victim.Hand;
                int pick = State.Rng.Next(vHand.Total);
                foreach (var r in AllResources)
                {
                    if (pick < vHand[r])
                    {
                        vHand.Add(r, -1);
                        victim.Hand = vHand;
                        var mine = State.Current.Hand;
                        mine.Add(r, 1);
                        State.Current.Hand = mine;
                        break;
                    }

                    pick -= vHand[r];
                }

                AppendLog($"{State.Current.Name} robbed {victim.Name}.");
            }

            State.Phase = MatchPhase.Main;
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult BuildRoad(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Main)
                return MatchCommandResult.Rejected("Roll first.");
            var e = NormalizeEdge(c.Edge);
            if (CountRoads(c.Seat) >= BuildingPieceLimits.MaxRoads)
                return MatchCommandResult.Rejected("No roads left.");
            if (!_placement.CanPlaceRoad(State.Board, e, State.Current.Owner))
                return MatchCommandResult.Rejected("Invalid road.");
            if (!TryPay(RoadCost))
                return MatchCommandResult.Rejected("Can't afford a road.");
            State.Board.Roads[e] = State.Current.Owner;
            AfterBuild();
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult BuildSettlement(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Main)
                return MatchCommandResult.Rejected("Roll first.");
            var v = VertexGraph.Canonicalize(c.Vertex);
            if (CountSettlements(c.Seat) >= BuildingPieceLimits.MaxSettlements)
                return MatchCommandResult.Rejected("No settlements left.");
            if (!_placement.CanPlaceSettlement(State.Board, v, State.Current.Owner))
                return MatchCommandResult.Rejected("Invalid settlement spot.");
            if (!TryPay(SettlementCost))
                return MatchCommandResult.Rejected("Can't afford a settlement.");
            State.Board.VertexBuildings[v] = (BuildingType.Settlement, State.Current.Owner);
            AppendLog($"{State.Current.Name} built a settlement.");
            AfterBuild();
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult BuildCity(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Main)
                return MatchCommandResult.Rejected("Roll first.");
            var v = VertexGraph.Canonicalize(c.Vertex);
            if (CountCities(c.Seat) >= BuildingPieceLimits.MaxCities)
                return MatchCommandResult.Rejected("No cities left.");
            if (!_placement.CanUpgradeToCity(State.Board, v, State.Current.Owner))
                return MatchCommandResult.Rejected("Upgrade one of your settlements.");
            if (!TryPay(CityCost))
                return MatchCommandResult.Rejected("Can't afford a city.");
            State.Board.VertexBuildings[v] = (BuildingType.City, State.Current.Owner);
            AppendLog($"{State.Current.Name} built a city.");
            AfterBuild();
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult BankTrade(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Main)
                return MatchCommandResult.Rejected("Roll first.");
            if (c.Give == c.Receive)
                return MatchCommandResult.Rejected("Pick two different resources.");
            var hand = State.Current.Hand;
            if (hand[c.Give] < BankTradeRatio)
                return MatchCommandResult.Rejected($"Need {BankTradeRatio} {c.Give}.");
            hand.Add(c.Give, -BankTradeRatio);
            hand.Add(c.Receive, 1);
            State.Current.Hand = hand;
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult UseExtraTime(MatchCommand c)
        {
            if (IsSetup || State.Phase == MatchPhase.GameOver)
                return MatchCommandResult.Rejected("Extra time is for normal turns.");
            var seat = State.Current;
            if (seat.ExtraTimeUsedThisTurn)
                return MatchCommandResult.Rejected("Already used extra time this turn.");
            if (seat.ExtraTimeCharges <= 0)
                return MatchCommandResult.Rejected("No extra-time charges.");
            seat.ExtraTimeCharges--;
            seat.ExtraTimeUsedThisTurn = true;
            AppendLog($"{seat.Name} used extra time (+{(int)TurnClock.ExtraTimeSeconds} s).");
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult EndTurn(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Main)
                return MatchCommandResult.Rejected(State.Phase == MatchPhase.Roll ? "Roll first." : "Finish the robber first.");
            State.Current.ExtraTimeUsedThisTurn = false;
            State.CurrentSeat = (State.CurrentSeat + 1) % State.Seats.Count;
            State.TurnNumber++;
            if (State.CurrentSeat == 0)
            {
                State.Round++;
                if (State.Round % TurnClock.RoundsPerNewCharge == 0)
                {
                    foreach (var seat in State.Seats)
                        seat.ExtraTimeCharges++;
                    AppendLog($"Round {State.Round}: everyone gains an extra-time charge.");
                }
            }

            State.Phase = MatchPhase.Roll;
            return MatchCommandResult.Accepted;
        }

        // ---------- helpers ----------

        public static readonly ResourceType[] AllResources =
        {
            ResourceType.Wood, ResourceType.Brick, ResourceType.Wheat, ResourceType.Sheep, ResourceType.Stone
        };

        private bool TryPay(ResourceBundle cost)
        {
            var hand = State.Current.Hand;
            if (!hand.CanAfford(cost))
                return false;
            hand.Pay(cost);
            State.Current.Hand = hand;
            return true;
        }

        private void AfterBuild()
        {
            RefreshVictoryPoints();
            if (State.Current.VictoryPoints >= State.Config.VictoryPoints)
            {
                State.Winner = State.CurrentSeat;
                State.Phase = MatchPhase.GameOver;
                AppendLog($"{State.Current.Name} wins with {State.Current.VictoryPoints} VP!");
            }
        }

        private void RefreshVictoryPoints()
        {
            // Longest road: ≥5 and strictly longer than everyone else; the holder keeps it on ties.
            var lengths = State.Seats.Select(s => RouteCalculator.LongestRoadLength(State.Board, s.Owner)).ToArray();
            int? holder = State.LongestRoadSeat;
            int best = lengths.Max();
            if (holder.HasValue && lengths[holder.Value] < LongestRoadMinimum)
                holder = null;
            if (best >= LongestRoadMinimum)
            {
                var leaders = Enumerable.Range(0, lengths.Length).Where(i => lengths[i] == best).ToList();
                if (holder.HasValue && lengths[holder.Value] == best) { }
                else if (leaders.Count == 1) holder = leaders[0];
                else if (holder.HasValue && lengths[holder.Value] < best) holder = null;
            }

            if (holder != State.LongestRoadSeat && holder.HasValue)
                AppendLog($"{State.Seats[holder.Value].Name} takes Longest Road.");
            State.LongestRoadSeat = holder;

            foreach (var seat in State.Seats)
            {
                int vp = State.Board.CountBuildings(seat.Owner, BuildingType.Settlement)
                         + 2 * State.Board.CountBuildings(seat.Owner, BuildingType.City);
                if (State.LongestRoadSeat == seat.Index)
                    vp += LongestRoadPoints;
                seat.VictoryPoints = vp;
            }
        }

        private void AppendLog(string line)
        {
            State.Log.Add(line);
            if (State.Log.Count > 200)
                State.Log.RemoveAt(0);
        }

        public static Edge NormalizeEdge(Edge edge) =>
            new(VertexGraph.Canonicalize(edge.A), VertexGraph.Canonicalize(edge.B));
    }
}

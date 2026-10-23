using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
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
        public static readonly ResourceBundle DevCardCost = new() { Wheat = 1, Sheep = 1, Stone = 1 };
        public const int LargestArmyMinimum = 3;
        public const int LargestArmyPoints = 2;
        public const int RoadBuildingRoads = 2;

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

            // v135: harbours (same discovery as the single-player board, so the 3D view and the rules agree).
            State.Ports.AddRange(PortAccess.DiscoverPorts(board));
            // v135: development deck — classic 25 (14 K, 5 VP, 2 RB, 2 YoP, 2 Mono); 5–6 player extension 34.
            bool big = config.SeatCount >= 5;
            AddCards(MatchDevCard.Knight, big ? 20 : 14);
            AddCards(MatchDevCard.VictoryPoint, 5);
            AddCards(MatchDevCard.RoadBuilding, big ? 3 : 2);
            AddCards(MatchDevCard.YearOfPlenty, big ? 3 : 2);
            AddCards(MatchDevCard.Monopoly, big ? 3 : 2);
            for (int i = State.DevDeck.Count - 1; i > 0; i--)
            {
                int j = State.Rng.Next(i + 1);
                (State.DevDeck[i], State.DevDeck[j]) = (State.DevDeck[j], State.DevDeck[i]);
            }

            // Snake setup order 0..N-1, N-1..0.
            for (int i = 0; i < config.SeatCount; i++) State.SetupOrder.Add(i);
            for (int i = config.SeatCount - 1; i >= 0; i--) State.SetupOrder.Add(i);
            State.Phase = MatchPhase.SetupSettlement;
            State.CurrentSeat = State.SetupOrder[0];
            AppendLog($"{State.Current.Name} places a settlement.");
        }

        private void AddCards(MatchDevCard card, int count)
        {
            for (int i = 0; i < count; i++)
                State.DevDeck.Add(card);
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
            if (command.Type == MatchCommandType.RespondTrade)
            {
                // Any other seat may answer the open offer.
                if (State.ActiveTrade == null)
                    return MatchCommandResult.Rejected("No open trade offer.");
                if (command.Seat == State.ActiveTrade.From)
                    return MatchCommandResult.Rejected("You can't answer your own offer.");
            }
            else if (command.Seat != State.ActingSeat)
                return MatchCommandResult.Rejected($"Not seat {command.Seat}'s turn (seat {State.ActingSeat} acts).");

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
                MatchCommandType.BuyDevCard => BuyDevCard(command),
                MatchCommandType.PlayKnight => PlayDevCard(command, MatchDevCard.Knight),
                MatchCommandType.PlayRoadBuilding => PlayDevCard(command, MatchDevCard.RoadBuilding),
                MatchCommandType.PlayYearOfPlenty => PlayDevCard(command, MatchDevCard.YearOfPlenty),
                MatchCommandType.PlayMonopoly => PlayDevCard(command, MatchDevCard.Monopoly),
                MatchCommandType.Discard => Discard(command),
                MatchCommandType.ProposeTrade => ProposeTrade(command),
                MatchCommandType.RespondTrade => RespondTrade(command),
                MatchCommandType.ConfirmTrade => ConfirmTrade(command),
                MatchCommandType.CancelTrade => CancelTrade(command),
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

        /// <summary>v135: bank rate for giving <paramref name="give"/>: 2 with that resource's harbour, 3 with a generic one, else 4.</summary>
        public int BankRatio(int seat, ResourceType give)
        {
            var owner = MatchSeats.ToPlayerId(seat);
            int best = BankTradeRatio;
            foreach (var port in State.Ports)
            {
                if (!PortAccess.PlayerControlsVertex(State.Board, owner, port.Vertex))
                    continue;
                if (port.SpecificResource == give)
                    return 2;
                if (port.IsGeneric)
                    best = Math.Min(best, 3);
            }

            return best;
        }

        /// <summary>Whether <paramref name="seat"/> may play <paramref name="card"/> now (owned since before this turn, one per turn).</summary>
        public bool CanPlayDevCard(int seat, MatchDevCard card)
        {
            if (card == MatchDevCard.VictoryPoint || seat != State.CurrentSeat)
                return false;
            if (State.Phase != MatchPhase.Main && State.Phase != MatchPhase.Roll)
                return false;
            var s = State.Seats[seat];
            return !s.PlayedDevCardThisTurn && s.DevCards.Contains(card) && State.ActiveTrade == null;
        }

        public bool CanBuyDevCard(int seat) =>
            seat == State.CurrentSeat && State.Phase == MatchPhase.Main && State.DevDeck.Count > 0
            && State.Seats[seat].Hand.CanAfford(DevCardCost);

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
                State.PendingDiscards.Clear();
                foreach (var seat in State.Seats)
                    if (seat.CardCount > DiscardLimit)
                        State.PendingDiscards[seat.Index] = seat.CardCount / 2;
                State.ResumePhase = MatchPhase.Main;
                State.Phase = State.PendingDiscards.Count > 0 ? MatchPhase.Discard : MatchPhase.MoveRobber;
                if (State.PendingDiscards.Count > 0)
                    AppendLog("Seven! " + string.Join(", ",
                        State.PendingDiscards.Select(kv => $"{State.Seats[kv.Key].Name} discards {kv.Value}")) + ".");
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

        /// <summary>v135: a seat that owes a discard (hand over 7 on a 7) picks exactly half its cards.</summary>
        private MatchCommandResult Discard(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Discard || !State.PendingDiscards.TryGetValue(c.Seat, out int owed))
                return MatchCommandResult.Rejected("Nothing to discard.");
            if (!IsValidBundle(c.Offer) || c.Offer.Total != owed)
                return MatchCommandResult.Rejected($"Discard exactly {owed} cards.");
            var seat = State.Seats[c.Seat];
            var hand = seat.Hand;
            if (!hand.CanAfford(c.Offer))
                return MatchCommandResult.Rejected("You don't have those cards.");
            hand.Pay(c.Offer);
            seat.Hand = hand;
            State.PendingDiscards.Remove(c.Seat);
            AppendLog($"{seat.Name} discarded {owed}.");
            if (State.PendingDiscards.Count == 0)
                State.Phase = MatchPhase.MoveRobber;
            return MatchCommandResult.Accepted;
        }

        /// <summary>Largest-piles-first discard of <paramref name="count"/> cards (AI / timeout policy).</summary>
        public static ResourceBundle SuggestDiscard(ResourceBundle hand, int count)
        {
            var discard = ResourceBundle.Zero;
            for (int i = 0; i < count; i++)
            {
                var largest = AllResources.OrderByDescending(r => hand[r] - discard[r]).ThenBy(r => (int)r).First();
                discard.Add(largest, 1);
            }

            return discard;
        }

        private static bool IsValidBundle(ResourceBundle b) =>
            b.Wood >= 0 && b.Brick >= 0 && b.Wheat >= 0 && b.Sheep >= 0 && b.Stone >= 0;

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

            State.Phase = State.ResumePhase;
            State.ResumePhase = MatchPhase.Main;
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult BuildRoad(MatchCommand c)
        {
            bool free = State.Phase == MatchPhase.RoadBuilding;
            if (State.Phase != MatchPhase.Main && !free)
                return MatchCommandResult.Rejected("Roll first.");
            if (State.ActiveTrade != null)
                return MatchCommandResult.Rejected("Close the trade offer first.");
            var e = NormalizeEdge(c.Edge);
            if (CountRoads(c.Seat) >= BuildingPieceLimits.MaxRoads)
                return MatchCommandResult.Rejected("No roads left.");
            if (!_placement.CanPlaceRoad(State.Board, e, State.Current.Owner))
                return MatchCommandResult.Rejected("Invalid road.");
            if (!free && !TryPay(RoadCost))
                return MatchCommandResult.Rejected("Can't afford a road.");
            State.Board.Roads[e] = State.Current.Owner;
            if (free)
            {
                State.FreeRoadsLeft--;
                if (State.FreeRoadsLeft <= 0 || !ValidRoads(c.Seat).Any()
                                             || CountRoads(c.Seat) >= BuildingPieceLimits.MaxRoads)
                    FinishRoadBuilding();
            }

            AfterBuild();
            return MatchCommandResult.Accepted;
        }

        private void FinishRoadBuilding()
        {
            State.FreeRoadsLeft = 0;
            State.Phase = State.ResumePhase;
            State.ResumePhase = MatchPhase.Main;
        }

        // ---------- v135: development cards ----------

        private MatchCommandResult BuyDevCard(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Main)
                return MatchCommandResult.Rejected("Roll first.");
            if (State.ActiveTrade != null)
                return MatchCommandResult.Rejected("Close the trade offer first.");
            if (State.DevDeck.Count == 0)
                return MatchCommandResult.Rejected("The development deck is empty.");
            if (!TryPay(DevCardCost))
                return MatchCommandResult.Rejected("Can't afford a development card.");
            var card = State.DevDeck[State.DevDeck.Count - 1];
            State.DevDeck.RemoveAt(State.DevDeck.Count - 1);
            State.Current.NewDevCards.Add(card);
            AppendLog($"{State.Current.Name} bought a development card.");
            AfterBuild();
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult PlayDevCard(MatchCommand c, MatchDevCard card)
        {
            if (!CanPlayDevCard(c.Seat, card))
            {
                if (State.Current.PlayedDevCardThisTurn)
                    return MatchCommandResult.Rejected("Only one development card per turn.");
                return MatchCommandResult.Rejected(State.Current.NewDevCards.Contains(card) && !State.Current.DevCards.Contains(card)
                    ? "Cards bought this turn can be played next turn."
                    : $"You can't play {card} now.");
            }

            var seat = State.Current;
            var resume = State.Phase;
            switch (card)
            {
                case MatchDevCard.Knight:
                    seat.KnightsPlayed++;
                    State.ResumePhase = resume;
                    State.Phase = MatchPhase.MoveRobber;
                    AppendLog($"{seat.Name} played a Knight.");
                    break;
                case MatchDevCard.RoadBuilding:
                {
                    if (!ValidRoads(c.Seat).Any() || CountRoads(c.Seat) >= BuildingPieceLimits.MaxRoads)
                        return MatchCommandResult.Rejected("No room for a road.");
                    State.ResumePhase = resume;
                    State.FreeRoadsLeft = Math.Min(RoadBuildingRoads, BuildingPieceLimits.MaxRoads - CountRoads(c.Seat));
                    State.Phase = MatchPhase.RoadBuilding;
                    AppendLog($"{seat.Name} played Road Building.");
                    break;
                }
                case MatchDevCard.YearOfPlenty:
                {
                    var hand = seat.Hand;
                    hand.Add(c.Give, 1);
                    hand.Add(c.Receive, 1);
                    seat.Hand = hand;
                    AppendLog($"{seat.Name} played Year of Plenty.");
                    break;
                }
                case MatchDevCard.Monopoly:
                {
                    int taken = 0;
                    foreach (var other in State.Seats)
                    {
                        if (other.Index == seat.Index || other.Hand[c.Receive] == 0)
                            continue;
                        var oh = other.Hand;
                        taken += oh[c.Receive];
                        oh.Set(c.Receive, 0);
                        other.Hand = oh;
                    }

                    var mine = seat.Hand;
                    mine.Add(c.Receive, taken);
                    seat.Hand = mine;
                    AppendLog($"{seat.Name} played Monopoly on {c.Receive} (+{taken}).");
                    break;
                }
            }

            seat.DevCards.Remove(card);
            seat.PlayedDevCardThisTurn = true;
            AfterBuild();
            return MatchCommandResult.Accepted;
        }

        // ---------- v135: player trading (offer / counter-offer board) ----------

        private MatchCommandResult ProposeTrade(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Main)
                return MatchCommandResult.Rejected("Roll first.");
            if (!IsValidBundle(c.Offer) || !IsValidBundle(c.Request) || c.Offer.Total == 0 || c.Request.Total == 0)
                return MatchCommandResult.Rejected("Offer at least one card and ask for at least one.");
            if (AllResources.Any(r => c.Offer[r] > 0 && c.Request[r] > 0))
                return MatchCommandResult.Rejected("Don't offer and ask for the same resource.");
            if (!State.Current.Hand.CanAfford(c.Offer))
                return MatchCommandResult.Rejected("You don't have the cards you offer.");
            State.ActiveTrade = new MatchTradeOffer { From = c.Seat, Give = c.Offer, Get = c.Request };
            for (int i = 0; i < State.Seats.Count; i++)
                State.ActiveTrade.Responses.Add(new MatchTradeResponse
                {
                    Kind = i == c.Seat ? TradeResponseKind.Declined : TradeResponseKind.Pending
                });
            AppendLog($"{State.Current.Name} offers a trade.");
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult RespondTrade(MatchCommand c)
        {
            var trade = State.ActiveTrade;
            var responder = State.Seats[c.Seat];
            var response = trade.Responses[c.Seat];
            switch (c.Response)
            {
                case TradeResponseKind.Accepted:
                    if (!responder.Hand.CanAfford(trade.Get))
                        return MatchCommandResult.Rejected("You don't have the cards they ask for.");
                    response.Kind = TradeResponseKind.Accepted;
                    break;
                case TradeResponseKind.Declined:
                    response.Kind = TradeResponseKind.Declined;
                    break;
                case TradeResponseKind.Countered:
                    if (!IsValidBundle(c.Offer) || !IsValidBundle(c.Request) || c.Offer.Total == 0 || c.Request.Total == 0)
                        return MatchCommandResult.Rejected("A counter needs cards on both sides.");
                    if (!responder.Hand.CanAfford(c.Request))
                        return MatchCommandResult.Rejected("You don't have the cards you counter with.");
                    response.Kind = TradeResponseKind.Countered;
                    response.CounterGive = c.Offer;
                    response.CounterGet = c.Request;
                    break;
                default:
                    return MatchCommandResult.Rejected("Accept, decline or counter.");
            }

            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult ConfirmTrade(MatchCommand c)
        {
            var trade = State.ActiveTrade;
            if (trade == null || trade.From != c.Seat)
                return MatchCommandResult.Rejected("No offer of yours is open.");
            if (c.Victim < 0 || c.Victim >= State.Seats.Count || c.Victim == c.Seat)
                return MatchCommandResult.Rejected("Pick a trade partner.");
            var response = trade.Responses[c.Victim];
            ResourceBundle give, get;
            if (response.Kind == TradeResponseKind.Accepted)
            {
                give = trade.Give;
                get = trade.Get;
            }
            else if (response.Kind == TradeResponseKind.Countered)
            {
                give = response.CounterGive;
                get = response.CounterGet;
            }
            else
                return MatchCommandResult.Rejected("That player hasn't accepted.");

            var me = State.Current;
            var partner = State.Seats[c.Victim];
            if (!me.Hand.CanAfford(give) || !partner.Hand.CanAfford(get))
                return MatchCommandResult.Rejected("Someone no longer has the cards.");
            var mh = me.Hand;
            mh.Pay(give);
            mh.Add(get);
            me.Hand = mh;
            var ph = partner.Hand;
            ph.Pay(get);
            ph.Add(give);
            partner.Hand = ph;
            State.ActiveTrade = null;
            AppendLog($"{me.Name} traded with {partner.Name}.");
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult CancelTrade(MatchCommand c)
        {
            if (State.ActiveTrade == null || State.ActiveTrade.From != c.Seat)
                return MatchCommandResult.Rejected("No offer of yours is open.");
            State.ActiveTrade = null;
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
            if (State.ActiveTrade != null)
                return MatchCommandResult.Rejected("Close the trade offer first.");
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
            if (State.ActiveTrade != null)
                return MatchCommandResult.Rejected("Close the trade offer first.");
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
            int ratio = BankRatio(c.Seat, c.Give);
            if (hand[c.Give] < ratio)
                return MatchCommandResult.Rejected($"Need {ratio} {c.Give}.");
            hand.Add(c.Give, -ratio);
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
            // v135: cards bought this turn become playable; any open offer lapses.
            State.Current.DevCards.AddRange(State.Current.NewDevCards);
            State.Current.NewDevCards.Clear();
            State.Current.PlayedDevCardThisTurn = false;
            State.ActiveTrade = null;
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

        /// <summary>Recompute longest road, largest army and VP (for edited states, e.g. tests and tooling).</summary>
        public void RecomputeScores() => RefreshVictoryPoints();

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

            // v135 Largest Army: ≥3 knights and strictly more than the holder.
            int? army = State.LargestArmySeat;
            foreach (var seat in State.Seats)
            {
                if (seat.KnightsPlayed < LargestArmyMinimum)
                    continue;
                if (!army.HasValue || seat.KnightsPlayed > State.Seats[army.Value].KnightsPlayed)
                    army = seat.Index;
            }

            if (army != State.LargestArmySeat && army.HasValue)
                AppendLog($"{State.Seats[army.Value].Name} takes Largest Army.");
            State.LargestArmySeat = army;

            foreach (var seat in State.Seats)
            {
                int vp = State.Board.CountBuildings(seat.Owner, BuildingType.Settlement)
                         + 2 * State.Board.CountBuildings(seat.Owner, BuildingType.City);
                if (State.LongestRoadSeat == seat.Index)
                    vp += LongestRoadPoints;
                if (State.LargestArmySeat == seat.Index)
                    vp += LargestArmyPoints;
                vp += seat.HiddenVictoryPoints;
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

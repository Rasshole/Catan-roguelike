using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Victory;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

using CatanRoguelike.Core.Trade;

namespace CatanRoguelike.Core.Match
{
    /// <summary>
    /// v134 Phase 1: authoritative rules for an N-player (2–5 seats) classic match on the roguelike board.
    /// Every state change goes through <see cref="Apply"/> with a serializable <see cref="MatchCommand"/>; accepted
    /// commands are appended to <see cref="CommandLog"/>. Same config + same log ⇒ same <see cref="MatchState.ComputeHash"/>.
    /// Humans and AIs act the same way: AI seats just get their commands from <see cref="MatchAi"/>.
    /// </summary>
    public sealed partial class MatchController
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
            // v137: every match shuffles its terrain per seed, like dealing the tiles at a real table.
            var board = MapPresets.CreateBoard(config.MapSize, config.Seed, new MapGenerationOptions(fishermenOfCatan: config.Fishermen, shuffleTerrain: true));
            State = new MatchState(config, board)
            {
                Rng = new MatchRng(config.Seed, 0x5EA7), PartyRng = new MatchRng(config.Seed, 0x9A27)
            };
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
            if (config.PartyRules)
                BeginCivPick();
            else
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
        /// <summary>v136: before the first roll (civ pick, placement, quest pick).</summary>
        public bool IsPregame => IsSetup || State.IsPicking;
        public bool PartyRules => State.Config.PartyRules;
        /// <summary>v143 setup screen: party event cards (one per round) on.</summary>
        public bool EventsOn => PartyRules && State.Config.EventCards;
        /// <summary>v143 setup screen: sabotage, bounties, saboteur guesses (and the Decree/Night Market/Chaos events).</summary>
        public bool ShenanigansOn => PartyRules && State.Config.Shenanigans;
        public bool IsOver => State.Phase == MatchPhase.GameOver;

        private static MatchCommandResult NoShenanigans() =>
            MatchCommandResult.Rejected("Shenanigans are switched off for this match.");

        public MatchCommandResult Apply(MatchCommand command)
        {
            if (command == null)
                return MatchCommandResult.Rejected("No command.");
            if (IsOver)
                return MatchCommandResult.Rejected("The match is over.");
            if (command.Seat < 0 || command.Seat >= State.Seats.Count)
                return MatchCommandResult.Rejected("Unknown seat.");
            if (State.HarbourPickSeat >= 0 && command.Type != MatchCommandType.ChooseHarbourPilotResource
                && command.Type != MatchCommandType.UseExtraTime)
                return MatchCommandResult.Rejected($"{State.Seats[State.HarbourPickSeat].Name} picks a Harbour Pilot resource first.");
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
                MatchCommandType.BankTradeBatch => BankTradeBatch(command),
                MatchCommandType.FishForCard => FishForCard(command),
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
                MatchCommandType.PickCivilization => PickCivilization(command),
                MatchCommandType.PickQuest => PickQuest(command),
                MatchCommandType.CastVote => CastVote(command),
                MatchCommandType.PlaceBid => PlaceBid(command),
                MatchCommandType.PostBounty => ShenanigansOn ? PostBounty(command) : NoShenanigans(),
                MatchCommandType.BuySabotage => ShenanigansOn ? BuySabotage(command) : NoShenanigans(),
                MatchCommandType.PlaySabotage => ShenanigansOn ? PlaySabotage(command) : NoShenanigans(),
                MatchCommandType.GuessSaboteur => ShenanigansOn ? GuessSaboteur(command) : NoShenanigans(),
                MatchCommandType.RerollToken => RerollToken(command),
                MatchCommandType.ChooseHarbourPilotResource => ChooseHarbourPilotResource(command),
                _ => MatchCommandResult.Rejected("Unknown command.")
            };

            if (result.Ok)
            {
                if (PartyRules)
                    AfterPartyCommand();
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

        /// <summary>v142: Sun Kingdom hands this small can't be robbed.</summary>
        public const int SunKingdomSafeHand = 4;

        /// <summary>v144 Head Start: until the holder's first normal turn ends, no discard on a 7 and no card can be stolen.</summary>
        public bool IsHeadStartProtected(int seat) =>
            PartyRules && State.Seats[seat].Trait == MatchTraitId.HeadStart && !State.Seats[seat].HasTakenFirstTurn;

        public IEnumerable<int> RobberVictims(int seat, HexCoord hex)
        {
            var victims = new SortedSet<int>();
            for (int c = 0; c < 6; c++)
            {
                var v = VertexGraph.Canonicalize(new Vertex(hex, c));
                if (State.Board.VertexBuildings.TryGetValue(v, out var b))
                {
                    int s = MatchSeats.ToSeat(b.owner);
                    // v142: Sun Kingdom can't be robbed under party rules.
                    if (s != seat && State.Seats[s].CardCount > 0 && !IsHeadStartProtected(s)
                        && !(PartyRules && State.Seats[s].Civilization == CivilizationId.SunKingdom
                             && State.Seats[s].CardCount <= SunKingdomSafeHand))
                        victims.Add(s);
                }
            }

            return victims;
        }

        /// <summary>v135: bank rate for giving <paramref name="give"/>: 2 with that resource's harbour, 3 with a generic one, else 4.</summary>
        public int BankRatio(int seat, ResourceType give)
        {
            var owner = MatchSeats.ToPlayerId(seat);
            var s = State.Seats[seat];
            int best = BankTradeRatio;
            // v136 party rules: civ base rate, Market Day, Sea Folk harbours, Harbour Pilot.
            if (s.Civilization == CivilizationId.MerchantRepublic) best = s.BankTradesThisTurn == 0 ? 2 : 3;
            if (s.Civilization == CivilizationId.SeaFolk) best = 3;
            if (State.ActiveEvent == EventId.MarketDay) best = Math.Min(best, 3);
            int generic = s.Civilization == CivilizationId.SeaFolk ? 2 : 3;
            // v142 Harbour Tariff decree: the harbours are closed today.
            bool closed = State.ActiveDecree == MatchDecreeId.PortsThreeToOne;
            foreach (var port in State.Ports)
            {
                if (closed)
                    break;
                if (!PortAccess.PlayerControlsPort(State.Board, owner, port))
                    continue;
                if (port.SpecificResource == give || (port.SpecificResource != null && s.Civilization == CivilizationId.SeaFolk))
                    best = Math.Min(best, 2);
                if (port.IsGeneric)
                    best = Math.Min(best, generic);
            }

            if (s.Trait == MatchTraitId.HarbourPilot)
                best = s.BankTradesThisTurn == 0 ? 2 : Math.Max(2, best - 1);
            return best;
        }

        // v136: per-seat build costs (civilization + trait modifiers; the classic costs when party rules are off).
        public ResourceBundle RoadCostFor(int seat)
        {
            var c = MatchCivRules.RoadCost(State.Seats[seat].Civilization, State.Seats[seat].Trait);
            if (IsNextRoadCrewPieceFree(seat))
                c = default;
            return c;
        }

        /// <summary>v144 Road Crew: every third road you build is free (setup roads count).</summary>
        public const int RoadCrewFreeEvery = 3;

        /// <summary>
        /// v144: pieces that advance the Road Crew counter. Roads only today; a future ship piece adds its count here
        /// so roads and ships share one "every third" counter.
        /// </summary>
        public int RoadCrewPiecesBuilt(int seat) => CountRoads(seat);

        /// <summary>v144 Road Crew: true when the next road (or future ship) this seat builds is free.</summary>
        public bool IsNextRoadCrewPieceFree(int seat) =>
            State.Seats[seat].Trait == MatchTraitId.RoadCrew
            && (RoadCrewPiecesBuilt(seat) + 1) % RoadCrewFreeEvery == 0;
        public ResourceBundle SettlementCostFor(int seat) =>
            MatchCivRules.SettlementCost(State.Seats[seat].Civilization, State.Seats[seat].Trait);
        public ResourceBundle CityCostFor(int seat) =>
            MatchCivRules.CityCost(State.Seats[seat].Civilization, State.Seats[seat].Trait);
        public ResourceBundle DevCardCostFor(int seat) =>
            MatchCivRules.DevCardCost(State.Seats[seat].Civilization, State.Seats[seat].Trait);

        /// <summary>v136: hand size above which a 7 forces a discard (Granary raises it).</summary>
        public int DiscardLimitFor(int seat) => DiscardLimit + (State.Seats[seat].Trait == MatchTraitId.Granary ? 1 : 0);

        /// <summary>v136: cards owed on a 7 (0 = none).</summary>
        public int DiscardOwed(int seat)
        {
            var s = State.Seats[seat];
            if (s.CardCount <= DiscardLimitFor(seat) || IsHeadStartProtected(seat))
                return 0;
            int owed = s.CardCount / 2;
            // v142: Tough Hide caps the loss at 2 cards.
            return s.Trait == MatchTraitId.ToughHide ? 0 : owed;
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
            && State.Seats[seat].Hand.CanAfford(DevCardCostFor(seat));

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
            OweHarbourPilotPick(State.CurrentSeat, v);
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
            if (State.SetupStep >= State.SetupOrder.Count && PartyRules)
            {
                BeginQuestPick();
            }
            else if (State.SetupStep >= State.SetupOrder.Count)
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
            if (ShenanigansOn)
                OnDawnSabotage();
            int dieA = State.Rng.RollDie();
            int dieB = State.Rng.RollDie();
            int roll = dieA + dieB;
            State.LastRoll = roll;
            State.LastDieA = dieA; // v143: the two faces, for the dice animation (presentation only, not hashed)
            State.LastDieB = dieB;
            AppendLog($"{State.Current.Name} rolled {roll}.");
            if (roll == 7)
            {
                State.SevensRolled++;
                State.PendingDiscards.Clear();
                if (PartyRules)
                    OnSevenParty();
                foreach (var seat in State.Seats)
                {
                    int owed = DiscardOwed(seat.Index);
                    if (owed > 0)
                        State.PendingDiscards[seat.Index] = owed;
                }
                State.ResumePhase = MatchPhase.Main;
                State.Phase = State.PendingDiscards.Count > 0 ? MatchPhase.Discard : MatchPhase.MoveRobber;
                if (State.PendingDiscards.Count > 0)
                    AppendLog("Seven! " + string.Join(", ",
                        State.PendingDiscards.Select(kv => $"{State.Seats[kv.Key].Name} discards {kv.Value}")) + ".");
            }
            else
            {
                var gained = Produce(roll);
                if (PartyRules)
                    OnRollParty(roll, gained);
                State.Phase = MatchPhase.Main;
            }

            if (ShenanigansOn)
                AfterRollSabotage();

            return MatchCommandResult.Accepted;
        }

        private ResourceBundle[] Produce(int roll)
        {
            var gains = new ResourceBundle[State.Seats.Count];
            foreach (var tile in State.Board.Tiles.Values)
            {
                if (tile.IsDesert || tile.NumberToken != roll)
                    continue;
                // v136 events: Storm silences one hex, Famine all grain; Gold Rush / Good Harvest double ore / grain.
                if (State.ActiveEvent == EventId.Storm && State.EventTile == tile.Coord)
                    continue;
                if (State.ActiveEvent == EventId.Famine && tile.Resource == ResourceType.Wheat)
                    continue;
                int factor = State.ActiveEvent == EventId.GoldRush && tile.Resource == ResourceType.Stone
                             || State.ActiveEvent == EventId.GoodHarvest && tile.Resource == ResourceType.Wheat ? 2 : 1;
                for (int corner = 0; corner < 6; corner++)
                {
                    var v = VertexGraph.Canonicalize(new Vertex(tile.Coord, corner));
                    if (!State.Board.VertexBuildings.TryGetValue(v, out var b))
                        continue;
                    int seat = MatchSeats.ToSeat(b.owner);
                    var civ = State.Seats[seat].Civilization;
                    if (tile.HasRobber && civ != CivilizationId.SunKingdom)
                        continue;
                    // v141 Blight: this hex produces nothing for the target on this roll.
                    if (State.ActiveBlight != null && State.ActiveBlight.Target == seat && State.ActiveBlight.Hex == tile.Coord)
                        continue;
                    int amount = MatchCivRules.HitAmount(civ, tile.Resource, b.type == BuildingType.City ? 2 : 1) * factor;
                    gains[seat].Add(tile.Resource, amount);
                }
            }

            if (State.Config.Fishermen)
                ProduceFish(roll);

            for (int i = 0; i < gains.Length; i++)
            {
                if (gains[i].Total == 0) continue;
                var hand = State.Seats[i].Hand;
                hand.Add(gains[i]);
                State.Seats[i].Hand = hand;
            }

            return gains;
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
            if (State.ActiveDecree == MatchDecreeId.NoDesertRobber && State.Board.TryGetTile(hex, out var dt) && dt.IsDesert)
                return MatchCommandResult.Rejected("Today's decree: the robber can't enter a desert.");
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
                if (PartyRules)
                    ClaimBounties(c.Seat, c.Victim);
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
            if (!free && !TryPay(RoadCostFor(c.Seat)))
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
            if (!TryPay(DevCardCostFor(c.Seat)))
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
                    if (PartyRules && seat.Trait == MatchTraitId.Quartermaster)
                        Give(seat, ResourceType.Wheat, 1);
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
            if (!IsValidBundle(c.Offer) || !IsValidBundle(c.Request))
                return MatchCommandResult.Rejected("Invalid cards.");
            if (c.GiveAny < 0 || c.GetAny < 0 || c.GiveAny > TradeWildcards.MaxWildcards || c.GetAny > TradeWildcards.MaxWildcards)
                return MatchCommandResult.Rejected("Invalid wildcard count.");
            if (c.GiveAny > 0 && c.GetAny > 0)
                return MatchCommandResult.Rejected("Wildcards go on one side of a trade only.");
            if (c.Offer.Total + c.GiveAny == 0 || c.Request.Total + c.GetAny == 0)
                return MatchCommandResult.Rejected("Offer at least one card and ask for at least one.");
            if (AllResources.Any(r => c.Offer[r] > 0 && c.Request[r] > 0))
                return MatchCommandResult.Rejected("Don't offer and ask for the same resource.");
            if (!State.Current.Hand.CanAfford(c.Offer))
                return MatchCommandResult.Rejected("You don't have the cards you offer.");
            if (c.GiveAny > 0 && OffererWildPool(State.Current.Hand, c.Offer, c.Request).Total < c.GiveAny)
                return MatchCommandResult.Rejected("Not enough other cards to back the wildcard.");
            State.ActiveTrade = new MatchTradeOffer
            {
                From = c.Seat, Give = c.Offer, Get = c.Request, GiveAny = c.GiveAny, GetAny = c.GetAny
            };
            for (int i = 0; i < State.Seats.Count; i++)
                State.ActiveTrade.Responses.Add(new MatchTradeResponse
                {
                    Kind = i == c.Seat ? TradeResponseKind.Declined : TradeResponseKind.Pending
                });
            AppendLog($"{State.Current.Name} offers a trade.");
            return MatchCommandResult.Accepted;
        }

        /// <summary>
        /// v139: what an offerer's give wildcards may become — their hand minus the cards already offered, never a
        /// resource they ask for in the same trade.
        /// </summary>
        public static ResourceBundle OffererWildPool(ResourceBundle offererHand, ResourceBundle give, ResourceBundle get) =>
            TradeWildcards.Pool(offererHand, give, get);

        /// <summary>
        /// v139: what the offerer may take for their get wildcards — the partner's hand minus the cards the partner
        /// already gives, never a resource the offerer hands over (offered or picked by the partner).
        /// </summary>
        public static ResourceBundle PartnerWildPool(ResourceBundle partnerHand, MatchTradeOffer trade, ResourceBundle anyChoice) =>
            TradeWildcards.Pool(partnerHand, trade.Get, trade.Give, anyChoice);

        /// <summary>v139: the open offer's pool for the accepter's wildcard pick (from the offerer's hand).</summary>
        public ResourceBundle AcceptWildPool(MatchTradeOffer trade) =>
            OffererWildPool(State.Seats[trade.From].Hand, trade.Give, trade.Get);

        /// <summary>v139: whether <paramref name="seat"/> could accept the open offer (cards + wildcard backing).</summary>
        public bool CanAcceptTrade(int seat, MatchTradeOffer trade, ResourceBundle anyChoice)
        {
            var hand = State.Seats[seat].Hand;
            if (!hand.CanAfford(trade.Get))
                return false;
            if (trade.GiveAny > 0 && !TradeWildcards.Fits(anyChoice, AcceptWildPool(trade), trade.GiveAny))
                return false;
            if (trade.GiveAny == 0 && anyChoice.Total != 0)
                return false;
            return trade.GetAny == 0 || PartnerWildPool(hand, trade, anyChoice).Total >= trade.GetAny;
        }

        private MatchCommandResult RespondTrade(MatchCommand c)
        {
            var trade = State.ActiveTrade;
            var responder = State.Seats[c.Seat];
            var response = trade.Responses[c.Seat];
            switch (c.Response)
            {
                case TradeResponseKind.Accepted:
                    if (!IsValidBundle(c.Choice))
                        return MatchCommandResult.Rejected("Invalid wildcard pick.");
                    if (!responder.Hand.CanAfford(trade.Get))
                        return MatchCommandResult.Rejected("You don't have the cards they ask for.");
                    if (trade.GiveAny > 0 && !TradeWildcards.Fits(c.Choice, AcceptWildPool(trade), trade.GiveAny))
                        return MatchCommandResult.Rejected($"Pick {trade.GiveAny} card(s) they can give.");
                    if (trade.GiveAny == 0 && c.Choice.Total != 0)
                        return MatchCommandResult.Rejected("There is no wildcard to pick for.");
                    if (trade.GetAny > 0 && PartnerWildPool(responder.Hand, trade, c.Choice).Total < trade.GetAny)
                        return MatchCommandResult.Rejected("You don't have enough other cards for their wildcard.");
                    response.Kind = TradeResponseKind.Accepted;
                    response.AnyChoice = c.Choice;
                    break;
                case TradeResponseKind.Declined:
                    response.Kind = TradeResponseKind.Declined;
                    response.AnyChoice = ResourceBundle.Zero;
                    break;
                case TradeResponseKind.Countered:
                    if (!IsValidBundle(c.Offer) || !IsValidBundle(c.Request) || c.Offer.Total == 0 || c.Request.Total == 0)
                        return MatchCommandResult.Rejected("A counter needs cards on both sides.");
                    if (AllResources.Any(r => c.Offer[r] > 0 && c.Request[r] > 0))
                        return MatchCommandResult.Rejected("Don't offer and ask for the same resource.");
                    if (!responder.Hand.CanAfford(c.Request))
                        return MatchCommandResult.Rejected("You don't have the cards you counter with.");
                    response.Kind = TradeResponseKind.Countered;
                    response.CounterGive = c.Offer;
                    response.CounterGet = c.Request;
                    response.AnyChoice = ResourceBundle.Zero;
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
            if (!IsValidBundle(c.Choice))
                return MatchCommandResult.Rejected("Invalid wildcard pick.");
            var response = trade.Responses[c.Victim];
            var me = State.Current;
            var partner = State.Seats[c.Victim];
            ResourceBundle give, get;
            if (response.Kind == TradeResponseKind.Accepted)
            {
                // Re-check the wildcard backing against the hands as they are now.
                if (trade.GiveAny > 0 && !TradeWildcards.Fits(response.AnyChoice, AcceptWildPool(trade), trade.GiveAny))
                    return MatchCommandResult.Rejected("You no longer have the cards they picked.");
                if (trade.GetAny > 0)
                {
                    if (!TradeWildcards.Fits(c.Choice, PartnerWildPool(partner.Hand, trade, response.AnyChoice), trade.GetAny))
                        return MatchCommandResult.Rejected($"Pick {trade.GetAny} card(s) they can give.");
                }
                else if (c.Choice.Total != 0)
                    return MatchCommandResult.Rejected("There is no wildcard to pick for.");

                give = trade.Give;
                give.Add(response.AnyChoice);
                get = trade.Get;
                get.Add(c.Choice);
            }
            else if (response.Kind == TradeResponseKind.Countered)
            {
                if (c.Choice.Total != 0)
                    return MatchCommandResult.Rejected("A counter has no wildcards.");
                give = response.CounterGive;
                get = response.CounterGet;
            }
            else
                return MatchCommandResult.Rejected("That player hasn't accepted.");

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
            me.PlayerTrades++;
            partner.PlayerTrades++;
            AppendLog($"{me.Name} traded with {partner.Name}.");
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult CancelTrade(MatchCommand c)
        {
            if (State.ActiveTrade == null || State.ActiveTrade.From != c.Seat)
                return MatchCommandResult.Rejected("No offer of yours is open.");
            State.ActiveTrade = null;
            AppendLog($"{State.Seats[c.Seat].Name} withdrew the trade offer.");
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
            if (!TryPay(SettlementCostFor(c.Seat)))
                return MatchCommandResult.Rejected("Can't afford a settlement.");
            State.Board.VertexBuildings[v] = (BuildingType.Settlement, State.Current.Owner);
            AppendLog($"{State.Current.Name} built a settlement.");
            OweHarbourPilotPick(c.Seat, v);
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
            if (!TryPay(CityCostFor(c.Seat)))
                return MatchCommandResult.Rejected("Can't afford a city.");
            State.Board.VertexBuildings[v] = (BuildingType.City, State.Current.Owner);
            AppendLog($"{State.Current.Name} built a city.");
            OweHarbourPilotPick(c.Seat, v);
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
            State.Current.BankTradesThisTurn++;
            return MatchCommandResult.Accepted;
        }

        /// <summary>
        /// v143: how many cards a multi-batch bank trade buys. Give-resources are spent in a fixed order, one batch at a
        /// time at the rate that applies at that moment (so "first trade of the turn" perks like Merchant Republic or
        /// Harbour Pilot count once, exactly as the same trades made one by one). Returns -1 when any give-resource
        /// isn't spent in exact whole batches.
        /// </summary>
        public int BankBatchCards(int seat, ResourceBundle give)
        {
            var s = State.Seats[seat];
            int saved = s.BankTradesThisTurn;
            int cards = 0;
            try
            {
                foreach (var r in AllResources)
                {
                    int left = give[r];
                    if (left < 0)
                        return -1;
                    while (left > 0)
                    {
                        int rate = BankRatio(seat, r);
                        if (left < rate)
                            return -1;
                        left -= rate;
                        cards++;
                        s.BankTradesThisTurn++;
                    }
                }
            }
            finally
            {
                s.BankTradesThisTurn = saved;
            }

            return cards;
        }

        private MatchCommandResult BankTradeBatch(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Main)
                return MatchCommandResult.Rejected("Roll first.");
            if (c.Seat != State.CurrentSeat)
                return MatchCommandResult.Rejected("Not your turn.");
            var give = c.Offer;
            var get = c.Request;
            if (give.Total <= 0 || get.Total <= 0)
                return MatchCommandResult.Rejected("Pick what to give and what to get.");
            foreach (var r in AllResources)
            {
                if (get[r] < 0)
                    return MatchCommandResult.Rejected("Bad request.");
                if (give[r] > 0 && get[r] > 0)
                    return MatchCommandResult.Rejected("Give and get different resources.");
            }

            var hand = State.Current.Hand;
            if (!hand.CanAfford(give))
                return MatchCommandResult.Rejected("Not enough cards.");
            int cards = BankBatchCards(c.Seat, give);
            if (cards <= 0)
                return MatchCommandResult.Rejected("Give whole batches at your bank rates.");
            if (cards != get.Total)
                return MatchCommandResult.Rejected($"That buys {cards} card(s), not {get.Total}.");
            hand.Pay(give);
            hand.Add(get);
            State.Current.Hand = hand;
            State.Current.BankTradesThisTurn += cards;
            AppendLog($"{State.Current.Name} traded {give.Total} cards with the bank for {get.Total}.");
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult UseExtraTime(MatchCommand c)
        {
            if (IsPregame || State.Phase == MatchPhase.GameOver)
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
            State.Current.BankTradesThisTurn = 0;
            State.Current.HasTakenFirstTurn = true;
            if (State.ActiveTrade != null)
                AppendLog($"{State.Seats[State.ActiveTrade.From].Name}'s trade offer lapsed.");
            State.ActiveTrade = null;
            State.CurrentSeat = (State.CurrentSeat + 1) % State.Seats.Count;
            State.TurnNumber++;
            // v141: the night (event draw) may open a ballot or auction, which then hands over to the roll.
            State.Phase = MatchPhase.Roll;
            if (State.CurrentSeat == 0)
            {
                State.Round++;
                if (State.Round % TurnClock.RoundsPerNewCharge == 0)
                {
                    foreach (var seat in State.Seats)
                        seat.ExtraTimeCharges++;
                    AppendLog($"Round {State.Round}: everyone gains an extra-time charge.");
                }

                if (PartyRules)
                {
                    BeginNight();
                    if (EventsOn)
                        DrawEvent();
                }
            }

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

        /// <summary>v144: true when <paramref name="v"/> is a harbour pier corner.</summary>
        public bool IsPortVertex(Vertex v) => State.Ports.Any(p => PortAccess.Serves(State.Board, p, v));

        /// <summary>v144 Harbour Pilot: a settlement or city on a harbour owes its holder one free resource pick.</summary>
        private void OweHarbourPilotPick(int seat, Vertex v)
        {
            if (PartyRules && State.Seats[seat].Trait == MatchTraitId.HarbourPilot && IsPortVertex(v))
                State.Seats[seat].HarbourPilotPicks++;
        }

        /// <summary>v144: Harbour Pilot picked after setup — each setup settlement already on a harbour counts.</summary>
        private void OweHarbourPilotPicksForExisting(int seat)
        {
            var owner = State.Seats[seat].Owner;
            foreach (var kv in State.Board.VertexBuildings)
                if (kv.Value.owner == owner)
                    OweHarbourPilotPick(seat, kv.Key);
        }

        private MatchCommandResult ChooseHarbourPilotResource(MatchCommand c)
        {
            var seat = State.Seats[c.Seat];
            if (seat.HarbourPilotPicks <= 0)
                return MatchCommandResult.Rejected("No Harbour Pilot pick owed.");
            if (!AllResources.Contains(c.Receive))
                return MatchCommandResult.Rejected("Pick a resource.");
            var hand = seat.Hand;
            hand.Add(c.Receive, 1);
            seat.Hand = hand;
            seat.HarbourPilotPicks--;
            AppendLog($"{seat.Name}'s Harbour Pilot takes 1 {c.Receive}.");
            return MatchCommandResult.Accepted;
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

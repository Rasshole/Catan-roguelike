using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Victory;
using CatanRoguelike.Core.Yield;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core
{
    public sealed class GameController
    {
        public GameState State { get; }
        public PlacementValidator Placement { get; }
        public RollEngine RollEngine { get; }
        public CardEngine CardEngine { get; }
        public ShopGenerator Shop { get; }
        public AiController Ai { get; }

        public event Action<GameState> OnStateChanged;

        private Vertex? _lastPlacedSettlement;

        public GameController(int? seed = null, bool useThirteenHex = false)
        {
            var board = MapPresets.CreateBoard(useThirteenHex);
            State = new GameState(board);
            State.Ports = PortAccess.DiscoverPorts(board);
            Placement = new PlacementValidator();
            RollEngine = new RollEngine(seed);
            CardEngine = new CardEngine(seed);
            Shop = new ShopGenerator(seed);
            Ai = new AiController(seed);
            State.StatusMessage = "AI places first settlement...";
        }

        public void NotifyChanged() => OnStateChanged?.Invoke(State);

        public bool PlaceSettlement(Vertex vertex, PlayerId player)
        {
            vertex = VertexGraph.Canonicalize(vertex);
            bool setup = State.IsSetupPhase;

            if (!Placement.CanPlaceSettlement(State.Board, vertex, player, setup))
                return false;

            if (!setup)
            {
                var cost = GetEffectiveCost(player, BalanceConfig.GetSettlementCost(State.Board, player));
                if (!TryPay(player, cost)) return false;
            }

            State.Board.VertexBuildings[vertex] = (BuildingType.Settlement, player);
            _lastPlacedSettlement = vertex;

            if (State.HarborCharterPending && player == PlayerId.Human && IsCoastalVertexOnBoard(vertex))
            {
                State.AddVictoryPoints(player, 1);
                State.HarborCharterPending = false;
                State.StatusMessage = "Harbor Charter: +1 VP for coastal settlement!";
            }

            AdvanceSetupAfterSettlement(player);
            VictoryCalculator.RefreshVictoryPoints(State);
            NotifyChanged();
            return true;
        }

        public bool PlaceRoad(Edge edge, PlayerId player)
        {
            edge = NormalizeEdge(edge);
            bool setup = State.IsSetupPhase;

            if (!Placement.CanPlaceRoad(State.Board, edge, player, setup))
                return false;

            bool freeRoad = !setup && State.PendingCard == CardId.RoadBuilder && player == PlayerId.Human;

            if (!setup && !freeRoad)
            {
                var cost = GetEffectiveCost(player, BalanceConfig.GetRoadCost(State.Board, player));
                if (!TryPay(player, cost)) return false;
            }

            State.Board.Roads[edge] = player;

            if (freeRoad)
                State.PendingCard = null;

            AdvanceSetupAfterRoad(player);
            VictoryCalculator.RefreshVictoryPoints(State);
            NotifyChanged();
            return true;
        }

        public bool UpgradeCity(Vertex vertex, PlayerId player)
        {
            vertex = VertexGraph.Canonicalize(vertex);
            if (!Placement.CanUpgradeToCity(State.Board, vertex, player)) return false;

            var cost = GetEffectiveCost(player, BalanceConfig.GetCityCost(State.Board, player));
            if (!TryPay(player, cost)) return false;

            State.Board.VertexBuildings[vertex] = (BuildingType.City, player);

            if (State.PendingCard == CardId.MasterBuilder && player == PlayerId.Human)
                State.PendingCard = null;

            VictoryCalculator.RefreshVictoryPoints(State);
            NotifyChanged();
            return true;
        }

        public bool MoveRobber(HexCoord tile, PlayerId player, bool steal = false)
        {
            if (!State.Board.TryGetTile(tile, out _)) return false;
            State.Board.PlaceRobber(tile);

            if (steal)
            {
                var opponent = player == PlayerId.Human ? PlayerId.Ai : PlayerId.Human;
                StealRandomResource(opponent, player);
            }

            NotifyChanged();
            return true;
        }

        public bool BuyShopDeal(ShopDeal deal) => Shop.TryPurchase(State, PlayerId.Human, deal);

        public int GetShopDealCost(ShopDeal deal) =>
            Shop.GetEffectiveGiveAmount(State, PlayerId.Human, deal);

        public bool PlayPlayerCard(CardId card, ResourceType? resource = null,
            HexCoord? robberTile = null, Edge? road = null)
        {
            if (State.Phase != GamePhase.NightPlayCard) return false;
            if (!CardEngine.PlayCard(State, PlayerId.Human, card, resource, robberTile, road))
                return false;

            State.StatusMessage = $"Played {CardLibrary.Get(card).Name}";
            AdvanceFromNightCard();
            NotifyChanged();
            return true;
        }

        public void SkipNightCard()
        {
            if (State.Phase != GamePhase.NightPlayCard) return;
            AdvanceFromNightCard();
            NotifyChanged();
        }

        public void EndPlayerDay()
        {
            if (State.Phase != GamePhase.DayPlayerActions) return;
            State.Phase = GamePhase.DayAiTurn;
            State.StatusMessage = "AI is taking its turn...";
            Ai.ExecuteDayTurn(this);
            EndDay();
            NotifyChanged();
        }

        private void EndDay()
        {
            State.Board.DisabledRoads.Clear();
            State.AiShopEmbargo = null;
            State.Board.DayNumber++;
            var winner = VictoryCalculator.CheckWinner(State);
            if (winner.HasValue)
            {
                State.Winner = winner;
                State.Phase = GamePhase.GameOver;
                State.StatusMessage = winner == PlayerId.Human ? "You win!" : "AI wins!";
                NotifyChanged();
                return;
            }

            BeginNight();
        }

        public void BeginNight()
        {
            State.Phase = GamePhase.NightRoll;
            State.TomorrowRolls = RollEngine.RollNightly(2);

            CardEngine.DrawToHand(State, PlayerId.Human, BalanceConfig.CardsDrawnPerNight);
            CardEngine.DrawToHand(State, PlayerId.Ai, 1);

            State.Phase = GamePhase.NightPlayCard;
            State.StatusMessage = "Night: review tomorrow's rolls. Play a card or continue.";
            NotifyChanged();
        }

        private void AdvanceFromNightCard()
        {
            State.Phase = GamePhase.NightAiPlan;
            Ai.ExecuteNightPlan(this);

            State.TodayRolls = new Dictionary<ResourceType, int>(State.TomorrowRolls);
            State.Phase = GamePhase.DayProduction;
            ApplyProduction();

            State.ShopDeals = Shop.GenerateDailyDeals();
            State.Phase = GamePhase.DayPlayerActions;
            State.StatusMessage = "Day: build, shop, or end turn.";
            NotifyChanged();
        }

        private void ApplyProduction()
        {
            var playerProd = ProductionCalculator.CalculateForPlayer(State.Board, PlayerId.Human, State.TodayRolls);
            var aiProd = ProductionCalculator.CalculateForPlayer(State.Board, PlayerId.Ai, State.TodayRolls);

            var pInv = State.PlayerInventory;
            pInv.Add(playerProd);
            State.PlayerInventory = pInv;

            var aInv = State.AiInventory;
            aInv.Add(aiProd);
            State.AiInventory = aInv;
        }

        public void RunAiSetupStep()
        {
            switch (State.Phase)
            {
                case GamePhase.SetupAiSettlement1:
                case GamePhase.SetupAiSettlement2:
                    Ai.PlaceSetupSettlement(this);
                    break;
                case GamePhase.SetupAiRoad1:
                case GamePhase.SetupAiRoad2:
                    Ai.PlaceSetupRoad(this);
                    break;
            }
            NotifyChanged();
        }

        private void AdvanceSetupAfterSettlement(PlayerId player)
        {
            if (player == PlayerId.Ai)
            {
                State.Phase = State.Phase switch
                {
                    GamePhase.SetupAiSettlement1 => GamePhase.SetupAiRoad1,
                    GamePhase.SetupAiSettlement2 => GamePhase.SetupAiRoad2,
                    _ => State.Phase
                };
                State.StatusMessage = "AI places a road...";
                RunAiSetupStep();
            }
            else
            {
                State.Phase = State.Phase switch
                {
                    GamePhase.SetupPlayerSettlement1 => GamePhase.SetupPlayerRoad1,
                    GamePhase.SetupPlayerSettlement2 => GamePhase.SetupPlayerRoad2,
                    _ => State.Phase
                };
                State.StatusMessage = State.Phase switch
                {
                    GamePhase.SetupPlayerRoad1 => "Place your first road.",
                    GamePhase.SetupPlayerRoad2 => "Place your second road.",
                    _ => "Your turn."
                };
            }
        }

        private void AdvanceSetupAfterRoad(PlayerId player)
        {
            if (player == PlayerId.Ai)
            {
                State.Phase = State.Phase switch
                {
                    GamePhase.SetupAiRoad1 => GamePhase.SetupPlayerSettlement1,
                    GamePhase.SetupAiRoad2 => GamePhase.SetupPlayerSettlement2,
                    _ => State.Phase
                };
                State.StatusMessage = State.Phase == GamePhase.SetupPlayerSettlement1
                    ? "Place your first settlement."
                    : "Place your second settlement.";
            }
            else
            {
                if (State.Phase == GamePhase.SetupPlayerRoad2)
                {
                    BeginNight();
                    State.StatusMessage = "Setup complete. Night falls — review tomorrow's rolls.";
                }
                else
                {
                    State.Phase = GamePhase.SetupPlayerSettlement2;
                    State.StatusMessage = "Place your second settlement.";
                }
            }
        }

        public IEnumerable<Vertex> GetValidSettlements(PlayerId player) =>
            Placement.GetValidSettlementSpots(State.Board, player, State.IsSetupPhase);

        public IEnumerable<Edge> GetValidRoads(PlayerId player) =>
            Placement.GetValidRoadSpots(State.Board, player, State.IsSetupPhase);

        public IEnumerable<Vertex> GetUpgradeableCities(PlayerId player) =>
            State.Board.VertexBuildings
                .Where(kv => kv.Value.owner == player && kv.Value.type == BuildingType.Settlement)
                .Select(kv => kv.Key);

        public ResourceBundle GetEffectiveCost(PlayerId player, ResourceBundle baseCost)
        {
            if (player != PlayerId.Human || State.PendingCard != CardId.MasterBuilder)
                return baseCost;

            return new ResourceBundle
            {
                Wood = Discount(baseCost.Wood),
                Brick = Discount(baseCost.Brick),
                Wheat = Discount(baseCost.Wheat),
                Sheep = Discount(baseCost.Sheep),
                Stone = Discount(baseCost.Stone)
            };
        }

        private static int Discount(int value) =>
            value == 0 ? 0 : Math.Max(1, (int)Math.Ceiling(value * 0.75f));

        private bool TryPay(PlayerId player, ResourceBundle cost)
        {
            cost = GetEffectiveCost(player, cost);
            var inv = State.GetInventory(player);
            if (!inv.CanAfford(cost)) return false;
            inv.Pay(cost);
            State.SetInventory(player, inv);

            if (player == PlayerId.Human && State.PendingCard == CardId.MasterBuilder)
                State.PendingCard = null;

            return true;
        }

        private void StealRandomResource(PlayerId from, PlayerId to)
        {
            var oppInv = State.GetInventory(from);
            var available = oppInv.EnumerateNonZero().ToList();
            if (available.Count == 0) return;

            var pick = available[new Random().Next(available.Count)];
            oppInv.Add(pick.type, -1);
            var inv = State.GetInventory(to);
            inv.Add(pick.type, 1);
            State.SetInventory(from, oppInv);
            State.SetInventory(to, inv);
        }

        private bool IsCoastalVertexOnBoard(Vertex vertex)
        {
            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (State.Board.TryGetTile(hex, out var tile) && tile.IsCoastal)
                    return true;
            }
            return false;
        }

        private static Edge NormalizeEdge(Edge edge) =>
            new Edge(VertexGraph.Canonicalize(edge.A), VertexGraph.Canonicalize(edge.B));

        public Vertex? GetLastPlacedSettlement() => _lastPlacedSettlement;
    }
}

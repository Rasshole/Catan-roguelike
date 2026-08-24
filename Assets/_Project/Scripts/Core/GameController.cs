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
                var cost = BalanceConfig.GetSettlementCost(State.Board, player);
                if (!TryPay(player, cost)) return false;
            }

            State.Board.VertexBuildings[vertex] = (BuildingType.Settlement, player);
            _lastPlacedSettlement = vertex;
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

            var cost = BalanceConfig.GetRoadCost(State.Board, player);
            if (!setup && !TryPay(player, cost)) return false;

            State.Board.Roads[edge] = player;
            AdvanceSetupAfterRoad(player);
            NotifyChanged();
            return true;
        }

        public bool UpgradeCity(Vertex vertex, PlayerId player)
        {
            vertex = VertexGraph.Canonicalize(vertex);
            if (!Placement.CanUpgradeToCity(State.Board, vertex, player)) return false;

            var cost = BalanceConfig.GetCityCost(State.Board, player);
            if (!TryPay(player, cost)) return false;

            State.Board.VertexBuildings[vertex] = (BuildingType.City, player);
            VictoryCalculator.RefreshVictoryPoints(State);
            NotifyChanged();
            return true;
        }

        public bool BuyShopDeal(ShopDeal deal) => Shop.TryPurchase(State, PlayerId.Human, deal);

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
                    GamePhase.SetupPlayerSettlement2 => "Place your second settlement.",
                    _ => "Your turn."
                };

                if (State.Phase == GamePhase.SetupPlayerSettlement2)
                    State.StatusMessage = "Place your second settlement.";

                if (State.Phase == GamePhase.NightRoll || State.Phase == GamePhase.SetupPlayerRoad2)
                {
                    // After player road 2, start game
                }
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

        private bool TryPay(PlayerId player, ResourceBundle cost)
        {
            var inv = State.GetInventory(player);
            if (!inv.CanAfford(cost)) return false;
            inv.Pay(cost);
            State.SetInventory(player, inv);
            return true;
        }

        private static Edge NormalizeEdge(Edge edge) =>
            new Edge(VertexGraph.Canonicalize(edge.A), VertexGraph.Canonicalize(edge.B));

        public Vertex? GetLastPlacedSettlement() => _lastPlacedSettlement;
    }
}

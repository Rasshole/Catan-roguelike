using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
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
        public EventEngine Events { get; }

        public event Action<GameState> OnStateChanged;
        public event Action OnBoardRebuilt;

        private readonly Random _random;
        private Vertex? _lastPlacedSettlement;

        public GameController(int? seed = null, MapSize mapSize = MapSize.Small)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            var board = MapPresets.CreateBoard(mapSize);
            State = new GameState(board) { MapSize = mapSize, Phase = GamePhase.RunSelectMap };
            State.Ports = PortAccess.DiscoverPorts(board);
            Placement = new PlacementValidator();
            RollEngine = new RollEngine(seed);
            CardEngine = new CardEngine(seed);
            Shop = new ShopGenerator(seed);
            Ai = new AiController(seed);
            Events = new EventEngine(seed);
            State.StatusMessage = "Vælg kortstørrelse.";
        }

        public void SelectMap(MapSize mapSize)
        {
            if (State.Phase != GamePhase.RunSelectMap) return;

            var board = MapPresets.CreateBoard(mapSize);
            State.ResetForNewMap(board, mapSize);
            State.Ports = PortAccess.DiscoverPorts(board);
            State.StatusMessage = "Vælg din leader.";
            _lastPlacedSettlement = null;

            OnBoardRebuilt?.Invoke();
            NotifyChanged();
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
                var baseCost = BalanceConfig.GetSettlementCost(State.Board, player,
                    ModifierService.GetSettlementThreshold(State));
                baseCost = ModifierService.ApplyLeaderCostModifiers(State, player, baseCost,
                    isSettlement: true, isCity: false, isRoad: false);
                var cost = GetEffectiveCost(player, baseCost);
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

            bool freeRoad = !setup && player == PlayerId.Human && (
                State.PendingCard == CardId.RoadBuilder && State.FreeRoadCharges > 0
                || State.Leader == LeaderId.Pioneer && State.PioneerFreeRoadAvailable);

            if (!setup && !freeRoad)
            {
                var baseCost = ModifierService.ApplyLeaderCostModifiers(State, player,
                    BalanceConfig.GetRoadCost(State.Board, player), false, false, true);
                var cost = GetEffectiveCost(player, baseCost);
                if (!TryPay(player, cost)) return false;
            }

            State.Board.Roads[edge] = player;

            if (freeRoad)
            {
                if (State.PendingCard == CardId.RoadBuilder && State.FreeRoadCharges > 0)
                {
                    State.FreeRoadCharges--;
                    if (State.FreeRoadCharges <= 0) State.PendingCard = null;
                }
                else
                    State.PioneerFreeRoadAvailable = false;
            }

            AdvanceSetupAfterRoad(player);
            VictoryCalculator.RefreshVictoryPoints(State);
            NotifyChanged();
            return true;
        }

        public bool UpgradeCity(Vertex vertex, PlayerId player)
        {
            vertex = VertexGraph.Canonicalize(vertex);
            if (!Placement.CanUpgradeToCity(State.Board, vertex, player)) return false;

            var baseCity = ModifierService.ApplyLeaderCostModifiers(State, player,
                BalanceConfig.GetCityCost(State.Board, player), false, true, false);
            var cost = GetEffectiveCost(player, baseCity);
            if (!TryPay(player, cost)) return false;

            State.Board.VertexBuildings[vertex] = (BuildingType.City, player);

            if (player == PlayerId.Human && !State.FirstCityBuiltThisRun)
            {
                State.FirstCityBuiltThisRun = true;
                if (State.HasPerk(LevelUpPerkId.FirstCityVp))
                    State.AddVictoryPoints(player, 1);
            }

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
            Events.ClearDailyEventEffects(State);
            State.Board.DisabledRoads.Clear();

            if (State.AiEmbargoDaysLeft > 0)
            {
                State.AiEmbargoDaysLeft--;
                if (State.AiEmbargoDaysLeft <= 0) State.AiShopEmbargo = null;
            }
            else
                State.AiShopEmbargo = null;

            State.Board.DayNumber++;

            if (RunProgression.ShouldOfferLevelUp(State))
            {
                State.PendingLevelUpChoices.Clear();
                State.PendingLevelUpChoices.AddRange(RunProgression.GenerateLevelUpChoices(State, _random));
                if (State.PendingLevelUpChoices.Count > 0)
                {
                    State.Phase = GamePhase.LevelUpChoice;
                    State.StatusMessage = "Level up! Choose a perk.";
                    NotifyChanged();
                    return;
                }
            }

            ContinueAfterDayIncrement();
        }

        private void ContinueAfterDayIncrement()
        {
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

        public void SelectLeader(LeaderId leader)
        {
            if (State.Phase != GamePhase.RunSelectLeader) return;
            State.Leader = leader;
            State.Phase = GamePhase.RunSelectDraft;
            State.StatusMessage = $"Leader: {LeaderLibrary.Get(leader).Name}. Draft {RunProgression.DraftPickCount} uniques.";
            NotifyChanged();
        }

        public void ToggleDraftUnique(UniqueBuildingId id)
        {
            if (State.Phase != GamePhase.RunSelectDraft) return;
            if (State.DraftedUniques.Contains(id))
                State.DraftedUniques.Remove(id);
            else if (State.DraftedUniques.Count < RunProgression.DraftPickCount)
                State.DraftedUniques.Add(id);
            NotifyChanged();
        }

        public void ConfirmRunSetup()
        {
            if (State.Phase != GamePhase.RunSelectDraft) return;
            if (State.DraftedUniques.Count != RunProgression.DraftPickCount) return;

            State.RunSetupComplete = true;
            State.Phase = GamePhase.SetupAiSettlement1;
            State.StatusMessage = "AI places first settlement...";
            RunAiSetupStep();
            NotifyChanged();
        }

        public void ChooseLevelUpPerk(LevelUpPerkId perk)
        {
            if (State.Phase != GamePhase.LevelUpChoice) return;
            if (!State.PendingLevelUpChoices.Contains(perk)) return;

            State.AcquiredPerks.Add(perk);
            State.LevelUpsTaken++;
            State.LastLevelUpDay = State.Board.DayNumber;
            State.PendingLevelUpChoices.Clear();
            State.StatusMessage = $"Gained: {LevelUpLibrary.GetDescription(perk)}";
            ContinueAfterDayIncrement();
            NotifyChanged();
        }

        public void BeginNight()
        {
            State.PioneerFreeRoadAvailable = State.Leader == LeaderId.Pioneer;
            State.Phase = GamePhase.NightRoll;
            State.TomorrowRolls = RollEngine.RollNightly(2);

            var eventId = Events.MaybeRollEvent();
            if (eventId != EventId.None)
                Events.ApplyEvent(State, eventId);

            ModifierService.ApplyNightUniques(State);

            int draws = BalanceConfig.CardsDrawnPerNight;
            if (State.HasUnique(UniqueBuildingId.CaravanPost)) draws++;
            if (State.HasPerk(LevelUpPerkId.ExtraCardDraw)) draws++;

            CardEngine.DrawToHand(State, PlayerId.Human, draws);
            CardEngine.DrawToHand(State, PlayerId.Ai, 1);

            State.Phase = GamePhase.NightPlayCard;
            string eventNote = State.ActiveEvent != EventId.None ? $" Event: {State.EventMessage}" : "";
            State.StatusMessage = "Night: review tomorrow's rolls. Play a card or continue." + eventNote;
            NotifyChanged();
        }

        private void AdvanceFromNightCard()
        {
            State.Phase = GamePhase.NightAiPlan;
            Ai.ExecuteNightPlan(this);

            State.TodayRolls = new Dictionary<ResourceType, int>(State.TomorrowRolls);
            State.Phase = GamePhase.DayProduction;
            ApplyProduction();

            State.ShopDeals = Shop.GenerateDailyDeals(State);
            State.Phase = GamePhase.DayPlayerActions;
            State.StatusMessage = "Day: build, shop, or end turn.";
            NotifyChanged();
        }

        private void ApplyProduction()
        {
            var playerProd = ProductionCalculator.CalculateForPlayer(State, PlayerId.Human, State.TodayRolls);
            var aiProd = ProductionCalculator.CalculateForPlayer(State, PlayerId.Ai, State.TodayRolls);

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
            if (player != PlayerId.Human) return baseCost;

            float discount = State.PendingCard == CardId.MasterBuilder ? 0.75f : 1f;
            if (State.Leader == LeaderId.Architect && State.PendingCard == CardId.MasterBuilder)
                discount = 0.65f;

            if (discount >= 1f) return baseCost;

            return new ResourceBundle
            {
                Wood = Discount(baseCost.Wood, discount),
                Brick = Discount(baseCost.Brick, discount),
                Wheat = Discount(baseCost.Wheat, discount),
                Sheep = Discount(baseCost.Sheep, discount),
                Stone = Discount(baseCost.Stone, discount)
            };
        }

        private static int Discount(int value, float factor) =>
            value == 0 ? 0 : Math.Max(1, (int)Math.Ceiling(value * factor));

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

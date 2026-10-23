using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Trade;
using CatanRoguelike.Core.UI;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Save;
using CatanRoguelike.Core.Victory;
using CatanRoguelike.Core.Yield;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core
{
    public sealed class GameController
    {
        public GameState State { get; private set; }
        public PlacementValidator Placement { get; }
        public RollEngine RollEngine { get; }
        public DiceRollEngine DiceRollEngine { get; }
        public CardEngine CardEngine { get; }
        public ShopGenerator Shop { get; }
        public AiController Ai { get; }
        public EventEngine Events { get; }

        public event Action<GameState> OnStateChanged;
        public event Action OnBoardRebuilt;
        /// <summary>
        /// Fired when night fully resolves into <see cref="GamePhase.DayPlayerActions"/> — stable autosave point
        /// (production applied, shop generated; not mid-card picker).
        /// </summary>
        public event Action OnAutosavePoint;
        public event Action<ResourceBundle> HumanBuildCostPaid;

        public int RunSeed { get; }
        public MetaProgression Meta { get; private set; }

        private readonly Random _random;
        private Vertex? _lastPlacedSettlement;
        private bool _metaStartCardGranted;

        /// <summary>When true, new maps include Seafarers fish hexes and fish spend rules.</summary>
        public bool FishermenMapOption { get; set; } = true;

        /// <summary>
        /// v131: when true, <see cref="ConfirmRunSetup"/> opens <see cref="GamePhase.RunSelectCivilization"/>
        /// (pick 1 of 3) before setup. Off by default so headless tests keep the old flow; the game turns it on.
        /// </summary>
        public bool CivilizationPickEnabled { get; set; }

        public GameController(int? seed = null, MapSize mapSize = MapSize.Small, MetaProgression meta = null)
        {
            Meta = meta;
            RunSeed = seed ?? 0;
            var board = MapPresets.CreateBoard(mapSize, RunSeed);
            var state = new GameState(board) { MapSize = mapSize, Phase = GamePhase.RunSelectMap };
            state.Ports = PortAccess.DiscoverPorts(board);
            state.StatusMessage = "Vælg kortstørrelse.";
            State = state;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _lastPlacedSettlement = null;
            Placement = new PlacementValidator();
            RollEngine = new RollEngine(seed);
            DiceRollEngine = new DiceRollEngine(seed);
            CardEngine = new CardEngine(seed);
            Shop = new ShopGenerator(seed);
            Ai = new AiController(seed);
            Events = new EventEngine(seed) { Cards = CardEngine };
        }

        public void SetMeta(MetaProgression meta) => Meta = meta;

        private GameController(int runSeed, GameState state, Vertex? lastPlacedSettlement, MetaProgression meta = null)
        {
            RunSeed = runSeed;
            Meta = meta;
            State = state;
            _random = new Random(runSeed);
            _lastPlacedSettlement = lastPlacedSettlement;
            Placement = new PlacementValidator();
            RollEngine = new RollEngine(runSeed);
            DiceRollEngine = new DiceRollEngine(runSeed);
            CardEngine = new CardEngine(runSeed);
            Shop = new ShopGenerator(runSeed);
            Ai = new AiController(runSeed);
            Events = new EventEngine(runSeed) { Cards = CardEngine };
        }

        public static GameController FromSave(string json)
        {
            var doc = SaveGame.Parse(json);
            var state = SaveGame.RestoreState(doc);
            NumberTokenLibrary.AssignMissingTokens(state.Board, doc.RunSeed);
            Vertex? lastSettlement = null;
            if (doc.LastPlacedSettlement != null)
                lastSettlement = VertexGraph.Canonicalize(new Vertex(
                    new HexCoord(doc.LastPlacedSettlement.HexQ, doc.LastPlacedSettlement.HexR),
                    doc.LastPlacedSettlement.CornerIndex));
            var controller = new GameController(doc.RunSeed, state, lastSettlement);
            controller.RestoreRuntimeFlagsFromSave(doc);
            return controller;
        }

        public bool GetMetaStartCardGrantedForSave() => _metaStartCardGranted;

        private void RestoreRuntimeFlagsFromSave(GameSaveDocument doc)
        {
            _metaStartCardGranted = doc.MetaStartCardGranted
                ?? (State.RunSetupComplete && State.Phase >= GamePhase.NightPlayCard);
        }

        public void SelectMap(MapSize mapSize)
        {
            if (State.Phase != GamePhase.RunSelectMap) return;
            if (Meta != null && !Meta.IsMapAvailable(mapSize))
            {
                State.StatusMessage = $"{MapPresets.GetDisplayName(mapSize)} is locked — spend stars in Unlocks.";
                NotifyChanged();
                return;
            }

            var gen = new MapGenerationOptions(FishermenMapOption);
            var board = MapPresets.CreateBoard(mapSize, RunSeed, gen);
            State.ResetForNewMap(board, mapSize);
            State.FishermenOfCatanEnabled = FishermenMapOption;
            State.FishermenRunSeed = RunSeed;
            if (FishermenMapOption)
                FishTokenBag.EnsureInitialized(State, RunSeed);
            State.Ports = PortAccess.DiscoverPorts(board);
            State.StatusMessage = "Vælg din leader.";
            _lastPlacedSettlement = null;
            if (CivilizationPickEnabled)
                OpenCivilizationPick();

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

            ResourceBundle paidCost = ResourceBundle.Zero;
            if (!setup)
            {
                var baseCost = BalanceConfig.GetSettlementCost(State.Board, player,
                    ModifierService.GetSettlementThreshold(State));
                baseCost = ModifierService.ApplyLeaderCostModifiers(State, player, baseCost,
                    isSettlement: true, isCity: false, isRoad: false);
                var cost = GetEffectiveCost(player, baseCost);
                if (!TryPay(player, cost)) return false;
                paidCost = cost;
            }

            bool grantSetupBonus = setup && (
                State.Phase == GamePhase.SetupAiSettlement2
                || State.Phase == GamePhase.SetupPlayerSettlement2);

            State.Board.VertexBuildings[vertex] = (BuildingType.Settlement, player);
            _lastPlacedSettlement = vertex;

            if (grantSetupBonus)
                GrantSetupBonus(vertex, player);

            if (State.HarborCharterPending && player == PlayerId.Human && IsCoastalVertexOnBoard(vertex))
            {
                State.AddVictoryPoints(player, 1);
                State.HarborCharterPending = false;
                State.StatusMessage = "Harbor Charter: +1 VP for coastal settlement!";
            }

            AdvanceSetupAfterSettlement(player);
            VictoryCalculator.RefreshVictoryPoints(State);
            if (!setup && player == PlayerId.Human && paidCost.Total > 0)
                HumanBuildCostPaid?.Invoke(paidCost);
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
                || LeaderTraits.HasArchetypePassive(State, LeaderId.Pioneer) && State.PioneerFreeRoadAvailable);

            ResourceBundle paidCost = ResourceBundle.Zero;
            if (!setup && !freeRoad)
            {
                var baseCost = ModifierService.ApplyLeaderCostModifiers(State, player,
                    BalanceConfig.GetRoadCost(State.Board, player), false, false, true);
                var cost = GetEffectiveCost(player, baseCost);
                if (!TryPay(player, cost)) return false;
                paidCost = cost;
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
            if (!setup && player == PlayerId.Human && paidCost.Total > 0)
                HumanBuildCostPaid?.Invoke(paidCost);
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
            if (player == PlayerId.Human && cost.Total > 0)
                HumanBuildCostPaid?.Invoke(cost);

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

        public bool MoveRobber(HexCoord tile, PlayerId player, bool steal = true)
        {
            if (!State.Board.TryGetTile(tile, out _)) return false;
            State.Board.PlaceRobber(tile);

            if (steal)
                RobberSteal.StealFromHex(State, tile, player, _random);

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
            GameLog.Append(State, "You ended your turn.");
            NotifyChanged();
        }

        /// <summary>Called after paced AI presentation (see GameplayFeelController).</summary>
        public void CompleteAiDayTurn()
        {
            if (State.Phase != GamePhase.DayAiTurn)
                return;
            Ai.ExecuteDayTurn(this);
            GameLog.Append(State, "AI finished its turn.");
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

            if (State.PlayerEmbargoDaysLeft > 0)
            {
                State.PlayerEmbargoDaysLeft--;
                if (State.PlayerEmbargoDaysLeft <= 0) State.PlayerShopEmbargo = null;
            }
            else
                State.PlayerShopEmbargo = null;

            State.Board.DayNumber++;

            ApplyActTransitions();

            if (RunProgression.ShouldOfferLevelUp(State))
            {
                State.PendingLevelUpChoices.Clear();
                State.PendingLevelUpChoices.AddRange(
                    RunProgression.GenerateLevelUpChoices(
                        State, RunProgression.CreateLevelUpRandom(RunSeed, State.Board.DayNumber)));
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
            if (Meta != null && !Meta.IsLeaderAvailable(leader))
            {
                State.StatusMessage = $"{LeaderLibrary.Get(leader).Name} is locked — spend stars in Unlocks.";
                NotifyChanged();
                return;
            }

            State.Leader = leader;
            State.Phase = GamePhase.RunSelectDraft;
            State.StatusMessage =
                $"Leader: {LeaderLibrary.Get(leader).Name}. Draft {GetRequiredDraftCount()} uniques.";
            NotifyChanged();
        }

        public void ToggleDraftUnique(UniqueBuildingId id)
        {
            if (State.Phase != GamePhase.RunSelectDraft) return;
            if (Meta != null)
            {
                var pool = Meta.GetDraftPool().ToHashSet();
                if (!pool.Contains(id))
                    return;
            }

            if (State.DraftedUniques.Contains(id))
                State.DraftedUniques.Remove(id);
            else if (State.DraftedUniques.Count < GetRequiredDraftCount())
                State.DraftedUniques.Add(id);
            NotifyChanged();
        }

        public void ConfirmRunSetup()
        {
            if (State.Phase != GamePhase.RunSelectDraft) return;
            if (State.DraftedUniques.Count != GetRequiredDraftCount()) return;

            if (Meta != null)
            {
                var pool = Meta.GetDraftPool().ToHashSet();
                if (State.DraftedUniques.Any(id => !pool.Contains(id)))
                {
                    State.StatusMessage = "One or more drafted uniques are locked — spend stars in Unlocks.";
                    NotifyChanged();
                    return;
                }
            }

            if (Meta != null && Meta.HasStartWheatBonus())
            {
                var inv = State.PlayerInventory;
                inv.Wheat += 1;
                State.PlayerInventory = inv;
            }

            _metaStartCardGranted = false;
            if (CivilizationPickEnabled && State.Civilization == CivilizationId.None)
            {
                // Legacy path (e.g. a save made mid-setup before v134): pick the civilization last.
                OpenCivilizationPick();
                return;
            }

            BeginBoardSetup();
        }

        /// <summary>
        /// v134: civs and leaders are merged — the civilization pick (1 of 3, each with its leader portrait) is step 2,
        /// right after the map, and replaces the separate leader step. Only civs whose leader is unlocked are offered.
        /// </summary>
        private void OpenCivilizationPick()
        {
            State.CivilizationOffer.Clear();
            State.CivilizationOffer.AddRange(CivilizationOffer.Generate(RunSeed, CivilizationOffer.OfferSize,
                def => Meta == null || Meta.IsLeaderAvailable(def.Leader)));
            State.Phase = GamePhase.RunSelectCivilization;
            State.StatusMessage = "Choose your civilization.";
            NotifyChanged();
        }

        /// <summary>
        /// v131/v134: pick one of the offered civilizations. Sets its leader (passive + level-up perks), applies the
        /// start bonus and continues to the unique-building draft (or straight to board setup on the legacy path).
        /// </summary>
        public bool ChooseCivilization(CivilizationId id)
        {
            if (State.Phase != GamePhase.RunSelectCivilization) return false;
            if (!State.CivilizationOffer.Contains(id)) return false;

            var civ = CivilizationLibrary.Get(id);
            bool draftDone = State.DraftedUniques.Count == GetRequiredDraftCount() && GetRequiredDraftCount() > 0;
            State.Civilization = id;
            State.Leader = civ.Leader;
            CivilizationRules.ApplyStartBonus(State);
            GameLog.Append(State, $"You lead {civ.Name} under {civ.LeaderName}: {civ.Passive}");
            if (draftDone)
            {
                BeginBoardSetup();
                return true;
            }

            State.Phase = GamePhase.RunSelectDraft;
            State.StatusMessage = $"{civ.LeaderName} leads {civ.Name}. Draft {GetRequiredDraftCount()} uniques.";
            NotifyChanged();
            return true;
        }

        private void BeginBoardSetup()
        {
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
            State.PioneerFreeRoadAvailable = LeaderTraits.HasArchetypePassive(State, LeaderId.Pioneer);
            State.Phase = GamePhase.NightRoll;

            int act = ActProgression.GetAct(State.Board.DayNumber);
            var (rollPasses, maxRoll) = ActProgression.GetYieldConfig(State.Board.DayNumber);
            State.TomorrowRolls = rollPasses > 1
                ? RollEngine.RollNightlyCombined(rollPasses, maxRoll)
                : RollEngine.RollNightly(maxRoll);
            State.TomorrowDiceRolls = DiceRollEngine.RollNightly(rollPasses);

            var eventId = Events.RollNightEvent(State, act);
            if (eventId != EventId.None)
                Events.ApplyEvent(State, eventId);

            ModifierService.ApplyNightUniques(State);

            if (Meta != null && !_metaStartCardGranted)
            {
                var bonusCard = Meta.GetStartBonusCard();
                if (bonusCard.HasValue)
                    State.PlayerHand.Add(bonusCard.Value);
                _metaStartCardGranted = true;
            }

            int draws = BalanceConfig.CardsDrawnPerNight;
            if (State.HasUnique(UniqueBuildingId.CaravanPost)) draws++;
            if (State.HasPerk(LevelUpPerkId.ExtraCardDraw)) draws++;
            draws += CivilizationRules.ExtraNightDraws(State);

            var humanCardPool = Meta?.GetCardPool().ToList();
            CardEngine.DrawToHand(State, PlayerId.Human, draws, humanPool: humanCardPool);
            CardEngine.DrawToHand(State, PlayerId.Ai, ActProgression.GetAiNightDraws(act), act);

            State.Phase = GamePhase.NightPlayCard;
            string actNote = act > 1 ? $" Act {act}." : "";
            string eventNote = State.ActiveEvent != EventId.None ? $" Event: {State.EventMessage}" : "";
            State.StatusMessage = "Night: review tomorrow's rolls. Play a card or continue." + actNote + eventNote;
            NotifyChanged();
        }

        private void ApplyActTransitions()
        {
            int act = ActProgression.GetAct(State.Board.DayNumber);
            var target = ActProgression.GetMapExpansionTarget(State.MapSize, act);
            if (target.HasValue && target.Value != State.MapSize)
            {
                int added = MapPresets.ExpandBoard(State.Board, target.Value, RunSeed);
                if (added > 0)
                {
                    State.MapSize = target.Value;
                    State.Ports = PortAccess.DiscoverPorts(State.Board);
                    State.ActUnlockMessage =
                        $"{ActProgression.GetActLabel(act)}: map +{added} hex → {MapPresets.GetDisplayName(target.Value)}";
                    OnBoardRebuilt?.Invoke();
                }
            }
            else if (act > 1 && string.IsNullOrEmpty(State.ActUnlockMessage))
            {
                State.ActUnlockMessage = ActProgression.GetActUnlockSummary(act);
            }
        }

        private void AdvanceFromNightCard()
        {
            State.Phase = GamePhase.NightAiPlan;
            Ai.ExecuteNightPlan(this);

            State.TodayRolls = new Dictionary<ResourceType, int>(State.TomorrowRolls);
            State.TodayDiceRolls = new List<int>(State.TomorrowDiceRolls);
            State.Phase = GamePhase.DayProduction;
            State.StatusMessage = "Press Roll (Space) to roll the dice.";
            NotifyChanged();
        }

        /// <summary>Applies morning production and opens the player day. Call after roll presentation.</summary>
        public void ResolveDayProduction()
        {
            if (State.Phase != GamePhase.DayProduction)
                return;

            string morning = LeaderTraits.OnMorning(State);
            if (!string.IsNullOrEmpty(morning))
                GameLog.Append(State, morning);

            if (SevenRollRules.RolledSeven(State.TodayDiceRolls))
            {
                GameLog.Append(State, "Rolled 7 — no production.");
                string civSeven = CivilizationRules.OnSevenRolled(State, _random);
                if (!string.IsNullOrEmpty(civSeven))
                    GameLog.Append(State, civSeven);
                string leaderSeven = LeaderTraits.OnSevenRolled(State);
                if (!string.IsNullOrEmpty(leaderSeven))
                    GameLog.Append(State, leaderSeven);
                State.PendingSevenDiscard = SevenRollRules.GetDiscardCount(State.PlayerInventory.Total);
                if (State.PendingSevenDiscard > 0)
                {
                    State.Phase = GamePhase.DayDiscardForSeven;
                    State.StatusMessage = $"Discard {State.PendingSevenDiscard} resources (hand > 7).";
                    NotifyChanged();
                    return;
                }

                BeginRobberPlacementAfterSeven();
                return;
            }

            ApplyProduction();
            LogProductionGains();
            OpenPlayerDay();
        }

        public bool SubmitSevenDiscard(ResourceBundle discard)
        {
            if (State.Phase != GamePhase.DayDiscardForSeven)
                return false;
            if (!SevenRollRules.ValidateDiscardTotal(State.PendingSevenDiscard, discard))
                return false;

            var inv = State.PlayerInventory;
            inv.Pay(discard);
            State.PlayerInventory = inv;
            State.PendingSevenDiscard = 0;
            GameLog.Append(State, $"Discarded {discard.Total} cards for seven.");
            BeginRobberPlacementAfterSeven();
            return true;
        }

        public bool PlaceRobberForSeven(HexCoord tile)
        {
            if (State.Phase != GamePhase.DayPlaceRobberSeven)
                return false;
            if (!State.Board.TryGetTile(tile, out _))
                return false;

            State.Board.PlaceRobber(tile);
            int stolen = RobberSteal.StealFromHex(State, tile, PlayerId.Human, _random);
            GameLog.Append(State, stolen > 0
                ? $"Robber placed — stole {stolen} resource(s)."
                : "Robber placed — no steal.");
            OpenPlayerDay();
            return true;
        }

        private void BeginRobberPlacementAfterSeven()
        {
            State.Phase = GamePhase.DayPlaceRobberSeven;
            State.StatusMessage = "Place the robber (click a hex).";
            NotifyChanged();
        }

        private void OpenPlayerDay()
        {
            State.ShopDeals = Shop.GenerateDailyDeals(State);
            State.Phase = GamePhase.DayPlayerActions;
            State.StatusMessage = "Day: build, trade, or end turn.";
            if (AiTradeOfferGenerator.TryCreateOffer(State, _random, out var incoming))
            {
                State.IncomingAiTrade = incoming;
                GameLog.Append(State, "AI sent a trade offer.");
            }
            OnAutosavePoint?.Invoke();
            NotifyChanged();
        }

        private void LogProductionGains()
        {
            var rolls = State.TodayRolls;
            var gained = ProductionCalculator.CalculateForPlayer(State, PlayerId.Human, rolls);
            foreach (var pair in gained.EnumerateNonZero())
                GameLog.Append(State, $"+{pair.amount} {pair.type} from production.");
        }

        public bool TryBankTrade(ResourceType give, ResourceType receive, int receiveAmount = 1)
        {
            if (State.Phase != GamePhase.DayPlayerActions || State.Winner.HasValue)
                return false;
            if (!BankTradeCalculator.CanTrade(State, PlayerId.Human, give, receive, receiveAmount))
            {
                HudToastBuffer.Push(State, "Ugyldigt bytte — tjek havn/bank-forhold.");
                NotifyChanged();
                return false;
            }

            if (!State.Bank.TryWithdraw(receive, receiveAmount))
            {
                HudToastBuffer.Push(State, "Banken har ikke nok af den ressource.");
                NotifyChanged();
                return false;
            }

            if (!BankTradeCalculator.TryExecute(State, PlayerId.Human, give, receive, receiveAmount))
            {
                State.Bank.Deposit(receive, receiveAmount);
                State.StatusMessage = "Trade failed.";
                NotifyChanged();
                return false;
            }

            int rate = BankTradeCalculator.GetGiveRateForResource(State, PlayerId.Human, give);
            int pay = rate * receiveAmount;
            State.Bank.Deposit(give, pay);

            GameLog.Append(State, $"Bank trade: {pay} {give} → {receiveAmount} {receive}.");
            HudToastBuffer.Push(State, "Bankbytte gennemført.");
            NotifyChanged();
            return true;
        }

        public bool ProposePlayerTrade(PlayerTradeOffer offer)
        {
            if (State.Phase != GamePhase.DayPlayerActions)
                return false;
            if (!State.PlayerInventory.CanAfford(offer.Give))
                return false;

            State.PendingPlayerTrade = offer;
            State.PendingAiTradeResponses = PlayerTradeResolver.EvaluateAiResponses(State, offer);
            GameLog.Append(State, "Trade offer sent to AI.");
            NotifyChanged();
            return true;
        }

        public bool AcceptPlayerTradeWith(PlayerId acceptor)
        {
            if (!State.PendingPlayerTrade.HasValue)
                return false;
            var offer = State.PendingPlayerTrade.Value;
            if (!PlayerTradeResolver.TryExecute(State, acceptor, offer))
                return false;

            State.PendingPlayerTrade = null;
            State.PendingAiTradeResponses = Array.Empty<AiTradeResponse>();
            GameLog.Append(State, "Player trade completed.");
            NotifyChanged();
            return true;
        }

        public bool RespondToIncomingAiTrade(bool accept)
        {
            if (!State.IncomingAiTrade.HasValue)
                return false;
            var offer = State.IncomingAiTrade.Value;
            if (!accept)
            {
                State.IncomingAiTrade = null;
                GameLog.Append(State, "Declined AI trade.");
                NotifyChanged();
                return true;
            }

            if (!State.PlayerInventory.CanAfford(offer.Receive))
            {
                HudToastBuffer.Push(State, "Du har ikke råd til AI-buddet.");
                NotifyChanged();
                return false;
            }

            State.IncomingAiTrade = null;
            var human = State.PlayerInventory;
            human.Pay(offer.Receive);
            human.Add(offer.Give);
            State.PlayerInventory = human;
            var ai = State.AiInventory;
            ai.Pay(offer.Give);
            ai.Add(offer.Receive);
            State.AiInventory = ai;
            GameLog.Append(State, "Accepted AI trade.");
            NotifyChanged();
            return true;
        }

        private void ApplyProduction()
        {
            var playerProd = ProductionCalculator.CalculateForPlayer(State, PlayerId.Human, State.TodayRolls);
            var aiProd = ProductionCalculator.CalculateForPlayer(State, PlayerId.Ai, State.TodayRolls);

            playerProd.Add(ProductionCalculator.ApplyEventModifiers(State,
                CivilizationRules.DiceBonus(State, PlayerId.Human, State.TodayDiceRolls)));
            playerProd.Add(ProductionCalculator.ApplyEventModifiers(State,
                LeaderTraits.DiceBonus(State, PlayerId.Human, State.TodayDiceRolls)));

            var pInv = State.PlayerInventory;
            pInv.Add(playerProd);
            State.PlayerInventory = pInv;

            var aInv = State.AiInventory;
            aInv.Add(aiProd);
            State.AiInventory = aInv;

            if (FishermenRules.IsEnabled(State) && !State.EventFishStorm)
            {
                int caught = FishProductionCalculator.CalculateForPlayer(
                    State, PlayerId.Human, State.TodayDiceRolls);
                State.PlayerFish += caught + CivilizationRules.FishCatchBonus(State, PlayerId.Human, caught);
                State.AiFish += FishProductionCalculator.CalculateForPlayer(
                    State, PlayerId.Ai, State.TodayDiceRolls);
            }
        }

        public bool TrySpendFish(FishermenSpendAction action) =>
            FishermenFishActions.TrySpend(State, PlayerId.Human, action);

        private void GrantSetupBonus(Vertex vertex, PlayerId player)
        {
            var bonus = SetupBonusCalculator.CalculateForVertex(State.Board, vertex);
            if (bonus.Total == 0) return;

            var inv = State.GetInventory(player);
            inv.Add(bonus);
            State.SetInventory(player, inv);
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
                if (State.Phase == GamePhase.SetupAiRoad1)
                {
                    State.Phase = GamePhase.SetupAiSettlement2;
                    State.StatusMessage = "AI places second settlement...";
                    RunAiSetupStep();
                    return;
                }

                if (State.Phase == GamePhase.SetupAiRoad2)
                {
                    State.Phase = GamePhase.SetupPlayerSettlement1;
                    State.StatusMessage = "Place your first settlement.";
                }
            }
            else
            {
                if (State.Phase == GamePhase.SetupPlayerRoad2)
                {
                    BeginNight();
                    State.StatusMessage = "Setup complete. Night falls — review tomorrow's rolls.";
                }
                else if (State.Phase == GamePhase.SetupPlayerRoad1)
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

            // Master Builder: 25% off (0.75). Architect passive adds 10 pts → 35% off (0.65).
            float discount = State.PendingCard == CardId.MasterBuilder ? 0.75f : 1f;
            if (LeaderTraits.HasArchetypePassive(State, LeaderId.Architect) && State.PendingCard == CardId.MasterBuilder)
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

        private int GetRequiredDraftCount()
        {
            if (Meta != null)
                return Meta.GetDraftPickCount();

            return RunProgression.DraftPickCount;
        }
    }
}

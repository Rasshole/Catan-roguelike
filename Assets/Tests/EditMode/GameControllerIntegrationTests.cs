using System;
using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Victory;
using NUnit.Framework;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    /// <summary>
    /// Integration tests for GameController day/night loop wiring (EventEngine + CardEngine + AI).
    /// Does not duplicate unit coverage in EventEngineTests or CardEngineTests.
    /// </summary>
    public class GameControllerIntegrationTests
    {
        private const int Seed = 7;

        private static LeaderId PickLeader(int seed)
        {
            var all = (LeaderId[])Enum.GetValues(typeof(LeaderId));
            return all[Math.Abs(seed) % all.Length];
        }

        private static void CompleteRunSetup(GameController game, int seed)
        {
            game.SelectMap(MapSize.Small);
            game.SelectLeader(PickLeader(seed));

            var uniques = (UniqueBuildingId[])Enum.GetValues(typeof(UniqueBuildingId));
            int u0 = Math.Abs(seed) % uniques.Length;
            int u1 = (u0 + 1) % uniques.Length;
            game.ToggleDraftUnique(uniques[u0]);
            game.ToggleDraftUnique(uniques[u1]);
            game.ConfirmRunSetup();

            while (game.State.IsSetupPhase)
                AdvanceSetupStep(game);
        }

        private static void AdvanceSetupStep(GameController game)
        {
            switch (game.State.Phase)
            {
                case GamePhase.SetupAiSettlement1:
                case GamePhase.SetupAiSettlement2:
                case GamePhase.SetupAiRoad1:
                case GamePhase.SetupAiRoad2:
                    game.RunAiSetupStep();
                    break;

                case GamePhase.SetupPlayerSettlement1:
                case GamePhase.SetupPlayerSettlement2:
                    if (!TryPlaceFirstValidSettlement(game, PlayerId.Human))
                        throw new InvalidOperationException("no valid player settlement in " + game.State.Phase);
                    break;

                case GamePhase.SetupPlayerRoad1:
                case GamePhase.SetupPlayerRoad2:
                    if (!TryPlaceFirstValidRoad(game, PlayerId.Human))
                        throw new InvalidOperationException("no valid player road in " + game.State.Phase);
                    break;

                default:
                    throw new InvalidOperationException("unexpected setup phase " + game.State.Phase);
            }
        }

        private static bool TryPlaceFirstValidSettlement(GameController game, PlayerId player)
        {
            foreach (var vertex in game.Placement.GetValidSettlementSpots(
                         game.State.Board, player, game.State.IsSetupPhase))
            {
                if (game.PlaceSettlement(vertex, player))
                    return true;
            }
            return false;
        }

        private static bool TryPlaceFirstValidRoad(GameController game, PlayerId player)
        {
            foreach (var edge in game.Placement.GetValidRoadSpots(
                         game.State.Board, player, game.State.IsSetupPhase))
            {
                if (game.PlaceRoad(edge, player))
                    return true;
            }
            return false;
        }

        private static void PrepareNightAdvance(GameController game)
        {
            // MatchDriver: AI night-plan reads TodayRolls before AdvanceFromNightCard copies them.
            if (game.State.TodayRolls.Count == 0 && game.State.TomorrowRolls.Count > 0)
                game.State.TodayRolls = new Dictionary<ResourceType, int>(game.State.TomorrowRolls);
        }

        private static void SkipNightAndEndDay(GameController game)
        {
            if (game.State.Phase == GamePhase.NightPlayCard)
            {
                PrepareNightAdvance(game);
                game.SkipNightCard();
            }

            if (game.State.Phase == GamePhase.LevelUpChoice)
            {
                if (game.State.PendingLevelUpChoices.Count > 0)
                    game.ChooseLevelUpPerk(game.State.PendingLevelUpChoices[0]);
                return;
            }

            if (game.State.Phase == GamePhase.DayPlayerActions)
                game.EndPlayerDay();
        }

        private static void CompleteDayCycle(GameController game)
        {
            SkipNightAndEndDay(game);
            if (game.State.Phase == GamePhase.LevelUpChoice)
                SkipNightAndEndDay(game);
        }

        private static GameController CreateGamePastSetup(int seed = Seed)
        {
            var game = new GameController(seed, MapSize.Small);
            CompleteRunSetup(game, seed);
            Assert.AreEqual(GamePhase.NightPlayCard, game.State.Phase,
                "setup should finish by entering the first night");
            return game;
        }

        [Timeout(5000)]
        [Test]
        public void SeededRun_SetupThroughThreeDays_DoesNotHang()
        {
            var game = CreateGamePastSetup(Seed);

            for (int day = 0; day < 3; day++)
            {
                Assert.AreEqual(GamePhase.NightPlayCard, game.State.Phase,
                    $"expected night before day cycle {day}");
                Assert.Greater(game.State.TomorrowRolls.Count, 0,
                    "nightly rolls should be populated");

                CompleteDayCycle(game);

                if (game.State.Phase == GamePhase.LevelUpChoice)
                {
                    CompleteDayCycle(game);
                    continue;
                }

                if (game.State.Phase == GamePhase.GameOver)
                    break;

                Assert.AreEqual(GamePhase.NightPlayCard, game.State.Phase,
                    $"expected next night after day {day + 1}");
                Assert.GreaterOrEqual(game.State.Board.DayNumber, day + 1);
            }

            Assert.AreNotEqual(GamePhase.RunSelectMap, game.State.Phase);
            Assert.IsFalse(game.State.IsSetupPhase);
        }

        [Test]
        public void SkipNightCard_AdvancesThroughProductionToDayPlayerActions()
        {
            var game = CreateGamePastSetup(Seed);
            int handBefore = game.State.PlayerHand.Count;

            PrepareNightAdvance(game);
            game.SkipNightCard();

            Assert.AreEqual(GamePhase.DayPlayerActions, game.State.Phase);
            Assert.Greater(game.State.TodayRolls.Count, 0);
            CollectionAssert.AreEquivalent(game.State.TomorrowRolls, game.State.TodayRolls);
            Assert.GreaterOrEqual(game.State.ShopDeals.Count, 1);
            Assert.AreEqual(handBefore, game.State.PlayerHand.Count,
                "skipping night should not remove cards from hand");
        }

        [Test]
        public void PlayPlayerCard_FromNightPlayCard_AdvancesToDayPlayerActions()
        {
            var game = CreateGamePastSetup(Seed);
            game.State.PlayerHand.Clear();
            game.State.PlayerHand.Add(CardId.YearOfPlenty);
            int woodBefore = game.State.PlayerInventory.Wood;

            PrepareNightAdvance(game);
            Assert.IsTrue(game.PlayPlayerCard(CardId.YearOfPlenty, ResourceType.Wood));

            Assert.AreEqual(GamePhase.DayPlayerActions, game.State.Phase);
            Assert.Greater(game.State.PlayerInventory.Wood, woodBefore);
            CollectionAssert.DoesNotContain(game.State.PlayerHand, CardId.YearOfPlenty);
        }

        [Test]
        public void EndPlayerDay_AtTenVictoryPoints_TransitionsToGameOverHumanWin()
        {
            var game = CreateGamePastSetup(Seed);
            CompleteDayCycle(game);

            Assert.AreEqual(GamePhase.NightPlayCard, game.State.Phase);
            game.State.Phase = GamePhase.DayPlayerActions;

            VictoryCalculator.RefreshVictoryPoints(game.State);
            int need = BalanceConfig.VictoryPointGoal - game.State.PlayerVictoryPoints;
            Assert.Greater(need, 0, "test expects player below win threshold before bonus VP");
            game.State.AddVictoryPoints(PlayerId.Human, need);

            game.EndPlayerDay();

            Assert.AreEqual(GamePhase.GameOver, game.State.Phase);
            Assert.AreEqual(PlayerId.Human, game.State.Winner);
            Assert.GreaterOrEqual(game.State.PlayerVictoryPoints, BalanceConfig.VictoryPointGoal);
            Assert.That(game.State.StatusMessage, Does.Contain("win"));
        }

        [Test]
        public void EndPlayerDay_OnDayFive_OffersLevelUpChoice()
        {
            var game = CreateGamePastSetup(Seed);

            for (int i = 0; i < 3; i++)
            {
                PrepareNightAdvance(game);
                game.SkipNightCard();
                game.EndPlayerDay();
            }

            Assert.AreEqual(4, game.State.Board.DayNumber);
            Assert.AreEqual(GamePhase.NightPlayCard, game.State.Phase);

            PrepareNightAdvance(game);
            game.SkipNightCard();
            game.EndPlayerDay();

            Assert.AreEqual(5, game.State.Board.DayNumber);
            Assert.AreEqual(GamePhase.LevelUpChoice, game.State.Phase);
            Assert.IsTrue(RunProgression.ShouldOfferLevelUp(game.State));
            Assert.Greater(game.State.PendingLevelUpChoices.Count, 0);
        }

        [Test]
        public void EndPlayerDay_ClearsDisabledRoadsAcrossDayBoundary()
        {
            var game = CreateGamePastSetup(Seed);
            CompleteDayCycle(game);

            game.State.Phase = GamePhase.DayPlayerActions;
            var edge = new Edge(new Vertex(new HexCoord(0, 0), 0), new Vertex(new HexCoord(0, 0), 1));
            game.State.Board.DisabledRoads.Add(edge);
            game.Events.ApplyEvent(game.State, EventId.GoldRush);
            Assert.IsTrue(game.State.EventStoneDouble);
            Assert.AreEqual(1, game.State.Board.DisabledRoads.Count);

            game.EndPlayerDay();

            Assert.AreEqual(0, game.State.Board.DisabledRoads.Count);
            Assert.AreEqual(EventId.None, game.State.ActiveEvent);
            Assert.IsFalse(game.State.EventStoneDouble);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Save;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Turn;
using NUnit.Framework;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class SaveGameRoundTripTests
    {
        private const int RunSeed = 42;

        [Test]
        public void RoundTrip_PopulatedRunState_MatchesKeyFields()
        {
            var original = BuildPopulatedController();
            var json = SaveGame.Serialize(original);
            var loaded = SaveGame.LoadGame(json);

            Assert.AreEqual(RunSeed, loaded.RunSeed);
            AssertStatesEquivalent(original.State, loaded.State);
            Assert.AreEqual(original.GetLastPlacedSettlement(), loaded.GetLastPlacedSettlement());
        }

        [Test]
        public void RoundTrip_UnknownFormatVersion_Throws()
        {
            var json = SaveGame.Serialize(new GameController(RunSeed, MapSize.Small));
            json = json.Replace("\"formatVersion\": 1", "\"formatVersion\": 99");

            Assert.Throws<InvalidOperationException>(() => SaveGame.LoadGame(json));
        }

        private static GameController BuildPopulatedController()
        {
            var game = new GameController(RunSeed, MapSize.Small);
            CompleteRunSetup(game);

            Assert.Greater(game.State.Board.VertexBuildings.Count, 0);
            Assert.Greater(game.State.Board.Roads.Count, 0);
            game.State.Phase = GamePhase.DayPlayerActions;

            game.State.PlayerInventory = new ResourceBundle
            {
                Wood = 3,
                Brick = 2,
                Wheat = 1,
                Sheep = 4,
                Stone = 0
            };
            game.State.AiInventory = new ResourceBundle { Wood = 1, Brick = 1, Wheat = 2, Sheep = 0, Stone = 1 };

            game.State.TomorrowRolls = new Dictionary<ResourceType, int>
            {
                [ResourceType.Wood] = 2,
                [ResourceType.Brick] = 0,
                [ResourceType.Wheat] = 1,
                [ResourceType.Sheep] = 0,
                [ResourceType.Stone] = 1
            };
            game.State.TodayRolls = new Dictionary<ResourceType, int>
            {
                [ResourceType.Wood] = 1,
                [ResourceType.Brick] = 1,
                [ResourceType.Wheat] = 0,
                [ResourceType.Sheep] = 2,
                [ResourceType.Stone] = 0
            };
            game.State.TomorrowDiceRolls = new List<int> { 6, 8 };
            game.State.TodayDiceRolls = new List<int> { 5, 9 };

            game.State.PlayerHand.Clear();
            game.State.PlayerHand.Add(CardId.Knight);
            game.State.PlayerHand.Add(CardId.RoadBuilder);
            game.State.AiHand.Clear();
            game.State.AiHand.Add(CardId.Embargo);

            game.State.ShopDeals = new List<ShopDeal>
            {
                new ShopDeal(ResourceType.Wood, 4, ResourceType.Brick, 1),
                new ShopDeal(ResourceType.Wheat, 2, ResourceType.Sheep, 1, true, "robber")
            };

            game.State.PlayerVictoryPoints = 4;
            game.State.AiVictoryPoints = 3;
            game.State.PlayerBonusVictoryPoints = 1;
            game.State.AiBonusVictoryPoints = 0;
            game.State.PlayerKnightsPlayed = 3;
            game.State.AiKnightsPlayed = 1;
            game.State.LargestArmyOwner = PlayerId.Human;
            game.State.PendingCard = CardId.MasterBuilder;
            game.State.HarborCharterPending = true;
            game.State.PlayerShopEmbargo = ResourceType.Wheat;
            game.State.PlayerEmbargoDaysLeft = 2;
            game.State.AcquiredPerks.Add(LevelUpPerkId.ExtraCardDraw);
            game.State.LevelUpsTaken = 1;
            game.State.LastLevelUpDay = 5;
            game.State.ActiveEvent = EventId.Storm;
            game.State.EventMessage = "Storm incoming";
            game.State.EventStormTile = new HexCoord(0, 0);
            game.State.EventBlockedPortVertex = VertexGraph.Canonicalize(
                new Vertex(new HexCoord(1, 0), 2));
            game.State.EventStoneDouble = true;
            game.State.EventShopBonus = 1;
            game.State.Phase = GamePhase.DayPlayerActions;
            game.State.StatusMessage = "Mid-run snapshot";
            game.State.Board.DayNumber = 7;

            var robberTile = game.State.Board.Tiles.Keys.First();
            game.State.Board.PlaceRobber(robberTile);

            var disabledRoads = game.State.Board.Roads.Keys.ToList();
            if (disabledRoads.Count > 0)
                game.State.Board.DisabledRoads.Add(disabledRoads[0]);

            return game;
        }

        private static void CompleteRunSetup(GameController game)
        {
            game.SelectMap(MapSize.Small);
            game.SelectLeader(LeaderId.Merchant);

            var uniques = (UniqueBuildingId[])Enum.GetValues(typeof(UniqueBuildingId));
            game.ToggleDraftUnique(uniques[0]);
            game.ToggleDraftUnique(uniques[1]);
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
                    var settlement = game.GetValidSettlements(PlayerId.Human).First();
                    Assert.IsTrue(game.PlaceSettlement(settlement, PlayerId.Human));
                    break;

                case GamePhase.SetupPlayerRoad1:
                case GamePhase.SetupPlayerRoad2:
                    var road = game.GetValidRoads(PlayerId.Human).First();
                    Assert.IsTrue(game.PlaceRoad(road, PlayerId.Human));
                    break;

                default:
                    throw new InvalidOperationException("unexpected setup phase " + game.State.Phase);
            }
        }

        private static void AssertStatesEquivalent(GameState expected, GameState actual)
        {
            Assert.AreEqual(expected.MapSize, actual.MapSize);
            Assert.AreEqual(expected.Phase, actual.Phase);
            AssertResourceBundlesEqual(expected.PlayerInventory, actual.PlayerInventory);
            AssertResourceBundlesEqual(expected.AiInventory, actual.AiInventory);
            AssertRollsEqual(expected.TomorrowRolls, actual.TomorrowRolls);
            AssertRollsEqual(expected.TodayRolls, actual.TodayRolls);
            CollectionAssert.AreEqual(expected.TomorrowDiceRolls, actual.TomorrowDiceRolls);
            CollectionAssert.AreEqual(expected.TodayDiceRolls, actual.TodayDiceRolls);
            CollectionAssert.AreEquivalent(expected.PlayerHand, actual.PlayerHand);
            CollectionAssert.AreEquivalent(expected.AiHand, actual.AiHand);
            AssertShopDealsEqual(expected.ShopDeals, actual.ShopDeals);
            AssertPortsEqual(expected.Ports, actual.Ports);

            Assert.AreEqual(expected.PlayerVictoryPoints, actual.PlayerVictoryPoints);
            Assert.AreEqual(expected.AiVictoryPoints, actual.AiVictoryPoints);
            Assert.AreEqual(expected.PlayerBonusVictoryPoints, actual.PlayerBonusVictoryPoints);
            Assert.AreEqual(expected.AiBonusVictoryPoints, actual.AiBonusVictoryPoints);
            Assert.AreEqual(expected.PlayerKnightsPlayed, actual.PlayerKnightsPlayed);
            Assert.AreEqual(expected.AiKnightsPlayed, actual.AiKnightsPlayed);
            Assert.AreEqual(expected.LargestArmyOwner, actual.LargestArmyOwner);
            Assert.AreEqual(expected.Winner, actual.Winner);
            Assert.AreEqual(expected.StatusMessage, actual.StatusMessage);
            Assert.AreEqual(expected.PendingCard, actual.PendingCard);
            Assert.AreEqual(expected.HarborCharterPending, actual.HarborCharterPending);
            Assert.AreEqual(expected.AiShopEmbargo, actual.AiShopEmbargo);
            Assert.AreEqual(expected.AiEmbargoDaysLeft, actual.AiEmbargoDaysLeft);
            Assert.AreEqual(expected.PlayerShopEmbargo, actual.PlayerShopEmbargo);
            Assert.AreEqual(expected.PlayerEmbargoDaysLeft, actual.PlayerEmbargoDaysLeft);
            Assert.AreEqual(expected.Leader, actual.Leader);
            CollectionAssert.AreEquivalent(expected.DraftedUniques, actual.DraftedUniques);
            Assert.AreEqual(expected.RunSetupComplete, actual.RunSetupComplete);
            CollectionAssert.AreEquivalent(expected.AcquiredPerks, actual.AcquiredPerks);
            CollectionAssert.AreEquivalent(expected.PendingLevelUpChoices, actual.PendingLevelUpChoices);
            Assert.AreEqual(expected.LevelUpsTaken, actual.LevelUpsTaken);
            Assert.AreEqual(expected.LastLevelUpDay, actual.LastLevelUpDay);
            Assert.AreEqual(expected.PioneerFreeRoadAvailable, actual.PioneerFreeRoadAvailable);
            Assert.AreEqual(expected.FreeRoadCharges, actual.FreeRoadCharges);
            Assert.AreEqual(expected.FirstCityBuiltThisRun, actual.FirstCityBuiltThisRun);
            Assert.AreEqual(expected.MonasteryUsed, actual.MonasteryUsed);
            Assert.AreEqual(expected.ActiveEvent, actual.ActiveEvent);
            Assert.AreEqual(expected.EventMessage, actual.EventMessage);
            Assert.AreEqual(expected.EventStormTile, actual.EventStormTile);
            Assert.AreEqual(expected.EventBlockedPortVertex, actual.EventBlockedPortVertex);
            Assert.AreEqual(expected.EventStoneDouble, actual.EventStoneDouble);
            Assert.AreEqual(expected.EventShopBonus, actual.EventShopBonus);

            AssertBoardsEquivalent(expected.Board, actual.Board);
        }

        private static void AssertResourceBundlesEqual(ResourceBundle expected, ResourceBundle actual)
        {
            Assert.AreEqual(expected.Wood, actual.Wood);
            Assert.AreEqual(expected.Brick, actual.Brick);
            Assert.AreEqual(expected.Wheat, actual.Wheat);
            Assert.AreEqual(expected.Sheep, actual.Sheep);
            Assert.AreEqual(expected.Stone, actual.Stone);
        }

        private static void AssertRollsEqual(Dictionary<ResourceType, int> expected, Dictionary<ResourceType, int> actual)
        {
            foreach (ResourceType resource in Enum.GetValues(typeof(ResourceType)))
                Assert.AreEqual(expected.GetValueOrDefault(resource), actual.GetValueOrDefault(resource));
        }

        private static void AssertShopDealsEqual(List<ShopDeal> expected, List<ShopDeal> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].Give, actual[i].Give);
                Assert.AreEqual(expected[i].GiveAmount, actual[i].GiveAmount);
                Assert.AreEqual(expected[i].Receive, actual[i].Receive);
                Assert.AreEqual(expected[i].ReceiveAmount, actual[i].ReceiveAmount);
                Assert.AreEqual(expected[i].IsRisky, actual[i].IsRisky);
                Assert.AreEqual(expected[i].RiskDescription, actual[i].RiskDescription);
            }
        }

        private static void AssertPortsEqual(List<PortDefinition> expected, List<PortDefinition> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].Vertex, actual[i].Vertex);
                Assert.AreEqual(expected[i].SpecificResource, actual[i].SpecificResource);
            }
        }

        private static void AssertBoardsEquivalent(BoardState expected, BoardState actual)
        {
            Assert.AreEqual(expected.DayNumber, actual.DayNumber);
            Assert.AreEqual(expected.RobberTile, actual.RobberTile);
            Assert.AreEqual(expected.Tiles.Count, actual.Tiles.Count);
            foreach (var kv in expected.Tiles)
            {
                Assert.IsTrue(actual.Tiles.TryGetValue(kv.Key, out var actualTile));
                var expectedTile = kv.Value;
                Assert.AreEqual(expectedTile.Resource, actualTile.Resource);
                Assert.AreEqual(expectedTile.HasRobber, actualTile.HasRobber);
                Assert.AreEqual(expectedTile.Building, actualTile.Building);
                Assert.AreEqual(expectedTile.Owner, actualTile.Owner);
                Assert.AreEqual(expectedTile.VertexIndex, actualTile.VertexIndex);
                Assert.AreEqual(expectedTile.IsCoastal, actualTile.IsCoastal);
                Assert.AreEqual(expectedTile.IsDesert, actualTile.IsDesert);
                Assert.AreEqual(expectedTile.NumberToken, actualTile.NumberToken);
            }

            Assert.AreEqual(expected.Roads.Count, actual.Roads.Count);
            foreach (var kv in expected.Roads)
            {
                Assert.IsTrue(actual.Roads.TryGetValue(kv.Key, out var owner));
                Assert.AreEqual(kv.Value, owner);
            }

            Assert.AreEqual(expected.VertexBuildings.Count, actual.VertexBuildings.Count);
            foreach (var kv in expected.VertexBuildings)
            {
                Assert.IsTrue(actual.VertexBuildings.TryGetValue(kv.Key, out var building));
                Assert.AreEqual(kv.Value.type, building.type);
                Assert.AreEqual(kv.Value.owner, building.owner);
            }

            CollectionAssert.AreEquivalent(expected.DisabledRoads.ToList(), actual.DisabledRoads.ToList());
        }
    }
}

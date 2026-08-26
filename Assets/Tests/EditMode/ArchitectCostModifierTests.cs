using CatanRoguelike.Core;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class ArchitectCostModifierTests
    {
        private static readonly ResourceBundle SampleCost = new()
        {
            Wood = 10,
            Brick = 10,
            Wheat = 10,
            Sheep = 10,
            Stone = 10
        };

        private static GameState CreateState(LeaderId leader, int humanSettlements = 0, int humanCities = 0)
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var state = new GameState(board) { Leader = leader };
            PlaceBuildings(board, BuildingType.Settlement, PlayerId.Human, humanSettlements);
            PlaceBuildings(board, BuildingType.City, PlayerId.Human, humanCities);
            return state;
        }

        [Test]
        public void Architect_NonThresholdSettlement_IsFullPrice()
        {
            var state = CreateState(LeaderId.Architect, humanSettlements: 4);

            var result = ModifierService.ApplyLeaderCostModifiers(
                state, PlayerId.Human, SampleCost, isSettlement: true, isCity: false, isRoad: false);

            AssertBundleEqual(SampleCost, result);
        }

        [Test]
        public void Architect_Road_IsFullPrice()
        {
            var state = CreateState(LeaderId.Architect, humanSettlements: 10);

            var result = ModifierService.ApplyLeaderCostModifiers(
                state, PlayerId.Human, SampleCost, isSettlement: false, isCity: false, isRoad: true);

            AssertBundleEqual(SampleCost, result);
        }

        [Test]
        public void Architect_City_IsFullPrice()
        {
            var state = CreateState(LeaderId.Architect, humanCities: 5);

            var result = ModifierService.ApplyLeaderCostModifiers(
                state, PlayerId.Human, SampleCost, isSettlement: false, isCity: true, isRoad: false);

            AssertBundleEqual(SampleCost, result);
        }

        [Test]
        public void Architect_ThresholdSettlement_IsTenPercentOffThresholdedCost()
        {
            var state = CreateState(LeaderId.Architect, humanSettlements: 5);
            var thresholded = BalanceConfig.GetSettlementCost(
                state.Board, PlayerId.Human, ModifierService.GetSettlementThreshold(state));

            var result = ModifierService.ApplyLeaderCostModifiers(
                state, PlayerId.Human, thresholded, isSettlement: true, isCity: false, isRoad: false);

            AssertBundleEqual(ExpectedTenPercentOff(thresholded), result);
        }

        [Test]
        public void NonArchitect_ThresholdSettlement_IsFullThresholdedCost()
        {
            var state = CreateState(LeaderId.Merchant, humanSettlements: 5);
            var thresholded = BalanceConfig.GetSettlementCost(
                state.Board, PlayerId.Human, ModifierService.GetSettlementThreshold(state));

            var result = ModifierService.ApplyLeaderCostModifiers(
                state, PlayerId.Human, thresholded, isSettlement: true, isCity: false, isRoad: false);

            AssertBundleEqual(thresholded, result);
        }

        [Test]
        public void MasterBuilder_NonArchitect_PaysSeventyFivePercent()
        {
            var game = new GameController(seed: 42);
            game.SelectMap(MapSize.Small);
            game.State.Leader = LeaderId.Merchant;
            game.State.PendingCard = CardId.MasterBuilder;

            var effective = game.GetEffectiveCost(PlayerId.Human, SampleCost);

            AssertBundleEqual(Scale(SampleCost, 0.75f), effective);
        }

        [Test]
        public void MasterBuilder_Architect_PaysSixtyFivePercent_NotDoubleDiscounted()
        {
            var game = new GameController(seed: 42);
            game.SelectMap(MapSize.Small);
            game.State.Leader = LeaderId.Architect;
            game.State.PendingCard = CardId.MasterBuilder;

            var effective = game.GetEffectiveCost(PlayerId.Human, SampleCost);

            AssertBundleEqual(Scale(SampleCost, 0.65f), effective,
                "Architect Master Builder should be 35% off (0.65), not blanket 10% plus 0.65");
        }

        [Test]
        public void MasterBuilder_Architect_Road_UsesOnlyMasterBuilderDiscount()
        {
            var game = new GameController(seed: 42);
            game.SelectMap(MapSize.Small);
            game.State.Leader = LeaderId.Architect;
            game.State.PendingCard = CardId.MasterBuilder;

            var baseCost = ModifierService.ApplyLeaderCostModifiers(
                game.State, PlayerId.Human, SampleCost, isSettlement: false, isCity: false, isRoad: true);
            var effective = game.GetEffectiveCost(PlayerId.Human, baseCost);

            AssertBundleEqual(Scale(SampleCost, 0.65f), effective);
        }

        private static ResourceBundle ExpectedTenPercentOff(ResourceBundle cost) =>
            new()
            {
                Wood = CeilDiscount(cost.Wood, 0.1f),
                Brick = CeilDiscount(cost.Brick, 0.1f),
                Wheat = CeilDiscount(cost.Wheat, 0.1f),
                Sheep = CeilDiscount(cost.Sheep, 0.1f),
                Stone = CeilDiscount(cost.Stone, 0.1f)
            };

        private static ResourceBundle Scale(ResourceBundle cost, float factor) =>
            new()
            {
                Wood = CeilDiscount(cost.Wood, 1f - factor),
                Brick = CeilDiscount(cost.Brick, 1f - factor),
                Wheat = CeilDiscount(cost.Wheat, 1f - factor),
                Sheep = CeilDiscount(cost.Sheep, 1f - factor),
                Stone = CeilDiscount(cost.Stone, 1f - factor)
            };

        private static int CeilDiscount(int value, float percent) =>
            value == 0 ? 0 : System.Math.Max(1, (int)System.Math.Ceiling(value * (1f - percent)));

        private static void PlaceBuildings(BoardState board, BuildingType type, PlayerId player, int count)
        {
            int placed = 0;
            foreach (var hex in board.Tiles.Keys)
            {
                for (int c = 0; c < 6 && placed < count; c++)
                {
                    var vertex = VertexGraph.Canonicalize(new Vertex(hex, c));
                    if (board.VertexBuildings.ContainsKey(vertex))
                        continue;
                    board.VertexBuildings[vertex] = (type, player);
                    placed++;
                }
            }

            Assert.AreEqual(count, placed, $"Could not place {count} {type} buildings on test board");
        }

        private static void AssertBundleEqual(ResourceBundle expected, ResourceBundle actual, string message = null)
        {
            Assert.AreEqual(expected.Wood, actual.Wood, message);
            Assert.AreEqual(expected.Brick, actual.Brick, message);
            Assert.AreEqual(expected.Wheat, actual.Wheat, message);
            Assert.AreEqual(expected.Sheep, actual.Sheep, message);
            Assert.AreEqual(expected.Stone, actual.Stone, message);
        }
    }
}

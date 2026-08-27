using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class SetupBonusTests
    {
        private static GameController CreateReadyForSetup(int seed = 7)
        {
            var game = new GameController(seed: seed, MapSize.Small);
            game.SelectMap(MapSize.Small);
            game.SelectLeader(LeaderId.Merchant);
            game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
            game.ToggleDraftUnique(UniqueBuildingId.GuildHall);
            game.State.RunSetupComplete = true;
            return game;
        }

        private static Vertex FirstValidSettlement(GameController game, PlayerId player)
        {
            return game.Placement
                .GetValidSettlementSpots(game.State.Board, player, setupPhase: true)
                .First();
        }

        [Test]
        public void SetupBonusCalculator_AdjacentHexes_GrantOneEach()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var vertex = VertexGraph.Canonicalize(new Vertex(new HexCoord(0, 0), 0));

            var bonus = SetupBonusCalculator.CalculateForVertex(board, vertex);

            Assert.Greater(bonus.Total, 0);
            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (!board.TryGetTile(hex, out var tile) || tile.IsDesert) continue;
                Assert.Greater(bonus[tile.Resource], 0,
                    $"Expected at least one {tile.Resource} from adjacent hex {hex}");
            }
        }

        [Test]
        public void SetupBonusCalculator_DesertTile_Skipped()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var desertHex = new HexCoord(1, 0);
            var desertResource = board.GetTile(desertHex).Resource;
            board.Tiles[desertHex].IsDesert = true;

            var vertex = VertexGraph.Canonicalize(new Vertex(desertHex, 1));
            var bonus = SetupBonusCalculator.CalculateForVertex(board, vertex);

            Assert.AreEqual(0, bonus[desertResource],
                "Desert tile should not contribute its resource to setup bonus");
        }

        [Test]
        public void LargeMap_CenterDesert_IsMarkedAndSkipped()
        {
            var board = MapPresets.CreateBoard(MapSize.Large);
            var center = new HexCoord(0, 0);

            Assert.IsTrue(board.GetTile(center).IsDesert);

            var vertex = VertexGraph.Canonicalize(new Vertex(center, 0));
            var bonus = SetupBonusCalculator.CalculateForVertex(board, vertex);

            Assert.AreEqual(0, bonus[ResourceType.Stone],
                "Large-map center desert should not grant stone in setup bonus");
        }

        [Test]
        public void FirstSettlement_DoesNotGrantResources_Player()
        {
            var game = CreateReadyForSetup();
            game.State.Phase = GamePhase.SetupPlayerSettlement1;

            var vertex = FirstValidSettlement(game, PlayerId.Human);
            Assert.IsTrue(game.PlaceSettlement(vertex, PlayerId.Human));
            Assert.AreEqual(0, game.State.PlayerInventory.Total);
        }

        [Test]
        public void SecondSettlement_GrantsAdjacentResources_Player()
        {
            var game = CreateReadyForSetup();
            game.State.Phase = GamePhase.SetupPlayerSettlement2;

            var vertex = FirstValidSettlement(game, PlayerId.Human);
            var expected = SetupBonusCalculator.CalculateForVertex(game.State.Board, vertex);

            Assert.IsTrue(game.PlaceSettlement(vertex, PlayerId.Human));
            Assert.AreEqual(expected.Total, game.State.PlayerInventory.Total);
            Assert.Greater(expected.Total, 0);
        }

        [Timeout(2000)]
        [Test]
        public void SecondSettlement_GrantsAdjacentResources_Ai()
        {
            var game = CreateReadyForSetup(seed: 42);
            game.ConfirmRunSetup();

            Assert.AreEqual(GamePhase.SetupPlayerSettlement1, game.State.Phase);
            Assert.Greater(game.State.AiInventory.Total, 0,
                "AI second settlement should grant setup bonus from adjacent tiles");
        }

        [Timeout(2000)]
        [Test]
        public void AiSetupBonus_OnlyFromSecondSettlement_NotFirst()
        {
            var game = CreateReadyForSetup(seed: 42);
            game.ConfirmRunSetup();

            var aiVertices = game.State.Board.VertexBuildings
                .Where(kv => kv.Value.owner == PlayerId.Ai && kv.Value.type == BuildingType.Settlement)
                .Select(kv => kv.Key)
                .ToList();
            Assert.AreEqual(2, aiVertices.Count);

            var bonusFirst = SetupBonusCalculator.CalculateForVertex(game.State.Board, aiVertices[0]);
            var bonusSecond = SetupBonusCalculator.CalculateForVertex(game.State.Board, aiVertices[1]);
            var combined = AddBundles(bonusFirst, bonusSecond);

            Assert.IsTrue(
                BundlesEqual(game.State.AiInventory, bonusFirst)
                || BundlesEqual(game.State.AiInventory, bonusSecond),
                "AI inventory should match exactly one settlement's setup bonus");
            Assert.IsFalse(
                BundlesEqual(game.State.AiInventory, combined),
                "First settlement must not contribute setup bonus");
        }

        [Test]
        public void SecondSettlement_SkipsDesertTile()
        {
            var game = CreateReadyForSetup();
            var desertHex = new HexCoord(1, 0);
            game.State.Board.Tiles[desertHex].IsDesert = true;
            var desertResource = game.State.Board.GetTile(desertHex).Resource;

            game.State.Phase = GamePhase.SetupPlayerSettlement2;
            var vertex = VertexGraph.Canonicalize(new Vertex(desertHex, 1));
            Assert.IsTrue(game.Placement.CanPlaceSettlement(
                game.State.Board, vertex, PlayerId.Human, setupPhase: true));

            var expected = SetupBonusCalculator.CalculateForVertex(game.State.Board, vertex);
            Assert.IsTrue(game.PlaceSettlement(vertex, PlayerId.Human));
            Assert.AreEqual(expected.Total, game.State.PlayerInventory.Total);
            Assert.AreEqual(0, game.State.PlayerInventory[desertResource]);
        }

        private static ResourceBundle AddBundles(ResourceBundle a, ResourceBundle b)
        {
            var sum = a;
            sum.Add(b);
            return sum;
        }

        private static bool BundlesEqual(ResourceBundle a, ResourceBundle b) =>
            a.Wood == b.Wood && a.Brick == b.Brick && a.Wheat == b.Wheat
            && a.Sheep == b.Sheep && a.Stone == b.Stone;
    }
}

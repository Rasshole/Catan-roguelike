using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Turn;
using NUnit.Framework;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class AiControllerSetupRoadTests
    {
        private const int Seed = 42;

        private static GameController CreateReadyForAiSetup(int seed = Seed)
        {
            var game = new GameController(seed: seed, MapSize.Small);
            game.SelectMap(MapSize.Small);
            game.SelectLeader(LeaderId.Merchant);
            game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
            game.ToggleDraftUnique(UniqueBuildingId.GuildHall);
            game.State.RunSetupComplete = true;
            return game;
        }

        private static Vertex GetAiFirstSetupSettlementChoice(int seed)
        {
            var scratch = CreateReadyForAiSetup(seed);
            var before = scratch.State.Board.VertexBuildings.Keys.ToHashSet();
            scratch.State.Phase = GamePhase.SetupAiSettlement1;
            scratch.Ai.PlaceSetupSettlement(scratch);

            return scratch.State.Board.VertexBuildings
                .Where(kv => kv.Value.owner == PlayerId.Ai && kv.Value.type == BuildingType.Settlement)
                .Where(kv => !before.Contains(kv.Key))
                .Select(kv => kv.Key)
                .First();
        }

        private static Vertex PlaceAiFirstSettlementOnly(GameController game)
        {
            var settlement = GetAiFirstSetupSettlementChoice(game.RunSeed);
            game.State.Board.VertexBuildings[settlement] = (BuildingType.Settlement, PlayerId.Ai);
            game.State.Phase = GamePhase.SetupAiRoad1;
            return settlement;
        }

        [Test]
        public void PlaceSetupRoad_AfterSetupSettlement_PlacesValidRoadTouchingSettlement()
        {
            var game = CreateReadyForAiSetup();
            var settlement = PlaceAiFirstSettlementOnly(game);

            var validRoads = game.Placement
                .GetValidRoadSpots(game.State.Board, PlayerId.Ai, setupPhase: true)
                .ToList();
            Assert.Greater(validRoads.Count, 0, "Expected at least one setup road touching the AI settlement");

            var roadsBefore = game.State.Board.Roads.Keys.ToHashSet();
            game.Ai.PlaceSetupRoad(game);

            var placedRoad = game.State.Board.Roads
                .Where(kv => kv.Value == PlayerId.Ai && !roadsBefore.Contains(kv.Key))
                .Select(kv => kv.Key)
                .First();
            CollectionAssert.Contains(validRoads, placedRoad);
            Assert.IsTrue(
                VertexGraph.Canonicalize(placedRoad.A).Equals(settlement)
                || VertexGraph.Canonicalize(placedRoad.B).Equals(settlement),
                "Setup road must touch the AI settlement");
        }

        [Test]
        public void PlaceSetupRoad_SameSeed_PicksSameEdge()
        {
            Edge firstRun = RunFirstSetupRoad(Seed);
            Edge secondRun = RunFirstSetupRoad(Seed);

            Assert.AreEqual(firstRun, secondRun);
        }

        [Test]
        public void PlaceSetupRoad_NoAiSettlement_NoOpsWithoutThrowing()
        {
            var game = CreateReadyForAiSetup();
            game.State.Phase = GamePhase.SetupAiRoad1;

            Assert.DoesNotThrow(() => game.Ai.PlaceSetupRoad(game));
            Assert.AreEqual(0, game.State.Board.CountRoads(PlayerId.Ai));
        }

        [Test]
        public void PlaceSetupRoad_NoValidAdjacentEdges_NoOpsWithoutThrowing()
        {
            var game = CreateReadyForAiSetup();
            var settlement = PlaceAiFirstSettlementOnly(game);

            foreach (var adjacent in VertexGraph.GetAdjacentVertices(settlement))
            {
                var edge = VertexGraph.GetEdgeBetween(settlement, adjacent);
                game.State.Board.Roads[edge] = PlayerId.Human;
            }

            int roadCountBefore = game.State.Board.Roads.Count;
            Assert.AreEqual(
                0,
                game.Placement
                    .GetValidRoadSpots(game.State.Board, PlayerId.Ai, setupPhase: true)
                    .Count());

            Assert.DoesNotThrow(() => game.Ai.PlaceSetupRoad(game));
            Assert.AreEqual(roadCountBefore, game.State.Board.Roads.Count);
            Assert.AreEqual(0, game.State.Board.CountRoads(PlayerId.Ai));
        }

        private static Edge RunFirstSetupRoad(int seed)
        {
            var game = CreateReadyForAiSetup(seed);
            PlaceAiFirstSettlementOnly(game);
            var roadsBefore = game.State.Board.Roads.Keys.ToHashSet();
            game.Ai.PlaceSetupRoad(game);
            return game.State.Board.Roads
                .Where(kv => kv.Value == PlayerId.Ai && !roadsBefore.Contains(kv.Key))
                .Select(kv => kv.Key)
                .First();
        }
    }
}

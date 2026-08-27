using System.Collections.Generic;
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
    public class AiControllerSetupSettlementTests
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

        private static Vertex PlaceAiFirstSetupSettlement(GameController game)
        {
            var before = game.State.Board.VertexBuildings.Keys.ToHashSet();
            game.State.Phase = GamePhase.SetupAiSettlement1;
            game.Ai.PlaceSetupSettlement(game);
            return game.State.Board.VertexBuildings
                .Where(kv => kv.Value.owner == PlayerId.Ai && kv.Value.type == BuildingType.Settlement)
                .Where(kv => !before.Contains(kv.Key))
                .Select(kv => kv.Key)
                .First();
        }

        [Test]
        public void PlaceSetupSettlement_AfterSetupAiSettlement1_PlacesExactlyOneValidSetupSpot()
        {
            var game = CreateReadyForAiSetup();
            game.State.Phase = GamePhase.SetupAiSettlement1;

            var validSpots = game.Placement
                .GetValidSettlementSpots(game.State.Board, PlayerId.Ai, setupPhase: true)
                .ToList();
            Assert.Greater(validSpots.Count, 0, "Expected at least one valid setup settlement spot");

            var vertex = PlaceAiFirstSetupSettlement(game);

            CollectionAssert.Contains(validSpots, vertex);
            Assert.AreEqual(1, game.State.Board.CountBuildings(PlayerId.Ai, BuildingType.Settlement));
            Assert.AreEqual(0, game.State.Board.CountBuildings(PlayerId.Human, BuildingType.Settlement));
        }

        [Test]
        public void PlaceSetupSettlement_ChosenSpot_SatisfiesCatanDistanceRule()
        {
            var game = CreateReadyForAiSetup();
            var occupied = VertexGraph.Canonicalize(
                game.Placement
                    .GetValidSettlementSpots(game.State.Board, PlayerId.Human, setupPhase: true)
                    .First());
            game.State.Board.VertexBuildings[occupied] = (BuildingType.Settlement, PlayerId.Human);
            game.State.Phase = GamePhase.SetupAiSettlement1;

            var validSpots = game.Placement
                .GetValidSettlementSpots(game.State.Board, PlayerId.Ai, setupPhase: true)
                .ToList();
            Assert.Greater(validSpots.Count, 0, "Blocking one vertex should still leave AI setup spots");

            var vertex = PlaceAiFirstSetupSettlement(game);

            CollectionAssert.Contains(validSpots, vertex);
            Assert.GreaterOrEqual(
                VertexGraph.VertexDistance(vertex, occupied),
                2,
                "Catan distance rule: AI settlement cannot sit on or adjacent to an existing building");
        }

        [Test]
        public void PlaceSetupSettlement_SameSeed_PicksSameVertex()
        {
            // Scoring is pure (no Random in PlaceSetupSettlement); seed still matters
            // because map tokens and tile layout come from GameController(seed).
            Vertex firstRun = RunFirstSetupSettlement(Seed);
            Vertex secondRun = RunFirstSetupSettlement(Seed);

            Assert.AreEqual(firstRun, secondRun);
        }

        [Test]
        public void PlaceSetupSettlement_NoValidSpots_NoOpsWithoutThrowing()
        {
            var game = CreateReadyForAiSetup();
            game.State.Phase = GamePhase.SetupAiSettlement1;
            OccupyUntilNoValidSetupSpots(game);

            Assert.AreEqual(
                0,
                game.Placement
                    .GetValidSettlementSpots(game.State.Board, PlayerId.Ai, setupPhase: true)
                    .Count());

            int buildingCountBefore = game.State.Board.VertexBuildings.Count;
            Assert.DoesNotThrow(() => game.Ai.PlaceSetupSettlement(game));
            Assert.AreEqual(buildingCountBefore, game.State.Board.VertexBuildings.Count);
            Assert.AreEqual(0, game.State.Board.CountBuildings(PlayerId.Ai, BuildingType.Settlement));
        }

        [Test]
        public void PlaceSetupSettlement_MultipleSpots_PicksHighestScore()
        {
            var game = CreateReadyForAiSetup();
            game.State.Phase = GamePhase.SetupAiSettlement1;

            var validSpots = game.Placement
                .GetValidSettlementSpots(game.State.Board, PlayerId.Ai, setupPhase: true)
                .ToList();
            Assert.Greater(validSpots.Count, 1, "Need multiple spots so scoring can choose");

            var expected = validSpots
                .Select(v => (vertex: v, score: ScoreSettlementSpotLikeAi(game, v)))
                .OrderByDescending(x => x.score)
                .First()
                .vertex;

            var placed = PlaceAiFirstSetupSettlement(game);
            Assert.AreEqual(expected, placed);
        }

        private static Vertex RunFirstSetupSettlement(int seed)
        {
            var game = CreateReadyForAiSetup(seed);
            return PlaceAiFirstSetupSettlement(game);
        }

        private static void OccupyUntilNoValidSetupSpots(GameController game)
        {
            while (true)
            {
                var remaining = game.Placement
                    .GetValidSettlementSpots(game.State.Board, PlayerId.Ai, setupPhase: true)
                    .ToList();
                if (remaining.Count == 0)
                    return;

                game.State.Board.VertexBuildings[remaining[0]] = (BuildingType.Settlement, PlayerId.Human);
            }
        }

        /// <summary>
        /// Mirrors <c>AiController.ScoreSettlementSpot</c> so tests can assert the public
        /// placement result without exposing the private scorer.
        /// </summary>
        private static float ScoreSettlementSpotLikeAi(GameController game, Vertex vertex)
        {
            float score = 0;
            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (!game.State.Board.TryGetTile(hex, out var tile)) continue;
                if (tile.IsDesert || !tile.NumberToken.HasValue) continue;
                score += ResourceValueLikeAi(tile.Resource);
                score += NumberTokenLibrary.GetPipWeight(tile.NumberToken.Value);
                if (game.State.TodayRolls.TryGetValue(tile.Resource, out int roll))
                    score += roll;
            }
            return score;
        }

        private static int ResourceValueLikeAi(ResourceType r) => r switch
        {
            ResourceType.Wheat => 3,
            ResourceType.Stone => 3,
            ResourceType.Wood => 2,
            ResourceType.Brick => 2,
            ResourceType.Sheep => 2,
            _ => 1
        };
    }
}

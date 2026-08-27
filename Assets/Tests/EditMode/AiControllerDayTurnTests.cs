using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    /// <summary>
    /// EditMode coverage for <see cref="AiController.ExecuteDayTurn"/> build/robber paths.
    /// Shop buy/skip and night-card strategy live in other suites.
    /// </summary>
    public class AiControllerDayTurnTests
    {
        private const int Seed = 42;
        private static readonly HexCoord BrickHex = new HexCoord(1, 0);
        private static readonly HexCoord SheepHex = new HexCoord(0, -1);

        private static GameController CreateDayTurnGame(int seed = Seed, int dayNumber = 1)
        {
            var game = new GameController(seed: seed, MapSize.Small);
            game.SelectMap(MapSize.Small);
            game.State.Phase = GamePhase.DayPlayerActions;
            game.State.RunSetupComplete = true;
            game.State.Board.DayNumber = dayNumber;
            return game;
        }

        private static List<Vertex> PlaceAiSettlementsAtSetupSpots(GameController game, int count)
        {
            var spots = game.Placement
                .GetValidSettlementSpots(game.State.Board, PlayerId.Ai, setupPhase: true)
                .Take(count)
                .ToList();
            Assert.GreaterOrEqual(spots.Count, count, "Need enough disjoint setup spots for injection");

            var placed = new List<Vertex>();
            foreach (var spot in spots)
            {
                game.State.Board.VertexBuildings[spot] = (BuildingType.Settlement, PlayerId.Ai);
                placed.Add(spot);
            }

            return placed;
        }

        private static void PlaceAiExpansionAnchor(
            GameController game,
            BuildingType anchorType = BuildingType.Settlement,
            int roadSegments = 3)
        {
            var hex = new HexCoord(0, 0);
            var anchor = VertexGraph.Canonicalize(new Vertex(hex, 0));
            game.State.Board.VertexBuildings[anchor] = (anchorType, PlayerId.Ai);

            var vertices = new List<Vertex>();
            for (int corner = 0; corner <= roadSegments; corner++)
                vertices.Add(VertexGraph.Canonicalize(new Vertex(hex, corner)));

            for (int i = 0; i < vertices.Count - 1; i++)
            {
                var edge = VertexGraph.GetEdgeBetween(vertices[i], vertices[i + 1]);
                game.State.Board.Roads[edge] = PlayerId.Ai;
            }
        }

        private static void PlaceTwoSettlementsOnHex(BoardState board, HexCoord hex, PlayerId player)
        {
            var corner0 = VertexGraph.Canonicalize(new Vertex(hex, 0));
            var corner3 = VertexGraph.Canonicalize(new Vertex(hex, 3));
            board.VertexBuildings[corner0] = (BuildingType.Settlement, player);
            board.VertexBuildings[corner3] = (BuildingType.Settlement, player);
        }

        private static void PlaceSettlementOnHex(BoardState board, HexCoord hex, PlayerId player, int corner = 0)
        {
            var vertex = VertexGraph.Canonicalize(new Vertex(hex, corner));
            board.VertexBuildings[vertex] = (BuildingType.Settlement, player);
        }

        [Test]
        public void ExecuteDayTurn_Act1_UpgradesOnlyOneAffordableSettlement()
        {
            var game = CreateDayTurnGame(dayNumber: 1);
            var placed = PlaceAiSettlementsAtSetupSpots(game, 3);
            game.State.AiInventory = new ResourceBundle { Wheat = 10, Stone = 20 };

            game.Ai.ExecuteDayTurn(game);

            Assert.AreEqual(1, game.State.Board.CountBuildings(PlayerId.Ai, BuildingType.City));
            Assert.AreEqual(2, game.State.Board.CountBuildings(PlayerId.Ai, BuildingType.Settlement));
            Assert.AreEqual(BuildingType.City, game.State.Board.VertexBuildings[placed[0]].type);
            Assert.AreEqual(BuildingType.Settlement, game.State.Board.VertexBuildings[placed[1]].type);
            Assert.AreEqual(BuildingType.Settlement, game.State.Board.VertexBuildings[placed[2]].type);
            Assert.AreEqual(1, ActProgression.GetAct(game.State.Board.DayNumber));
        }

        [Test]
        public void ExecuteDayTurn_Act2_UpgradesEveryAffordableSettlement()
        {
            var game = CreateDayTurnGame(dayNumber: BalanceConfig.Act2StartDay);
            var placed = PlaceAiSettlementsAtSetupSpots(game, 3);
            game.State.AiInventory = new ResourceBundle { Wheat = 10, Stone = 20 };

            game.Ai.ExecuteDayTurn(game);

            Assert.AreEqual(3, game.State.Board.CountBuildings(PlayerId.Ai, BuildingType.City));
            Assert.AreEqual(0, game.State.Board.CountBuildings(PlayerId.Ai, BuildingType.Settlement));
            foreach (var vertex in placed)
                Assert.AreEqual(BuildingType.City, game.State.Board.VertexBuildings[vertex].type);
            Assert.AreEqual(2, ActProgression.GetAct(game.State.Board.DayNumber));
        }

        [Test]
        public void ExecuteDayTurn_WhenNoCityUpgrade_PlacesHighestScoredSettlement()
        {
            var game = CreateDayTurnGame();
            PlaceAiExpansionAnchor(game);

            var validSpots = game.Placement
                .GetValidSettlementSpots(game.State.Board, PlayerId.Ai, setupPhase: false)
                .ToList();
            Assert.Greater(validSpots.Count, 1, "Need multiple expansion spots to exercise scoring");

            var expected = validSpots
                .Select(v => (vertex: v, score: ScoreSettlementSpotLikeAi(game, v)))
                .OrderByDescending(x => x.score)
                .First()
                .vertex;

            game.State.AiInventory = new ResourceBundle { Wood = 1, Brick = 1, Wheat = 1, Sheep = 1 };
            int settlementsBefore = game.State.Board.CountBuildings(PlayerId.Ai, BuildingType.Settlement);

            game.Ai.ExecuteDayTurn(game);

            Assert.AreEqual(settlementsBefore + 1, game.State.Board.CountBuildings(PlayerId.Ai, BuildingType.Settlement));
            Assert.IsTrue(game.State.Board.VertexBuildings.ContainsKey(expected));
            Assert.AreEqual(BuildingType.Settlement, game.State.Board.VertexBuildings[expected].type);
            Assert.AreEqual(PlayerId.Ai, game.State.Board.VertexBuildings[expected].owner);
        }

        [Test]
        public void ExecuteDayTurn_PlacesHighestScoredRoad()
        {
            var game = CreateDayTurnGame(dayNumber: BalanceConfig.Act2StartDay);
            PlaceAiExpansionAnchor(game, BuildingType.City, roadSegments: 4);

            int act = ActProgression.GetAct(game.State.Board.DayNumber);
            var validRoads = game.Placement
                .GetValidRoadSpots(game.State.Board, PlayerId.Ai, setupPhase: false)
                .ToList();
            Assert.Greater(validRoads.Count, 1, "Need multiple road spots to exercise scoring");

            var expected = validRoads
                .Select(e => (edge: e, score: ScoreRoadLikeAi(game, e, act)))
                .OrderByDescending(x => x.score)
                .First()
                .edge;

            game.State.AiInventory = new ResourceBundle { Wood = 1, Brick = 1 };
            int roadsBefore = game.State.Board.CountRoads(PlayerId.Ai);

            game.Ai.ExecuteDayTurn(game);

            Assert.AreEqual(roadsBefore + 1, game.State.Board.CountRoads(PlayerId.Ai));
            Assert.IsTrue(game.State.Board.Roads.ContainsKey(expected));
            Assert.AreEqual(PlayerId.Ai, game.State.Board.Roads[expected]);
        }

        [Test]
        public void ExecuteDayTurn_MovesRobberToBestHumanHex_AndSteals()
        {
            var game = CreateDayTurnGame();
            PlaceTwoSettlementsOnHex(game.State.Board, BrickHex, PlayerId.Human);
            PlaceSettlementOnHex(game.State.Board, SheepHex, PlayerId.Human);
            game.State.PlayerInventory = new ResourceBundle { Brick = 3 };
            int humanBefore = game.State.PlayerInventory.Total;

            game.Ai.ExecuteDayTurn(game);

            Assert.AreEqual(BrickHex, game.State.Board.RobberTile);
            Assert.Less(game.State.PlayerInventory.Total, humanBefore, "AI robber move should steal from human");
            Assert.Greater(game.State.AiInventory.Total, 0, "Stolen resource should land in AI inventory");
        }

        [Test]
        public void ExecuteDayTurn_NoHumanBuildings_DoesNotMoveRobber()
        {
            var game = CreateDayTurnGame();
            var robberBefore = game.State.Board.RobberTile;

            game.Ai.ExecuteDayTurn(game);

            Assert.AreEqual(robberBefore, game.State.Board.RobberTile);
        }

        [Test]
        public void ExecuteDayTurn_EmptyInventoryNoDealsNoNetwork_NoOpsWithoutThrowing()
        {
            var game = CreateDayTurnGame();
            game.State.AiInventory = ResourceBundle.Zero;
            game.State.ShopDeals.Clear();

            int buildingsBefore = game.State.Board.VertexBuildings.Count;
            int roadsBefore = game.State.Board.Roads.Count;

            Assert.DoesNotThrow(() => game.Ai.ExecuteDayTurn(game));
            Assert.AreEqual(buildingsBefore, game.State.Board.VertexBuildings.Count);
            Assert.AreEqual(roadsBefore, game.State.Board.Roads.Count);
        }

        /// <summary>
        /// Mirrors private <c>AiController.ScoreSettlementSpot</c>.
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

        /// <summary>
        /// Mirrors private <c>AiController.ScoreRoad</c>.
        /// </summary>
        private static float ScoreRoadLikeAi(GameController game, Edge edge, int act)
        {
            float score = 1f;
            float blockWeight = act >= 2 ? 3f : 2f;
            foreach (var v in new[] { edge.A, edge.B })
            {
                var humanSpots = game.Placement
                    .GetValidSettlementSpots(game.State.Board, PlayerId.Human, false)
                    .Count(s => VertexGraph.VertexDistance(s, v) <= 2);
                score += humanSpots * blockWeight;
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

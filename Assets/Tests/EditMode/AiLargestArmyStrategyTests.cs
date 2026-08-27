using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    /// <summary>
    /// AI prefers Knight when close to Largest Army or when human holds it and AI can overtake.
    /// </summary>
    public class AiLargestArmyStrategyTests
    {
        [Test]
        public void ExecuteNightPlan_PrefersKnightWhenOneAwayFromThreshold()
        {
            var game = CreateGame();
            game.State.AiKnightsPlayed = 2;
            game.State.AiHand.Clear();
            game.State.AiHand.Add(CardId.MerchantsLedger);
            game.State.AiHand.Add(CardId.Knight);

            var ai = new AiController(42);
            ai.ExecuteNightPlan(game);

            Assert.AreEqual(3, game.State.AiKnightsPlayed);
            Assert.IsFalse(game.State.AiHand.Contains(CardId.Knight));
        }

        [Test]
        public void ExecuteNightPlan_PrefersKnightWhenHumanHoldsArmyAndAiCanOvertake()
        {
            var game = CreateGame();
            game.State.PlayerKnightsPlayed = 3;
            game.State.AiKnightsPlayed = 3;
            game.State.LargestArmyOwner = PlayerId.Human;
            game.State.AiHand.Clear();
            game.State.AiHand.Add(CardId.MerchantsLedger);
            game.State.AiHand.Add(CardId.Knight);

            var ai = new AiController(42);
            ai.ExecuteNightPlan(game);

            Assert.AreEqual(4, game.State.AiKnightsPlayed);
            Assert.AreEqual(PlayerId.Ai, game.State.LargestArmyOwner);
            Assert.IsFalse(game.State.AiHand.Contains(CardId.Knight));
        }

        private static GameController CreateGame()
        {
            var game = new GameController(seed: 42, MapSize.Small);
            SeedTodayRolls(game);
            PlaceHumanSettlementForRobberTarget(game);
            return game;
        }

        private static void SeedTodayRolls(GameController game)
        {
            game.State.TodayRolls = new Dictionary<ResourceType, int>
            {
                { ResourceType.Wheat, 2 },
                { ResourceType.Wood, 1 },
            };
        }

        private static void PlaceHumanSettlementForRobberTarget(GameController game)
        {
            var board = game.State.Board;
            foreach (var hex in board.Tiles.Keys)
            {
                for (int c = 0; c < 6; c++)
                {
                    var vertex = VertexGraph.Canonicalize(new Vertex(hex, c));
                    if (board.VertexBuildings.ContainsKey(vertex))
                        continue;

                    board.VertexBuildings[vertex] = (BuildingType.Settlement, PlayerId.Human);
                    return;
                }
            }

            Assert.Fail("No free vertex for human settlement");
        }
    }
}

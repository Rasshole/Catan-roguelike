using System;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Turn;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class GameControllerDayRoadPhaseTests
    {
        [Timeout(5000)]
        [Test]
        public void Seed5_PlacingRoadDuringDay_DoesNotRewindToSetupSettlement2()
        {
            var game = CompleteSetupLikeSimRunner(seed: 5);

            if (game.State.TodayRolls.Count == 0 && game.State.TomorrowRolls.Count > 0)
                game.State.TodayRolls = new System.Collections.Generic.Dictionary<ResourceType, int>(game.State.TomorrowRolls);
            game.SkipNightCard();

            Assert.AreEqual(GamePhase.DayPlayerActions, game.State.Phase);

            bool placed = false;
            foreach (var edge in game.Placement.GetValidRoadSpots(game.State.Board, PlayerId.Human, setupPhase: false))
            {
                if (game.PlaceRoad(edge, PlayerId.Human))
                {
                    placed = true;
                    break;
                }
            }

            Assert.IsTrue(placed, "Expected at least one buildable road on seed 5 day 1.");
            Assert.AreEqual(GamePhase.DayPlayerActions, game.State.Phase,
                "Day road placement must not rewind setup to settlement 2.");
        }

        private static GameController CompleteSetupLikeSimRunner(int seed)
        {
            var game = new GameController(seed, MapSize.Small);
            game.SelectMap(MapSize.Small);

            var leaders = (LeaderId[])Enum.GetValues(typeof(LeaderId));
            game.SelectLeader(leaders[Math.Abs(seed) % leaders.Length]);

            var uniques = (UniqueBuildingId[])Enum.GetValues(typeof(UniqueBuildingId));
            int u0 = Math.Abs(seed) % uniques.Length;
            int u1 = (u0 + 1) % uniques.Length;
            game.ToggleDraftUnique(uniques[u0]);
            game.ToggleDraftUnique(uniques[u1]);
            game.ConfirmRunSetup();

            while (game.State.Phase != GamePhase.NightPlayCard)
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
                        PlaceSetupSettlement1WithLookahead(game);
                        break;
                    case GamePhase.SetupPlayerSettlement2:
                        PlaceFirstSettlement(game);
                        break;
                    case GamePhase.SetupPlayerRoad1:
                    case GamePhase.SetupPlayerRoad2:
                        PlaceFirstRoad(game);
                        break;
                    default:
                        Assert.Fail("Unexpected phase during setup: " + game.State.Phase);
                        break;
                }
            }

            return game;
        }

        private static void PlaceSetupSettlement1WithLookahead(GameController game)
        {
            foreach (var vertex in game.Placement.GetValidSettlementSpots(game.State.Board, PlayerId.Human, true))
            {
                if (!LeavesSecondSetupSettlement(game.Placement, game.State.Board, vertex))
                    continue;

                if (game.PlaceSettlement(vertex, PlayerId.Human))
                    return;
            }

            Assert.Fail("No valid setup settlement 1 with lookahead.");
        }

        private static bool LeavesSecondSetupSettlement(PlacementValidator placement, BoardState board, Vertex first)
        {
            first = VertexGraph.Canonicalize(first);
            board.VertexBuildings[first] = (BuildingType.Settlement, PlayerId.Human);
            try
            {
                foreach (var _ in placement.GetValidSettlementSpots(board, PlayerId.Human, setupPhase: true))
                    return true;
                return false;
            }
            finally
            {
                board.VertexBuildings.Remove(first);
            }
        }

        private static void PlaceFirstSettlement(GameController game)
        {
            foreach (var vertex in game.Placement.GetValidSettlementSpots(game.State.Board, PlayerId.Human, true))
            {
                if (game.PlaceSettlement(vertex, PlayerId.Human))
                    return;
            }

            Assert.Fail("No valid setup settlement.");
        }

        private static void PlaceFirstRoad(GameController game)
        {
            foreach (var edge in game.Placement.GetValidRoadSpots(game.State.Board, PlayerId.Human, true))
            {
                if (game.PlaceRoad(edge, PlayerId.Human))
                    return;
            }

            Assert.Fail("No valid setup road.");
        }
    }
}

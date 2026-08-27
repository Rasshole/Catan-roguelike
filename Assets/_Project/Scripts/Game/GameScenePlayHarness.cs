using System;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Scripted run-select + setup driver shared by PlayMode harness tests and editor capture tools.
    /// Uses <see cref="GameController"/> public APIs only (no DebugHooks / GUI).
    /// </summary>
    public static class GameScenePlayHarness
    {
        public static void CompleteRunSelect(GameController controller, MetaProgression meta)
        {
            controller.SelectMap(MapSize.Small);
            if (controller.State.Phase != GamePhase.RunSelectLeader)
                throw new InvalidOperationException(
                    $"SelectMap should advance to leader select, got {controller.State.Phase}.");

            var leader = meta.GetAvailableLeaders().First();
            controller.SelectLeader(leader);
            if (controller.State.Phase != GamePhase.RunSelectDraft)
                throw new InvalidOperationException(
                    $"SelectLeader should advance to unique draft, got {controller.State.Phase}.");

            int pickCount = meta.GetDraftPickCount();
            var draftIds = meta.GetDraftPool().Take(pickCount).ToList();
            if (draftIds.Count < pickCount)
                throw new InvalidOperationException(
                    "meta draft pool should expose enough uniques for a fresh run.");

            foreach (var id in draftIds)
                controller.ToggleDraftUnique(id);

            controller.ConfirmRunSetup();
            if (!controller.State.RunSetupComplete)
                throw new InvalidOperationException("ConfirmRunSetup should mark run setup complete.");
        }

        public static void CompleteSetup(GameController controller)
        {
            int safety = 0;
            while (controller.State.IsSetupPhase && safety++ < 24)
                AdvanceSetupStep(controller);

            if (controller.State.IsSetupPhase)
                throw new InvalidOperationException(
                    "setup loop should finish within bounded steps (stall guard).");
        }

        public static void CompleteRunSelectAndSetup(GameController controller, MetaProgression meta)
        {
            CompleteRunSelect(controller, meta);
            CompleteSetup(controller);
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
            foreach (var vertex in game.GetValidSettlements(player))
            {
                if (game.PlaceSettlement(vertex, player))
                    return true;
            }
            return false;
        }

        private static bool TryPlaceFirstValidRoad(GameController game, PlayerId player)
        {
            foreach (var edge in game.GetValidRoads(player))
            {
                if (game.PlaceRoad(edge, player))
                    return true;
            }
            return false;
        }
    }
}

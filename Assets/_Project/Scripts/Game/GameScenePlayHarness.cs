using System;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Yield;
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
        /// <summary>Colonist capture / play harness uses standard base Catan (19 hex, desert center).</summary>
        public const MapSize DefaultHarnessMapSize = MapSize.Large;

        public static void CompleteRunSelect(
            GameController controller,
            MetaProgression meta,
            bool fishermenOfCatan = true,
            bool stopAtCivilizationPick = false,
            CivilizationId? civilization = null)
        {
            EnsureHarnessMapUnlocked(meta);
            controller.FishermenMapOption = fishermenOfCatan;
            controller.SelectMap(DefaultHarnessMapSize);
            if (controller.State.Phase == GamePhase.RunSelectCivilization)
            {
                // v134: civ (with its leader) is step 2, right after the map.
                if (stopAtCivilizationPick)
                    return;
                ChooseCivilizationForTest(controller, civilization);
            }
            else if (controller.State.Phase == GamePhase.RunSelectLeader)
            {
                controller.SelectLeader(meta.GetAvailableLeaders().First());
            }
            else
            {
                throw new InvalidOperationException(
                    $"SelectMap should advance to civilization or leader select, got {controller.State.Phase}.");
            }

            if (controller.State.Phase != GamePhase.RunSelectDraft)
                throw new InvalidOperationException(
                    $"Civ/leader pick should advance to unique draft, got {controller.State.Phase}.");

            int pickCount = meta.GetDraftPickCount();
            var draftIds = meta.GetDraftPool().Take(pickCount).ToList();
            if (draftIds.Count < pickCount)
                throw new InvalidOperationException(
                    "meta draft pool should expose enough uniques for a fresh run.");

            foreach (var id in draftIds)
                controller.ToggleDraftUnique(id);

            controller.ConfirmRunSetup();
            if (stopAtCivilizationPick && controller.State.Phase == GamePhase.RunSelectCivilization)
                return;
            if (controller.State.Phase == GamePhase.RunSelectCivilization)
                ChooseCivilizationForTest(controller, civilization);
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

        public static void CompleteRunSelectAndSetup(
            GameController controller,
            MetaProgression meta,
            bool fishermenOfCatan = true)
        {
            CompleteRunSelect(controller, meta, fishermenOfCatan);
            CompleteSetup(controller);
        }

        /// <summary>After setup: night skip → morning production → player day (handles 7 flow when needed).</summary>
        public static void AdvanceToDayPlayerActions(GameController controller)
        {
            if (controller.State.Phase == GamePhase.NightPlayCard)
                controller.SkipNightCard();

            int guard = 0;
            while (controller.State.Phase != GamePhase.DayPlayerActions && guard++ < 12)
            {
                switch (controller.State.Phase)
                {
                    case GamePhase.DayProduction:
                        controller.ResolveDayProduction();
                        break;
                    case GamePhase.DayDiscardForSeven:
                        if (!TrySubmitMinimalSevenDiscard(controller))
                            throw new InvalidOperationException("could not submit seven discard.");
                        break;
                    case GamePhase.DayPlaceRobberSeven:
                        if (!TryPlaceRobberForSeven(controller))
                            throw new InvalidOperationException("could not place robber after seven.");
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"AdvanceToDayPlayerActions stalled in {controller.State.Phase}.");
                }
            }

            if (controller.State.Phase != GamePhase.DayPlayerActions)
                throw new InvalidOperationException(
                    $"expected DayPlayerActions, got {controller.State.Phase}.");
        }

        private static bool TrySubmitMinimalSevenDiscard(GameController controller)
        {
            int need = controller.State.PendingSevenDiscard;
            if (need <= 0)
                return false;

            var inv = controller.State.PlayerInventory;
            var discard = new ResourceBundle();
            foreach (ResourceType type in ResourceTypes.Productive)
            {
                while (need > 0 && inv[type] > discard[type])
                {
                    discard.Set(type, discard[type] + 1);
                    need--;
                }
            }

            return controller.SubmitSevenDiscard(discard);
        }

        private static bool TryPlaceRobberForSeven(GameController controller)
        {
            foreach (var coord in controller.State.Board.Tiles.Keys)
            {
                if (controller.PlaceRobberForSeven(coord))
                    return true;
            }

            return false;
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

        /// <summary>Play/capture harness uses Base 19 — grant Large map unlock when meta would block SelectMap.</summary>
        private static void EnsureHarnessMapUnlocked(MetaProgression meta)
        {
            if (meta == null)
                return;

            var unlock = MetaCatalog.MapUnlockFor(DefaultHarnessMapSize);
            if (!unlock.HasValue || meta.IsMapAvailable(DefaultHarnessMapSize))
                return;

            meta.AddStarsForTesting(MetaCatalog.Get(unlock.Value).Cost);
            if (!meta.TryPurchase(unlock.Value))
                throw new InvalidOperationException(
                    $"Harness could not unlock {DefaultHarnessMapSize} for scripted SelectMap.");
        }

        /// <summary>Picks the requested civ (swapping it into the offer if the meta filter left it out) or offer[0].</summary>
        private static void ChooseCivilizationForTest(GameController controller, CivilizationId? civilization)
        {
            var offer = controller.State.CivilizationOffer;
            if (civilization.HasValue && !offer.Contains(civilization.Value))
                offer[0] = civilization.Value;
            if (!controller.ChooseCivilization(civilization ?? offer[0]))
                throw new System.InvalidOperationException($"ChooseCivilization({civilization}) was rejected.");
        }
    }
}

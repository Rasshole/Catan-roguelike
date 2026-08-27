using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests.PlayMode
{
    /// <summary>
    /// PlayMode harness: drive a short scripted run in the loaded Game.unity scene via
    /// GameManager.Controller public APIs (no DebugHooks / no GUI clicks).
    /// EditMode <see cref="CatanRoguelike.Tests.GameControllerIntegrationTests"/> covers the full loop.
    /// </summary>
    public class GameScenePlayTests
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string GameSceneName = "Game";

        private static readonly HashSet<GamePhase> SetupPhases = new()
        {
            GamePhase.SetupAiSettlement1,
            GamePhase.SetupAiRoad1,
            GamePhase.SetupAiSettlement2,
            GamePhase.SetupAiRoad2,
            GamePhase.SetupPlayerSettlement1,
            GamePhase.SetupPlayerRoad1,
            GamePhase.SetupPlayerSettlement2,
            GamePhase.SetupPlayerRoad2,
        };

        private static readonly HashSet<GamePhase> DayNightPhases = new()
        {
            GamePhase.NightRoll,
            GamePhase.NightPlayCard,
            GamePhase.NightAiPlan,
            GamePhase.DayProduction,
            GamePhase.DayPlayerActions,
            GamePhase.DayAiTurn,
            GamePhase.LevelUpChoice,
        };

        [Timeout(15000)]
        [UnityTest]
        public IEnumerator GameScene_ScriptedPreGame_AdvancesPastRunSelectIntoSetup()
        {
            if (!File.Exists(GameScenePath))
            {
                Assert.Fail(
                    $"Game scene not found at {GameScenePath}. " +
                    "Run Catan Roguelike → Setup Game Scene or pull a commit that includes Game.unity.");
            }

            var loadOp = SceneManager.LoadSceneAsync(GameSceneName, LoadSceneMode.Single);
            Assert.IsNotNull(loadOp, $"Failed to start loading scene '{GameSceneName}'. Is it in Build Settings?");
            yield return loadOp;

            for (int i = 0; i < 3; i++)
                yield return null;

            var gameManager = Object.FindFirstObjectByType<GameManager>();
            Assert.IsNotNull(gameManager, "GameManager should exist after scene load.");
            Assert.IsNotNull(gameManager.Controller, "Controller should be initialized after Start().");
            Assert.IsNotNull(gameManager.Meta, "Meta should be loaded after Start().");

            Assert.AreEqual(GamePhase.RunSelectMap, gameManager.Controller.State.Phase,
                "Fresh scene boot should begin at map select.");

            CompleteRunSelect(gameManager.Controller, gameManager.Meta);

            var phase = gameManager.Controller.State.Phase;
            CollectionAssert.Contains(SetupPhases, phase,
                $"Expected a setup phase after run select, got {phase}.");
            Assert.True(gameManager.Controller.State.RunSetupComplete,
                "Run setup flag should be set after ConfirmRunSetup.");
            Assert.AreEqual(gameManager.Controller.RunSeed, 42,
                "Game.unity serializes randomSeed=42 on GameManager.");
        }

        [Timeout(30000)]
        [UnityTest]
        public IEnumerator GameScene_ScriptedRun_CompletesSetupAndOneDayCycle()
        {
            if (!File.Exists(GameScenePath))
            {
                Assert.Fail(
                    $"Game scene not found at {GameScenePath}. " +
                    "Run Catan Roguelike → Setup Game Scene or pull a commit that includes Game.unity.");
            }

            var loadOp = SceneManager.LoadSceneAsync(GameSceneName, LoadSceneMode.Single);
            Assert.IsNotNull(loadOp, $"Failed to start loading scene '{GameSceneName}'. Is it in Build Settings?");
            yield return loadOp;

            for (int i = 0; i < 3; i++)
                yield return null;

            var gameManager = Object.FindFirstObjectByType<GameManager>();
            Assert.IsNotNull(gameManager, "GameManager should exist after scene load.");

            var game = gameManager.Controller;
            Assert.IsNotNull(game, "Controller should be initialized after Start().");

            CompleteRunSelect(game, gameManager.Meta);
            CompleteSetup(game);

            Assert.AreEqual(GamePhase.NightPlayCard, game.State.Phase,
                "setup should finish by entering the first night");
            Assert.IsNull(game.State.Winner, "no winner during setup");

            CompleteOneDayCycle(game);

            Assert.GreaterOrEqual(game.State.Board.DayNumber, 1,
                "one ended day should increment DayNumber to at least 1");
            Assert.IsNull(game.State.Winner,
                "short harness should not reach 10 VP");
            CollectionAssert.Contains(DayNightPhases, game.State.Phase,
                $"expected a day/night phase after one cycle, got {game.State.Phase}");
        }

        private static void CompleteRunSelect(GameController controller, MetaProgression meta)
        {
            controller.SelectMap(MapSize.Small);
            Assert.AreEqual(GamePhase.RunSelectLeader, controller.State.Phase,
                "SelectMap should advance to leader select.");

            var leader = meta.GetAvailableLeaders().First();
            controller.SelectLeader(leader);
            Assert.AreEqual(GamePhase.RunSelectDraft, controller.State.Phase,
                "SelectLeader should advance to unique draft.");

            int pickCount = meta.GetDraftPickCount();
            var draftIds = meta.GetDraftPool().Take(pickCount).ToList();
            Assert.GreaterOrEqual(draftIds.Count, pickCount,
                "meta draft pool should expose enough uniques for a fresh run.");

            foreach (var id in draftIds)
                controller.ToggleDraftUnique(id);

            controller.ConfirmRunSetup();
            Assert.True(controller.State.RunSetupComplete,
                "ConfirmRunSetup should mark run setup complete.");
        }

        private static void CompleteSetup(GameController game)
        {
            int safety = 0;
            while (game.State.IsSetupPhase && safety++ < 24)
                AdvanceSetupStep(game);

            Assert.False(game.State.IsSetupPhase,
                "setup loop should finish within bounded steps (stall guard).");
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

        private static void PrepareNightAdvance(GameController game)
        {
            if (game.State.TodayRolls.Count == 0 && game.State.TomorrowRolls.Count > 0)
                game.State.TodayRolls = new Dictionary<ResourceType, int>(game.State.TomorrowRolls);
            if (game.State.TodayDiceRolls.Count == 0 && game.State.TomorrowDiceRolls.Count > 0)
                game.State.TodayDiceRolls = new List<int>(game.State.TomorrowDiceRolls);
        }

        private static void CompleteOneDayCycle(GameController game)
        {
            PrepareNightAdvance(game);
            game.SkipNightCard();

            if (game.State.Phase == GamePhase.LevelUpChoice)
            {
                Assert.Greater(game.State.PendingLevelUpChoices.Count, 0,
                    "level-up choice should list perks when phase is LevelUpChoice");
                game.ChooseLevelUpPerk(game.State.PendingLevelUpChoices[0]);
            }

            Assert.AreEqual(GamePhase.DayPlayerActions, game.State.Phase,
                "skipping the first night should reach day player actions");

            game.EndPlayerDay();

            if (game.State.Phase == GamePhase.LevelUpChoice)
            {
                Assert.Greater(game.State.PendingLevelUpChoices.Count, 0,
                    "level-up after EndPlayerDay should list perks");
                game.ChooseLevelUpPerk(game.State.PendingLevelUpChoices[0]);
            }
        }
    }
}

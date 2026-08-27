using System.Collections;
using System.Collections.Generic;
using System.IO;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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

            var gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            Assert.IsNotNull(gameManager, "GameManager should exist after scene load.");
            Assert.IsNotNull(gameManager.Controller, "Controller should be initialized after Start().");
            Assert.IsNotNull(gameManager.Meta, "Meta should be loaded after Start().");

            Assert.AreEqual(GamePhase.RunSelectMap, gameManager.Controller.State.Phase,
                "Fresh scene boot should begin at map select.");

            GameScenePlayHarness.CompleteRunSelect(gameManager.Controller, gameManager.Meta);

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

            var gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            Assert.IsNotNull(gameManager, "GameManager should exist after scene load.");

            var game = gameManager.Controller;
            Assert.IsNotNull(game, "Controller should be initialized after Start().");

            GameScenePlayHarness.CompleteRunSelect(game, gameManager.Meta);
            GameScenePlayHarness.CompleteSetup(game);

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

using System.Collections;
using System.IO;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CatanRoguelike.Tests.PlayMode
{
    /// <summary>
    /// PlayMode smoke tests for Game.unity scene wiring (MonoBehaviours + boot).
    /// Core day/night logic is covered in EditMode — not duplicated here.
    /// </summary>
    public class GameSceneSmokeTests
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string GameSceneName = "Game";

        [UnityTest]
        public IEnumerator GameScene_LoadsWithRequiredComponents()
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

            Assert.AreEqual(GameSceneName, SceneManager.GetActiveScene().name,
                $"Expected active scene '{GameSceneName}' after load.");

            yield return null;
            yield return null;

            var gameManager = Object.FindFirstObjectByType<GameManager>();
            Assert.IsNotNull(gameManager, "GameManager MonoBehaviour should exist on a loaded scene object.");

            var boardView = Object.FindFirstObjectByType<BoardView>();
            Assert.IsNotNull(boardView, "BoardView MonoBehaviour should exist on a loaded scene object.");

            var placeholderUi = Object.FindFirstObjectByType<PlaceholderUI>();
            Assert.IsNotNull(placeholderUi, "PlaceholderUI MonoBehaviour should exist on a loaded scene object.");
        }

        [UnityTest]
        public IEnumerator GameScene_AfterStart_GameManagerHasControllerInRunSelectOrSetupPhase()
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

            Assert.IsNotNull(gameManager.Controller,
                "GameManager.Controller should be initialized after Start() (no immediate NRE).");
            Assert.IsNotNull(gameManager.Controller.State,
                "GameController.State should be non-null after boot.");

            var phase = gameManager.Controller.State.Phase;
            Assert.That(phase, Is.AnyOf(
                    GamePhase.RunSelectMap,
                    GamePhase.RunSelectLeader,
                    GamePhase.RunSelectDraft,
                    GamePhase.SetupAiSettlement1,
                    GamePhase.SetupAiRoad1,
                    GamePhase.SetupAiSettlement2,
                    GamePhase.SetupAiRoad2,
                    GamePhase.SetupPlayerSettlement1,
                    GamePhase.SetupPlayerRoad1,
                    GamePhase.SetupPlayerSettlement2,
                    GamePhase.SetupPlayerRoad2),
                $"Expected run-select or setup phase after boot, got {phase}.");
        }
    }
}

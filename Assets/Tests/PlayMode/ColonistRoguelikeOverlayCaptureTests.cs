using System.Collections;
using System.IO;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatanRoguelike.Tests.PlayMode
{
    /// <summary>
    /// v131: the civilization pick overlay and the event card render in the real Colonist scene.
    /// Set COLONIST_CIV_SHOT / COLONIST_EVENT_SHOT to a png path to write review captures.
    /// </summary>
    public class ColonistRoguelikeOverlayCaptureTests
    {
        private const string ColonistScenePath = "Assets/_Project/Scenes/ColonistStylePrototype.unity";
        private const string ColonistSceneName = "ColonistStylePrototype";

        private static IEnumerator LoadScene()
        {
            if (!File.Exists(ColonistScenePath))
                Assert.Inconclusive("Colonist prototype scene missing.");
            Screen.SetResolution(GameSceneCapture.CaptureWidth, GameSceneCapture.CaptureHeight, false);
            yield return null;
            var loadOp = SceneManager.LoadSceneAsync(ColonistSceneName, LoadSceneMode.Single);
            yield return loadOp;
            for (int i = 0; i < 4; i++)
                yield return null;
        }

        private static IEnumerator ReadyHud(GameManager gameManager, System.Action<RunHudController> done)
        {
            RunHudController hud = null;
            for (int i = 0; i < 120; i++)
            {
                gameManager.EnsureRunHudReady();
                hud = gameManager.GetComponent<RunHudController>();
                if (hud != null && hud.IsHudCaptureReady())
                    break;
                yield return null;
            }

            Assert.IsNotNull(hud);
            hud.Refresh(gameManager.Controller.State);
            Canvas.ForceUpdateCanvases();
            if (hud.HudCanvas != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(hud.HudCanvas.GetComponent<RectTransform>());
            for (int i = 0; i < 4; i++)
                yield return null;
            done(hud);
        }

        private static void MaybeWrite(RunHudController hud, string envVar)
        {
            string path = System.Environment.GetEnvironmentVariable(envVar);
            if (string.IsNullOrEmpty(path))
                return;
            var tex = GameSceneCapture.CaptureMainCameraWithHudComposite(hud);
            Assert.IsNotNull(tex);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"v131 capture written: {path}");
        }

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator CivilizationPick_ShowsThreeCards_AndChoosingStartsSetup()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            Assert.IsNotNull(gameManager?.Controller);
            Assert.IsTrue(gameManager.Controller.CivilizationPickEnabled);

            GameScenePlayHarness.CompleteRunSelect(gameManager.Controller, gameManager.Meta,
                fishermenOfCatan: true, stopAtCivilizationPick: true);
            Assert.AreEqual(GamePhase.RunSelectCivilization, gameManager.Controller.State.Phase);
            Assert.AreEqual(3, gameManager.Controller.State.CivilizationOffer.Count);

            RunHudController hud = null;
            yield return ReadyHud(gameManager, h => hud = h);
            Assert.IsTrue(hud.IsCivilizationPickVisibleForTests, "Civilization pick overlay should be visible");
            Assert.IsNotNull(ColonistRoguelikeArt.GetCardFrame(false), "Card frame art should load");
            foreach (var civ in CivilizationLibrary.Pool)
                Assert.IsNotNull(ColonistRoguelikeArt.GetCivilizationEmblem(civ.Id), civ.Name + " emblem");
            MaybeWrite(hud, "COLONIST_CIV_SHOT");

            var pick = gameManager.Controller.State.CivilizationOffer[0];
            Assert.IsTrue(gameManager.Controller.ChooseCivilization(pick));
            hud.Refresh(gameManager.Controller.State);
            Assert.IsFalse(hud.IsCivilizationPickVisibleForTests);
            Assert.IsTrue(gameManager.Controller.State.IsSetupPhase);
        }

        [Timeout(90000)]
        [UnityTest]
        public IEnumerator EventCard_ShowsAtNight_AndDismisses()
        {
            yield return LoadScene();
            var gameManager = Object.FindFirstObjectByType<GameManager>();
            var controller = gameManager.Controller;
            GameScenePlayHarness.CompleteRunSelect(controller, gameManager.Meta, fishermenOfCatan: true,
                civilization: CivilizationId.HighlandClans);
            GameScenePlayHarness.CompleteSetup(controller);
            Assert.AreEqual(CivilizationId.HighlandClans, controller.State.Civilization);

            if (controller.State.Phase != GamePhase.NightPlayCard)
                controller.BeginNight();
            Assert.AreEqual(GamePhase.NightPlayCard, controller.State.Phase);

            string eventName = System.Environment.GetEnvironmentVariable("COLONIST_EVENT_ID");
            var id = !string.IsNullOrEmpty(eventName) && System.Enum.TryParse<EventId>(eventName, out var parsed)
                ? parsed
                : EventId.Earthquake;
            controller.Events.ApplyEvent(controller.State, id);
            ColonistCapturePieceSeeding.ForceDemoBoardPiecesForCapture(controller.State);
            Object.FindFirstObjectByType<BoardView>()?.Refresh(controller.State);

            RunHudController hud = null;
            yield return ReadyHud(gameManager, h => hud = h);
            Assert.IsTrue(hud.IsEventCardVisibleForTests, "Event card should be visible at night");
            Assert.IsNotNull(ColonistRoguelikeArt.GetEventEmblem(id));
            MaybeWrite(hud, "COLONIST_EVENT_SHOT");

            controller.SkipNightCard();
            GameScenePlayHarness.AdvanceToDayPlayerActions(controller);
            hud.Refresh(controller.State);
            Assert.IsFalse(hud.IsEventCardVisibleForTests, "Event card hides once the day is under way");
        }
    }
}

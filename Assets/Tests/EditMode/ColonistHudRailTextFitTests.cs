using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Tests
{
    public class ColonistHudRailTextFitTests
    {
        [Test]
        public void ColonistRailHeadersAndPlayerLines_PreferredWidthFitsPanel()
        {
            bool wasColonist = ColonistPresentation.IsActive;
            var root = new GameObject("ColonistHudRailTextFit_Root");
            try
            {
                ColonistPresentation.SetActive(true);
                var hudGo = new GameObject("RunHud");
                hudGo.transform.SetParent(root.transform);
                var hud = hudGo.AddComponent<RunHudController>();

                var game = new GameController(11, MapSize.Small);
                game.SelectMap(MapSize.Small);
                game.SelectLeader(LeaderId.Merchant);
                game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
                game.ToggleDraftUnique(UniqueBuildingId.GuildHall);
                game.ConfirmRunSetup();
                game.State.Phase = GamePhase.DayPlayerActions;
                game.State.FishermenOfCatanEnabled = true;
                game.State.PlayerFish = 2;

                hud.Initialize(game, new MetaProgression(), null);
                hud.Refresh(game.State);
                Canvas.ForceUpdateCanvases();

                var canvas = hud.HudCanvas;
                Assert.IsNotNull(canvas, "Colonist HUD canvas should build.");

                ColonistHudTextFit.AssertAllColonistRailTextsFit(canvas.transform);

                var texts = new System.Collections.Generic.List<Text>();
                ColonistHudTextFit.CollectColonistRailHeaderTexts(canvas.transform, texts);
                Assert.GreaterOrEqual(texts.Count, 4, "Expected log/chat titles and both player body texts.");
            }
            finally
            {
                ColonistPresentation.SetActive(wasColonist);
                Object.DestroyImmediate(root);
            }
        }
    }
}

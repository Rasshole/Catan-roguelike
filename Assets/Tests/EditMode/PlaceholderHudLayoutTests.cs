using CatanRoguelike.Core.Turn;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class PlaceholderHudLayoutTests
    {
        [Test]
        public void GetPanelWidth_PreGame_Is400()
        {
            Assert.AreEqual(PlaceholderHudLayout.PreGamePanelWidth,
                PlaceholderHudLayout.GetPanelWidth(GamePhase.RunSelectMap));
            Assert.AreEqual(PlaceholderHudLayout.PreGamePanelWidth,
                PlaceholderHudLayout.GetPanelWidth(GamePhase.RunSelectLeader));
            Assert.AreEqual(PlaceholderHudLayout.PreGamePanelWidth,
                PlaceholderHudLayout.GetPanelWidth(GamePhase.RunSelectDraft));
        }

        [Test]
        public void GetPanelWidth_InRun_Is260()
        {
            Assert.AreEqual(PlaceholderHudLayout.InRunPanelWidth,
                PlaceholderHudLayout.GetPanelWidth(GamePhase.SetupPlayerSettlement1));
            Assert.AreEqual(PlaceholderHudLayout.InRunPanelWidth,
                PlaceholderHudLayout.GetPanelWidth(GamePhase.DayPlayerActions));
            Assert.AreEqual(PlaceholderHudLayout.InRunPanelWidth,
                PlaceholderHudLayout.GetPanelWidth(GamePhase.NightPlayCard));
        }

        [Test]
        public void GetPanelRect_InRun_HeightCappedOnTallScreens()
        {
            var rect = PlaceholderHudLayout.GetPanelRect(GamePhase.DayPlayerActions, 1920, 1080);
            Assert.AreEqual(PlaceholderHudLayout.InRunPanelWidth, rect.width);
            Assert.AreEqual(PlaceholderHudLayout.MaxInRunPanelHeight, rect.height);
            Assert.AreEqual(PlaceholderHudLayout.Margin, rect.x);
            Assert.AreEqual(PlaceholderHudLayout.Margin, rect.y);
        }

        [Test]
        public void GetPanelRect_PreGame_UsesFullAvailableHeight()
        {
            var rect = PlaceholderHudLayout.GetPanelRect(GamePhase.RunSelectMap, 1280, 900);
            Assert.AreEqual(PlaceholderHudLayout.PreGamePanelWidth, rect.width);
            Assert.AreEqual(880f, rect.height);
        }

        [Test]
        public void GetBoardScreenOffsetX_ShiftsRightOfScreenCenter()
        {
            float panelWidth = PlaceholderHudLayout.InRunPanelWidth;
            float hudLeft = PlaceholderHudLayout.Margin + panelWidth + PlaceholderHudLayout.Margin;
            float expected = hudLeft + (1920f - hudLeft) * 0.5f - 960f;

            float offset = PlaceholderHudLayout.GetBoardScreenOffsetX(panelWidth, 1920);
            Assert.Greater(offset, 0f);
            Assert.AreEqual(expected, offset, 0.01f);
        }

        [Test]
        public void GetBoardScreenOffsetX_WithRightRail_CentersBetweenChrome()
        {
            float panelWidth = 0f;
            float rightWidth = RunHudLayout.RightChromePanelWidth;
            float hudLeft = PlaceholderHudLayout.Margin + panelWidth + PlaceholderHudLayout.Margin;
            float hudRight = PlaceholderHudLayout.Margin + rightWidth + PlaceholderHudLayout.Margin;
            float expected = hudLeft + (1920f - hudLeft - hudRight) * 0.5f - 960f;

            float offset = PlaceholderHudLayout.GetBoardScreenOffsetX(panelWidth, 1920, rightWidth);
            Assert.AreEqual(expected, offset, 0.01f);
        }

        [Test]
        public void IsPreGamePhase_OnlyRunSelectPhases()
        {
            var preGamePhases = new[] { GamePhase.RunSelectMap, GamePhase.RunSelectLeader, GamePhase.RunSelectDraft };
            CollectionAssert.Contains(preGamePhases, GamePhase.RunSelectLeader);
            Assert.IsTrue(PlaceholderHudLayout.IsPreGamePhase(GamePhase.RunSelectMap));
            Assert.IsTrue(PlaceholderHudLayout.IsPreGamePhase(GamePhase.RunSelectDraft));
            Assert.IsFalse(PlaceholderHudLayout.IsPreGamePhase(GamePhase.SetupPlayerSettlement1));
            Assert.IsFalse(PlaceholderHudLayout.IsPreGamePhase(GamePhase.DayPlayerActions));
        }
    }
}

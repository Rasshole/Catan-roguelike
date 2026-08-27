using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class EventBoardVisualTests
    {
        private static readonly HexCoord WheatHexA = new HexCoord(1, -1);
        private static readonly HexCoord WheatHexB = new HexCoord(0, 1);
        private static readonly HexCoord StoneHex = new HexCoord(-1, 0);
        private static readonly HexCoord BrickHex = new HexCoord(1, 0);

        private static GameState CreateState()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            return new GameState(board);
        }

        private static IEnumerable<HexCoord> AllCoords(GameState state) =>
            state.Board.Tiles.Keys;

        private static IEnumerable<HexCoord> OverlayCoords(GameState state) =>
            AllCoords(state).Where(c =>
                EventBoardVisual.TryGetOverlay(state, c, out _));

        [Test]
        public void TryGetOverlay_ReturnsFalse_WhenActiveEventIsNone()
        {
            var state = CreateState();

            foreach (var coord in AllCoords(state))
                Assert.IsFalse(EventBoardVisual.TryGetOverlay(state, coord, out var overlay));

            Assert.IsEmpty(OverlayCoords(state));
        }

        [Test]
        public void TryGetOverlay_Storm_OnlyOnEventStormTile()
        {
            var state = CreateState();
            state.ActiveEvent = EventId.Storm;
            state.EventStormTile = BrickHex;

            Assert.IsTrue(EventBoardVisual.TryGetOverlay(state, BrickHex, out var brickOverlay));
            Assert.AreEqual(EventTileOverlayKind.Storm, brickOverlay);

            foreach (var coord in AllCoords(state).Where(c => c != BrickHex))
                Assert.IsFalse(EventBoardVisual.TryGetOverlay(state, coord, out _));

            CollectionAssert.AreEquivalent(new[] { BrickHex }, OverlayCoords(state).ToList());
        }

        [Test]
        public void TryGetOverlay_Famine_OnlyOnWheatTiles()
        {
            var state = CreateState();
            state.ActiveEvent = EventId.Famine;

            var expected = new[] { WheatHexA, WheatHexB };
            CollectionAssert.AreEquivalent(expected, OverlayCoords(state).ToList());

            foreach (var coord in expected)
            {
                Assert.IsTrue(EventBoardVisual.TryGetOverlay(state, coord, out var overlay));
                Assert.AreEqual(EventTileOverlayKind.Famine, overlay);
            }

            Assert.IsFalse(EventBoardVisual.TryGetOverlay(state, StoneHex, out _));
            Assert.IsFalse(EventBoardVisual.TryGetOverlay(state, BrickHex, out _));
        }

        [Test]
        public void TryGetOverlay_GoldRush_OnlyOnStoneTiles()
        {
            var state = CreateState();
            state.ActiveEvent = EventId.GoldRush;

            Assert.IsTrue(EventBoardVisual.TryGetOverlay(state, StoneHex, out var overlay));
            Assert.AreEqual(EventTileOverlayKind.GoldRush, overlay);

            foreach (var coord in AllCoords(state).Where(c => c != StoneHex))
                Assert.IsFalse(EventBoardVisual.TryGetOverlay(state, coord, out _));

            CollectionAssert.AreEquivalent(new[] { StoneHex }, OverlayCoords(state).ToList());
        }

        [Test]
        public void TryGetOverlay_GoodHarvest_OnAllResourceTiles()
        {
            var state = CreateState();
            state.ActiveEvent = EventId.GoodHarvest;

            CollectionAssert.AreEquivalent(AllCoords(state).ToList(), OverlayCoords(state).ToList());

            foreach (var coord in AllCoords(state))
            {
                Assert.IsTrue(EventBoardVisual.TryGetOverlay(state, coord, out var overlay));
                Assert.AreEqual(EventTileOverlayKind.GoodHarvest, overlay);
            }
        }

        [Test]
        public void TryGetOverlay_MarketDay_NoTileOverlays()
        {
            var state = CreateState();
            state.ActiveEvent = EventId.MarketDay;

            Assert.IsEmpty(OverlayCoords(state));
        }

        [Test]
        public void TryGetOverlay_BanditRaid_NoTileOverlays()
        {
            var state = CreateState();
            state.ActiveEvent = EventId.BanditRaid;
            state.Board.PlaceRobber(StoneHex);

            Assert.IsEmpty(OverlayCoords(state));
        }
    }
}

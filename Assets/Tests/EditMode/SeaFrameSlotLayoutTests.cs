using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using NUnit.Framework;

namespace CatanRoguelike.Tests.EditMode
{
    public class SeaFrameSlotLayoutTests
    {
        private const float HexScale = 1.2f;

        [Test]
        public void LargeFishermenBoard_PortAndFishSlots_AreDistinctSeaHexes()
        {
            var board = MapPresets.CreateBoard(MapSize.Large, 42, new MapGenerationOptions(true));
            var ports = PortAccess.DiscoverPorts(board);

            Assert.AreEqual(ports.Count, board.PortVertexToSeaSlot.Count,
                "Each port should own one sea hex slot.");

            var owned = new HashSet<HexCoord>(board.PortVertexToSeaSlot.Values);
            Assert.AreEqual(board.PortVertexToSeaSlot.Count, owned.Count,
                "Port sea slots must be unique.");

            foreach (var fishCoord in board.SeaFishGrounds.Keys)
                Assert.IsFalse(owned.Contains(fishCoord),
                    $"Fish ground at {fishCoord} must not share a port sea slot.");

            var land = new HashSet<HexCoord>(board.Tiles.Keys);
            foreach (var coord in owned)
                Assert.IsFalse(land.Contains(coord), $"Port slot {coord} must not be land.");
            foreach (var coord in board.SeaFishGrounds.Keys)
                Assert.IsFalse(land.Contains(coord), $"Fish slot {coord} must not be land.");
        }

        [Test]
        public void PortBadgeCenters_SitInSeaSlots_NotOnLand()
        {
            var board = MapPresets.CreateBoard(MapSize.Large, 42, new MapGenerationOptions(true));
            var ports = PortAccess.DiscoverPorts(board);
            float radius = HexScale * HexMath.InnerRadius * 0.72f;

            foreach (var port in ports)
            {
                Assert.IsTrue(
                    SeaFrameSlotLayout.TryGetBadgeCenter(board, port.Vertex, HexScale, out MapPlaneXz center),
                    $"Badge center should resolve for port {port.Vertex}.");

                foreach (var landCoord in board.Tiles.Keys)
                {
                    Assert.IsFalse(
                        SeaFrameSlotLayout.DiscIntersectsHexPolygon(center, radius, landCoord, HexScale),
                        $"Port badge at {center} overlaps land hex {landCoord}.");
                }
            }
        }
    }
}

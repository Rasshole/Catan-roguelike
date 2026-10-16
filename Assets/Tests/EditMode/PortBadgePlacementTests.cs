using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class PortBadgePlacementTests
    {
        private const float HexScale = 1.2f;

        [TestCase(MapSize.Small)]
        [TestCase(MapSize.Medium)]
        [TestCase(MapSize.Large)]
        public void PortBadges_DiscDoesNotIntersectLandHexes(MapSize size)
        {
            var board = MapPresets.CreateBoard(size);
            var ports = PortAccess.DiscoverPorts(board);
            Assert.Greater(ports.Count, 0, $"Expected ports on {size}.");

            float radius = PortMarkerVisuals.BadgeDiscWorldRadius;
            foreach (var port in ports)
            {
                Assert.IsTrue(
                    PortBadgePlacement.TryComputeBadgeCenter(board, port.Vertex, HexScale, radius, out var center, out _),
                    $"Badge center should resolve for port at {port.Vertex}.");

                Assert.IsFalse(
                    PortBadgePlacement.DiscIntersectsAnyLandHex(center, radius, board, HexScale),
                    $"Port badge disc at {center} should sit in open water (port {port.Vertex}).");
            }
        }
    }
}

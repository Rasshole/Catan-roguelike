using System.Collections.Generic;
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

        [Test]
        public void PortBadges_DiscDiameterIsEightyPercentOfNumberToken()
        {
            float expectedRadius = HexTileView.TokenWorldFaceDiameter * 0.5f * HexTileView.PortBadgeDiameterFraction;
            Assert.AreEqual(expectedRadius, PortMarkerVisuals.BadgeDiscWorldRadius, 0.0001f);
        }

        [TestCase(MapSize.Large)]
        public void PortBadges_NoPairwiseOverlap(MapSize size)
        {
            var board = MapPresets.CreateBoard(size);
            var ports = PortAccess.DiscoverPorts(board);
            Assert.Greater(ports.Count, 1, $"Expected multiple ports on {size}.");

            float radius = PortMarkerVisuals.BadgeDiscWorldRadius;
            var centers = new List<Vector2>(ports.Count);
            foreach (var port in ports)
            {
                Assert.IsTrue(
                    PortBadgePlacement.TryComputeBadgeCenter(board, port.Vertex, HexScale, radius, out var center, out _),
                    $"Badge center should resolve for port at {port.Vertex}.");
                centers.Add(center);
            }

            float minCenterDistance = radius * 2f - 0.002f;
            for (int i = 0; i < centers.Count; i++)
            {
                for (int j = i + 1; j < centers.Count; j++)
                {
                    float dist = Vector2.Distance(centers[i], centers[j]);
                    Assert.Greater(
                        dist,
                        minCenterDistance,
                        $"Port badges {i} and {j} overlap (dist={dist:F3}, min={minCenterDistance:F3}).");
                }
            }
        }

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

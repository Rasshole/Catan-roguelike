using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Shop;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class PortSpacingTests
    {
        [Test]
        public void DiscoverPorts_Large_NoAdjacentHarborVertices()
        {
            var board = MapPresets.CreateBoard(MapSize.Large);
            var ports = PortAccess.DiscoverPorts(board);
            Assert.AreEqual(9, ports.Count);

            for (int i = 0; i < ports.Count; i++)
            {
                for (int j = i + 1; j < ports.Count; j++)
                {
                    Assert.IsFalse(
                        PortSpacingRules.AreTooClose(ports[i].Vertex, ports[j].Vertex),
                        $"Ports {i} and {j} are on adjacent coastal vertices.");
                }
            }
        }

        [TestCase(MapSize.Small)]
        [TestCase(MapSize.Medium)]
        [TestCase(MapSize.Large)]
        public void DiscoverPorts_CountsMatchClassicTargets(MapSize size)
        {
            var board = MapPresets.CreateBoard(size);
            var ports = PortAccess.DiscoverPorts(board);
            int land = board.Tiles.Values.Count(t => !t.IsFishTile);
            int expectedSpecific = land switch { 7 => 3, 13 => 4, 19 => 5, 30 => 7, 37 => 7, _ => System.Math.Min(5, land / 2) };
            int expectedGeneric = land switch { 7 => 2, 13 => 3, 19 => 4, 30 => 4, 37 => 5, _ => System.Math.Max(1, land / 6) };

            Assert.AreEqual(expectedSpecific, ports.Count(p => !p.IsGeneric));
            Assert.AreEqual(expectedGeneric, ports.Count(p => p.IsGeneric));
        }
    }
}

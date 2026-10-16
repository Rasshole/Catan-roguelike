using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Game;
using NUnit.Framework;
namespace CatanRoguelike.Tests.EditMode
{
    public class SeaFishHarborLayoutTests
    {
        private const float HexScale = 1.2f;

        [Test]
        public void SeaFishGrounds_DoNotOverlapColonistPortBadges()
        {
            var board = MapPresets.CreateBoard(MapSize.Large, 42, new MapGenerationOptions(true));
            var ports = PortAccess.DiscoverPorts(board);
            float radius = PortMarkerVisuals.BadgeDiscWorldRadius;

            foreach (var port in ports)
            {
                Assert.IsTrue(
                    SeaFishHarborLayout.TryComputeShortPierBadgeCenter(
                        board, port.Vertex, HexScale, radius, out MapPlaneXz center),
                    $"Short pier badge should resolve for {port.Vertex}.");

                foreach (var kvp in board.SeaFishGrounds)
                {
                    Assert.IsFalse(
                        SeaFishHarborLayout.DiscIntersectsHexPolygon(
                            center, radius + 0.24f, kvp.Key, HexScale),
                        $"Port badge at {center} overlaps sea fish at {kvp.Key}.");
                }
            }
        }
    }
}

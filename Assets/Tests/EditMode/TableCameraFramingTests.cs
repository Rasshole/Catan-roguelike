using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Game;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class TableCameraFramingTests
    {
        private const float HexScale = 1.2f;

        [Test]
        public void ComputeBoardBoundingRadius_Small_IsLessThanLarge()
        {
            var small = MapPresets.CreateBoard(MapSize.Small);
            var large = MapPresets.CreateBoard(MapSize.Large);

            float smallRadius = TableCameraFraming.ComputeBoardBoundingRadius(small, HexScale);
            float largeRadius = TableCameraFraming.ComputeBoardBoundingRadius(large, HexScale);

            Assert.Less(smallRadius, largeRadius);
            Assert.Greater(smallRadius, 2.5f);
            Assert.Less(smallRadius, 4f);
            Assert.Greater(largeRadius, smallRadius + 0.5f);
        }

        [Test]
        public void ComputeOrbitDistance_Small_IsCloserThanLarge()
        {
            var small = MapPresets.CreateBoard(MapSize.Small);
            var large = MapPresets.CreateBoard(MapSize.Large);

            float smallDistance = TableCameraFraming.ComputeOrbitDistance(
                TableCameraFraming.ComputeBoardBoundingRadius(small, HexScale));
            float largeDistance = TableCameraFraming.ComputeOrbitDistance(
                TableCameraFraming.ComputeBoardBoundingRadius(large, HexScale));

            Assert.Less(smallDistance, largeDistance);
            Assert.Less(smallDistance, 4f);
            Assert.Greater(largeDistance, smallDistance);
        }

        [Test]
        public void ComputeOrbitDistance_Medium_BetweenSmallAndLarge()
        {
            var small = MapPresets.CreateBoard(MapSize.Small);
            var medium = MapPresets.CreateBoard(MapSize.Medium);
            var large = MapPresets.CreateBoard(MapSize.Large);

            float smallDistance = TableCameraFraming.ComputeOrbitDistance(
                TableCameraFraming.ComputeBoardBoundingRadius(small, HexScale));
            float mediumDistance = TableCameraFraming.ComputeOrbitDistance(
                TableCameraFraming.ComputeBoardBoundingRadius(medium, HexScale));
            float largeDistance = TableCameraFraming.ComputeOrbitDistance(
                TableCameraFraming.ComputeBoardBoundingRadius(large, HexScale));

            Assert.Less(smallDistance, mediumDistance);
            Assert.Less(mediumDistance, largeDistance);
        }

        [Test]
        public void ComputeOrbitHeight_ScalesWithDistance()
        {
            float distance = TableCameraFraming.ComputeOrbitDistance(3.13f);
            Assert.AreEqual(distance * TableCameraFraming.HeightToDistanceRatio,
                TableCameraFraming.ComputeOrbitHeight(distance),
                0.001f);
        }

        [Test]
        public void ComputeBoardBoundingRadius_UsesHexOuterRadiusAtScale()
        {
            float radius = TableCameraFraming.ComputeBoardBoundingRadius(
                new[] { new HexCoord(0, 0) },
                HexScale);

            Assert.AreEqual(HexScale * HexMath.OuterRadius, radius, 0.001f);
        }
    }
}

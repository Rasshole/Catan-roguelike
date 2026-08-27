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
            Assert.Greater(largeRadius, smallRadius);
        }

        [Test]
        public void ComputeOrbitDistance_Small_IsCloserThanLarge()
        {
            var small = MapPresets.CreateBoard(MapSize.Small);
            var large = MapPresets.CreateBoard(MapSize.Large);

            float smallRadius = TableCameraFraming.ComputeBoardBoundingRadius(small, HexScale);
            float largeRadius = TableCameraFraming.ComputeBoardBoundingRadius(large, HexScale);
            float smallDistance = TableCameraFraming.ComputeOrbitDistance(smallRadius);
            float largeDistance = TableCameraFraming.ComputeOrbitDistance(largeRadius);

            Assert.Less(smallDistance, largeDistance);
            Assert.Greater(smallDistance, smallRadius);
            Assert.Greater(largeDistance, largeRadius);
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
            var small = MapPresets.CreateBoard(MapSize.Small);
            float radius = TableCameraFraming.ComputeBoardBoundingRadius(small, HexScale);
            float distance = TableCameraFraming.ComputeOrbitDistance(radius);
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

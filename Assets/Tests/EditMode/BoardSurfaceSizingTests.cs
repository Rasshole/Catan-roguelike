using CatanRoguelike.Core.Data;
using CatanRoguelike.Game;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class BoardSurfaceSizingTests
    {
        private const float HexScale = 1.2f;
        private const float MaxDiameterFactor = 1.3f;

        [Test]
        public void ComputeTableDiskScale_Small_IsModestlyLargerThanBoundingDiameter()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            float radius = TableCameraFraming.ComputeBoardBoundingRadius(board, HexScale);
            float diameter = 2f * radius;
            var scale = TableCameraFraming.ComputeTableDiskScale(radius);

            Assert.Greater(scale.x, diameter);
            Assert.Less(scale.x, diameter * MaxDiameterFactor);
            Assert.AreEqual(scale.x, scale.z, 0.001f);
            Assert.AreEqual(TableCameraFraming.TableSurfaceThinY, scale.y * 2f, 0.001f);
        }

        [Test]
        public void ComputeTableDiskScale_MediumAndLarge_AreLargerThanSmall()
        {
            var small = MapPresets.CreateBoard(MapSize.Small);
            var medium = MapPresets.CreateBoard(MapSize.Medium);
            var large = MapPresets.CreateBoard(MapSize.Large);

            float smallRadius = TableCameraFraming.ComputeBoardBoundingRadius(small, HexScale);
            float mediumRadius = TableCameraFraming.ComputeBoardBoundingRadius(medium, HexScale);
            float largeRadius = TableCameraFraming.ComputeBoardBoundingRadius(large, HexScale);

            var smallScale = TableCameraFraming.ComputeTableDiskScale(smallRadius);
            var mediumScale = TableCameraFraming.ComputeTableDiskScale(mediumRadius);
            var largeScale = TableCameraFraming.ComputeTableDiskScale(largeRadius);

            Assert.Less(smallScale.x, mediumScale.x);
            Assert.Less(mediumScale.x, largeScale.x);
        }

        [Test]
        public void ComputeTableDiskWorldRadius_UsesPadFactor()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            float boundingRadius = TableCameraFraming.ComputeBoardBoundingRadius(board, HexScale);
            float diskRadius = TableCameraFraming.ComputeTableDiskWorldRadius(boundingRadius);

            Assert.AreEqual(
                boundingRadius * TableCameraFraming.TableSurfacePadFactor,
                diskRadius,
                0.001f);
        }
    }
}

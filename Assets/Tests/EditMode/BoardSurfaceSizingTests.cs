using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class BoardSurfaceSizingTests
    {
        private const float HexScale = 1.2f;
        private const float MaxDiameterFactor = 1.8f;

        [Test]
        public void ComputeTableSurfaceScale_Small_IsModestlyLargerThanBoundingDiameter()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            float radius = TableCameraFraming.ComputeBoardBoundingRadius(board, HexScale);
            float diameter = 2f * radius;
            var scale = TableCameraFraming.ComputeTableSurfaceScale(radius);

            Assert.Greater(scale.x, diameter);
            Assert.Greater(scale.z, diameter);
            Assert.Less(scale.x, diameter * MaxDiameterFactor);
            Assert.Less(scale.z, diameter * MaxDiameterFactor);
            Assert.AreEqual(TableCameraFraming.TableSurfaceThinY, scale.y, 0.001f);
        }

        [Test]
        public void ComputeTableSurfaceScale_MediumAndLarge_AreLargerThanSmall()
        {
            var small = MapPresets.CreateBoard(MapSize.Small);
            var medium = MapPresets.CreateBoard(MapSize.Medium);
            var large = MapPresets.CreateBoard(MapSize.Large);

            float smallRadius = TableCameraFraming.ComputeBoardBoundingRadius(small, HexScale);
            float mediumRadius = TableCameraFraming.ComputeBoardBoundingRadius(medium, HexScale);
            float largeRadius = TableCameraFraming.ComputeBoardBoundingRadius(large, HexScale);

            var smallScale = TableCameraFraming.ComputeTableSurfaceScale(smallRadius);
            var mediumScale = TableCameraFraming.ComputeTableSurfaceScale(mediumRadius);
            var largeScale = TableCameraFraming.ComputeTableSurfaceScale(largeRadius);

            Assert.Less(smallScale.x, mediumScale.x);
            Assert.Less(mediumScale.x, largeScale.x);
            Assert.Less(smallScale.z, mediumScale.z);
            Assert.Less(mediumScale.z, largeScale.z);
        }

        [Test]
        public void BoardView_TableSurface_TracksMapSizeWithPadding()
        {
            var smallRoot = CreateBoardView(MapSize.Small);
            var mediumRoot = CreateBoardView(MapSize.Medium);
            var largeRoot = CreateBoardView(MapSize.Large);

            var smallSurface = FindBoardSurface(smallRoot);
            var mediumSurface = FindBoardSurface(mediumRoot);
            var largeSurface = FindBoardSurface(largeRoot);

            AssertSmallTableBounds(smallSurface.localScale);
            Assert.Less(smallSurface.localScale.x, mediumSurface.localScale.x);
            Assert.Less(mediumSurface.localScale.x, largeSurface.localScale.x);

            Object.DestroyImmediate(smallRoot);
            Object.DestroyImmediate(mediumRoot);
            Object.DestroyImmediate(largeRoot);
        }

        private static GameObject CreateBoardView(MapSize mapSize)
        {
            var go = new GameObject($"BoardView_{mapSize}");
            var boardView = go.AddComponent<BoardView>();
            var controller = new GameController(seed: 1, mapSize);
            controller.SelectMap(mapSize);
            boardView.Initialize(controller);
            return go;
        }

        private static Transform FindBoardSurface(GameObject root)
        {
            foreach (Transform child in root.transform)
            {
                if (child.name == "BoardSurface")
                    return child;
            }

            Assert.Fail("BoardSurface child not found under BoardView.");
            return null;
        }

        private static void AssertSmallTableBounds(Vector3 scale)
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            float radius = TableCameraFraming.ComputeBoardBoundingRadius(board, HexScale);
            float diameter = 2f * radius;

            Assert.Greater(scale.x, diameter);
            Assert.Greater(scale.z, diameter);
            Assert.Less(scale.x, diameter * MaxDiameterFactor);
            Assert.Less(scale.z, diameter * MaxDiameterFactor);
            Assert.AreEqual(TableCameraFraming.TableSurfaceThinY, scale.y, 0.001f);
        }
    }
}

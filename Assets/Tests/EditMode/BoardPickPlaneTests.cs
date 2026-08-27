using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class BoardPickPlaneTests
    {
        private const float TileHeight = 0.15f;
        private const float Epsilon = 0.001f;
        private const float TableCameraPitchDegrees = 55f;

        [Test]
        public void GetPickPlaneY_PositiveTileHeight_ReturnsTileHeight()
        {
            Assert.AreEqual(TileHeight, BoardPickPlane.GetPickPlaneY(TileHeight), Epsilon);
            Assert.AreNotEqual(0f, BoardPickPlane.GetPickPlaneY(TileHeight));
        }

        [Test]
        public void GetPickPlaneY_ZeroTileHeight_ReturnsZero()
        {
            Assert.AreEqual(0f, BoardPickPlane.GetPickPlaneY(0f), Epsilon);
        }

        [Test]
        public void TryRaycast_AtTileHeight_HitsVertexXZ_UnderTableCameraPitch()
        {
            var vertex = new Vector3(1.2f, TileHeight, 2.4f);
            Ray ray = CreateTableCameraStyleRay(vertex);

            Assert.IsTrue(BoardPickPlane.TryRaycast(ray, BoardPickPlane.GetPickPlaneY(TileHeight), out var hit));

            Assert.AreEqual(vertex.x, hit.x, Epsilon);
            Assert.AreEqual(vertex.z, hit.z, Epsilon);
            Assert.AreEqual(TileHeight, hit.y, Epsilon);
        }

        [Test]
        public void TryRaycast_AtZeroPlane_MissesVertexXZ_UnderTableCameraPitch()
        {
            var vertex = new Vector3(1.2f, TileHeight, 2.4f);
            Ray ray = CreateTableCameraStyleRay(vertex);

            Assert.IsTrue(BoardPickPlane.TryRaycast(ray, 0f, out var hit));

            float deltaX = Mathf.Abs(hit.x - vertex.x);
            float deltaZ = Mathf.Abs(hit.z - vertex.z);
            Assert.Greater(deltaX + deltaZ, Epsilon);
        }

        private static Ray CreateTableCameraStyleRay(Vector3 targetOnHexTop)
        {
            float distance = 6f;
            float height = distance * TableCameraFraming.HeightToDistanceRatio;
            var lookTarget = Vector3.zero;

            var offset = Quaternion.Euler(TableCameraPitchDegrees, 30f, 0f)
                * new Vector3(0f, 0f, -distance);
            var cameraPosition = lookTarget + offset + Vector3.up * (height * 0.3f);

            var direction = (targetOnHexTop - cameraPosition).normalized;
            return new Ray(cameraPosition, direction);
        }
    }
}

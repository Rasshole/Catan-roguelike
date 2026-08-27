using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class ThinDiskMeshTests
    {
        [Test]
        public void Create_HasExpectedVertexAndTriangleCounts()
        {
            var mesh = ThinDiskMesh.Create();

            Assert.AreEqual(ThinDiskMesh.SegmentCount * 2 + 2, mesh.vertexCount);
            Assert.AreEqual(ThinDiskMesh.SegmentCount * 4, mesh.triangles.Length / 3);
        }

        [Test]
        public void Create_RingVerticesAreAtMeshRadius()
        {
            var mesh = ThinDiskMesh.Create();
            var vertices = mesh.vertices;

            for (int i = 0; i < ThinDiskMesh.SegmentCount * 2; i++)
            {
                var v = vertices[i];
                float radius = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                Assert.AreEqual(ThinDiskMesh.MeshRadius, radius, 1e-5f);
            }
        }

        [Test]
        public void Create_HasNoNaNsInVerticesOrNormals()
        {
            var mesh = ThinDiskMesh.Create();

            foreach (var v in mesh.vertices)
            {
                Assert.IsFalse(float.IsNaN(v.x));
                Assert.IsFalse(float.IsNaN(v.y));
                Assert.IsFalse(float.IsNaN(v.z));
            }

            foreach (var n in mesh.normals)
            {
                Assert.IsFalse(float.IsNaN(n.x));
                Assert.IsFalse(float.IsNaN(n.y));
                Assert.IsFalse(float.IsNaN(n.z));
            }
        }

        [Test]
        public void Create_TopRingAtPositiveOne_BottomRingAtNegativeOne()
        {
            var mesh = ThinDiskMesh.Create();
            var vertices = mesh.vertices;

            for (int i = 0; i < ThinDiskMesh.SegmentCount; i++)
                Assert.AreEqual(ThinDiskMesh.TopY, vertices[i].y, 1e-5f);

            for (int i = ThinDiskMesh.SegmentCount; i < ThinDiskMesh.SegmentCount * 2; i++)
                Assert.AreEqual(ThinDiskMesh.BottomY, vertices[i].y, 1e-5f);
        }

        [Test]
        public void Shared_ReturnsSameInstance()
        {
            var first = ThinDiskMesh.Shared;
            var second = ThinDiskMesh.Shared;

            Assert.AreSame(first, second);
        }
    }
}

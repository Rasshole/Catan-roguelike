using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class HexPrismMeshTests
    {
        private const float HexScale = 1.2f;

        [Test]
        public void Create_HasExpectedVertexCount()
        {
            var mesh = HexPrismMesh.Create(new HexCoord(0, 0), HexScale);
            Assert.AreEqual(HexPrismMesh.SideCount * 2 + 2, mesh.vertexCount);
        }

        [Test]
        public void Create_TopRingAtPositiveOne_BottomRingAtNegativeOne()
        {
            var mesh = HexPrismMesh.Create(new HexCoord(0, 0), HexScale);
            var vertices = mesh.vertices;

            int topCount = 0;
            int bottomCount = 0;
            foreach (var v in vertices)
            {
                if (Mathf.Approximately(v.y, HexPrismMesh.TopY))
                    topCount++;
                if (Mathf.Approximately(v.y, HexPrismMesh.BottomY))
                    bottomCount++;
            }

            Assert.AreEqual(HexPrismMesh.SideCount + 1, topCount);
            Assert.AreEqual(HexPrismMesh.SideCount + 1, bottomCount);
        }

        [Test]
        public void Create_CornerZeroIsNorthernmostOnTopRing()
        {
            var mesh = HexPrismMesh.Create(new HexCoord(0, 0), HexScale);
            var cornerZero = mesh.vertices[0];

            Assert.AreEqual(HexPrismMesh.TopY, cornerZero.y, 1e-5f);
            for (int i = 1; i < HexPrismMesh.SideCount; i++)
                Assert.GreaterOrEqual(cornerZero.z, mesh.vertices[i].z);
        }

        [Test]
        public void Create_TopRingMatchesCornerVerticesAndCanonicalGraph()
        {
            var hex = new HexCoord(0, 0);
            var mesh = HexPrismMesh.Create(hex, HexScale);
            var (centerX, centerZ) = HexMath.ToWorldPosition(hex, HexScale);

            for (int corner = 0; corner < HexPrismMesh.SideCount; corner++)
            {
                var (cornerWorldX, cornerWorldZ) = HexMath.VertexToWorldPosition(hex, corner, HexScale);
                var canonical = VertexGraph.Canonicalize(new Vertex(hex, corner));
                var (canonicalWorldX, canonicalWorldZ) = HexMath.VertexToWorldPosition(canonical, HexScale);

                Assert.That(canonicalWorldX, Is.EqualTo(cornerWorldX).Within(1e-4f));
                Assert.That(canonicalWorldZ, Is.EqualTo(cornerWorldZ).Within(1e-4f));

                var expected = new Vector3(cornerWorldX - centerX, HexPrismMesh.TopY, cornerWorldZ - centerZ);
                var actual = mesh.vertices[corner];

                Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-4f));
                Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-4f));
                Assert.That(actual.z, Is.EqualTo(expected.z).Within(1e-4f));
            }
        }

        [Test]
        public void Create_TopRingXzAspect_IsPointyTopClassic()
        {
            var mesh = HexPrismMesh.Create(new HexCoord(0, 0), HexScale);
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;
            for (int i = 0; i < HexPrismMesh.SideCount; i++)
            {
                var v = mesh.vertices[i];
                minX = Mathf.Min(minX, v.x);
                maxX = Mathf.Max(maxX, v.x);
                minZ = Mathf.Min(minZ, v.z);
                maxZ = Mathf.Max(maxZ, v.z);
            }

            float ratio = (maxX - minX) / (maxZ - minZ);
            Assert.That(ratio, Is.EqualTo(0.8660254f).Within(0.02f));
        }

        [Test]
        public void Create_AdjacentTilesShareCornerWorldPositions()
        {
            var left = new HexCoord(0, 0);
            var right = new HexCoord(1, 0);
            var leftMesh = HexPrismMesh.Create(left, HexScale);
            var rightMesh = HexPrismMesh.Create(right, HexScale);

            var leftCornerTwo = WorldTopRingVertex(left, leftMesh.vertices[2]);
            var rightCornerFour = WorldTopRingVertex(right, rightMesh.vertices[4]);

            Assert.That(leftCornerTwo.x, Is.EqualTo(rightCornerFour.x).Within(1e-4f));
            Assert.That(leftCornerTwo.z, Is.EqualTo(rightCornerFour.z).Within(1e-4f));
        }

        private static Vector3 WorldTopRingVertex(HexCoord hex, Vector3 localTopVertex)
        {
            var (centerX, centerZ) = HexMath.ToWorldPosition(hex, HexScale);
            return new Vector3(centerX + localTopVertex.x, HexPrismMesh.TopY, centerZ + localTopVertex.z);
        }
    }
}

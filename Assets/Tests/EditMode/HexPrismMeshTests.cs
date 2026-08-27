using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class HexPrismMeshTests
    {
        [Test]
        public void Create_HasExpectedVertexCount()
        {
            var mesh = HexPrismMesh.Create();
            Assert.AreEqual(HexPrismMesh.SideCount * 2 + 2, mesh.vertexCount);
        }

        [Test]
        public void Create_TopRingAtPositiveOne_BottomRingAtNegativeOne()
        {
            var mesh = HexPrismMesh.Create();
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
        public void Create_CornerZeroAtPositiveZ_WithDefaultRadius()
        {
            var mesh = HexPrismMesh.Create();
            var vertices = mesh.vertices;

            bool found = false;
            foreach (var v in vertices)
            {
                if (!Mathf.Approximately(v.y, HexPrismMesh.TopY))
                    continue;
                if (Mathf.Approximately(v.x, 0f)
                    && Mathf.Approximately(v.z, HexPrismMesh.DefaultRadius))
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found, "Expected pointy-top corner 0 at (0, 1, radius).");
        }

        [Test]
        public void Create_TopRingMatchesHexMathCircumradius()
        {
            var mesh = HexPrismMesh.Create();
            var vertices = mesh.vertices;
            float maxRadius = 0f;

            foreach (var v in vertices)
            {
                if (!Mathf.Approximately(v.y, HexPrismMesh.TopY))
                    continue;
                if (Mathf.Approximately(v.x, 0f) && Mathf.Approximately(v.z, 0f))
                    continue;
                float r = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                maxRadius = Mathf.Max(maxRadius, r);
            }

            Assert.AreEqual(HexPrismMesh.DefaultRadius, maxRadius, 1e-5f);
        }

        [Test]
        public void BoardScaleMultiplier_MatchesHexMathOuterRadius()
        {
            Assert.AreEqual(
                1f / HexPrismMesh.DefaultRadius,
                HexPrismMesh.BoardScaleMultiplier,
                1e-5f);
        }
    }
}

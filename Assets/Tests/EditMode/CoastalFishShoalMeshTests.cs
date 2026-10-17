using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests.EditMode
{
    public sealed class CoastalFishShoalMeshTests
    {
        private const float HexScale = 1.2f;

        [Test]
        public void CreateLocal_ProducesSixVertexChevronBand()
        {
            var board = MapPresets.CreateBoard(MapSize.Large, 42, new MapGenerationOptions(true));
            FishShoalPlacement placement = default;
            bool found = false;
            foreach (var sea in board.SeaFishGrounds.Keys)
            {
                if (SeaFrameSlotLayout.TryGetFishShoalPlacement(board, sea, HexScale, out placement))
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found, "Expected at least one fish shoal placement on the test board.");

            var mesh = CoastalFishShoalMesh.CreateLocal(placement, placement.TokenCenter, HexScale);
            Assert.AreEqual(6, mesh.vertexCount, "Fish chevron mesh must be a 6-vertex band.");

            var verts = mesh.vertices;
            float tol = HexScale * 0.02f;
            AssertXZNear(verts[0], placement.CoastArmLeft, placement.TokenCenter, tol, "P1");
            AssertXZNear(verts[1], placement.InlandTip, placement.TokenCenter, tol, "C");
            AssertXZNear(verts[2], placement.CoastArmRight, placement.TokenCenter, tol, "P2");

            float hexArea = 3f * Mathf.Sqrt(3f) * 0.5f * Mathf.Pow(HexScale * HexMath.OuterRadius, 2f);
            float bandArea = CoastalFishShoalMesh.ComputePolygonAreaXZ(verts);
            Assert.Less(bandArea, hexArea * 0.6f,
                $"Chevron band area ({bandArea:F3}) should be < 60% of hex area ({hexArea:F3}).");
        }

        private static void AssertXZNear(
            Vector3 local,
            MapPlaneXz world,
            MapPlaneXz origin,
            float tol,
            string label)
        {
            float x = world.X - origin.X;
            float z = world.Z - origin.Z;
            Assert.Less(Mathf.Abs(local.x - x), tol, $"{label} X");
            Assert.Less(Mathf.Abs(local.z - z), tol, $"{label} Z");
        }
    }
}

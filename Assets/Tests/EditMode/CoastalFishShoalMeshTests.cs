using CatanRoguelike.Core.Data;
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
        public void CreateLocal_ProducesFiveVertexChevron()
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
            Assert.AreEqual(5, mesh.vertexCount, "Fish chevron mesh must be a pentagon (5 vertices).");

            var verts = mesh.vertices;
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            for (int i = 0; i < verts.Length; i++)
            {
                minX = Mathf.Min(minX, verts[i].x);
                maxX = Mathf.Max(maxX, verts[i].x);
            }

            float width = maxX - minX;
            float tokenWidth = HexTileView.TokenWorldFaceDiameter;
            Assert.Greater(width, tokenWidth * 1.2f,
                $"Chevron should span ~1.3–1.5× token width (width={width:F2}, token={tokenWidth:F2}).");
            Assert.Less(width, tokenWidth * 2.8f, "Chevron should not read as a full hex quad.");
        }
    }
}

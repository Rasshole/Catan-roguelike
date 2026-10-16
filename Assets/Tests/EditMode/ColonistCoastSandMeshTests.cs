using CatanRoguelike.Core.Data;
using CatanRoguelike.Game;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class ColonistCoastSandMeshTests
    {
        [Test]
        public void SandSkirt_IsDisabledByDefault()
        {
            Assert.IsFalse(ColonistCoastSandMesh.SandSkirtEnabled);
        }

        [Test]
        public void Create_WhenDisabled_ReturnsEmptyMesh()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            float hexScale = 1.2f;
            float beachWidth = hexScale * ColonistCoastSandMesh.BeachWidthFactor;
            var mesh = ColonistCoastSandMesh.Create(board, hexScale, beachWidth);

            Assert.IsNotNull(mesh);
            Assert.AreEqual(0, mesh.triangles.Length);
            Assert.AreEqual(0, ColonistCoastSandMesh.LastBuiltQuadCount);
        }
    }
}

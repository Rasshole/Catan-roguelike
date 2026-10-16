using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Game;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class ColonistCoastSandMeshTests
    {
        [Test]
        public void Create_ProducesCoastalTrianglesForSmallBoard()
        {
            var board = new BoardState();
            var a = new HexTileData(new HexCoord(0, 0), ResourceType.Wood);
            a.NumberToken = 8;
            board.Tiles[a.Coord] = a;
            var b = new HexTileData(new HexCoord(1, 0), ResourceType.Brick);
            b.NumberToken = 6;
            board.Tiles[b.Coord] = b;

            var mesh = ColonistCoastSandMesh.Create(board, 1.2f, 0.3f);
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 6);
            Assert.Greater(mesh.triangles.Length, 6);
        }
    }
}

using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

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

            float hexScale = 1.2f;
            float beachWidth = hexScale * ColonistCoastSandMesh.BeachWidthFactor;
            var mesh = ColonistCoastSandMesh.Create(board, hexScale, beachWidth);
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 6);
            Assert.Greater(mesh.triangles.Length, 6);
        }

        [Test]
        public void Create_CentroidNearBoardCenter_AndOneQuadPerOuterEdge()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            float hexScale = 1.2f;
            float beachWidth = hexScale * ColonistCoastSandMesh.BeachWidthFactor;
            var mesh = ColonistCoastSandMesh.Create(board, hexScale, beachWidth);

            var centroid = ColonistCoastSandMesh.ComputeCentroidXZ(mesh.vertices);
            var center = ColonistCoastSandMesh.ComputeBoardCenterXZ(board, hexScale);
            Assert.Less(Vector2.Distance(centroid, center), 0.05f);

            int edges = ColonistCoastSandMesh.CountCoastalEdges(board);
            Assert.Greater(edges, 0);
            Assert.AreEqual(edges, ColonistCoastSandMesh.LastBuiltQuadCount);
        }
    }
}

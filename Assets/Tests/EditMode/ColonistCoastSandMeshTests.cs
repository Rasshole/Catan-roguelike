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
            Assert.Greater(ColonistCoastSandMesh.LastBuiltQuadCount, 0);
        }

        [TestCase(MapSize.Small)]
        [TestCase(MapSize.Large)]
        public void Create_ScaledHexSand_NonEmptyAndCoversCoastalMidpoints(MapSize size)
        {
            var board = MapPresets.CreateBoard(size);
            float hexScale = 1.2f;
            float beachWidth = hexScale * ColonistCoastSandMesh.BeachWidthFactor;
            var mesh = ColonistCoastSandMesh.Create(board, hexScale, beachWidth);

            Assert.Greater(mesh.vertexCount, 12, $"Sand mesh should be non-empty on {size}.");
            Assert.Greater(ColonistCoastSandMesh.LastBuiltQuadCount, 0);
            Assert.Greater(ColonistCoastSandMesh.LastLandHexCount, 0);

            var land = new System.Collections.Generic.HashSet<HexCoord>();
            foreach (var kvp in board.Tiles)
            {
                if (!kvp.Value.IsFishTile)
                    land.Add(kvp.Key);
            }

            Assert.AreEqual(land.Count * HexPrismMesh.SideCount, ColonistCoastSandMesh.LastBuiltQuadCount);

            foreach (var coord in land)
            {
                for (int direction = 0; direction < 6; direction++)
                {
                    if (land.Contains(HexMath.GetNeighbor(coord, direction)))
                        continue;

                    Assert.IsTrue(
                        ColonistCoastSandMesh.TryMeasureSandAtCoastalEdgeMidpoint(
                            board, coord, direction, hexScale, beachWidth, out _),
                        $"Sand union should cover coastal midpoint + beachWidth/2 at {coord} dir={direction} on {size}.");
                }
            }
        }

        [Test]
        public void Create_CentroidNearBoardCenter()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            float hexScale = 1.2f;
            float beachWidth = hexScale * ColonistCoastSandMesh.BeachWidthFactor;
            var mesh = ColonistCoastSandMesh.Create(board, hexScale, beachWidth);

            var centroid = ColonistCoastSandMesh.ComputeCentroidXZ(mesh.vertices);
            var center = ColonistCoastSandMesh.ComputeBoardCenterXZ(board, hexScale);
            Assert.Less(Vector2.Distance(centroid, center), 0.15f);
        }
    }
}

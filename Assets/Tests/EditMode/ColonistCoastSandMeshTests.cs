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
        [TestCase(MapSize.Small)]
        [TestCase(MapSize.Large)]
        public void BuildLandOutline_SmallAndLarge_ReturnsClosedLoop(MapSize size)
        {
            var board = MapPresets.CreateBoard(size);
            float hexScale = 1.2f;
            Assert.IsTrue(
                ColonistCoastSandMesh.TryBuildLandOutlinePolygon(board, hexScale, 0f, out var outline),
                $"Land outline walk should succeed on {size}.");
            Assert.Greater(outline.Count, 6, $"Outline should have multiple vertices on {size} (got {outline.Count}).");

            int coastalEdges = ColonistCoastSandMesh.CountCoastalEdges(board);
            Assert.AreEqual(
                coastalEdges,
                outline.Count,
                $"Outline vertex count should match coastal edge count on {size}.");

            float beachWidth = hexScale * ColonistCoastSandMesh.BeachWidthFactor;
            var mesh = ColonistCoastSandMesh.Create(board, hexScale, beachWidth);
            Assert.Greater(mesh.vertexCount, 6, $"Sand mesh should be non-empty on {size}.");
            Assert.AreEqual(outline.Count, ColonistCoastSandMesh.LastBuiltQuadCount);
        }

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
        public void Create_CentroidNearBoardCenter_AndUniformEdgeWidth()
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
            Assert.AreEqual(edges, ColonistCoastSandMesh.LastBuiltQuadCount,
                "Continuous sand ring should have one quad per coastal edge.");

            AssertUniformEdgeWidths(board, hexScale, beachWidth);
            AssertMeshCoastalMidpointWidths(mesh, board, hexScale, beachWidth);
        }

        private static void AssertMeshCoastalMidpointWidths(
            Mesh mesh,
            BoardState board,
            float hexScale,
            float beachWidth)
        {
            var land = new System.Collections.Generic.HashSet<HexCoord>();
            foreach (var kvp in board.Tiles)
            {
                if (!kvp.Value.IsFishTile)
                    land.Add(kvp.Key);
            }

            foreach (var coord in land)
            {
                for (int direction = 0; direction < 6; direction++)
                {
                    if (land.Contains(HexMath.GetNeighbor(coord, direction)))
                        continue;

                    Assert.IsTrue(
                        ColonistCoastSandMesh.TryMeasureMeshWidthAtCoastalEdgeMidpoint(
                            mesh, board, coord, direction, hexScale, beachWidth, out float width),
                        $"Mesh should cover coastal edge midpoint at {coord} dir={direction}.");
                    Assert.Less(
                        Mathf.Abs(width - beachWidth) / beachWidth,
                        0.10f,
                        $"Mesh sand width at {coord} dir={direction} should be within 10% " +
                        $"(got {width:F3}, target {beachWidth:F3}).");
                }
            }
        }

        private static void AssertUniformEdgeWidths(BoardState board, float hexScale, float beachWidth)
        {
            var tileSet = new System.Collections.Generic.HashSet<HexCoord>(board.Tiles.Keys);
            float y = ThinDiskMesh.TopY;
            foreach (var coord in tileSet)
            {
                var (cx, cz) = HexMath.ToWorldPosition(coord, hexScale);
                var hexCenter = new Vector2(cx, cz);
                for (int direction = 0; direction < 6; direction++)
                {
                    if (tileSet.Contains(HexMath.GetNeighbor(coord, direction)))
                        continue;

                    int corner0 = direction;
                    int corner1 = (direction + 1) % 6;
                    var (x0, z0) = HexMath.VertexToWorldPosition(coord, corner0, hexScale);
                    var (x1, z1) = HexMath.VertexToWorldPosition(coord, corner1, hexScale);
                    var inner0 = new Vector3(x0, y, z0);
                    var inner1 = new Vector3(x1, y, z1);
                    var edge = new Vector2(inner1.x - inner0.x, inner1.z - inner0.z).normalized;
                    var n1 = new Vector2(-edge.y, edge.x);
                    var n2 = new Vector2(edge.y, -edge.x);
                    var mid = new Vector2((inner0.x + inner1.x) * 0.5f, (inner0.z + inner1.z) * 0.5f);
                    var outward = Vector2.Dot(n1, mid - hexCenter) >= Vector2.Dot(n2, mid - hexCenter) ? n1 : n2;
                    var outer0 = new Vector3(inner0.x + outward.x * beachWidth, y, inner0.z + outward.y * beachWidth);
                    var outer1 = new Vector3(inner1.x + outward.x * beachWidth, y, inner1.z + outward.y * beachWidth);
                    Assert.IsTrue(
                        ColonistCoastSandMesh.TryMeasureEdgeQuadWidth(inner0, inner1, outer0, outer1, out float width),
                        "Coastal edge should produce measurable sand width.");
                    Assert.Less(Mathf.Abs(width - beachWidth) / beachWidth, 0.10f,
                        $"Sand width should be within 10% of target (got {width:F3}, target {beachWidth:F3}).");
                }
            }
        }
    }
}

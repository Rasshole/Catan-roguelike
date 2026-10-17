using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class ColonistTerrainDecorTests
    {
        private const float TestHexScale = 1.2f;

        [Test]
        public void TerrainHexes_MeetMinimumDecorCountsWithinCap()
        {
            var board = MapPresets.CreateBoard(MapSize.Large, seed: 77);
            float keepOut = HexTileView.TokenWorldFaceDiameter * 0.5f + TestHexScale * 0.08f;
            float maxCenter = TestHexScale * 0.70f;
            bool sawBrick = false;
            bool sawStone = false;
            bool sawWheat = false;
            bool sawSheep = false;

            foreach (var kvp in board.Tiles)
            {
                var tile = kvp.Value;
                if (tile.IsDesert)
                    continue;

                var hexGo = new GameObject($"DecorHex_{tile.Coord.Q}_{tile.Coord.R}");
                ColonistHexTileNatureDecor.Apply(hexGo, tile, TestHexScale);
                var props = hexGo.transform.Find("TileNatureProps");
                if (props == null)
                {
                    Object.DestroyImmediate(hexGo);
                    continue;
                }

                int count = ColonistHexTileNatureDecor.CountDecorProps(props);
                switch (tile.Resource)
                {
                    case ResourceType.Brick:
                        sawBrick = true;
                        Assert.GreaterOrEqual(count, 4, $"Brick hex {tile.Coord} needs clay/brick props (found {count}).");
                        break;
                    case ResourceType.Stone:
                        sawStone = true;
                        Assert.GreaterOrEqual(count, 5, $"Stone hex {tile.Coord} needs boulders/peak props (found {count}).");
                        break;
                    case ResourceType.Wheat:
                        sawWheat = true;
                        Assert.GreaterOrEqual(count, 6, $"Wheat hex {tile.Coord} needs field props (found {count}).");
                        break;
                    case ResourceType.Sheep:
                        sawSheep = true;
                        Assert.GreaterOrEqual(count, 4, $"Sheep hex {tile.Coord} needs flock props (found {count}).");
                        break;
                }

                foreach (Transform child in props)
                {
                    var flat = new Vector2(child.localPosition.x, child.localPosition.z);
                    if (flat.sqrMagnitude < 0.0001f)
                        continue;
                    Assert.LessOrEqual(flat.magnitude, maxCenter + 0.03f,
                        $"Prop {child.name} on {tile.Coord} should stay within 0.70 hex cap.");
                    if (!ColonistHexTileNatureDecor.IsForestTreePropName(child.name))
                        continue;
                    Assert.GreaterOrEqual(flat.magnitude, keepOut - 0.03f,
                        $"Tree {child.name} on {tile.Coord} should clear token keep-out.");
                }

                Object.DestroyImmediate(hexGo);
            }

            Assert.IsTrue(sawBrick && sawStone && sawWheat && sawSheep, "Large map should include all productive terrains.");
        }
    }
}

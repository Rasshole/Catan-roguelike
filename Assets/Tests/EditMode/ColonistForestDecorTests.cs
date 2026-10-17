using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class ColonistForestDecorTests
    {
        private const float TestHexScale = 1.2f;

        [Test]
        public void ForestHexes_HaveAtLeastSevenTreesWithinAnnulus()
        {
            var board = MapPresets.CreateBoard(MapSize.Small, seed: 4242);
            float keepOut = HexTileView.TokenWorldFaceDiameter * 0.5f + TestHexScale * 0.08f;
            float maxCenter = TestHexScale * 0.70f;
            int forestHexes = 0;

            foreach (var kvp in board.Tiles)
            {
                var tile = kvp.Value;
                if (tile.IsDesert || tile.Resource != ResourceType.Wood)
                    continue;

                forestHexes++;
                var hexGo = new GameObject($"ForestHex_{tile.Coord.Q}_{tile.Coord.R}");
                ColonistHexTileNatureDecor.Apply(hexGo, tile, TestHexScale);
                var props = hexGo.transform.Find("TileNatureProps");
                Assert.IsNotNull(props, $"Forest hex {tile.Coord} should spawn TileNatureProps.");

                int trees = 0;
                foreach (Transform child in props)
                {
                    if (!ColonistHexTileNatureDecor.IsForestTreePropName(child.name))
                        continue;

                    trees++;
                    var flat = new Vector2(child.localPosition.x, child.localPosition.z);
                    Assert.GreaterOrEqual(flat.magnitude, keepOut - 0.02f,
                        $"Tree on {tile.Coord} should sit outside token keep-out (r={flat.magnitude:F3} min={keepOut:F3}).");
                    Assert.Less(Mathf.Abs(child.localPosition.y - HexPrismMesh.TopY), 0.25f,
                        $"Tree on {tile.Coord} should sit on the hex top, not float (y={child.localPosition.y:F3}).");
                    Assert.LessOrEqual(flat.magnitude, maxCenter + 0.02f,
                        $"Tree on {tile.Coord} should stay within 0.70 hex cap (r={flat.magnitude:F3} max={maxCenter:F3}).");
                }

                Assert.GreaterOrEqual(trees, 7,
                    $"Forest hex {tile.Coord} should show a dense tree ring (found {trees}).");

                Object.DestroyImmediate(hexGo);
            }

            Assert.Greater(forestHexes, 0, "Small map preset should include forest tiles.");
        }
    }
}

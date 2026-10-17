using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
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
            bool sawDesert = false;

            foreach (var kvp in board.Tiles)
            {
                var tile = kvp.Value;
                var hexGo = new GameObject($"DecorHex_{tile.Coord.Q}_{tile.Coord.R}");
                ColonistHexTileNatureDecor.Apply(hexGo, tile, TestHexScale);
                var props = hexGo.transform.Find("TileNatureProps");
                if (props == null)
                {
                    Object.DestroyImmediate(hexGo);
                    if (!tile.IsDesert)
                        continue;
                }

                if (tile.IsDesert)
                {
                    sawDesert = true;
                    Assert.IsNotNull(props, $"Desert hex {tile.Coord} should spawn TileNatureProps.");
                    Assert.GreaterOrEqual(
                        ColonistHexTileNatureDecor.CountDescendantsNamed(props, "Cactus"),
                        5,
                        $"Desert hex {tile.Coord} needs cacti around pond/token (found cactus parts).");
                    Assert.GreaterOrEqual(
                        ColonistHexTileNatureDecor.CountDescendantsNamed(props, "DesertRock"),
                        3,
                        $"Desert hex {tile.Coord} needs desert rocks.");
                    Assert.GreaterOrEqual(
                        ColonistHexTileNatureDecor.CountDecorProps(props),
                        10,
                        $"Desert hex {tile.Coord} should use the full decor template.");
                    Object.DestroyImmediate(hexGo);
                    continue;
                }

                int count = ColonistHexTileNatureDecor.CountDecorProps(props);
                switch (tile.Resource)
                {
                    case ResourceType.Brick:
                        sawBrick = true;
                        Assert.AreEqual(
                            ColonistHexTileDecorTemplates.Brick.Count,
                            count,
                            $"Brick hex {tile.Coord} should use the brick decor template (found {count}).");
                        AssertBrickHasNoKilnLikeProps(props, tile.Coord);
                        break;
                    case ResourceType.Stone:
                        sawStone = true;
                        Assert.AreEqual(
                            ColonistHexTileDecorTemplates.Stone.Count,
                            count,
                            $"Stone hex {tile.Coord} should use the stone decor template (found {count}).");
                        Assert.AreEqual(
                            0,
                            ColonistHexTileNatureDecor.CountDescendantsNamed(props, "Peak"),
                            $"Stone hex {tile.Coord} should not use mountain peaks.");
                        break;
                    case ResourceType.Wheat:
                        sawWheat = true;
                        Assert.AreEqual(
                            ColonistHexTileDecorTemplates.Wheat.Count,
                            count,
                            $"Wheat hex {tile.Coord} should use the wheat decor template (found {count}).");
                        Assert.GreaterOrEqual(
                            ColonistHexTileNatureDecor.CountDescendantsNamed(props, "windmill"),
                            1,
                            $"Wheat hex {tile.Coord} should include the Kenney windmill.");
                        break;
                    case ResourceType.Sheep:
                        sawSheep = true;
                        Assert.AreEqual(
                            ColonistHexTileDecorTemplates.Sheep.Count,
                            count,
                            $"Sheep hex {tile.Coord} should use the sheep decor template (found {count}).");
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

            Assert.IsTrue(
                sawBrick && sawStone && sawWheat && sawSheep && sawDesert,
                "Large map should include all productive terrains and desert.");
        }
        private static void AssertBrickHasNoKilnLikeProps(Transform props, HexCoord coord)
        {
            foreach (var t in props.GetComponentsInChildren<Transform>(true))
            {
                string lower = t.name.ToLowerInvariant();
                if (lower.Contains("kiln") || lower.Contains("oven") || lower.Contains("smelter")
                    || lower.Contains("furnace"))
                {
                    Assert.Fail($"Brick hex {coord} should not contain kiln-like prop '{t.name}'.");
                }
            }
        }
    }
}

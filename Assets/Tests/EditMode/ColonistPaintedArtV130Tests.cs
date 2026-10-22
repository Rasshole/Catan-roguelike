using CatanRoguelike.Core;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    /// <summary>v130: painted resource icons + painted port art + sharp 2048 tile albedos.</summary>
    public class ColonistPaintedArtV130Tests
    {
        [TestCase(ResourceType.Wood)]
        [TestCase(ResourceType.Brick)]
        [TestCase(ResourceType.Wheat)]
        [TestCase(ResourceType.Sheep)]
        [TestCase(ResourceType.Stone)]
        public void PaintedIcon_IsUsedAcrossTheUi(ResourceType resource)
        {
            var painted = ColonistResourceIconTextures.GetPainted(resource);
            Assert.IsNotNull(painted, $"painted icon for {resource}");
            Assert.AreSame(painted, ColonistResourceIconTextures.GetForPortBadge(resource));
            Assert.AreSame(painted, ColonistHudResourceSprites.Get(resource).texture);
        }

        [Test]
        public void PaintedFish_IsTheFishermenGlyph()
        {
            var fish = ColonistResourceIconTextures.GetPaintedFish();
            Assert.IsNotNull(fish);
            Assert.AreSame(fish, ColonistHudResourceSprites.GetFish().texture);
        }

        [Test]
        public void PaintedPortArt_IsPresent()
        {
            Assert.IsNotNull(Resources.Load<Texture2D>($"{PortMarkerVisuals.PortArtFolder}/PortBadge"));
            Assert.IsNotNull(Resources.Load<Texture2D>($"{PortMarkerVisuals.PortArtFolder}/PierPlanks"));
            Assert.Greater(PortMarkerVisuals.BadgeFaceOverDisc, 1f, "painted face must cover the disc rim");
        }

        [Test]
        public void BlenderTiles_Are2048_Mipmapped_WithSharpeningBias()
        {
            foreach (var slot in new[] { "Wood", "Wheat", "Sheep", "Ore", "Brick", "Desert" })
            {
                var tex = Resources.Load<Texture2D>($"{ColonistBlenderHexTerrainArt.TextureFolder}/{slot}");
                Assert.IsNotNull(tex, slot);
                Assert.GreaterOrEqual(tex.width, 2048, $"{slot} albedo resolution");
                Assert.Greater(tex.mipmapCount, 1, $"{slot} mipmaps");
                Assert.AreNotEqual(FilterMode.Point, tex.filterMode);
            }

            Assert.Less(ColonistBlenderHexTerrainArt.MipMapBias, 0f);
        }
    }
}

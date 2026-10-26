using System;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Tests
{
    /// <summary>v133: leader medallions, tiled leader-card parchment on every light UI piece, new wool/wheat icons.</summary>
    public class ColonistPaintedArtV133Tests
    {
        [Test]
        public void LeaderPortrait_LoadsAsRoundMedallion_ForEveryLeader()
        {
            foreach (LeaderId id in Enum.GetValues(typeof(LeaderId)))
            {
                var sprite = ColonistRoguelikeArt.GetLeaderPortrait(id);
                Assert.IsNotNull(sprite, $"portrait for {id}");
                var tex = sprite.texture;
                Assert.AreEqual(tex.width, tex.height, $"{id} portrait is square");
                Assert.Less(tex.GetPixel(2, 2).a, 0.05f, $"{id} corner keyed to alpha");
                Assert.Less(tex.GetPixel(tex.width - 3, tex.height - 3).a, 0.05f, $"{id} corner keyed to alpha");
                Assert.Greater(tex.GetPixel(tex.width / 2, tex.height / 2).a, 0.95f, $"{id} medallion is opaque");
            }
        }

        [TestCase(UiKitPiece.FrameOrnate)]
        [TestCase(UiKitPiece.FrameMedium)]
        [TestCase(UiKitPiece.FrameThin)]
        [TestCase(UiKitPiece.Chip)]
        [TestCase(UiKitPiece.ButtonParchment)]
        public void LightKitPiece_TilesTheLeaderCardParchment(UiKitPiece piece)
        {
            Assert.IsTrue(ColonistUiKit.UsesTiledParchment(piece));
            var go = new GameObject("KitProbe", typeof(RectTransform));
            try
            {
                var image = go.AddComponent<Image>();
                Assert.IsTrue(ColonistUiKit.Apply(image, piece));
                Assert.AreEqual(Image.Type.Tiled, image.type, "parchment repeats instead of stretching");

                var sprite = ColonistUiKit.Get(piece);
                var tex = sprite.texture;
                int b = Mathf.RoundToInt(sprite.border.x);
                int tile = tex.width - 2 * b;
                Assert.GreaterOrEqual(tile, 96, "centre tile is large enough to show the grain");

                // Seamless: the tile's first and last columns differ no more than neighbouring columns do.
                float wrap = 0f, step = 0f;
                for (int y = b; y < b + tile; y++)
                {
                    wrap += Diff(tex.GetPixel(b, y), tex.GetPixel(b + tile - 1, y));
                    step += Diff(tex.GetPixel(b, y), tex.GetPixel(b + 1, y));
                }

                Assert.Less(wrap / tile, step / tile * 3f + 0.03f, $"{piece} tile wraps without a seam");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DarkPieces_StaySliced()
        {
            Assert.IsFalse(ColonistUiKit.UsesTiledParchment(UiKitPiece.ButtonGreen));
            Assert.IsFalse(ColonistUiKit.UsesTiledParchment(UiKitPiece.Banner));
        }

        [TestCase(ResourceType.Sheep)]
        [TestCase(ResourceType.Wheat)]
        public void NewWoolAndWheatIcons_AreKeyedToAlpha(ResourceType resource)
        {
            foreach (var tex in new[]
                     {
                         ColonistResourceIconTextures.GetPainted(resource),
                         ColonistResourceIconTextures.GetPaintedSmall(resource)
                     })
            {
                Assert.IsNotNull(tex, resource.ToString());
                Assert.Less(tex.GetPixel(1, 1).a, 0.05f);
                Assert.Less(tex.GetPixel(tex.width - 2, tex.height - 2).a, 0.05f);
                Assert.Greater(tex.GetPixel(tex.width / 2, tex.height / 2).a, 0.5f);
            }
        }

        private static float Diff(Color a, Color b) =>
            (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)) / 3f;
    }
}

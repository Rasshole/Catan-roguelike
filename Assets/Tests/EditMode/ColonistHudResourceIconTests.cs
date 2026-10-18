using CatanRoguelike.Core;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class ColonistHudResourceIconTests
    {
        [Test]
        public void HudSprites_UsePortBadgeResourceTextures()
        {
            foreach (ResourceType resource in new[]
                     {
                         ResourceType.Wood,
                         ResourceType.Brick,
                         ResourceType.Wheat,
                         ResourceType.Sheep,
                         ResourceType.Stone
                     })
            {
                var badgeTex = ColonistResourceIconTextures.GetForPortBadge(resource);
                var hudSprite = ColonistHudResourceSprites.Get(resource);
                Assert.IsNotNull(badgeTex, $"Port badge texture for {resource}.");
                Assert.IsNotNull(hudSprite, $"HUD sprite for {resource}.");
                Assert.AreEqual(
                    badgeTex,
                    hudSprite.texture,
                    $"HUD should use the same texture as port badges for {resource}.");
            }
        }

        [Test]
        public void Sheep_PortBadge_UsesPastureBoardArt()
        {
            var sheep = ColonistResourceIconTextures.GetForPortBadge(ResourceType.Sheep);
            Assert.IsNotNull(sheep);
            Assert.AreEqual("Sheep", sheep.name, "Sheep HUD/port icons should use the pasture tile Kenney art.");
        }

        [Test]
        public void Wheat_PortBadge_UsesKenneyOrSheafArt()
        {
            var wheat = ColonistResourceIconTextures.GetForPortBadge(ResourceType.Wheat);
            Assert.IsNotNull(wheat);
            Assert.Greater(wheat.width, 8);
            Assert.Greater(wheat.height, 8);
            Assert.AreNotEqual(
                wheat.name,
                "ColonistPortBadgeIcon_Wheat",
                "Wheat should use imported art, not the procedural fallback.");
        }
    }
}

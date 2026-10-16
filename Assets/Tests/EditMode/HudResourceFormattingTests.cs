using CatanRoguelike.Core;
using CatanRoguelike.Core.UI;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class HudResourceFormattingTests
    {
        [Test]
        public void FormatQuantity_UsesEnglishResourceName()
        {
            string line = HudResourceFormatting.FormatQuantity(3, ResourceType.Wheat);
            Assert.That(line, Does.Contain("3"));
            Assert.That(line, Does.Contain("Wheat"));
        }

        [Test]
        public void FormatQuantity_Wood_UsesLumberLabel()
        {
            string line = HudResourceFormatting.FormatQuantity(2, ResourceType.Wood);
            Assert.That(line, Does.Contain("2"));
            Assert.That(line, Does.Contain("Lumber"));
        }

        [Test]
        public void FormatInventoryLine_ReadsBundleAmount()
        {
            var bundle = new ResourceBundle { Brick = 4 };
            string line = HudResourceFormatting.FormatInventoryLine(bundle, ResourceType.Brick);
            Assert.That(line, Does.Contain("4"));
            Assert.That(line, Does.Contain("Brick"));
        }
    }
}

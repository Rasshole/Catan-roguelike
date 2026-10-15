using CatanRoguelike.Core;
using CatanRoguelike.Core.UI;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class BuildCostFormatterTests
    {
        [Test]
        public void FormatShort_IncludesDanishNames()
        {
            var cost = new ResourceBundle { Wood = 1, Brick = 1 };
            string text = BuildCostFormatter.FormatShort(cost);
            Assert.That(text, Does.Contain("Træ"));
            Assert.That(text, Does.Contain("Tegl"));
        }

        [Test]
        public void FormatButtonLabel_MarksUnaffordable()
        {
            var cost = new ResourceBundle { Stone = 2 };
            string label = BuildCostFormatter.FormatButtonLabel("Byudvid", cost, canAfford: false);
            Assert.That(label, Does.Contain("mangler"));
        }
    }
}

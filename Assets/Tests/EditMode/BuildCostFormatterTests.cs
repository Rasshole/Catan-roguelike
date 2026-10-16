using CatanRoguelike.Core;
using CatanRoguelike.Core.UI;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class BuildCostFormatterTests
    {
        [Test]
        public void FormatShort_IncludesEnglishNames()
        {
            var cost = new ResourceBundle { Wood = 1, Brick = 1 };
            string text = BuildCostFormatter.FormatShort(cost);
            Assert.That(text, Does.Contain("Lumber"));
            Assert.That(text, Does.Contain("Brick"));
        }

        [Test]
        public void FormatButtonLabel_MarksUnaffordable()
        {
            var cost = new ResourceBundle { Stone = 2 };
            string label = BuildCostFormatter.FormatButtonLabel("Byudvid", cost, canAfford: false);
            Assert.That(label, Does.Contain("can't afford"));
        }

        [Test]
        public void FormatTooltip_PrefixesCost()
        {
            var cost = new ResourceBundle { Wheat = 2 };
            Assert.That(BuildCostFormatter.FormatTooltip(cost), Does.Contain("Cost:"));
            Assert.That(BuildCostFormatter.FormatTooltip(cost), Does.Contain("Wheat"));
            Assert.That(BuildCostFormatter.FormatBankStyleChips(cost), Is.EqualTo("W2"));
        }
    }
}

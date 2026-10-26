using CatanRoguelike.Core.Cards;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class CardVisualStyleTests
    {
        [Test]
        public void GetCategoryColor_Sabotage_IsReddish()
        {
            var rgb = CardVisualStyle.GetCategoryColor(CardCategory.Sabotage);
            Assert.Greater(rgb.R, rgb.G);
            Assert.Greater(rgb.R, rgb.B);
        }

        [Test]
        public void GetCategoryColor_BuildTempo_IsGreenish()
        {
            var rgb = CardVisualStyle.GetCategoryColor(CardCategory.BuildTempo);
            Assert.Greater(rgb.G, rgb.R);
        }

        [Test]
        public void GetCategoryColor_KnownCategories_AreStable()
        {
            Assert.AreEqual(CardVisualStyle.Synergy, CardVisualStyle.GetCategoryColor(CardCategory.Synergy));
            Assert.AreEqual("Sab.", CardVisualStyle.GetCategoryLabel(CardCategory.Sabotage));
            Assert.AreEqual(CardVisualStyle.RollManipulation,
                CardVisualStyle.GetCategoryColor(CardCategory.RollManipulation));
        }
    }
}

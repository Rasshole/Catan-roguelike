using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Victory;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class ArmyCalculatorTests
    {
        private static GameState CreateState()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            return new GameState(board);
        }

        [Test]
        public void GetLargestArmyOwner_UnderThreshold_ReturnsNull()
        {
            var state = CreateState();
            state.PlayerKnightsPlayed = 2;
            state.AiKnightsPlayed = 1;

            ArmyCalculator.UpdateLargestArmyOwner(state);

            Assert.IsNull(state.LargestArmyOwner);
        }

        [Test]
        public void RecordKnightPlayed_ThirdKnightWithLead_GrantsHolder()
        {
            var state = CreateState();
            state.PlayerKnightsPlayed = 2;
            state.AiKnightsPlayed = 1;

            ArmyCalculator.RecordKnightPlayed(state, PlayerId.Human);

            Assert.AreEqual(3, state.PlayerKnightsPlayed);
            Assert.AreEqual(PlayerId.Human, state.LargestArmyOwner);
        }

        [Test]
        public void RecordKnightPlayed_TieAtThree_KeepsIncumbent()
        {
            var state = CreateState();
            state.PlayerKnightsPlayed = 2;
            state.AiKnightsPlayed = 3;
            state.LargestArmyOwner = PlayerId.Ai;

            ArmyCalculator.RecordKnightPlayed(state, PlayerId.Human);

            Assert.AreEqual(3, state.PlayerKnightsPlayed);
            Assert.AreEqual(PlayerId.Ai, state.LargestArmyOwner, "equal count must not steal Largest Army");
        }

        [Test]
        public void RecordKnightPlayed_Overtake_StealsFromIncumbent()
        {
            var state = CreateState();
            state.PlayerKnightsPlayed = 3;
            state.AiKnightsPlayed = 3;
            state.LargestArmyOwner = PlayerId.Human;

            ArmyCalculator.RecordKnightPlayed(state, PlayerId.Ai);

            Assert.AreEqual(4, state.AiKnightsPlayed);
            Assert.AreEqual(PlayerId.Ai, state.LargestArmyOwner);
        }

        [Test]
        public void RecordKnightPlayed_LossWhenSurpassed_DropsHolder()
        {
            var state = CreateState();
            state.PlayerKnightsPlayed = 3;
            state.AiKnightsPlayed = 3;
            state.LargestArmyOwner = PlayerId.Human;

            ArmyCalculator.RecordKnightPlayed(state, PlayerId.Ai);

            Assert.AreEqual(PlayerId.Ai, state.LargestArmyOwner);
            Assert.AreNotEqual(PlayerId.Human, state.LargestArmyOwner);
        }
    }
}

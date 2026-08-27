using CatanRoguelike.Core;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Victory;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class LargestArmyVictoryTests
    {
        private const int Seed = 42;

        [Test]
        public void LargestArmy_ThirdKnight_GrantsTwoVp()
        {
            var state = CreateState();
            var engine = new CardEngine(Seed);
            var hex = new HexCoord(0, 0);

            PlayKnight(engine, state, PlayerId.Human, hex);
            PlayKnight(engine, state, PlayerId.Human, hex);
            PlayKnight(engine, state, PlayerId.Human, hex);

            Assert.AreEqual(PlayerId.Human, state.LargestArmyOwner);
            Assert.AreEqual(2, state.PlayerVictoryPoints);
            var bd = VictoryCalculator.GetBreakdown(state, PlayerId.Human);
            Assert.AreEqual(2, bd.LargestArmy);
            StringAssert.Contains("largest army", bd.FormatLine());
        }

        [Test]
        public void LargestArmy_Tie_KeepsHolderVp()
        {
            var state = CreateState();
            var engine = new CardEngine(Seed);
            var hex = new HexCoord(0, 0);

            state.AiKnightsPlayed = 3;
            state.LargestArmyOwner = PlayerId.Ai;

            PlayKnight(engine, state, PlayerId.Human, hex);
            PlayKnight(engine, state, PlayerId.Human, hex);
            PlayKnight(engine, state, PlayerId.Human, hex);

            Assert.AreEqual(PlayerId.Ai, state.LargestArmyOwner);
            Assert.AreEqual(2, state.AiVictoryPoints);
            Assert.AreEqual(0, state.PlayerVictoryPoints);
        }

        [Test]
        public void LargestArmy_Overtake_TransfersVp()
        {
            var state = CreateState();
            var engine = new CardEngine(Seed);
            var hex = new HexCoord(0, 0);

            state.PlayerKnightsPlayed = 3;
            state.AiKnightsPlayed = 3;
            state.LargestArmyOwner = PlayerId.Human;
            VictoryCalculator.RefreshVictoryPoints(state);
            Assert.AreEqual(2, state.PlayerVictoryPoints);

            PlayKnight(engine, state, PlayerId.Ai, hex);

            Assert.AreEqual(PlayerId.Ai, state.LargestArmyOwner);
            Assert.AreEqual(0, state.PlayerVictoryPoints, "former holder loses army VP");
            Assert.AreEqual(2, state.AiVictoryPoints);
        }

        [Test]
        public void Knight_InvalidTarget_DoesNotIncrementCount()
        {
            var state = CreateState();
            state.PlayerHand.Add(CardId.Knight);
            var engine = new CardEngine(Seed);

            Assert.IsFalse(engine.PlayCard(state, PlayerId.Human, CardId.Knight, robberTarget: new HexCoord(99, 99)));

            Assert.AreEqual(0, state.PlayerKnightsPlayed);
            Assert.IsNull(state.LargestArmyOwner);
        }

        [Test]
        public void Breakdown_LargestArmy_PartsSumToTotal()
        {
            var state = CreateState();
            state.PlayerKnightsPlayed = 3;
            state.LargestArmyOwner = PlayerId.Human;

            VictoryCalculator.RefreshVictoryPoints(state);
            var bd = VictoryCalculator.GetBreakdown(state, PlayerId.Human);

            Assert.AreEqual(2, bd.LargestArmy);
            Assert.AreEqual(
                bd.Settlements + bd.Cities + bd.Longest + bd.LongRoadBonus + bd.LargestArmy + bd.BonusVp,
                bd.Total);
            Assert.AreEqual(state.PlayerVictoryPoints, bd.Total);
        }

        private static GameState CreateState()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            return new GameState(board);
        }

        private static void PlayKnight(CardEngine engine, GameState state, PlayerId player, HexCoord hex)
        {
            var hand = player == PlayerId.Human ? state.PlayerHand : state.AiHand;
            hand.Add(CardId.Knight);
            Assert.IsTrue(engine.PlayCard(state, player, CardId.Knight, robberTarget: hex));
        }
    }
}

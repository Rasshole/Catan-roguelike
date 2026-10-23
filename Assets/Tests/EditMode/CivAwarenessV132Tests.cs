using System;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Ai;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>v132: Merchant Republic drawback and basic AI awareness of civilizations / events.</summary>
    public class CivAwarenessV132Tests
    {
        private static GameState CreateState(CivilizationId civ = CivilizationId.None) =>
            new(MapPresets.CreateBoard(MapSize.Small)) { Civilization = civ };

        private static HexTileData TokenTile(GameState state, ResourceType? resource = null) =>
            state.Board.Tiles.Values.First(t => !t.IsDesert && !t.IsFishTile && t.NumberToken.HasValue
                                                && (!resource.HasValue || t.Resource == resource.Value));

        [Test]
        public void MerchantRepublic_NowHasADrawback()
        {
            var def = CivilizationLibrary.Get(CivilizationId.MerchantRepublic);
            Assert.IsTrue(def.HasDrawback);
            Assert.That(def.Drawback, Does.Contain("7"));
        }

        [Test]
        public void Merchant_PaysPirateTariffOnSeven()
        {
            var state = CreateState(CivilizationId.MerchantRepublic);
            state.PlayerInventory = new ResourceBundle { Wheat = 2 };
            string log = CivilizationRules.OnSevenRolled(state, new Random(1));
            Assert.AreEqual(1, state.PlayerInventory.Wheat);
            Assert.That(log, Does.Contain("tariff"));

            var broke = CreateState(CivilizationId.MerchantRepublic);
            Assert.That(CivilizationRules.OnSevenRolled(broke, new Random(1)), Does.Contain("empty"));
            Assert.AreEqual(0, broke.PlayerInventory.Total);
        }

        [Test]
        public void Ai_RobberAgainstSunKingdom_OnlyValuesTheSteal()
        {
            var plain = CreateState();
            var sun = CreateState(CivilizationId.SunKingdom);
            var tile = TokenTile(plain);
            int pip = NumberTokenLibrary.GetPipWeight(tile.NumberToken.Value);
            Assert.AreEqual(2f * pip, AiAwareness.RobberHexScore(plain, tile, 2, 0), 0.001f);
            Assert.AreEqual(2f, AiAwareness.RobberHexScore(sun, tile, 2, 0), 0.001f);
            Assert.IsFalse(AiAwareness.RobberBlocksHuman(sun));
            Assert.IsTrue(AiAwareness.RobberBlocksHuman(plain));
        }

        [Test]
        public void Ai_RobberPrefersCivFavouredAndBountyHexes_AvoidsBlightedAndOwnHexes()
        {
            var plain = CreateState();
            var highland = CreateState(CivilizationId.HighlandClans);
            var stone = TokenTile(plain, ResourceType.Stone);
            float baseScore = AiAwareness.RobberHexScore(plain, stone, 1, 0);
            Assert.Greater(AiAwareness.RobberHexScore(highland, stone, 1, 0), baseScore);

            plain.EventBonusResource = ResourceType.Stone;
            Assert.Greater(AiAwareness.RobberHexScore(plain, stone, 1, 0), baseScore);
            plain.EventBonusResource = null;
            plain.EventBlockedResource = ResourceType.Stone;
            Assert.Less(AiAwareness.RobberHexScore(plain, stone, 1, 0), baseScore);
            plain.EventBlockedResource = null;
            Assert.Less(AiAwareness.RobberHexScore(plain, stone, 1, 1), baseScore);
            Assert.AreEqual(0f, AiAwareness.RobberHexScore(plain, stone, 0, 0));
        }

        [Test]
        public void Ai_SettlementScoring_ReactsToTodaysEvent()
        {
            var state = CreateState();
            var wheat = TokenTile(state, ResourceType.Wheat);
            Assert.AreEqual(0f, AiAwareness.SettlementHexEventBonus(state, wheat));
            state.EventBonusResource = ResourceType.Wheat;
            Assert.Greater(AiAwareness.SettlementHexEventBonus(state, wheat), 0f);
            state.EventBonusResource = null;
            state.EventBlockedResource = ResourceType.Wheat;
            Assert.Less(AiAwareness.SettlementHexEventBonus(state, wheat), 0f);
            state.EventBlockedResource = null;
            state.EventStormTile = wheat.Coord;
            Assert.Less(AiAwareness.SettlementHexEventBonus(state, wheat), 0f);
        }

        [Test]
        public void Ai_CivFavouredResource_MatchesPassives()
        {
            Assert.AreEqual(ResourceType.Stone, AiAwareness.CivFavouredResource(CreateState(CivilizationId.HighlandClans)));
            Assert.AreEqual(ResourceType.Sheep, AiAwareness.CivFavouredResource(CreateState(CivilizationId.ShepherdKingdom)));
            Assert.AreEqual(ResourceType.Wood, AiAwareness.CivFavouredResource(CreateState(CivilizationId.ForestFolk)));
            Assert.IsNull(AiAwareness.CivFavouredResource(CreateState(CivilizationId.SeaFolk)));
        }
    }
}

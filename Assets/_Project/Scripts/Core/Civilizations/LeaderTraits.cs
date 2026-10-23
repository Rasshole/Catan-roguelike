using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Civilizations
{
    /// <summary>
    /// v135: every civilization's leader has their OWN passive, so no two civ cards share leader text.
    /// The four "archetype" civs (Merchant Republic, Forest Folk, Brickwrights, Northern Raiders) keep their
    /// archetype's classic passive (<see cref="LeaderLibrary"/>); the other six have a signature trait here.
    /// The archetype (<see cref="CivilizationDefinition.Leader"/>) still supplies the level-up perk pool and unlock.
    /// Human player only; pure functions of the state (unit-testable).
    /// </summary>
    public static class LeaderTraits
    {
        public sealed class Trait
        {
            public string Title { get; }
            public string Description { get; }
            /// <summary>True when the trait IS the archetype's classic passive (rules read <see cref="GameState.Leader"/>).</summary>
            public bool IsArchetypePassive { get; }

            public Trait(string title, string description, bool isArchetypePassive)
            {
                Title = title;
                Description = description;
                IsArchetypePassive = isArchetypePassive;
            }
        }

        private static Trait Archetype(LeaderId id) =>
            new(LeaderLibrary.Get(id).Name, LeaderLibrary.Get(id).PassiveDescription, true);

        public static readonly IReadOnlyDictionary<CivilizationId, Trait> ByCivilization =
            new Dictionary<CivilizationId, Trait>
            {
                [CivilizationId.MerchantRepublic] = Archetype(LeaderId.Merchant),
                [CivilizationId.ForestFolk] = Archetype(LeaderId.Pioneer),
                [CivilizationId.Brickwrights] = Archetype(LeaderId.Architect),
                [CivilizationId.NorthernRaiders] = Archetype(LeaderId.Warlord),
                [CivilizationId.SeaFolk] = new("The Tidecaller", "Each morning, gain 1 Fish.", false),
                [CivilizationId.SteppeNomads] = new("The Horse Lord", "Each morning, gain 1 Wool.", false),
                [CivilizationId.HighlandClans] = new("The Clan Muster", "Whenever a 7 is rolled, gain 1 Ore.", false),
                [CivilizationId.ShepherdKingdom] = new("The Good Harvest",
                    "Whenever a 2 or 12 is rolled, gain 1 Wheat.", false),
                [CivilizationId.Academy] = new("The Scholar",
                    "Each morning, gain 1 of the resource you hold least of.", false),
                [CivilizationId.SunKingdom] = new("The Sun Tithe", "Whenever a 7 is rolled, gain 1 Wheat.", false),
            };

        public static Trait Get(CivilizationId id) =>
            ByCivilization.TryGetValue(id, out var t) ? t : null;

        /// <summary>
        /// Whether the archetype passive of <paramref name="leader"/> applies: the state's leader is that archetype
        /// AND (no civilization chosen — legacy/tests — or the civ's trait IS the archetype passive).
        /// </summary>
        public static bool HasArchetypePassive(GameState state, LeaderId leader)
        {
            if (state == null || state.Leader != leader)
                return false;
            var trait = Get(state.Civilization);
            return trait == null || trait.IsArchetypePassive;
        }

        /// <summary>Morning gains, applied once per day before production. Returns a log line or null.</summary>
        public static string OnMorning(GameState state)
        {
            if (state == null)
                return null;
            switch (state.Civilization)
            {
                case CivilizationId.SeaFolk:
                    state.PlayerFish += 1;
                    return "Tide-Queen Maren: +1 Fish.";
                case CivilizationId.SteppeNomads:
                {
                    var inv = state.PlayerInventory;
                    inv.Add(ResourceType.Sheep, 1);
                    state.PlayerInventory = inv;
                    return "Khan Temur: +1 Wool.";
                }
                case CivilizationId.Academy:
                {
                    var inv = state.PlayerInventory;
                    var scarcest = ScarcestResource(inv);
                    inv.Add(scarcest, 1);
                    state.PlayerInventory = inv;
                    return $"Rector Sophia: +1 {Events.EventText.ResourceName(scarcest)}.";
                }
                default:
                    return null;
            }
        }

        /// <summary>7-roll leader gains. Returns a log line or null.</summary>
        public static string OnSevenRolled(GameState state)
        {
            if (state == null)
                return null;
            ResourceType? gain = state.Civilization switch
            {
                CivilizationId.HighlandClans => ResourceType.Stone,
                CivilizationId.SunKingdom => ResourceType.Wheat,
                _ => null
            };
            if (gain == null)
                return null;
            var inv = state.PlayerInventory;
            inv.Add(gain.Value, 1);
            state.PlayerInventory = inv;
            string who = state.Civilization == CivilizationId.HighlandClans ? "Chieftain Brann" : "Sun King Amaru";
            return $"{who}: +1 {Events.EventText.ResourceName(gain.Value)}.";
        }

        /// <summary>Dice-sum leader gains (Shepherd Kingdom on 2 and 12).</summary>
        public static ResourceBundle DiceBonus(GameState state, PlayerId player, IReadOnlyList<int> diceRolls)
        {
            var bonus = ResourceBundle.Zero;
            if (!CivilizationRules.Is(state, player, CivilizationId.ShepherdKingdom) || diceRolls == null)
                return bonus;
            foreach (int roll in diceRolls)
            {
                if (roll == 2 || roll == 12)
                    bonus.Add(ResourceType.Wheat, 1);
            }

            return bonus;
        }

        /// <summary>Lowest-held productive resource; ties go to the first in <see cref="ResourceTypes.Productive"/>.</summary>
        public static ResourceType ScarcestResource(ResourceBundle inv)
        {
            var best = ResourceTypes.Productive.First();
            foreach (var r in ResourceTypes.Productive)
            {
                if (inv[r] < inv[best])
                    best = r;
            }

            return best;
        }
    }
}

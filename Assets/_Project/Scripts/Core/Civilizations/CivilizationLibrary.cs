using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Leaders;

namespace CatanRoguelike.Core.Civilizations
{
    /// <summary>
    /// One civilization: an asymmetric passive and (sometimes) a drawback. Rules live in <see cref="CivilizationRules"/>.
    /// v134 (DECIDED): civilizations and leaders are ONE concept — every civilization is led by a named leader whose
    /// portrait is the civ's face. The leader's archetype (<see cref="Leader"/>) brings its passive and level-up perk
    /// pool as the run-progression layer, so picking a civ is the only "who am I" choice.
    /// </summary>
    public sealed class CivilizationDefinition
    {
        public CivilizationId Id { get; }
        public string Name { get; }
        public string Tagline { get; }
        public string Passive { get; }
        /// <summary>Empty when the civilization has no drawback.</summary>
        public string Drawback { get; }
        /// <summary>Resources/Art/Civilizations/{EmblemKey} painted emblem.</summary>
        public string EmblemKey { get; }

        /// <summary>Leader archetype: passive + level-up perk pool (see <see cref="LeaderLibrary"/>).</summary>
        public LeaderId Leader { get; }
        /// <summary>The named leader shown on the civ card ("Led by …").</summary>
        public string LeaderName { get; }
        /// <summary>
        /// Resources/Art/Leaders/{PortraitKey} fallback portrait (an existing leader painting that fits the theme).
        /// Art/Leaders/{Id} is tried first (v135: all six non-archetype civs have one); empty = emblem fallback.
        /// </summary>
        public string PortraitKey { get; }

        public bool HasDrawback => !string.IsNullOrEmpty(Drawback);
        /// <summary>v135: this civ's own leader passive (unique per civ, see <see cref="LeaderTraits"/>).</summary>
        public string LeaderAbility => LeaderTraits.Get(Id)?.Description ?? LeaderLibrary.Get(Leader).PassiveDescription;
        /// <summary>v135: title of the leader passive (e.g. "The Tidecaller").</summary>
        public string LeaderTitle => LeaderTraits.Get(Id)?.Title ?? LeaderLibrary.Get(Leader).Name;

        public CivilizationDefinition(CivilizationId id, string name, string tagline, string passive, string drawback,
            string emblemKey, LeaderId leader = LeaderId.Merchant, string leaderName = "", string portraitKey = "")
        {
            Id = id;
            Name = name;
            Tagline = tagline;
            Passive = passive;
            Drawback = drawback ?? "";
            EmblemKey = emblemKey;
            Leader = leader;
            LeaderName = leaderName ?? "";
            PortraitKey = portraitKey ?? "";
        }
    }

    /// <summary>The civilization pool; three are offered at random at run start (see <see cref="CivilizationOffer"/>).</summary>
    public static class CivilizationLibrary
    {
        public static readonly IReadOnlyList<CivilizationDefinition> Pool = new[]
        {
            new CivilizationDefinition(CivilizationId.SeaFolk, "The Sea Folk", "Children of the tide",
                "+1 fish whenever your fishing grounds catch. Fish-shop actions cost 1 fish less.",
                "", "SeaFolk",
                LeaderId.Merchant, "Tide-Queen Maren", ""),
            new CivilizationDefinition(CivilizationId.ForestFolk, "The Forest Folk", "Keepers of the old wood",
                "When a 2 or 12 is rolled, gain 1 Lumber per forest you border (at least 1).",
                "", "ForestFolk",
                LeaderId.Pioneer, "Warden Elm", "Pioneer"),
            new CivilizationDefinition(CivilizationId.MerchantRepublic, "Merchant Republic", "Every coin finds a purse",
                "Bank trades are 3:1 everywhere (2:1 ports still apply).",
                "Whenever a 7 is rolled, lose 1 random resource (pirate tariff).", "MerchantRepublic",
                LeaderId.Merchant, "Doge Valerio", "Merchant"),
            new CivilizationDefinition(CivilizationId.SteppeNomads, "Steppe Nomads", "Home is where the herd rests",
                "Settlements cost no Brick.",
                "Cities cost 1 more Ore.", "SteppeNomads",
                LeaderId.Pioneer, "Khan Temur", ""),
            new CivilizationDefinition(CivilizationId.HighlandClans, "Highland Clans", "Born under the peaks",
                "Mountains yield +1 Ore per hit.",
                "Pastures yield 1 less Wool per hit.", "HighlandClans",
                LeaderId.Warlord, "Chieftain Brann", ""),
            new CivilizationDefinition(CivilizationId.ShepherdKingdom, "Shepherd Kingdom", "Wool is wealth",
                "Start with 2 extra Wool. Pastures yield +1 Wool per hit.",
                "Cities cost 1 more Wheat.", "ShepherdKingdom",
                LeaderId.Pioneer, "Queen Elsbeth", ""),
            new CivilizationDefinition(CivilizationId.Brickwrights, "The Brickwrights", "Kiln-fire and mortar",
                "Roads cost no Lumber. Whenever a 7 is rolled, gain 1 Brick.",
                "", "Brickwrights",
                LeaderId.Architect, "Master Mason Orrin", "Architect"),
            new CivilizationDefinition(CivilizationId.Academy, "The Academy", "Knowledge is power",
                "Draw 1 extra card every night.",
                "Settlements cost 1 more Wheat.", "Academy",
                LeaderId.Architect, "Rector Sophia", ""),
            new CivilizationDefinition(CivilizationId.NorthernRaiders, "Northern Raiders", "The longships are coming",
                "Whenever a 7 is rolled, steal 1 random resource from your opponent.",
                "Bank trades without a port are 5:1.", "NorthernRaiders",
                LeaderId.Warlord, "Jarl Hakon", "Warlord"),
            new CivilizationDefinition(CivilizationId.SunKingdom, "Sun Kingdom", "Blessed by the noon sun",
                "The robber never blocks your production.",
                "", "SunKingdom",
                LeaderId.Merchant, "Sun King Amaru", "")
        };

        private static readonly Dictionary<CivilizationId, CivilizationDefinition> ById =
            Pool.ToDictionary(d => d.Id);

        public static CivilizationDefinition Get(CivilizationId id) => ById[id];

        public static bool TryGet(CivilizationId id, out CivilizationDefinition definition) =>
            ById.TryGetValue(id, out definition);
    }

    /// <summary>Deterministic 3-of-pool offer for a run seed.</summary>
    public static class CivilizationOffer
    {
        public const int OfferSize = 3;

        /// <param name="isAvailable">v134: optional filter (e.g. the civ's leader is unlocked); the seeded shuffle is
        /// unchanged, unavailable civs are skipped.</param>
        public static List<CivilizationId> Generate(int runSeed, int count = OfferSize,
            System.Func<CivilizationDefinition, bool> isAvailable = null)
        {
            var random = new System.Random(unchecked(runSeed * 7919 + 131));
            var ids = CivilizationLibrary.Pool.Select(d => d.Id).ToList();
            for (int i = ids.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (ids[i], ids[j]) = (ids[j], ids[i]);
            }

            return ids.Where(id => isAvailable == null || isAvailable(CivilizationLibrary.Get(id)))
                .Take(System.Math.Max(0, count)).ToList();
        }
    }
}

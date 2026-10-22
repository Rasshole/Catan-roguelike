using System.Collections.Generic;
using System.Linq;

namespace CatanRoguelike.Core.Civilizations
{
    /// <summary>One civilization: an asymmetric passive and (sometimes) a drawback. Rules live in <see cref="CivilizationRules"/>.</summary>
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

        public bool HasDrawback => !string.IsNullOrEmpty(Drawback);

        public CivilizationDefinition(CivilizationId id, string name, string tagline, string passive, string drawback,
            string emblemKey)
        {
            Id = id;
            Name = name;
            Tagline = tagline;
            Passive = passive;
            Drawback = drawback ?? "";
            EmblemKey = emblemKey;
        }
    }

    /// <summary>The civilization pool; three are offered at random at run start (see <see cref="CivilizationOffer"/>).</summary>
    public static class CivilizationLibrary
    {
        public static readonly IReadOnlyList<CivilizationDefinition> Pool = new[]
        {
            new CivilizationDefinition(CivilizationId.SeaFolk, "The Sea Folk", "Children of the tide",
                "+1 fish whenever your fishing grounds catch. Fish-shop actions cost 1 fish less.",
                "", "SeaFolk"),
            new CivilizationDefinition(CivilizationId.ForestFolk, "The Forest Folk", "Keepers of the old wood",
                "When a 2 or 12 is rolled, gain 1 Lumber per forest you border (at least 1).",
                "", "ForestFolk"),
            new CivilizationDefinition(CivilizationId.MerchantRepublic, "Merchant Republic", "Every coin finds a purse",
                "Bank trades are 3:1 everywhere (2:1 ports still apply).",
                "", "MerchantRepublic"),
            new CivilizationDefinition(CivilizationId.SteppeNomads, "Steppe Nomads", "Home is where the herd rests",
                "Settlements cost no Brick.",
                "Cities cost 1 more Ore.", "SteppeNomads"),
            new CivilizationDefinition(CivilizationId.HighlandClans, "Highland Clans", "Born under the peaks",
                "Mountains yield +1 Ore per hit.",
                "Pastures yield 1 less Wool per hit.", "HighlandClans"),
            new CivilizationDefinition(CivilizationId.ShepherdKingdom, "Shepherd Kingdom", "Wool is wealth",
                "Start with 2 extra Wool. Pastures yield +1 Wool per hit.",
                "Cities cost 1 more Wheat.", "ShepherdKingdom"),
            new CivilizationDefinition(CivilizationId.Brickwrights, "The Brickwrights", "Kiln-fire and mortar",
                "Roads cost no Lumber. Whenever a 7 is rolled, gain 1 Brick.",
                "", "Brickwrights"),
            new CivilizationDefinition(CivilizationId.Academy, "The Academy", "Knowledge is power",
                "Draw 1 extra card every night.",
                "Settlements cost 1 more Wheat.", "Academy"),
            new CivilizationDefinition(CivilizationId.NorthernRaiders, "Northern Raiders", "The longships are coming",
                "Whenever a 7 is rolled, steal 1 random resource from your opponent.",
                "Bank trades without a port are 5:1.", "NorthernRaiders"),
            new CivilizationDefinition(CivilizationId.SunKingdom, "Sun Kingdom", "Blessed by the noon sun",
                "The robber never blocks your production.",
                "", "SunKingdom")
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

        public static List<CivilizationId> Generate(int runSeed, int count = OfferSize)
        {
            var random = new System.Random(unchecked(runSeed * 7919 + 131));
            var ids = CivilizationLibrary.Pool.Select(d => d.Id).ToList();
            for (int i = ids.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (ids[i], ids[j]) = (ids[j], ids[i]);
            }

            return ids.Take(System.Math.Min(count, ids.Count)).ToList();
        }
    }
}

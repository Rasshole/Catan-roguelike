namespace CatanRoguelike.Core.Cards
{
    /// <summary>Rarity / category accent colors for roguelike card frames (RGB 0–1).</summary>
    public static class CardVisualStyle
    {
        public static readonly Rgb RollManipulation = new(0.35f, 0.62f, 0.95f);
        public static readonly Rgb BuildTempo = new(0.28f, 0.72f, 0.42f);
        public static readonly Rgb Sabotage = new(0.88f, 0.32f, 0.28f);
        public static readonly Rgb Synergy = new(0.78f, 0.55f, 0.18f);

        public static Rgb GetCategoryColor(CardCategory category) => category switch
        {
            CardCategory.RollManipulation => RollManipulation,
            CardCategory.BuildTempo => BuildTempo,
            CardCategory.Sabotage => Sabotage,
            CardCategory.Synergy => Synergy,
            _ => new Rgb(0.55f, 0.55f, 0.55f)
        };

        public static string GetCategoryLabel(CardCategory category) => category switch
        {
            CardCategory.RollManipulation => "Terning",
            CardCategory.BuildTempo => "Byg",
            CardCategory.Sabotage => "Sab.",
            CardCategory.Synergy => "Synergi",
            _ => "Kort"
        };

        public readonly struct Rgb
        {
            public readonly float R;
            public readonly float G;
            public readonly float B;

            public Rgb(float r, float g, float b)
            {
                R = r;
                G = g;
                B = b;
            }
        }
    }
}

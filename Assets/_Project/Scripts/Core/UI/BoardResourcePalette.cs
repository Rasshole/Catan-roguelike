namespace CatanRoguelike.Core.UI
{
    /// <summary>Bright, saturated cartoon hex colors (colonist.io-inspired, original palette).</summary>
    public static class BoardResourcePalette
    {
        public static readonly Rgb Desert = new(0.93f, 0.82f, 0.52f);
        public static readonly Rgb Wood = new(0.18f, 0.62f, 0.22f);
        public static readonly Rgb Brick = new(0.82f, 0.38f, 0.22f);
        public static readonly Rgb Wheat = new(0.91f, 0.77f, 0.28f);
        public static readonly Rgb Sheep = new(0.62f, 0.92f, 0.55f);
        public static readonly Rgb Stone = new(0.49f, 0.50f, 0.53f);
        public static readonly Rgb TileOutline = new(0.12f, 0.14f, 0.16f);
        public static readonly Rgb TopBevelHighlight = new(1f, 1f, 1f);

        public static Rgb GetResource(ResourceType resource) => resource switch
        {
            ResourceType.Wood => Wood,
            ResourceType.Brick => Brick,
            ResourceType.Wheat => Wheat,
            ResourceType.Sheep => Sheep,
            ResourceType.Stone => Stone,
            _ => new Rgb(0.5f, 0.5f, 0.5f)
        };

        public static Rgb GetHudSwatch(ResourceType resource) => GetResource(resource);

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

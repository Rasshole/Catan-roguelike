namespace CatanRoguelike.Core.Data
{
    public readonly struct MapGenerationOptions
    {
        public bool FishermenOfCatan { get; }

        /// <summary>
        /// v137: shuffle the terrain of the hand-laid 19-hex base map per seed (pass-and-play matches, like normal
        /// Catan). The 30/37-hex maps are always shuffled; the single-player base map keeps its designed layout.
        /// </summary>
        public bool ShuffleTerrain { get; }

        public MapGenerationOptions(bool fishermenOfCatan = false, bool shuffleTerrain = false)
        {
            FishermenOfCatan = fishermenOfCatan;
            ShuffleTerrain = shuffleTerrain;
        }

        public static MapGenerationOptions Default => new(false);
    }
}

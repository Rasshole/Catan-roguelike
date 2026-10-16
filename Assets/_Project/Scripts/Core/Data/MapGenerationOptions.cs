namespace CatanRoguelike.Core.Data
{
    public readonly struct MapGenerationOptions
    {
        public bool FishermenOfCatan { get; }

        public MapGenerationOptions(bool fishermenOfCatan = false)
        {
            FishermenOfCatan = fishermenOfCatan;
        }

        public static MapGenerationOptions Default => new(false);
    }
}

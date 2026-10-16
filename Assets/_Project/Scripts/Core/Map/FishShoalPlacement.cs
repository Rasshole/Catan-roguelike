namespace CatanRoguelike.Core.Map
{
    /// <summary>Coastal fishing ground (Fishermen): triangle on a land–sea edge, not a full sea hex.</summary>
    public readonly struct FishShoalPlacement
    {
        public FishShoalPlacement(
            MapPlaneXz coastVertexA,
            MapPlaneXz coastVertexB,
            MapPlaneXz seaTip,
            MapPlaneXz tokenCenter,
            MapPlaneXz outward)
        {
            CoastVertexA = coastVertexA;
            CoastVertexB = coastVertexB;
            SeaTip = seaTip;
            TokenCenter = tokenCenter;
            Outward = outward;
        }

        public MapPlaneXz CoastVertexA { get; }
        public MapPlaneXz CoastVertexB { get; }
        public MapPlaneXz SeaTip { get; }
        public MapPlaneXz TokenCenter { get; }
        public MapPlaneXz Outward { get; }
    }
}

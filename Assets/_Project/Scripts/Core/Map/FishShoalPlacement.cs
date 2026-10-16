using CatanRoguelike.Core.Hex;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Core.Map
{
    /// <summary>
    /// Fishermen fishing ground: blue chevron frame with inland V-notch, flat outer edge toward open water.
    /// </summary>
    public readonly struct FishShoalPlacement
    {
        public FishShoalPlacement(
            MapPlaneXz inlandTip,
            MapPlaneXz coastArmLeft,
            MapPlaneXz coastArmRight,
            MapPlaneXz seaFlatLeft,
            MapPlaneXz seaFlatRight,
            MapPlaneXz tokenCenter,
            MapPlaneXz outward,
            Vertex borderVertexInland,
            Vertex borderVertexLeft,
            Vertex borderVertexRight)
        {
            InlandTip = inlandTip;
            CoastArmLeft = coastArmLeft;
            CoastArmRight = coastArmRight;
            SeaFlatLeft = seaFlatLeft;
            SeaFlatRight = seaFlatRight;
            TokenCenter = tokenCenter;
            Outward = outward;
            BorderVertexInland = borderVertexInland;
            BorderVertexLeft = borderVertexLeft;
            BorderVertexRight = borderVertexRight;
        }

        /// <summary>V-notch tip nesting into the coast between two land hexes.</summary>
        public MapPlaneXz InlandTip { get; }

        public MapPlaneXz CoastArmLeft { get; }
        public MapPlaneXz CoastArmRight { get; }
        public MapPlaneXz SeaFlatLeft { get; }
        public MapPlaneXz SeaFlatRight { get; }
        public MapPlaneXz TokenCenter { get; }
        public MapPlaneXz Outward { get; }

        /// <summary>Three intersections that receive fish when this ground produces.</summary>
        public Vertex BorderVertexInland { get; }
        public Vertex BorderVertexLeft { get; }
        public Vertex BorderVertexRight { get; }

        public bool BordersVertex(Vertex v)
        {
            v = VertexGraph.Canonicalize(v);
            return v.Equals(VertexGraph.Canonicalize(BorderVertexInland))
                || v.Equals(VertexGraph.Canonicalize(BorderVertexLeft))
                || v.Equals(VertexGraph.Canonicalize(BorderVertexRight));
        }
    }
}

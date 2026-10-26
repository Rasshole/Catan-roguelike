using CatanRoguelike.Core.Hex;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Core.Map
{
    /// <summary>
    /// Fishermen fishing ground: 6-vertex chevron band (coast V-notch + seaward offset).
    /// </summary>
    public readonly struct FishShoalPlacement
    {
        public FishShoalPlacement(
            MapPlaneXz inlandTip,
            MapPlaneXz coastArmLeft,
            MapPlaneXz coastArmRight,
            MapPlaneXz seaFlatLeft,
            MapPlaneXz seaFlatRight,
            MapPlaneXz seaBandApex,
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
            SeaBandApex = seaBandApex;
            TokenCenter = tokenCenter;
            Outward = outward;
            BorderVertexInland = borderVertexInland;
            BorderVertexLeft = borderVertexLeft;
            BorderVertexRight = borderVertexRight;
        }

        /// <summary>Coastal vertex C between two land hexes.</summary>
        public MapPlaneXz InlandTip { get; }

        /// <summary>Outer coast corner P1 on the A–S edge.</summary>
        public MapPlaneXz CoastArmLeft { get; }

        /// <summary>Outer coast corner P2 on the B–S edge.</summary>
        public MapPlaneXz CoastArmRight { get; }

        /// <summary>Seaward offset P1'.</summary>
        public MapPlaneXz SeaFlatLeft { get; }

        /// <summary>Seaward offset P2'.</summary>
        public MapPlaneXz SeaFlatRight { get; }

        /// <summary>Seaward apex C' (uniform band thickness).</summary>
        public MapPlaneXz SeaBandApex { get; }

        public MapPlaneXz TokenCenter { get; }
        public MapPlaneXz Outward { get; }

        public Vertex BorderVertexInland { get; }
        public Vertex BorderVertexLeft { get; }
        public Vertex BorderVertexRight { get; }

        public bool BordersVertex(Vertex v)
        {
            v = VertexGraph.Canonicalize(v);
            return v.Equals(Inland)
                || v.Equals(Left)
                || v.Equals(Right);
        }

        private Vertex Inland => VertexGraph.Canonicalize(BorderVertexInland);
        private Vertex Left => VertexGraph.Canonicalize(BorderVertexLeft);
        private Vertex Right => VertexGraph.Canonicalize(BorderVertexRight);
    }
}

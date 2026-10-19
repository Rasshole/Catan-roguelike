namespace CatanRoguelike.Game
{
    /// <summary>
    /// AI-painted hex tops baked by tools/art/make_ai_hex_tiles.py (Resources/Art/Board/Blender). When enabled, land hexes use
    /// these albedos and skip Kenney top-down scatter decor so v114 can be restored by toggling off.
    /// </summary>
    public static class ColonistBlenderHexTerrainArt
    {
        /// <summary>Set false to restore v114 legacy board textures + template decor.</summary>
        public static bool Enabled { get; internal set; } = true;

        public const string TextureFolder = "Art/Board/Blender";

        /// <summary>
        /// Repeated terrains vary per tile: about half the painted tops are shown turned 180 degrees
        /// (the hex frame is symmetric under that turn, the painting is not). Deterministic per coord.
        /// </summary>
        public static bool IsRotated180(CatanRoguelike.Core.Hex.HexCoord coord)
        {
            unchecked
            {
                uint h = (uint)(coord.Q * 73856093) ^ (uint)(coord.R * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 1u) == 1u;
            }
        }

        /// <summary>Turns a painted-top material 180 degrees (u,v to 1-u,1-v; textures wrap Repeat).</summary>
        public static void ApplyRotation180(UnityEngine.Material material)
        {
            material.mainTextureScale = new UnityEngine.Vector2(-1f, -1f);
            material.mainTextureOffset = new UnityEngine.Vector2(1f, 1f);
        }
    }
}

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

        /// <summary>Mip bias for the 2048 painted albedos: slightly sharper than the default level.</summary>
        public const float MipMapBias = -0.5f;
    }
}

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v116 Blender-painted hex tops (Resources/Art/Board/Blender). When enabled, land hexes use
    /// these albedos and skip Kenney top-down scatter decor so v114 can be restored by toggling off.
    /// </summary>
    public static class ColonistBlenderHexTerrainArt
    {
        /// <summary>Set false to restore v114 legacy board textures + template decor.</summary>
        public static bool Enabled { get; internal set; } = false;

        public const string TextureFolder = "Art/Board/Blender";
    }
}

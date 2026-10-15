using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.UI;
using UnityEngine;

namespace CatanRoguelike.Game
{
    public static class BoardResourceColors
    {
        public static Color ToUnityColor(BoardResourcePalette.Rgb rgb) =>
            new Color(rgb.R, rgb.G, rgb.B);

        public static Color GetTileFace(HexTileData data)
        {
            if (data.IsDesert)
                return ToUnityColor(BoardResourcePalette.Desert);
            return ToUnityColor(BoardResourcePalette.GetResource(data.Resource));
        }

        public static Color GetTileTopBevel(HexTileData data)
        {
            var face = GetTileFace(data);
            return Color.Lerp(face, ToUnityColor(BoardResourcePalette.TopBevelHighlight), 0.22f);
        }

        public static Color GetOutline() => ToUnityColor(BoardResourcePalette.TileOutline);

        public static Color GetHudSwatch(ResourceType resource) =>
            ToUnityColor(BoardResourcePalette.GetHudSwatch(resource));
    }
}

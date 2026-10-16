using System.Collections.Generic;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Loads painted terrain textures from Resources/Art when present; falls back to flat palette colors.
    /// </summary>
    public static class ColonistBoardTextureLoader
    {
        private const string ArtRoot = "Art/Board";

        private static readonly Dictionary<BoardTerrainSlot, Material> _cache = new();

        public static Material GetTerrainMaterial(BoardTerrainSlot slot)
        {
            if (_cache.TryGetValue(slot, out var cached))
                return cached;

            var fallback = GetFallbackColor(slot);
            Texture2D texture = Resources.Load<Texture2D>($"{ArtRoot}/{slot}");
            if (texture == null)
            {
                try
                {
                    texture = ColonistProceduralBoardTextures.Create(slot);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"ColonistBoardTextureLoader: procedural texture failed for {slot}: {ex.Message}");
                    texture = null;
                }
            }

            Material mat = BuiltInMaterials.CreateTerrainSurface(texture, fallback);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;

            _cache[slot] = mat;
            return mat;
        }

        public static void ClearCache() => _cache.Clear();

        public static Color GetFallbackColorForSlot(BoardTerrainSlot slot) => GetFallbackColor(slot);

        private static Color GetFallbackColor(BoardTerrainSlot slot) => slot switch
        {
            BoardTerrainSlot.Wood => BoardResourceColors.GetTileFace(new HexTileData(default, Core.ResourceType.Wood)),
            BoardTerrainSlot.Brick => BoardResourceColors.GetTileFace(new HexTileData(default, Core.ResourceType.Brick)),
            BoardTerrainSlot.Wheat => BoardResourceColors.GetTileFace(new HexTileData(default, Core.ResourceType.Wheat)),
            BoardTerrainSlot.Sheep => BoardResourceColors.GetTileFace(new HexTileData(default, Core.ResourceType.Sheep)),
            BoardTerrainSlot.Ore => BoardResourceColors.GetTileFace(new HexTileData(default, Core.ResourceType.Stone)),
            BoardTerrainSlot.Desert => BoardResourceColors.GetTileFace(new HexTileData(default, Core.ResourceType.Wood) { IsDesert = true }),
            BoardTerrainSlot.SeaLake => new Color(0.22f, 0.48f, 0.72f),
            BoardTerrainSlot.CoastSand => new Color(0.94f, 0.88f, 0.72f),
            _ => Color.gray
        };
    }
}

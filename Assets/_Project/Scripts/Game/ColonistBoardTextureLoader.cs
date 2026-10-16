using System.Collections.Generic;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Loads painted terrain textures from Resources/Art when present; falls back to flat palette colors.
    /// Uses the same Standard opaque path as v66 (batchmode-safe); optional procedural albedo on Standard only.
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

            Material mat = texture != null
                ? BuiltInMaterials.Create(texture, Color.white)
                : BuiltInMaterials.CreateOpaque(fallback);

            ApplyTerrainSurface(mat);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            ColonistBoardTerrainDiagnostics.LogTerrainMaterialCreation(slot, mat, texture, fallback);

            _cache[slot] = mat;
            return mat;
        }

        public static Material CreateSeaFishGroundMaterial()
        {
            var color = GetFallbackColor(BoardTerrainSlot.SeaLake);
            return BuiltInMaterials.CreateOpaque(color);
        }

        public static void ClearCache() => _cache.Clear();

        public static bool HasCommittedTerrainArt() =>
            Resources.Load<Texture2D>($"{ArtRoot}/Wood") != null;

        private static void ApplyTerrainSurface(Material mat)
        {
            if (mat == null)
                return;
            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", 0.1f);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0f);
        }

        public static Color GetFallbackColorForSlot(BoardTerrainSlot slot) => GetFallbackColor(slot);

        private static Color GetFallbackColor(BoardTerrainSlot slot) => slot switch
        {
            BoardTerrainSlot.Wood => BoardResourceColors.GetTileFace(new HexTileData(default, Core.ResourceType.Wood)),
            BoardTerrainSlot.Brick => BoardResourceColors.GetTileFace(new HexTileData(default, Core.ResourceType.Brick)),
            BoardTerrainSlot.Wheat => BoardResourceColors.GetTileFace(new HexTileData(default, Core.ResourceType.Wheat)),
            BoardTerrainSlot.Sheep => BoardResourceColors.GetTileFace(new HexTileData(default, Core.ResourceType.Sheep)),
            BoardTerrainSlot.Ore => BoardResourceColors.GetTileFace(new HexTileData(default, Core.ResourceType.Stone)),
            BoardTerrainSlot.Desert => BoardResourceColors.GetTileFace(new HexTileData(default, Core.ResourceType.Wood) { IsDesert = true }),
            BoardTerrainSlot.SeaLake => new Color(0.55f, 0.78f, 0.92f),
            BoardTerrainSlot.CoastSand => new Color(0.94f, 0.88f, 0.72f),
            BoardTerrainSlot.FishShoal => new Color(0.42f, 0.62f, 0.56f),
            _ => Color.gray
        };
    }
}

using System.Text;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>One-shot logs for colonist terrain material debugging (batchmode / -nographics).</summary>
    public static class ColonistBoardTerrainDiagnostics
    {
        private static bool _loggedLoader;
        private static bool _loggedSampleTile;

        public static void LogTerrainMaterialCreation(
            BoardTerrainSlot slot,
            Material mat,
            Texture2D sourceTexture,
            Color flatFallback)
        {
            if (_loggedLoader)
                return;

            _loggedLoader = true;
            string shaderName = mat != null && mat.shader != null ? mat.shader.name : "null";
            string texInfo = DescribeTexture(sourceTexture);
            Color matColor = mat != null && mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.magenta;
            Texture bound = mat != null ? mat.mainTexture : null;

            Debug.Log(
                $"ColonistTerrainDiag loader slot={slot} shader={shaderName} " +
                $"matColor=({matColor.r:F3},{matColor.g:F3},{matColor.b:F3}) " +
                $"mainTexture={(bound != null ? bound.name : "null")} source={texInfo} " +
                $"fallback=({flatFallback.r:F3},{flatFallback.g:F3},{flatFallback.b:F3})");
        }

        public static void LogSampleLandHexRenderers(HexTileView tile)
        {
            if (_loggedSampleTile || tile == null)
                return;

            _loggedSampleTile = true;
            var sb = new StringBuilder();
            sb.AppendLine($"ColonistTerrainDiag sample hex {tile.Data.Coord} fish={tile.Data.IsFishTile} desert={tile.Data.IsDesert}");
            foreach (var renderer in tile.GetComponentsInChildren<Renderer>(true))
            {
                var mat = renderer.sharedMaterial;
                string shader = mat != null && mat.shader != null ? mat.shader.name : "null";
                string tex = "null";
                if (mat != null)
                {
                    if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null)
                        tex = mat.GetTexture("_MainTex").name;
                    else if (mat.mainTexture != null)
                        tex = mat.mainTexture.name;
                }
                Color c = mat != null && mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.black;
                sb.AppendLine(
                    $"  {renderer.gameObject.name} enabled={renderer.enabled} queue={mat?.renderQueue} " +
                    $"shader={shader} tex={tex} color=({c.r:F3},{c.g:F3},{c.b:F3}) bounds={renderer.bounds.size}");
            }

            Debug.Log(sb.ToString());
        }

        public static string DescribeTexture(Texture2D texture)
        {
            if (texture == null)
                return "null";
            var center = texture.GetPixel(texture.width / 2, texture.height / 2);
            return $"{texture.name} {texture.width}x{texture.height} center=({center.r:F3},{center.g:F3},{center.b:F3})";
        }

    }
}

using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Viewport projection helpers for colonist board capture (1920×1080 RT).</summary>
    public static class ColonistCaptureProjection
    {
        public static void ApplyCaptureCameraState(Camera camera, TableCamera tableCamera, int width, int height)
        {
            if (camera == null || tableCamera == null)
                return;

            camera.pixelRect = new Rect(0f, 0f, width, height);
            tableCamera.ApplyCapturePose(width, height);
        }

        public static Vector2Int ViewportToTexturePixel(Vector3 viewport, int textureWidth, int textureHeight)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(viewport.x * textureWidth), 0, textureWidth - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(viewport.y * textureHeight), 0, textureHeight - 1);
            return new Vector2Int(x, y);
        }

        /// <summary>Axis-aligned screen width/height of hex top corners (pointy-top ≈ 1.1–1.2).</summary>
        public static float MeasureHexTopScreenAspectRatio(
            Transform hexTileRoot,
            HexCoord coord,
            float hexScale,
            Camera camera,
            TableCamera tableCamera,
            int textureWidth,
            int textureHeight)
        {
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            int hits = 0;
            float topY = hexTileRoot.position.y + hexTileRoot.lossyScale.y * HexPrismMesh.TopY;

            var previousRect = camera.pixelRect;
            var previousTarget = camera.targetTexture;
            try
            {
                ApplyCaptureCameraState(camera, tableCamera, textureWidth, textureHeight);
                for (int corner = 0; corner < HexPrismMesh.SideCount; corner++)
                {
                    var (vx, vz) = HexMath.VertexToWorldPosition(coord, corner, hexScale);
                    Vector3 world = new Vector3(vx, topY, vz);
                    Vector3 viewport = camera.WorldToViewportPoint(world);
                    if (viewport.z < 0f || float.IsNaN(viewport.x) || float.IsNaN(viewport.y))
                        continue;

                    float px = viewport.x * textureWidth;
                    float py = viewport.y * textureHeight;
                    minX = Mathf.Min(minX, px);
                    maxX = Mathf.Max(maxX, px);
                    minY = Mathf.Min(minY, py);
                    maxY = Mathf.Max(maxY, py);
                    hits++;
                }
            }
            finally
            {
                camera.pixelRect = previousRect;
                camera.targetTexture = previousTarget;
            }

            if (hits < 4 || maxX <= minX || maxY <= minY)
                return 0f;

            return (maxX - minX) / (maxY - minY);
        }
    }
}

using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Screen-space measurements from renderer bounds (colonist capture camera).</summary>
    public static class ColonistBoardScreenMetrics
    {
        public const float PortBadgeTargetTokenScreenDiameterRatio = 0.9f;

        public static Vector2 MeasureScreenPixelSize(Renderer renderer, Camera camera)
        {
            return ColonistFlatBoardQuad.MeasureScreenPixelSize(renderer, camera);
        }

        public static float MeasureScreenDiameter(Renderer renderer, Camera camera)
        {
            Vector2 size = MeasureScreenPixelSize(renderer, camera);
            return Mathf.Max(size.x, size.y);
        }

        /// <summary>Projected chip diameter using rim bounds (falls back to face × rim factor).</summary>
        public static float MeasureNumberTokenScreenDiameter(HexTileView tile, Camera camera)
        {
            if (tile == null || camera == null)
                return 0f;

            float rim = tile.TokenRimRenderer != null
                ? MeasureScreenDiameter(tile.TokenRimRenderer, camera)
                : 0f;
            float face = tile.TokenFaceRenderer != null
                ? MeasureScreenDiameter(tile.TokenFaceRenderer, camera)
                : 0f;
            float faceScaled = face * HexTileView.RimOuterRadiusFactor;
            return Mathf.Max(rim, faceScaled, face);
        }

        public static float MeasureProjectedRadiusFromCenter(
            Renderer renderer,
            Camera camera,
            TableCamera tableCamera,
            int textureWidth,
            int textureHeight,
            Vector3 centerWorld)
        {
            if (renderer == null)
                return 0f;

            var centerPx = ColonistCaptureProjection.WorldToTexturePixel(
                centerWorld, camera, tableCamera, textureWidth, textureHeight);
            var centerV = new Vector2(centerPx.x, centerPx.y);

            Bounds bounds = renderer.bounds;
            Vector3 extents = bounds.extents;
            Vector3 c = bounds.center;
            Vector3[] offsets =
            {
                new Vector3(extents.x, extents.y, extents.z),
                new Vector3(extents.x, extents.y, -extents.z),
                new Vector3(extents.x, -extents.y, extents.z),
                new Vector3(extents.x, -extents.y, -extents.z),
                new Vector3(-extents.x, extents.y, extents.z),
                new Vector3(-extents.x, extents.y, -extents.z),
                new Vector3(-extents.x, -extents.y, extents.z),
                new Vector3(-extents.x, -extents.y, -extents.z)
            };

            float maxR = 0f;
            for (int i = 0; i < offsets.Length; i++)
            {
                var px = ColonistCaptureProjection.WorldToTexturePixel(
                    c + offsets[i], camera, tableCamera, textureWidth, textureHeight);
                maxR = Mathf.Max(maxR, Vector2.Distance(centerV, new Vector2(px.x, px.y)));
            }

            return maxR;
        }
    }
}

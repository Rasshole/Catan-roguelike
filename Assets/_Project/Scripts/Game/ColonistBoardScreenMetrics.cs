using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Screen-space measurements from renderer bounds (colonist capture camera).</summary>
    public static class ColonistBoardScreenMetrics
    {
        public const float PortBadgeTargetTokenScreenDiameterRatio = 0.85f;

        public static Vector2 MeasureScreenPixelSize(Renderer renderer, Camera camera)
        {
            return ColonistFlatBoardQuad.MeasureScreenPixelSize(renderer, camera);
        }

        public static float MeasureScreenDiameter(Renderer renderer, Camera camera)
        {
            Vector2 size = MeasureScreenPixelSize(renderer, camera);
            return Mathf.Max(size.x, size.y);
        }

        /// <summary>
        /// Projected on-screen diameter using capture pose and renderer bounds (same path as rim capture probes).
        /// </summary>
        public static float MeasureProjectedScreenDiameter(
            Renderer renderer,
            Camera camera,
            TableCamera tableCamera,
            int textureWidth,
            int textureHeight,
            Vector3 centerWorld)
        {
            float radius = MeasureProjectedRadiusFromCenter(
                renderer, camera, tableCamera, textureWidth, textureHeight, centerWorld);
            return radius * 2f;
        }

        /// <summary>
        /// Number-token outer chip diameter on the capture framebuffer (rim bounds, shared with rim probes).
        /// </summary>
        public static float MeasureNumberTokenScreenDiameter(
            HexTileView tile,
            Camera camera,
            TableCamera tableCamera,
            int textureWidth,
            int textureHeight)
        {
            if (tile == null || camera == null || tableCamera == null)
                return 0f;

            var faceRenderer = tile.TokenFaceRenderer;
            var rimRenderer = tile.TokenRimRenderer;
            if (faceRenderer == null)
                return 0f;

            Vector3 center = faceRenderer.bounds.center;
            float faceDiameter = MeasureProjectedScreenDiameter(
                faceRenderer, camera, tableCamera, textureWidth, textureHeight, center);
            if (rimRenderer == null)
                return faceDiameter * HexTileView.RimOuterRadiusFactor;

            float rimDiameter = MeasureProjectedScreenDiameter(
                rimRenderer, camera, tableCamera, textureWidth, textureHeight, center);
            float faceScaled = faceDiameter * HexTileView.RimOuterRadiusFactor;
            return Mathf.Max(rimDiameter, faceScaled, faceDiameter);
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

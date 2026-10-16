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
        /// Projected on-screen diameter using capture pose and mesh top-cap vertices (not world AABB).
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

            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                float meshRadius = MeasureProjectedMeshCapRadiusFromCenter(
                    meshFilter.sharedMesh,
                    renderer.transform,
                    camera,
                    tableCamera,
                    textureWidth,
                    textureHeight,
                    centerWorld);
                if (meshRadius > 0.01f)
                    return meshRadius;
            }

            return MeasureProjectedBoundsRadiusFromCenter(
                renderer, camera, tableCamera, textureWidth, textureHeight, centerWorld);
        }

        private static float MeasureProjectedBoundsRadiusFromCenter(
            Renderer renderer,
            Camera camera,
            TableCamera tableCamera,
            int textureWidth,
            int textureHeight,
            Vector3 centerWorld)
        {
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

        /// <summary>
        /// Max screen radius from <paramref name="centerWorld"/> using top-cap mesh vertices only.
        /// </summary>
        public static float MeasureProjectedMeshCapRadiusFromCenter(
            Mesh mesh,
            Transform meshTransform,
            Camera camera,
            TableCamera tableCamera,
            int textureWidth,
            int textureHeight,
            Vector3 centerWorld)
        {
            if (mesh == null || meshTransform == null)
                return 0f;

            var vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0)
                return 0f;

            float topLocalY = mesh.bounds.max.y;
            const float topEpsilon = 1e-3f;

            var centerPx = ColonistCaptureProjection.WorldToTexturePixel(
                centerWorld, camera, tableCamera, textureWidth, textureHeight);
            var centerV = new Vector2(centerPx.x, centerPx.y);

            float maxR = 0f;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertices[i].y < topLocalY - topEpsilon)
                    continue;

                Vector3 world = meshTransform.TransformPoint(vertices[i]);
                var px = ColonistCaptureProjection.WorldToTexturePixel(
                    world, camera, tableCamera, textureWidth, textureHeight);
                maxR = Mathf.Max(maxR, Vector2.Distance(centerV, new Vector2(px.x, px.y)));
            }

            return maxR;
        }
    }
}

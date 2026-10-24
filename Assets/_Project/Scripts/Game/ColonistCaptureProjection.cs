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

        public static Vector3 WorldToViewportPoint(
            Vector3 worldPoint,
            Camera camera,
            TableCamera tableCamera,
            int textureWidth,
            int textureHeight)
        {
            if (camera == null || tableCamera == null)
                return new Vector3(0.5f, 0.5f, 1f);

            var previousRect = camera.pixelRect;
            var previousTarget = camera.targetTexture;
            RenderTexture tempTarget = null;
            try
            {
                ApplyCaptureCameraState(camera, tableCamera, textureWidth, textureHeight);
                tempTarget = RenderTexture.GetTemporary(textureWidth, textureHeight, 16);
                camera.targetTexture = tempTarget;
                return camera.WorldToViewportPoint(worldPoint);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.pixelRect = previousRect;
                if (tempTarget != null)
                    RenderTexture.ReleaseTemporary(tempTarget);
            }
        }

        public static Vector2Int WorldToTexturePixel(
            Vector3 worldPoint,
            Camera camera,
            TableCamera tableCamera,
            int textureWidth,
            int textureHeight)
        {
            var viewport = WorldToViewportPoint(worldPoint, camera, tableCamera, textureWidth, textureHeight);
            return ViewportToTexturePixel(viewport, textureWidth, textureHeight);
        }

        /// <summary>Vertical screen span of an axis-aligned world bounds at capture resolution.</summary>
        public static float MeasureBoundsVerticalScreenPixels(
            Bounds worldBounds,
            Camera camera,
            TableCamera tableCamera,
            int textureWidth,
            int textureHeight)
        {
            var top = WorldToTexturePixel(worldBounds.max, camera, tableCamera, textureWidth, textureHeight);
            var bottom = WorldToTexturePixel(worldBounds.min, camera, tableCamera, textureWidth, textureHeight);
            return Mathf.Abs(top.y - bottom.y);
        }

        /// <summary>Axis-aligned screen width/height of the rendered hex top ring (pointy-top ≈ 1.1–1.2).</summary>
        public static float MeasureHexTopScreenAspectRatio(
            Transform hexTileRoot,
            Camera camera,
            TableCamera tableCamera,
            int textureWidth,
            int textureHeight)
        {
            var meshFilter = hexTileRoot != null ? hexTileRoot.GetComponent<MeshFilter>() : null;
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return 0f;

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            int hits = 0;

            var previousRect = camera.pixelRect;
            var previousTarget = camera.targetTexture;
            var localToWorld = hexTileRoot.localToWorldMatrix;
            var vertices = meshFilter.sharedMesh.vertices;

            try
            {
                ApplyCaptureCameraState(camera, tableCamera, textureWidth, textureHeight);
                for (int corner = 0; corner < HexPrismMesh.SideCount; corner++)
                {
                    Vector3 local = vertices[corner];
                    if (!Mathf.Approximately(local.y, HexPrismMesh.TopY))
                        continue;

                    Vector3 world = localToWorld.MultiplyPoint3x4(local);
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

        /// <summary>World-space XZ width/height of the hex top ring after tile transform (≈ 0.85–0.90).</summary>
        public static float MeasureHexTopWorldXzAspectRatio(Transform hexTileRoot)
        {
            var meshFilter = hexTileRoot != null ? hexTileRoot.GetComponent<MeshFilter>() : null;
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return 0f;

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;
            int hits = 0;

            var localToWorld = hexTileRoot.localToWorldMatrix;
            var vertices = meshFilter.sharedMesh.vertices;
            for (int corner = 0; corner < HexPrismMesh.SideCount; corner++)
            {
                Vector3 local = vertices[corner];
                if (!Mathf.Approximately(local.y, HexPrismMesh.TopY))
                    continue;

                Vector3 world = localToWorld.MultiplyPoint3x4(local);
                minX = Mathf.Min(minX, world.x);
                maxX = Mathf.Max(maxX, world.x);
                minZ = Mathf.Min(minZ, world.z);
                maxZ = Mathf.Max(maxZ, world.z);
                hits++;
            }

            if (hits < 4 || maxX <= minX || maxZ <= minZ)
                return 0f;

            return (maxX - minX) / (maxZ - minZ);
        }
    }
}

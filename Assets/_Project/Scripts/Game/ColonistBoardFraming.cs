using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace CatanRoguelike.Game
{
    /// <summary>Fit colonist board + sand ring inside the HUD play-area rect.</summary>
    public static class ColonistBoardFraming
    {
        public const float PlayAreaMarginPixels = 24f;
        public const float PlayAreaFitMarginFraction = 0.03f;
        public const float MinPlayAreaFillRatio = 0.85f;
        public const float ColonistCameraHeight = 28f;

        /// <summary>Tight fit slack after solving screen-space AABB.</summary>
        public const float FramingFitSlackMultiplier = 1.0f;

        public struct ColonistCameraFit
        {
            public float OrthographicSize;
            public Vector3 Position;
        }

        public static float ComputeOrthographicSize(
            float boardExtentRadiusWorld,
            int screenWidth,
            int screenHeight,
            float cameraAspect)
        {
            var play = ColonistPlayAreaLayout.Compute(screenWidth, screenHeight);
            float margin = ColonistUiScale.ScaleReferencePixels(PlayAreaMarginPixels, screenWidth, screenHeight);
            float usableWidth = Mathf.Max(8f, play.PlayWidth - margin * 2f);
            float usableHeight = Mathf.Max(8f, play.PlayHeight - margin * 2f);
            float diameter = Mathf.Max(0.01f, boardExtentRadiusWorld * 2f);

            float fromHeight = diameter * screenHeight / (2f * usableHeight);
            float fromWidth = diameter * screenWidth / (2f * usableWidth * Mathf.Max(0.01f, cameraAspect));
            return Mathf.Max(fromHeight, fromWidth) * FramingFitSlackMultiplier;
        }

        public static ColonistCameraFit ComputeColonistCameraFit(
            IReadOnlyList<Vector3> worldSamples,
            int screenWidth,
            int screenHeight)
        {
            if (worldSamples == null || worldSamples.Count == 0)
            {
                return new ColonistCameraFit
                {
                    OrthographicSize = 4f,
                    Position = new Vector3(0f, ColonistCameraHeight, 0f)
                };
            }

            Vector3 refPoint = ComputeBoundsCenter(worldSamples);
            var rotation = Quaternion.Euler(90f, ColonistBoardViewConstants.CameraYawDegrees, 0f);
            GetBoardPlaneAxes(rotation, out Vector3 camRight, out Vector3 camUp);
            ComputeCameraPlaneExtents(
                worldSamples,
                refPoint,
                camRight,
                camUp,
                out float halfWidthWorld,
                out float halfHeightWorld,
                out float midR,
                out float midU);
            Vector3 contentCenter = refPoint + camRight * midR + camUp * midU;
            contentCenter.y = refPoint.y;

            var play = ColonistPlayAreaLayout.Compute(screenWidth, screenHeight);
            float usableWidth = Mathf.Max(8f, play.PlayWidth * (1f - PlayAreaFitMarginFraction * 2f));
            float usableHeight = Mathf.Max(8f, play.PlayHeight * (1f - PlayAreaFitMarginFraction * 2f));
            float aspect = screenWidth / (float)Mathf.Max(1, screenHeight);

            float orthoH = halfHeightWorld * screenHeight / usableHeight;
            float orthoW = halfWidthWorld * screenWidth / (usableWidth * Mathf.Max(0.01f, aspect));
            float ortho = Mathf.Max(orthoH, orthoW) * FramingFitSlackMultiplier;

            float worldPerPixel = (2f * ortho) / screenHeight;
            float desiredScreenX = play.Left + play.PlayWidth * 0.5f;
            float desiredScreenY = play.Bottom + play.PlayHeight * 0.5f;
            float deltaScreenX = desiredScreenX - screenWidth * 0.5f;
            float deltaScreenY = desiredScreenY - screenHeight * 0.5f;
            Vector3 pan = camRight * (deltaScreenX * worldPerPixel) + camUp * (deltaScreenY * worldPerPixel);
            Vector3 position = new Vector3(contentCenter.x, contentCenter.y + ColonistCameraHeight, contentCenter.z) - pan;

            return new ColonistCameraFit { OrthographicSize = ortho, Position = position };
        }

        public static void MeasurePlayAreaFill(
            Camera camera,
            IReadOnlyList<Vector3> worldSamples,
            int layoutWidth,
            int layoutHeight,
            out float fillWidth,
            out float fillHeight)
        {
            fillWidth = 0f;
            fillHeight = 0f;
            if (camera == null || worldSamples == null || worldSamples.Count == 0)
                return;

            var play = ColonistPlayAreaLayout.Compute(layoutWidth, layoutHeight);
            float usableWidth = Mathf.Max(8f, play.PlayWidth * (1f - PlayAreaFitMarginFraction * 2f));
            float usableHeight = Mathf.Max(8f, play.PlayHeight * (1f - PlayAreaFitMarginFraction * 2f));

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            foreach (var world in worldSamples)
            {
                Vector2 px = WorldToLayoutPixels(camera, world, layoutWidth, layoutHeight);
                minX = Mathf.Min(minX, px.x);
                maxX = Mathf.Max(maxX, px.x);
                minY = Mathf.Min(minY, px.y);
                maxY = Mathf.Max(maxY, px.y);
            }

            fillWidth = (maxX - minX) / usableWidth;
            fillHeight = (maxY - minY) / usableHeight;
        }

        public static void AppendBoundsCorners(Bounds bounds, List<Vector3> samples)
        {
            if (samples == null)
                return;

            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
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

            for (int i = 0; i < offsets.Length; i++)
                samples.Add(center + offsets[i]);
        }

        public static float GetPlayAreaTopEdgeScreenY(int screenWidth, int screenHeight)
        {
            var play = ColonistPlayAreaLayout.Compute(screenWidth, screenHeight);
            return play.Bottom + play.PlayHeight;
        }

        public static void AssertBoardTopBelowTurnBanner(
            Camera camera,
            BoardView boardView,
            int screenWidth,
            int screenHeight)
        {
            Assert.IsNotNull(camera);
            Assert.IsNotNull(boardView);

            float playTopY = GetPlayAreaTopEdgeScreenY(screenWidth, screenHeight);
            float maxBoardY = float.NegativeInfinity;
            var samples = new System.Collections.Generic.List<Vector3>(512);
            boardView.FillColonistFramingSamples(samples);
            foreach (var world in samples)
            {
                var px = WorldToLayoutPixels(camera, world, screenWidth, screenHeight);
                maxBoardY = Mathf.Max(maxBoardY, px.y);
            }

            UnityEngine.Assertions.Assert.IsTrue(
                maxBoardY < playTopY,
                $"Board top screen Y ({maxBoardY:F1}) should sit below play-area top / banner bottom ({playTopY:F1}).");
        }

        public static void LogCaptureFramingState(Camera camera, int screenWidth, int screenHeight)
        {
            var play = ColonistPlayAreaLayout.Compute(screenWidth, screenHeight);
            float ortho = camera != null ? camera.orthographicSize : 0f;
            Debug.LogWarning(
                $"ColonistBoardFraming capture layout={screenWidth}x{screenHeight} " +
                $"play=({play.Left:F1},{play.Bottom:F1}) size={play.PlayWidth:F0}x{play.PlayHeight:F0} " +
                $"topInset={play.Top:F1} ortho={ortho:F4} camPos={camera?.transform.position}");
        }

        public static void LogPlayAreaFitDiagnostics(
            Vector2[] screenPixelsFromBottom,
            int screenWidth,
            int screenHeight)
        {
            var play = ColonistPlayAreaLayout.Compute(screenWidth, screenHeight);
            float minX = play.Left + play.PlayWidth * PlayAreaFitMarginFraction;
            float maxX = play.Left + play.PlayWidth * (1f - PlayAreaFitMarginFraction);
            float minY = play.Bottom + play.PlayHeight * PlayAreaFitMarginFraction;
            float maxY = play.Bottom + play.PlayHeight * (1f - PlayAreaFitMarginFraction);

            for (int i = 0; i < screenPixelsFromBottom.Length; i++)
            {
                var p = screenPixelsFromBottom[i];
                Debug.LogWarning(
                    $"ColonistBoardFraming extent[{i}] screen=({p.x:F1},{p.y:F1}) " +
                    $"inside=({p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY})");
            }

            Debug.LogWarning(
                $"ColonistBoardFraming layout={screenWidth}x{screenHeight} " +
                $"playRect margin=({minX:F1},{minY:F1})-({maxX:F1},{maxY:F1}) " +
                $"play=({play.Left:F1},{play.Bottom:F1}) {play.PlayWidth:F0}x{play.PlayHeight:F0}");
        }

        public static ColonistPlayAreaLayout.Rect GetPlayArea(int screenWidth, int screenHeight) =>
            ColonistPlayAreaLayout.Compute(screenWidth, screenHeight);

        public static Vector2Int GetPlayAreaCenterTexturePixels(int screenWidth, int screenHeight) =>
            ColonistPlayAreaLayout.GetCenterTexturePixels(screenWidth, screenHeight);

        public static Vector2Int GetHexColorSampleTexturePixels(int screenWidth, int screenHeight)
        {
            var center = GetPlayAreaCenterTexturePixels(screenWidth, screenHeight);
            return new Vector2Int(center.x + 72, center.y + 56);
        }

        public static void GetExtentSamplePoints(Vector3 boardCenter, float extentRadius, Vector3[] buffer)
        {
            int count = buffer.Length;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                buffer[i] = new Vector3(
                    boardCenter.x + Mathf.Cos(angle) * extentRadius,
                    boardCenter.y,
                    boardCenter.z + Mathf.Sin(angle) * extentRadius);
            }
        }

        public static bool ArePointsInsidePlayArea(
            Vector2[] screenPixelsFromBottom,
            int screenWidth,
            int screenHeight,
            float marginPixels = PlayAreaMarginPixels)
        {
            var play = ColonistPlayAreaLayout.Compute(screenWidth, screenHeight);
            float margin = ColonistUiScale.ScaleReferencePixels(marginPixels, screenWidth, screenHeight);
            float minX = play.Left + margin;
            float maxX = play.Left + play.PlayWidth - margin;
            float minY = play.Bottom + margin;
            float maxY = play.Bottom + play.PlayHeight - margin;
            return AreInsideRect(screenPixelsFromBottom, minX, maxX, minY, maxY);
        }

        public static bool ArePointsInsideFitMargin(
            Vector2[] screenPixelsFromBottom,
            int screenWidth,
            int screenHeight)
        {
            GetFitMarginRect(screenWidth, screenHeight, out float minX, out float maxX, out float minY, out float maxY);
            return AreInsideRect(screenPixelsFromBottom, minX, maxX, minY, maxY);
        }

        public static bool TryGetFirstSampleOutsideFitMargin(
            IReadOnlyList<Vector3> worldSamples,
            Camera camera,
            int layoutWidth,
            int layoutHeight,
            out int sampleIndex,
            out Vector3 worldPoint,
            out Vector2 screenPixels)
        {
            sampleIndex = -1;
            worldPoint = default;
            screenPixels = default;
            if (worldSamples == null || camera == null || worldSamples.Count == 0)
                return false;

            GetFitMarginRect(layoutWidth, layoutHeight, out float minX, out float maxX, out float minY, out float maxY);
            for (int i = 0; i < worldSamples.Count; i++)
            {
                var px = WorldToLayoutPixels(camera, worldSamples[i], layoutWidth, layoutHeight);
                if (px.x < minX || px.x > maxX || px.y < minY || px.y > maxY)
                {
                    sampleIndex = i;
                    worldPoint = worldSamples[i];
                    screenPixels = px;
                    return true;
                }
            }

            return false;
        }

        private static void GetFitMarginRect(
            int screenWidth,
            int screenHeight,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            var play = ColonistPlayAreaLayout.Compute(screenWidth, screenHeight);
            minX = play.Left + play.PlayWidth * PlayAreaFitMarginFraction;
            maxX = play.Left + play.PlayWidth * (1f - PlayAreaFitMarginFraction);
            minY = play.Bottom + play.PlayHeight * PlayAreaFitMarginFraction;
            maxY = play.Bottom + play.PlayHeight * (1f - PlayAreaFitMarginFraction);
        }

        public static Vector2 WorldToScreenPixelsFromBottom(Camera camera, Vector3 world)
        {
            var screen = camera.WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }

        public static void GetLayoutPixelSize(
            Camera camera,
            int nominalWidth,
            int nominalHeight,
            out int width,
            out int height)
        {
            if (camera != null && camera.targetTexture != null)
            {
                width = camera.targetTexture.width;
                height = camera.targetTexture.height;
                return;
            }

            if (Screen.width > 0 && Screen.height > 0)
            {
                width = Screen.width;
                height = Screen.height;
                return;
            }

            if (camera != null)
            {
                int pw = Mathf.RoundToInt(camera.pixelRect.width);
                int ph = Mathf.RoundToInt(camera.pixelRect.height);
                if (pw > 0 && ph > 0)
                {
                    width = pw;
                    height = ph;
                    return;
                }
            }

            width = nominalWidth;
            height = nominalHeight;
        }

        public static Vector2 WorldToLayoutPixels(Camera camera, Vector3 world, int layoutWidth, int layoutHeight)
        {
            var viewport = camera.WorldToViewportPoint(world);
            return new Vector2(viewport.x * layoutWidth, viewport.y * layoutHeight);
        }

        public static bool IsBoundsInsidePlayArea(
            Camera camera,
            Bounds bounds,
            int layoutWidth,
            int layoutHeight,
            float marginPixels = PlayAreaMarginPixels)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
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

            var screenPoints = new Vector2[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
                screenPoints[i] = WorldToLayoutPixels(camera, center + offsets[i], layoutWidth, layoutHeight);

            return ArePointsInsidePlayArea(screenPoints, layoutWidth, layoutHeight, marginPixels);
        }

        private static bool AreInsideRect(Vector2[] points, float minX, float maxX, float minY, float maxY)
        {
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                if (p.x < minX || p.x > maxX || p.y < minY || p.y > maxY)
                    return false;
            }

            return true;
        }

        private static Vector3 ComputeBoundsCenter(IReadOnlyList<Vector3> samples)
        {
            Vector3 min = samples[0];
            Vector3 max = samples[0];
            for (int i = 1; i < samples.Count; i++)
            {
                min = Vector3.Min(min, samples[i]);
                max = Vector3.Max(max, samples[i]);
            }

            return (min + max) * 0.5f;
        }

        private static void GetBoardPlaneAxes(Quaternion cameraRotation, out Vector3 camRight, out Vector3 camUp)
        {
            camRight = cameraRotation * Vector3.right;
            camRight.y = 0f;
            camRight.Normalize();
            camUp = cameraRotation * Vector3.up;
            camUp.y = 0f;
            camUp.Normalize();
        }

        private static void ComputeCameraPlaneExtents(
            IReadOnlyList<Vector3> worldSamples,
            Vector3 refPoint,
            Vector3 camRight,
            Vector3 camUp,
            out float halfWidthWorld,
            out float halfHeightWorld,
            out float midR,
            out float midU)
        {
            float minR = float.PositiveInfinity;
            float maxR = float.NegativeInfinity;
            float minU = float.PositiveInfinity;
            float maxU = float.NegativeInfinity;
            foreach (var point in worldSamples)
            {
                Vector3 delta = point - refPoint;
                delta.y = 0f;
                float r = Vector3.Dot(delta, camRight);
                float u = Vector3.Dot(delta, camUp);
                minR = Mathf.Min(minR, r);
                maxR = Mathf.Max(maxR, r);
                minU = Mathf.Min(minU, u);
                maxU = Mathf.Max(maxU, u);
            }

            halfWidthWorld = Mathf.Max((maxR - minR) * 0.5f, 0.01f);
            halfHeightWorld = Mathf.Max((maxU - minU) * 0.5f, 0.01f);
            midR = (maxR + minR) * 0.5f;
            midU = (maxU + minU) * 0.5f;
        }
    }
}

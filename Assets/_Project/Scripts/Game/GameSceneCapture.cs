using System;
using System.IO;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Shared Camera.main → PNG capture used by editor CLI/menu and PlayMode screenshot tests.
    /// </summary>
    public static class GameSceneCapture
    {
        public const string HudCompositePipelineMarker = "CATAN_HUD_CAPTURE_PIPELINE_V8";

        public const string DefaultOutputPath = "/workspace/game-view.png";
        public const string OutputPathEnvVar = "GAME_VIEW_SHOT";
        public const int CaptureWidth = 1920;
        public const int CaptureHeight = 1080;

        public static string ResolveOutputPath()
        {
            var env = Environment.GetEnvironmentVariable(OutputPathEnvVar);
            return string.IsNullOrWhiteSpace(env) ? DefaultOutputPath : env.Trim();
        }

        public static void CaptureMainCameraToPng(string path) =>
            CaptureMainCameraToPng(path, prepareHudForCapture: null);

        public static void CaptureMainCameraToPng(string path, RunHudController prepareHudForCapture)
        {
            var texture = prepareHudForCapture != null
                ? CaptureMainCameraWithHudComposite(prepareHudForCapture)
                : CaptureMainCameraToTexture(null);
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                DestroyCaptureTexture(texture);
            }
        }

        /// <summary>Board only (no HUD composite pipeline).</summary>
        public static Texture2D CaptureMainCameraToTexture(RunHudController hud) =>
            hud == null
                ? CaptureMainCameraToTexture(null, CaptureWidth, CaptureHeight)
                : CaptureMainCameraWithHudComposite(hud);

        public static Texture2D CaptureMainCameraToTexture(RunHudController hud, int width, int height) =>
            hud == null
                ? CaptureBoardOnlyTexture(width, height)
                : CaptureMainCameraWithHudComposite(hud, width, height);

        /// <summary>
        /// PlayMode / editor HUD screenshot path: world-space canvas + UI camera + sanity probe.
        /// </summary>
        public static Texture2D CaptureMainCameraWithHudComposite(RunHudController hud) =>
            CaptureMainCameraWithHudComposite(hud, CaptureWidth, CaptureHeight);

        public static Texture2D CaptureMainCameraWithHudComposite(RunHudController hud, int width, int height)
        {
            if (hud == null)
                throw new ArgumentNullException(nameof(hud));

            Debug.Log($"{HudCompositePipelineMarker} CaptureMainCameraWithHudComposite enter ({width}x{height})");

            var camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException("Camera.main (TableCamera) is required for capture.");

            if (!hud.IsHudCaptureReady())
                throw new InvalidOperationException(
                    $"{HudCompositePipelineMarker} HUD is not capture-ready (canvas inactive or no graphics).");

            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var previousPixelRect = camera.pixelRect;
            var previousCullingMask = camera.cullingMask;

            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = null;
            RunHudController.CanvasCaptureRestore canvasRestore = default;

            try
            {
                camera.pixelRect = new Rect(0f, 0f, width, height);
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.cullingMask = previousCullingMask & ~(1 << RunHudController.HudCaptureUiLayer);

                canvasRestore = hud.BeginCanvasCameraCapture(camera, width, height);
                if (!canvasRestore.Active || canvasRestore.UiCamera == null)
                {
                    throw new InvalidOperationException(
                        $"{HudCompositePipelineMarker} BeginCanvasCameraCapture failed " +
                        $"(active={canvasRestore.Active} uiCamera={(canvasRestore.UiCamera != null)}).");
                }

                hud.GetCaptureDiagnostics(camera).LogToConsole(HudCompositePipelineMarker);

                NumberTokenLabelOrientation.RefreshAllLabelsForCamera(camera);
                Canvas.ForceUpdateCanvases();
                camera.Render();

                Debug.Log($"{HudCompositePipelineMarker} main camera rendered; compositing HUD via RenderHudCaptureUiCamera");
                hud.RenderHudCaptureUiCamera(canvasRestore, renderTexture);
                AssertHudCaptureSanityProbeVisible(renderTexture, width, height);
                Debug.Log($"{HudCompositePipelineMarker} sanity probe passed");

                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                Debug.Log($"{HudCompositePipelineMarker} CaptureMainCameraWithHudComposite exit");
                return texture;
            }
            finally
            {
                hud.RestoreCanvasAfterCapture(canvasRestore);

                camera.cullingMask = previousCullingMask;
                camera.targetTexture = previousTarget;
                camera.pixelRect = previousPixelRect;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                DestroyCaptureTexture(renderTexture);
            }
        }

        public static Texture2D CaptureBoardOnlyTexture(int width, int height)
        {
            var camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException("Camera.main (TableCamera) is required for capture.");

            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var previousPixelRect = camera.pixelRect;

            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = null;

            try
            {
                camera.pixelRect = new Rect(0f, 0f, width, height);
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;

                NumberTokenLabelOrientation.RefreshAllLabelsForCamera(camera);
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                return texture;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.pixelRect = previousPixelRect;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                DestroyCaptureTexture(renderTexture);
            }
        }

        /// <summary>Writes HUD PNG to <see cref="ResolveOutputPath"/> and board-only to path + ".board.png".</summary>
        public static void WriteDebugCapturePngs(Texture2D boardOnly, Texture2D hudComposite)
        {
            var outputPath = ResolveOutputPath();
            Debug.Log($"{HudCompositePipelineMarker} WriteDebugCapturePngs path={outputPath}");

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (boardOnly != null)
            {
                var boardPath = outputPath + ".board.png";
                File.WriteAllBytes(boardPath, boardOnly.EncodeToPNG());
                Debug.Log($"{HudCompositePipelineMarker} wrote board-only PNG to {boardPath}");
            }

            if (hudComposite != null)
            {
                File.WriteAllBytes(outputPath, hudComposite.EncodeToPNG());
                Debug.Log($"{HudCompositePipelineMarker} wrote HUD-composited PNG to {outputPath}");
            }
        }

        private static void AssertHudCaptureSanityProbeVisible(RenderTexture target, int width, int height)
        {
            if (!RunHudController.HudCaptureSanityProbeEnabled)
            {
                throw new InvalidOperationException(
                    $"{HudCompositePipelineMarker} sanity probe disabled but required for HUD capture.");
            }

            int x = Mathf.Clamp((int)(width * RunHudController.SanityProbeScreenX), 0, width - 1);
            int y = Mathf.Clamp((int)(height * RunHudController.SanityProbeScreenY), 0, height - 1);

            if (!RunHudController.TrySampleRenderTargetPixel(target, x, y, out var pixel))
            {
                throw new InvalidOperationException(
                    $"{HudCompositePipelineMarker} sanity probe: failed to read capture RT at ({x},{y}).");
            }

            if (!RunHudController.ColorLooksMagenta(pixel))
            {
                throw new InvalidOperationException(
                    $"{HudCompositePipelineMarker} sanity probe: expected magenta at ({x},{y}) " +
                    $"but got r={pixel.r:F3} g={pixel.g:F3} b={pixel.b:F3}.");
            }
        }

        private static void DestroyCaptureTexture(Texture texture)
        {
            if (texture == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(texture);
            else
                UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}

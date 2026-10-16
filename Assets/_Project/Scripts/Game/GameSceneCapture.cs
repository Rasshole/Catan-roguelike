using System;
using System.IO;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Shared Camera.main → PNG capture used by editor CLI/menu and PlayMode screenshot tests.
    /// </summary>
    public static partial class GameSceneCapture
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

                texture = ReadbackRgbFromRenderTexture(renderTexture, width, height);
                LogCaptureTextureFingerprint(texture, "after-readback");
                AssertHudCaptureSanityProbeVisible(texture, width, height);
                LogCaptureTextureFingerprint(texture, "after-probe");
                Debug.Log($"{HudCompositePipelineMarker} sanity probe passed on readback texture id={texture.GetInstanceID()}");

                Debug.Log($"{HudCompositePipelineMarker} CaptureMainCameraWithHudComposite exit");
                return texture;
            }
            finally
            {
                DetachCamerasFromRenderTexture(renderTexture, camera, canvasRestore.UiCamera);
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

                texture = ReadbackRgbFromRenderTexture(renderTexture, width, height);
                LogCaptureTextureFingerprint(texture, "board-only-readback");
                return texture;
            }
            finally
            {
                DetachCamerasFromRenderTexture(renderTexture, camera, null);
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
                LogCaptureTextureFingerprint(boardOnly, "encode-board");
                string boardHash = ComputeTexturePixelHashHex(boardOnly);
                var boardPng = boardOnly.EncodeToPNG();
                File.WriteAllBytes(boardPath, boardPng);
                Debug.Log(
                    $"{HudCompositePipelineMarker} wrote board-only PNG to {boardPath} " +
                    $"textureId={boardOnly.GetInstanceID()} hash={boardHash} bytes={boardPng.Length}");
            }

            if (hudComposite != null)
            {
                LogCaptureTextureFingerprint(hudComposite, "encode-hud");
                var png = hudComposite.EncodeToPNG();
                string md5 = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(png)).ToLowerInvariant();
                File.WriteAllBytes(outputPath, png);
                Debug.Log(
                    $"{HudCompositePipelineMarker} wrote HUD-composited PNG to {outputPath} " +
                    $"textureId={hudComposite.GetInstanceID()} md5={md5} bytes={png.Length}");
            }
        }

        private static void AssertHudCaptureSanityProbeVisible(Texture2D texture, int width, int height)
        {
            if (!RunHudController.HudCaptureSanityProbeEnabled)
            {
                throw new InvalidOperationException(
                    $"{HudCompositePipelineMarker} sanity probe disabled but required for HUD capture.");
            }

            if (texture == null)
                throw new InvalidOperationException($"{HudCompositePipelineMarker} sanity probe: texture is null.");

            int x = Mathf.Clamp((int)(width * RunHudController.SanityProbeScreenX), 0, width - 1);
            int y = Mathf.Clamp((int)(height * RunHudController.SanityProbeScreenY), 0, height - 1);
            var pixel = texture.GetPixel(x, y);

            if (!RunHudController.ColorLooksMagenta(pixel))
            {
                throw new InvalidOperationException(
                    $"{HudCompositePipelineMarker} sanity probe: expected magenta at ({x},{y}) on texture id={texture.GetInstanceID()} " +
                    $"but got r={pixel.r:F3} g={pixel.g:F3} b={pixel.b:F3}.");
            }
        }
    }
}

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
        public const string ColonistHudOutputPathEnvVar = "COLONIST_HUD_SHOT";
        public const int CaptureWidth = 1920;
        public const int CaptureHeight = 1080;

        private static void PrepareMainCameraPoseForCapture(Camera camera, int width, int height)
        {
            if (camera == null)
                return;

            var tableCamera = camera.GetComponent<TableCamera>();
            tableCamera?.ApplyCapturePose(width, height);
        }

        public static string ResolveOutputPath()
        {
            var env = Environment.GetEnvironmentVariable(OutputPathEnvVar);
            return string.IsNullOrWhiteSpace(env) ? DefaultOutputPath : env.Trim();
        }

        /// <summary>
        /// Colonist HUD PlayMode PNG path — does not collide with <see cref="OutputPathEnvVar"/> (Game scene test).
        /// </summary>
        public static string ResolveColonistHudOutputPath()
        {
            var colonist = Environment.GetEnvironmentVariable(ColonistHudOutputPathEnvVar);
            if (!string.IsNullOrWhiteSpace(colonist))
                return colonist.Trim();

            var gameView = Environment.GetEnvironmentVariable(OutputPathEnvVar);
            if (!string.IsNullOrWhiteSpace(gameView))
                return gameView.Trim() + ".colonist.png";

            return DefaultOutputPath + ".colonist.png";
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
                PrepareMainCameraPoseForCapture(camera, width, height);
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
                PrepareMainCameraPoseForCapture(camera, width, height);

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

        /// <summary>Writes colonist HUD PNG (separate path from Game scene capture).</summary>
        public static void WriteColonistDebugCapturePngs(Texture2D boardOnly, Texture2D hudComposite) =>
            WriteDebugCapturePngs(boardOnly, hudComposite, ResolveColonistHudOutputPath());

        /// <summary>Writes HUD PNG to <see cref="ResolveOutputPath"/> and board-only to path + ".board.png".</summary>
        public static void WriteDebugCapturePngs(Texture2D boardOnly, Texture2D hudComposite) =>
            WriteDebugCapturePngs(boardOnly, hudComposite, ResolveOutputPath());

        public static void WriteDebugCapturePngs(Texture2D boardOnly, Texture2D hudComposite, string outputPath)
        {
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
                string hudHash = ComputeTexturePixelHashHex(hudComposite);
                var png = hudComposite.EncodeToPNG();
                File.WriteAllBytes(outputPath, png);
                Debug.Log(
                    $"{HudCompositePipelineMarker} wrote HUD-composited PNG to {outputPath} " +
                    $"textureId={hudComposite.GetInstanceID()} hash={hudHash} bytes={png.Length}");
            }
        }

    }
}

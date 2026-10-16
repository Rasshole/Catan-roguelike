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
            var texture = CaptureMainCameraToTexture(prepareHudForCapture);
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

        /// <summary>Renders <see cref="Camera.main"/> (with optional HUD canvas) to a new RGB texture.</summary>
        public static Texture2D CaptureMainCameraToTexture(RunHudController hud) =>
            CaptureMainCameraToTexture(hud, CaptureWidth, CaptureHeight);

        public static Texture2D CaptureMainCameraToTexture(
            RunHudController hud,
            int width,
            int height)
        {
            var camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException("Camera.main (TableCamera) is required for capture.");

            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var previousPixelRect = camera.pixelRect;

            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = null;
            RunHudController.CanvasCaptureRestore canvasRestore = default;
            var previousCullingMask = camera.cullingMask;

            try
            {
                camera.pixelRect = new Rect(0f, 0f, width, height);
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;

                if (hud != null)
                {
                    camera.cullingMask = previousCullingMask & ~(1 << RunHudController.HudCaptureUiLayer);
                    canvasRestore = hud.BeginCanvasCameraCapture(camera, width, height);
                    hud.GetCaptureDiagnostics(camera).LogToConsole("GameSceneCapture");
                }

                NumberTokenLabelOrientation.RefreshAllLabelsForCamera(camera);
                Canvas.ForceUpdateCanvases();
                camera.Render();

                if (canvasRestore.Active && canvasRestore.UiCamera != null)
                    canvasRestore.UiCamera.Render();

                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                return texture;
            }
            finally
            {
                if (hud != null)
                    hud.RestoreCanvasAfterCapture(canvasRestore);

                camera.cullingMask = previousCullingMask;
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
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (boardOnly != null)
            {
                var boardPath = outputPath + ".board.png";
                File.WriteAllBytes(boardPath, boardOnly.EncodeToPNG());
                Debug.Log($"GameSceneCapture: wrote board-only debug PNG to {boardPath}");
            }

            if (hudComposite != null)
            {
                File.WriteAllBytes(outputPath, hudComposite.EncodeToPNG());
                Debug.Log($"GameSceneCapture: wrote HUD-composited PNG to {outputPath}");
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

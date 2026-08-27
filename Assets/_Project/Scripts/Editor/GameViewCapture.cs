using System;
using System.IO;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatanRoguelike.Editor
{
    /// <summary>
    /// Boots Game.unity in Play Mode, drives scripted setup, captures Camera.main to PNG, exits Play Mode.
    /// </summary>
    public static class GameViewCapture
    {
        public const string DefaultOutputPath = "/workspace/game-view.png";
        public const string OutputPathEnvVar = "GAME_VIEW_SHOT";

        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;
        private const int BootFrameWait = 3;
        private const int PostSetupFrameWait = 8;

        private enum CapturePhase
        {
            None,
            OpenScene,
            EnterPlay,
            WaitBootFrames,
            DriveSetup,
            WaitVisualFrames,
            CaptureFrame,
            ExitPlay,
            Done,
        }

        private static CapturePhase _phase = CapturePhase.None;
        private static int _targetFrameCount;
        private static bool _exitEditorAfter;
        private static int _exitCode;
        private static string _outputPath;

        [MenuItem("Catan Roguelike/Capture Game View Screenshot")]
        public static void CaptureFromMenu()
        {
            BeginCapture(exitEditorAfter: false);
        }

        /// <summary>
        /// Unity CLI entry: -batchmode -executeMethod CatanRoguelike.Editor.GameViewCapture.CaptureAndQuit
        /// </summary>
        public static void CaptureAndQuit()
        {
            EditorApplication.delayCall += () => BeginCapture(exitEditorAfter: true);
        }

        public static string ResolveOutputPath()
        {
            var env = Environment.GetEnvironmentVariable(OutputPathEnvVar);
            return string.IsNullOrWhiteSpace(env) ? DefaultOutputPath : env.Trim();
        }

        private static void BeginCapture(bool exitEditorAfter)
        {
            if (_phase != CapturePhase.None)
            {
                Debug.LogWarning("GameViewCapture: capture already in progress.");
                return;
            }

            _exitEditorAfter = exitEditorAfter;
            _exitCode = 0;
            _outputPath = ResolveOutputPath();
            _phase = CapturePhase.OpenScene;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (_phase == CapturePhase.None || _phase == CapturePhase.Done)
                return;

            try
            {
                switch (_phase)
                {
                    case CapturePhase.OpenScene:
                        if (!File.Exists(GameScenePath))
                            throw new FileNotFoundException(
                                $"Game scene not found at {GameScenePath}. Run Catan Roguelike → Setup Game Scene.");

                        if (!EditorApplication.isPlaying)
                        {
                            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
                            _phase = CapturePhase.EnterPlay;
                        }
                        break;

                    case CapturePhase.EnterPlay:
                        if (!EditorApplication.isPlaying)
                            EditorApplication.isPlaying = true;
                        else
                        {
                            _targetFrameCount = Time.frameCount + BootFrameWait;
                            _phase = CapturePhase.WaitBootFrames;
                        }
                        break;

                    case CapturePhase.WaitBootFrames:
                        if (!Application.isPlaying)
                            return;

                        if (Time.frameCount < _targetFrameCount)
                            return;

                        _phase = CapturePhase.DriveSetup;
                        break;

                    case CapturePhase.DriveSetup:
                        DriveScriptedSetup();
                        _targetFrameCount = Time.frameCount + PostSetupFrameWait;
                        _phase = CapturePhase.WaitVisualFrames;
                        break;

                    case CapturePhase.WaitVisualFrames:
                        if (!Application.isPlaying)
                            return;

                        if (Time.frameCount < _targetFrameCount)
                            return;

                        _phase = CapturePhase.CaptureFrame;
                        break;

                    case CapturePhase.CaptureFrame:
                        CaptureMainCameraToPng(_outputPath);
                        Debug.Log($"GameViewCapture: wrote {CaptureWidth}x{CaptureHeight} PNG to {_outputPath}");
                        _phase = CapturePhase.ExitPlay;
                        break;

                    case CapturePhase.ExitPlay:
                        if (EditorApplication.isPlaying)
                        {
                            EditorApplication.isPlaying = false;
                            return;
                        }

                        _phase = CapturePhase.Done;
                        FinishCapture();
                        break;
                }
            }
            catch (Exception ex)
            {
                FailCapture(ex);
            }
        }

        private static void DriveScriptedSetup()
        {
            var gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (gameManager == null)
                throw new InvalidOperationException("GameManager should exist after scene load.");

            var controller = gameManager.Controller;
            if (controller == null)
                throw new InvalidOperationException("Controller should be initialized after Start().");

            if (gameManager.Meta == null)
                throw new InvalidOperationException("Meta should be loaded after Start().");

            if (controller.State.Phase != GamePhase.RunSelectMap)
                throw new InvalidOperationException(
                    $"Fresh scene boot should begin at map select, got {controller.State.Phase}.");

            GameScenePlayHarness.CompleteRunSelectAndSetup(controller, gameManager.Meta);
        }

        private static void CaptureMainCameraToPng(string path)
        {
            var camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException("Camera.main (TableCamera) is required for capture.");

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;

            var renderTexture = new RenderTexture(
                CaptureWidth,
                CaptureHeight,
                24,
                RenderTextureFormat.ARGB32);

            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();

                var texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                try
                {
                    texture.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static void FinishCapture()
        {
            EditorApplication.update -= OnEditorUpdate;
            _phase = CapturePhase.None;

            if (_exitEditorAfter)
                EditorApplication.Exit(_exitCode);
        }

        private static void FailCapture(Exception ex)
        {
            _exitCode = 1;
            Debug.LogException(ex);

            EditorApplication.update -= OnEditorUpdate;
            _phase = CapturePhase.None;

            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;

            if (_exitEditorAfter)
                EditorApplication.Exit(1);
        }
    }
}

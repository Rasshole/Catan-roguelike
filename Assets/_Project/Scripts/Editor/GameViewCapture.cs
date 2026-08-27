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
    /// Opens Game.unity in edit mode, bootstraps GameManager without Play Mode, drives
    /// GameScenePlayHarness, captures Camera.main to PNG.
    /// </summary>
    public static class GameViewCapture
    {
        public const string DefaultOutputPath = GameSceneCapture.DefaultOutputPath;
        public const string OutputPathEnvVar = GameSceneCapture.OutputPathEnvVar;

        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        [MenuItem("Catan Roguelike/Capture Game View Screenshot")]
        public static void CaptureFromMenu()
        {
            try
            {
                RunEditModeCaptureAndWritePng();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Unity CLI entry: -batchmode -executeMethod CatanRoguelike.Editor.GameViewCapture.CaptureAndQuit
        /// </summary>
        public static void CaptureAndQuit()
        {
            // Runs during Application::ParseARGVCommands — before the editor main loop exists, so
            // Play Mode and EditorApplication.update cannot be relied on here. Edit-mode bootstrap only.
            Debug.Log("GameViewCapture: CaptureAndQuit entry — edit-mode capture (no Play Mode).");

            var exitCode = 0;
            try
            {
                RunEditModeCaptureAndWritePng();
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Debug.LogException(ex);
            }

            ForceEditorShutdown(exitCode);
        }

        public static string ResolveOutputPath() => GameSceneCapture.ResolveOutputPath();

        private static void RunEditModeCaptureAndWritePng()
        {
            var outputPath = GameSceneCapture.ResolveOutputPath();
            Debug.Log($"GameViewCapture: capture started (output={outputPath}, editMode=true).");

            if (!File.Exists(GameScenePath))
            {
                throw new FileNotFoundException(
                    $"Game scene not found at {GameScenePath}. Run Catan Roguelike → Setup Game Scene.");
            }

            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            var gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (gameManager == null)
                throw new InvalidOperationException("GameManager should exist in Game.unity.");

            gameManager.EditorBootstrapForCapture();

            var controller = gameManager.Controller;
            var meta = gameManager.Meta;
            if (controller == null)
                throw new InvalidOperationException("Controller should be initialized after editor bootstrap.");
            if (meta == null)
                throw new InvalidOperationException("Meta should be loaded after editor bootstrap.");

            if (controller.State.Phase != GamePhase.RunSelectMap)
            {
                throw new InvalidOperationException(
                    $"Fresh scene boot should begin at map select, got {controller.State.Phase}.");
            }

            GameScenePlayHarness.CompleteRunSelectAndSetup(controller, meta);

            var tableCamera = UnityEngine.Object.FindFirstObjectByType<TableCamera>();
            if (tableCamera == null)
                throw new InvalidOperationException("TableCamera is required for capture.");

            tableCamera.ApplyPoseForCapture(GameSceneCapture.CaptureWidth, GameSceneCapture.CaptureHeight);
            GameSceneCapture.CaptureMainCameraToPng(outputPath);
            Debug.Log(
                $"GameViewCapture: wrote {GameSceneCapture.CaptureWidth}x{GameSceneCapture.CaptureHeight} PNG to {outputPath}");
        }

        private static void ForceEditorShutdown(int code)
        {
            if (code != 0)
                Environment.FailFast($"GameViewCapture: capture failed (exit {code}).");

            try
            {
                EditorApplication.Exit(code);
            }
            catch
            {
                // EditorApplication.Exit may be ignored in some batchmode paths.
            }

            Environment.Exit(code);
        }
    }
}

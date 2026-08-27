using System;
using System.Reflection;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;

namespace CatanRoguelike.Tests
{
    public class GameSceneCaptureTests
    {
        private static readonly Vector3 SceneDefaultCameraPosition = new(0f, 1f, -10f);

        [Test]
        public void ResolveOutputPath_DefaultsWhenEnvMissingOrBlank()
        {
            var original = Environment.GetEnvironmentVariable(GameSceneCapture.OutputPathEnvVar);
            try
            {
                Environment.SetEnvironmentVariable(GameSceneCapture.OutputPathEnvVar, null);
                Assert.AreEqual(GameSceneCapture.DefaultOutputPath, GameSceneCapture.ResolveOutputPath());

                Environment.SetEnvironmentVariable(GameSceneCapture.OutputPathEnvVar, string.Empty);
                Assert.AreEqual(GameSceneCapture.DefaultOutputPath, GameSceneCapture.ResolveOutputPath());

                Environment.SetEnvironmentVariable(GameSceneCapture.OutputPathEnvVar, "   ");
                Assert.AreEqual(GameSceneCapture.DefaultOutputPath, GameSceneCapture.ResolveOutputPath());
            }
            finally
            {
                Environment.SetEnvironmentVariable(GameSceneCapture.OutputPathEnvVar, original);
            }
        }

        [Test]
        public void CaptureDimensions_MatchScreenshotContract()
        {
            Assert.AreEqual(1920, GameSceneCapture.CaptureWidth);
            Assert.AreEqual(1080, GameSceneCapture.CaptureHeight);
        }

        [Test]
        public void ApplyPoseForCapture_MovesCameraOffSceneDefault_WhenBoardHasData()
        {
            var root = new GameObject("GameSceneCaptureTests_Root");
            try
            {
                var boardViewGo = new GameObject("BoardView");
                boardViewGo.transform.SetParent(root.transform);
                var boardView = boardViewGo.AddComponent<BoardView>();
                SetPrivateField(boardView, "_controller", new GameController(42, MapSize.Small));

                var cameraGo = new GameObject("Main Camera");
                cameraGo.transform.SetParent(root.transform);
                cameraGo.tag = "MainCamera";
                cameraGo.AddComponent<Camera>();
                var tableCamera = cameraGo.AddComponent<TableCamera>();
                SetPrivateField(tableCamera, "boardView", boardView);

                cameraGo.transform.position = SceneDefaultCameraPosition;

                tableCamera.ApplyPoseForCapture(GameSceneCapture.CaptureWidth, GameSceneCapture.CaptureHeight);

                Assert.AreNotEqual(SceneDefaultCameraPosition, cameraGo.transform.position);
                Assert.Greater(cameraGo.transform.position.y, SceneDefaultCameraPosition.y + 1f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Expected private field {fieldName} on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}

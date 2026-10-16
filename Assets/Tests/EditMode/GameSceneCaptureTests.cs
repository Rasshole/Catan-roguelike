using System;
using System.Reflection;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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
        public void PrepareCanvasForCameraCapture_AssignsWorldCamera()
        {
            var root = new GameObject("ColonistCaptureHudTests_Root");
            try
            {
                var camGo = new GameObject("Camera");
                camGo.transform.SetParent(root.transform);
                var camera = camGo.AddComponent<Camera>();

                var hudGo = new GameObject("RunHud");
                hudGo.transform.SetParent(root.transform);
                var hud = hudGo.AddComponent<RunHudController>();

                var game = new GameController(11, MapSize.Small);
                game.SelectMap(MapSize.Small);
                game.SelectLeader(LeaderId.Merchant);
                game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
                game.ToggleDraftUnique(UniqueBuildingId.GuildHall);
                game.ConfirmRunSetup();
                game.State.Phase = GamePhase.DayPlayerActions;

                hud.Initialize(game, new MetaProgression(), null);
                hud.PrepareCanvasForCameraCapture(camera);

                var canvas = hudGo.GetComponentInChildren<Canvas>();
                Assert.IsNotNull(canvas, "RunHud should build a canvas during Initialize.");
                Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
                Assert.AreEqual(camera, canvas.worldCamera);
                Assert.AreEqual(1f, canvas.planeDistance, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
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
                Vector3 boardCenter = GetTableLookPoint(tableCamera);
                Vector3 toBoard = (boardCenter - cameraGo.transform.position).normalized;
                float facingBoard = Vector3.Dot(cameraGo.transform.forward, toBoard);
                Assert.Greater(facingBoard, 0.5f, "Capture pose should face the board center.");
                Assert.Less(cameraGo.transform.position.z, boardCenter.z + 0.01f,
                    "Table camera should sit on the −Z side (same as Play Mode default orbit).");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Vector3 GetTableLookPoint(TableCamera tableCamera)
        {
            var field = typeof(TableCamera).GetField("lookTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var lookTarget = field.GetValue(tableCamera) as Transform;
            Vector3 center = lookTarget != null ? lookTarget.position : Vector3.zero;
            return center + Vector3.up * 0.5f;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Expected private field {fieldName} on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}

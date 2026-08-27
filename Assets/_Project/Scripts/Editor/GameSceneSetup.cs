using CatanRoguelike.Core.Data;
using CatanRoguelike.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatanRoguelike.Editor
{
    public static class GameSceneSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        [MenuItem("Catan Roguelike/Setup Game Scene")]
        public static void SetupGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var camera = Camera.main;
            if (camera != null)
            {
                camera.gameObject.name = "Main Camera";
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
                camera.gameObject.AddComponent<TableCamera>();
            }

            var gameGo = new GameObject("Game");
            var boardView = gameGo.AddComponent<BoardView>();
            var boardInput = gameGo.AddComponent<BoardInputController>();
            var ui = gameGo.AddComponent<PlaceholderUI>();
            var manager = gameGo.AddComponent<GameManager>();

            if (camera != null)
            {
                var tableCamera = camera.gameObject.GetComponent<TableCamera>();
                if (tableCamera != null)
                {
                    var camSo = new SerializedObject(tableCamera);
                    camSo.FindProperty("boardView").objectReferenceValue = boardView;
                    camSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // IMGUI needs an EventSystem for click-through detection
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            var so = new SerializedObject(manager);
            so.FindProperty("boardView").objectReferenceValue = boardView;
            so.FindProperty("boardInput").objectReferenceValue = boardInput;
            so.FindProperty("ui").objectReferenceValue = ui;
            so.FindProperty("randomSeed").intValue = 42;
            so.FindProperty("mapSize").enumValueIndex = (int)MapSize.Small;
            so.ApplyModifiedPropertiesWithoutUndo();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"Game scene created at {ScenePath}. Press Play to test.");
        }
    }
}

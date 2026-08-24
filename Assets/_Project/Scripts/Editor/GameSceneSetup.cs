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

            CreateTable();

            var gameGo = new GameObject("Game");
            var boardView = gameGo.AddComponent<BoardView>();
            var ui = gameGo.AddComponent<PlaceholderUI>();
            var manager = gameGo.AddComponent<GameManager>();

            var so = new SerializedObject(manager);
            so.FindProperty("boardView").objectReferenceValue = boardView;
            so.FindProperty("ui").objectReferenceValue = ui;
            so.FindProperty("randomSeed").intValue = 42;
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

        private static void CreateTable()
        {
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Table";
            table.transform.position = new Vector3(0f, -0.25f, 0f);
            table.transform.localScale = new Vector3(14f, 0.5f, 12f);

            var renderer = table.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.35f, 0.22f, 0.12f);
            renderer.sharedMaterial = mat;
        }
    }
}

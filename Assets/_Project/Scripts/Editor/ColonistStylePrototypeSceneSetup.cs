using CatanRoguelike.Core.Data;
using CatanRoguelike.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatanRoguelike.Editor
{
    public static class ColonistStylePrototypeSceneSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/ColonistStylePrototype.unity";

        [MenuItem("Catan Roguelike/Build Colonist Style Prototype Scene")]
        public static void BuildPrototypeScene()
        {
            GameSceneSetup.SetupGameScene();

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("Failed to open scene from GameSceneSetup.");
                return;
            }

            var manager = Object.FindFirstObjectByType<GameManager>();
            if (manager != null)
            {
                if (manager.GetComponent<RunHudController>() == null)
                    manager.gameObject.AddComponent<RunHudController>();
                if (manager.GetComponent<ColonistPresentationMode>() == null)
                    manager.gameObject.AddComponent<ColonistPresentationMode>();
            }

            var camera = Camera.main;
            if (camera != null)
            {
                camera.backgroundColor = new Color(0.10f, 0.16f, 0.22f);
            }

            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"Colonist-style prototype saved to {ScenePath}. Press Play to preview HUD + board.");
        }
    }
}

using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    internal static class ColonistBoardSurfaces
    {
        private static readonly Color SandAlbedo = new(0.82f, 0.74f, 0.52f, 1f);
        private static readonly Color OceanAlbedo = new(0.05f, 0.28f, 0.48f, 1f);

        public static void CreateOceanAndCoast(Transform boardRoot, float boardBoundingRadius)
        {
            float oceanRadius = boardBoundingRadius * 2.1f;
            float sandRadius = boardBoundingRadius * 1.18f;

            CreateFlatDisk(boardRoot, "ColonistOcean", -0.02f, oceanRadius, OceanAlbedo);
            CreateFlatDisk(boardRoot, "ColonistSandCoast", -0.01f, sandRadius, SandAlbedo);
        }

        private static void CreateFlatDisk(Transform parent, string name, float y, float worldRadius, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            float diameter = worldRadius * 2f;
            go.transform.localScale = new Vector3(diameter, diameter, 1f);

            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ThinDiskMesh.Shared;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BuiltInMaterials.Create(color);
        }
    }
}

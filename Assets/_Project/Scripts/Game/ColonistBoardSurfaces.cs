using UnityEngine;

namespace CatanRoguelike.Game
{
    internal static class ColonistBoardSurfaces
    {
        private static readonly Color SandAlbedo = new(0.82f, 0.74f, 0.52f, 1f);
        private static readonly Color OceanAlbedo = new(0.05f, 0.28f, 0.48f, 1f);

        public static void CreateOceanAndCoast(Transform boardRoot, float boardBoundingRadius)
        {
            CreateFlatDisk(
                boardRoot,
                "ColonistOcean",
                TableCameraFraming.ColonistOceanLocalY,
                TableCameraFraming.ComputeColonistOceanDiskScale(boardBoundingRadius),
                OceanAlbedo);
            CreateFlatDisk(
                boardRoot,
                "ColonistSandCoast",
                TableCameraFraming.ColonistSandLocalY,
                TableCameraFraming.ComputeColonistSandDiskScale(boardBoundingRadius),
                SandAlbedo);
        }

        private static void CreateFlatDisk(
            Transform parent,
            string name,
            float localY,
            Vector3 localScale,
            Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            go.transform.localPosition = new Vector3(0f, localY, 0f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ThinDiskMesh.Shared;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BuiltInMaterials.Create(color);
        }
    }
}

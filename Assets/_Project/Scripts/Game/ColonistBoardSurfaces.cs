using UnityEngine;

namespace CatanRoguelike.Game
{
    internal static class ColonistBoardSurfaces
    {
        public static readonly Color OceanClearColor = new(0.05f, 0.28f, 0.48f, 1f);
        private static readonly Color SandAlbedo = new(0.82f, 0.74f, 0.52f, 1f);

        public static void CreateOceanAndCoast(
            Transform boardRoot,
            float boardBoundingRadius,
            float hexPrismHeightScale)
        {
            var layout = TableCameraFraming.ComputeColonistSurfaceLayout(
                boardBoundingRadius,
                hexPrismHeightScale);

            Debug.Log(
                "ColonistBoardSurfaces: " +
                $"hexBottomY={layout.HexBottomY:F4} " +
                $"sandY={layout.SandCenterY:F4} sandTopY={layout.SandTopY:F4} " +
                $"sandInnerR={layout.SandInnerRadius:F3} sandOuterR={layout.SandOuterRadius:F3} " +
                $"hexPrismHeightScale={hexPrismHeightScale:F4}");

            CreateSandCoastRing(
                boardRoot,
                layout.SandCenterY,
                layout.SandInnerRadius,
                layout.SandOuterRadius,
                layout.SandThicknessY);
        }

        private static void CreateSandCoastRing(
            Transform parent,
            float localY,
            float innerRadius,
            float outerRadius,
            float thinY)
        {
            var go = new GameObject("ColonistSandCoast");
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            go.transform.localPosition = new Vector3(0f, localY, 0f);
            go.transform.localRotation = Quaternion.identity;
            float yScale = thinY / ThinDiskMesh.MeshHeight;
            go.transform.localScale = new Vector3(1f, yScale, 1f);

            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ColonistAnnulusMesh.Create(innerRadius, outerRadius);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BuiltInMaterials.Create(SandAlbedo);
        }
    }
}

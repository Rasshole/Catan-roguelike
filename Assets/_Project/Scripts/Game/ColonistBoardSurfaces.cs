using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    internal static class ColonistBoardSurfaces
    {
        public static readonly Color OceanClearColor = new(0.05f, 0.28f, 0.48f, 1f);
        public static readonly Color SandAlbedo = new(0.82f, 0.74f, 0.52f, 1f);
        public const int SandRenderQueue = 1990;
        private const float SandBelowHexTopLocalOffset = 0.012f;

        public static void CreateOceanAndCoast(
            Transform boardRoot,
            BoardState board,
            float boardBoundingRadius,
            float hexPrismHeightScale,
            float hexScale)
        {
            float beachWidth = hexScale * ColonistCoastSandMesh.BeachWidthFactor;
            var layout = TableCameraFraming.ComputeColonistSurfaceLayout(
                boardBoundingRadius,
                hexPrismHeightScale,
                hexScale,
                beachWidth);

            Debug.Log(
                "ColonistBoardSurfaces: " +
                $"hexBottomY={layout.HexBottomY:F4} " +
                $"sandY={layout.SandCenterY:F4} sandTopY={layout.SandTopY:F4} " +
                $"sandOuterExtent={layout.SandOuterRadius:F3} beachWidth={beachWidth:F3} " +
                $"hexPrismHeightScale={hexPrismHeightScale:F4}");

            float hexTopY = hexPrismHeightScale * HexPrismMesh.TopY;
            CreateSandCoastMesh(boardRoot, board, hexTopY, layout.SandThicknessY, hexScale, beachWidth);
        }

        private static void CreateSandCoastMesh(
            Transform parent,
            BoardState board,
            float hexTopLocalY,
            float thinY,
            float hexScale,
            float beachWidth)
        {
            var go = new GameObject("ColonistSandCoast");
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            go.transform.localPosition = new Vector3(0f, hexTopLocalY - SandBelowHexTopLocalOffset, 0f);
            go.transform.localRotation = Quaternion.identity;
            float yScale = thinY / ThinDiskMesh.MeshHeight;
            go.transform.localScale = new Vector3(1f, yScale, 1f);

            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ColonistCoastSandMesh.Create(board, hexScale, beachWidth, meshLocalY: 0f);
            var renderer = go.AddComponent<MeshRenderer>();
            var mat = BuiltInMaterials.CreateUnlit(SandAlbedo, zWrite: true);
            mat.renderQueue = SandRenderQueue;
            renderer.sharedMaterial = mat;
        }
    }
}

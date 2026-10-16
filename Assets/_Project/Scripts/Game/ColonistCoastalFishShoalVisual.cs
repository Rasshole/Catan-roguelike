using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    public static class ColonistCoastalFishShoalVisual
    {
        public const string ShoalRootName = "CoastalFishShoal";
        private static readonly Color ChevronWaterTint = new(0.62f, 0.82f, 0.96f, 1f);

        public static HexTileView Build(
            Transform boardRoot,
            HexTileData data,
            FishShoalPlacement placement,
            float hexHeightScale)
        {
            var root = new GameObject($"{ShoalRootName}_{data.Coord.Q}_{data.Coord.R}");
            root.transform.SetParent(boardRoot, false);
            root.transform.localPosition = new Vector3(
                placement.TokenCenter.X,
                0f,
                placement.TokenCenter.Z);
            root.transform.localScale = new Vector3(1f, hexHeightScale, 1f);

            var meshGo = new GameObject("ShoalMesh");
            meshGo.transform.SetParent(root.transform, false);
            var mesh = CoastalFishShoalMesh.CreateLocal(placement, placement.TokenCenter);
            var meshFilter = meshGo.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            var renderer = meshGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateChevronMaterial();
            renderer.sharedMaterial.renderQueue = 2002;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var view = root.AddComponent<HexTileView>();
            view.Initialize(data, renderer);
            view.RefreshNumberToken(data);
            return view;
        }

        private static Material CreateChevronMaterial()
        {
            var texture = ColonistBoardTextureLoader.LoadBoardTexture(BoardTerrainSlot.FishShoal);
            Material mat = texture != null
                ? BuiltInMaterials.CreateUnlitTextured(texture, ChevronWaterTint, zWrite: true)
                : BuiltInMaterials.CreateUnlit(ChevronWaterTint, zWrite: true);
            mat.renderQueue = 2002;
            return mat;
        }
    }
}

using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    public static class ColonistCoastalFishShoalVisual
    {
        public const string ShoalRootName = "CoastalFishShoal";

        public static HexTileView Build(
            Transform boardRoot,
            HexTileData data,
            FishShoalPlacement placement,
            float hexHeightScale,
            float hexScale)
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
            var mesh = CoastalFishShoalMesh.CreateLocal(placement, placement.TokenCenter, hexScale);
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
            var tex = ColonistBoardTextureLoader.LoadBoardTexture(BoardTerrainSlot.FishShoal);
            Material mat = tex != null
                ? BuiltInMaterials.Create(tex, Color.white)
                : BuiltInMaterials.CreateUnlit(new Color(0.58f, 0.78f, 0.91f, 1f), zWrite: true);
            mat.renderQueue = 2002;
            return mat;
        }
    }
}

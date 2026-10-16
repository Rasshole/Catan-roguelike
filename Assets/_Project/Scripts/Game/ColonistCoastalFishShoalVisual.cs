using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    public static class ColonistCoastalFishShoalVisual
    {
        public const string ShoalRootName = "CoastalFishShoal";
        private static readonly Color ChevronWaterColor = new(0.58f, 0.78f, 0.94f, 1f);

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
            renderer.sharedMaterial = BuiltInMaterials.CreateUnlit(ChevronWaterColor, zWrite: true);
            renderer.sharedMaterial.renderQueue = 2002;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            AddFishOverlay(meshGo.transform, hexScale);

            var view = root.AddComponent<HexTileView>();
            view.Initialize(data, renderer);
            view.RefreshNumberToken(data);
            return view;
        }

        private static void AddFishOverlay(Transform parent, float hexScale)
        {
            var fishTex = ColonistBoardTextureLoader.LoadBoardTexture(BoardTerrainSlot.FishShoal);
            if (fishTex == null)
                return;

            float w = hexScale * 0.55f;
            float h = w * 0.45f;
            ColonistFlatBoardQuad.Create(
                parent,
                "FishOverlay",
                new Vector3(0f, CoastalFishShoalMesh.TopLocalY + 0.01f, hexScale * 0.08f),
                w,
                h,
                fishTex,
                2005,
                alignment: ColonistFlatBoardQuad.AlignmentMode.BoardPitchOnly);
        }

    }
}

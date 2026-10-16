using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    public static class ColonistCoastalFishShoalVisual
    {
        public const string ShoalRootName = "CoastalFishShoal";

        public static HexTileView Build(
            Transform boardRoot,
            HexTileData data,
            FishShoalPlacement placement,
            float hexScale,
            float hexTopY)
        {
            var root = new GameObject($"{ShoalRootName}_{data.Coord.Q}_{data.Coord.R}");
            root.transform.SetParent(boardRoot, false);

            root.transform.localPosition = new Vector3(
                placement.TokenCenter.X,
                0f,
                placement.TokenCenter.Z);

            var meshGo = new GameObject("ShoalMesh");
            meshGo.transform.SetParent(root.transform, false);
            var mesh = CoastalFishShoalMesh.Create(placement, hexTopY, placement.TokenCenter);
            var meshFilter = meshGo.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            var renderer = meshGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ColonistBoardTextureLoader.GetTerrainMaterial(BoardTerrainSlot.FishShoal);
            renderer.sharedMaterial.renderQueue = 1990;

            var view = root.AddComponent<HexTileView>();
            view.Initialize(data, renderer);
            view.RefreshNumberToken(data);
            return view;
        }
    }
}

using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    public static class ColonistCoastalFishShoalVisual
    {
        public const string ShoalRootName = "CoastalFishShoal";

        /// <summary>Physical fishermen tile blue (unlit chevron fill).</summary>
        public static readonly Color ChevronWaterBlue = new(0.58f, 0.78f, 0.91f, 1f);

        private const string FishIconResourcePath = "Art/Board/FishIcon";
        private static Material _chevronMaterial;

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
            renderer.sharedMaterial = ChevronMaterial();
            renderer.sharedMaterial.renderQueue = 2002;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            AddFishIconQuads(root.transform, placement, renderer);

            var view = root.AddComponent<HexTileView>();
            view.Initialize(data, renderer);
            view.RefreshNumberToken(data);
            return view;
        }

        private static Material ChevronMaterial()
        {
            if (_chevronMaterial != null)
                return _chevronMaterial;

            _chevronMaterial = BuiltInMaterials.CreateUnlit(ChevronWaterBlue, zWrite: true);
            _chevronMaterial.SetInt("_Cull", (int)CullMode.Off);
            _chevronMaterial.renderQueue = 2002;
            return _chevronMaterial;
        }

        private static void AddFishIconQuads(
            Transform root,
            FishShoalPlacement placement,
            Renderer chevronRenderer)
        {
            var texture = Resources.Load<Texture2D>(FishIconResourcePath);
            if (texture == null)
                return;

            // Fish art fills ~70% x 30% of the PNG; size the quad so the visible fish body is ~0.3 hex long.
            float iconWidth = HexTileView.TokenWorldFaceDiameter * 0.72f;
            float iconHeight = iconWidth;
            // The shoal root is Y-scaled by hex height; quads parented to it got skewed (thin slivers).
            // Use an un-scaled container so the fish keep their aspect.
            float rootYScale = Mathf.Max(1e-4f, root.localScale.y);
            var iconRoot = new GameObject("FishIcons").transform;
            iconRoot.SetParent(root, false);
            iconRoot.localScale = new Vector3(1f, 1f / rootYScale, 1f);
            float y = CoastalFishShoalMesh.TopLocalY * rootYScale + 0.01f;

            // Fish sit in the seaward band either side of the token (outside the token disc).
            var armMidLeft = placement.CoastArmLeft + (placement.SeaFlatLeft - placement.CoastArmLeft) * 0.62f;
            var armMidRight = placement.CoastArmRight + (placement.SeaFlatRight - placement.CoastArmRight) * 0.62f;
            var centers = new[] { armMidLeft, armMidRight };

            for (int i = 0; i < centers.Length; i++)
            {
                var xz = centers[i];
                var local = new Vector3(
                    xz.X - placement.TokenCenter.X,
                    y,
                    xz.Z - placement.TokenCenter.Z);
                ColonistFlatBoardQuad.Create(
                    iconRoot,
                    $"FishIcon_{i}",
                    local,
                    iconWidth,
                    iconHeight,
                    texture,
                    renderQueue: 2003,
                    placeAbove: chevronRenderer,
                    clearanceAbove: 0.002f,
                    zWrite: false,
                    alignment: ColonistFlatBoardQuad.AlignmentMode.BoardPitchOnly);
            }
        }
    }
}

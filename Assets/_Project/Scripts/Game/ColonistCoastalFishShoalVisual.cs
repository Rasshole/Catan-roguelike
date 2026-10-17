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
        public static readonly Color ChevronOutlineBlue = new(0.22f, 0.38f, 0.58f, 1f);

        private const string FishIconResourcePath = "Art/Board/FishIcon";
        private static Material _chevronMaterial;
        private static Material _outlineMaterial;

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

            var outlineGo = new GameObject("ShoalOutline");
            outlineGo.transform.SetParent(root.transform, false);
            var outlineMesh = CoastalFishShoalMesh.CreateOutlineLocal(placement, placement.TokenCenter, hexScale);
            outlineGo.AddComponent<MeshFilter>().sharedMesh = outlineMesh;
            var outlineRenderer = outlineGo.AddComponent<MeshRenderer>();
            outlineRenderer.sharedMaterial = OutlineMaterial();
            outlineRenderer.sharedMaterial.renderQueue = 2001;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;

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

            AddFishIconQuads(boardRoot, placement, hexHeightScale, hexScale);

            var view = root.AddComponent<HexTileView>();
            view.Initialize(data, renderer);
            view.RefreshNumberToken(data);
            return view;
        }

        private static Material ChevronMaterial()
        {
            if (_chevronMaterial != null)
                return _chevronMaterial;

            _chevronMaterial = BuiltInMaterials.CreateUnlitTextured(
                ColonistFishChevronTextures.ChevronFill(),
                ChevronWaterBlue);
            _chevronMaterial.SetInt("_Cull", (int)CullMode.Off);
            _chevronMaterial.renderQueue = 2002;
            return _chevronMaterial;
        }

        private static Material OutlineMaterial()
        {
            if (_outlineMaterial != null)
                return _outlineMaterial;

            _outlineMaterial = CreateDoubleSidedUnlit(ChevronOutlineBlue);
            _outlineMaterial.renderQueue = 2001;
            return _outlineMaterial;
        }

        private static Material CreateDoubleSidedUnlit(Color color)
        {
            var mat = BuiltInMaterials.CreateUnlit(color, zWrite: true);
            mat.SetInt("_Cull", (int)CullMode.Off);
            return mat;
        }

        private static void AddFishIconQuads(
            Transform boardRoot,
            FishShoalPlacement placement,
            float hexHeightScale,
            float hexScale)
        {
            // The procedural silver sprite read as a pale bullet/blob from the camera; prefer the
            // drawn fish icon (side-view, horizontal) and fall back to the procedural one.
            var drawnIcon = Resources.Load<Texture2D>(FishIconResourcePath);
            var texture = drawnIcon != null ? drawnIcon : ColonistFishIconTextures.SilverBlueFish();
            float iconYawOffset = drawnIcon != null ? 90f : 0f;
            if (boardRoot == null)
                return;

            float iconSize = HexTileView.TokenWorldFaceDiameter * 0.9f;
            float boardY = boardRoot.position.y + hexHeightScale * CoastalFishShoalMesh.TopLocalY + 0.04f;
            float bandT = SeaFrameSlotLayout.FishChevronBandThicknessFactor * hexScale;
            float tokenRadius = HexTileView.TokenWorldFaceDiameter * 0.5f;
            float minCenterDist = tokenRadius + iconSize * 0.48f;

            // Arm midpoints sit ~0.5 world from the token, inside minCenterDist, so both icons were
            // skipped (no fish drawn). Slide each icon outward along its arm, mid-band, until it
            // clears the token.
            var armLeft = FishArmPointClearOfToken(placement, placement.CoastArmLeft, bandT, minCenterDist);
            var armRight = FishArmPointClearOfToken(placement, placement.CoastArmRight, bandT, minCenterDist);
            var centers = new[] { armLeft, armRight };
            var armEnds = new[] { placement.CoastArmLeft, placement.CoastArmRight };
            var halo = ColonistFishIconTextures.DarkIconHalo();

            for (int i = 0; i < centers.Length; i++)
            {
                var xz = centers[i];
                float dist = Vector2.Distance(
                    new Vector2(xz.X, xz.Z),
                    new Vector2(placement.TokenCenter.X, placement.TokenCenter.Z));
                if (dist < minCenterDist)
                    continue;

                var boardLocal = boardRoot.InverseTransformPoint(new Vector3(xz.X, boardY, xz.Z));
                var haloQuad = ColonistFlatBoardQuad.Create(
                    boardRoot,
                    $"FishIconHalo_{placement.TokenCenter.X:F0}_{placement.TokenCenter.Z:F0}_{i}",
                    boardLocal,
                    iconSize * 1.12f,
                    iconSize * 1.12f,
                    halo,
                    renderQueue: 2002,
                    zWrite: false,
                    alignment: ColonistFlatBoardQuad.AlignmentMode.ColonistCamera);

                var icon = ColonistFlatBoardQuad.Create(
                    boardRoot,
                    $"FishIcon_{placement.TokenCenter.X:F0}_{placement.TokenCenter.Z:F0}_{i}",
                    boardLocal,
                    iconSize,
                    iconSize,
                    texture,
                    renderQueue: 2003,
                    placeAbove: null,
                    clearanceAbove: 0f,
                    zWrite: false,
                    alignment: ColonistFlatBoardQuad.AlignmentMode.ColonistCamera);

                var armDir = armEnds[i] - placement.InlandTip;
                float armYaw = Mathf.Atan2(armDir.X, armDir.Z) * Mathf.Rad2Deg;
                var rot = Quaternion.Euler(
                    ColonistFlatBoardQuad.BoardPitchDegrees,
                    armYaw + iconYawOffset,
                    0f);
                icon.transform.rotation = rot;
                haloQuad.transform.rotation = rot;
            }
        }
    
        private static MapPlaneXz FishArmPointClearOfToken(
            FishShoalPlacement placement,
            MapPlaneXz armEnd,
            float bandT,
            float minCenterDist)
        {
            MapPlaneXz best = placement.InlandTip;
            for (float f = 0.5f; f <= 0.86f; f += 0.04f)
            {
                var onArm = placement.InlandTip + (armEnd - placement.InlandTip) * f;
                best = onArm + placement.Outward * (bandT * 0.5f);
                float dist = Vector2.Distance(
                    new Vector2(best.X, best.Z),
                    new Vector2(placement.TokenCenter.X, placement.TokenCenter.Z));
                if (dist >= minCenterDist)
                    return best;
            }

            return best;
        }
    }
}

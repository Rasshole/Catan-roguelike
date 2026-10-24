using CatanRoguelike.Core.Hex;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Small centred desert pond (~35% of hex) with sandy rim and Kenney water sprite.</summary>
    public static class ColonistDesertLakeVisual
    {
        private static readonly Color ShoreColor = new(0.94f, 0.84f, 0.62f, 0.95f);

        public static void Apply(GameObject hexRoot, float hexScale)
        {
            if (hexRoot == null || hexRoot.transform.Find("DesertLakeWater") != null
                || hexRoot.transform.Find("DesertLakeShore") != null)
                return;

            float hexDiameter = hexScale * HexMath.OuterRadius * 2f;
            float pondDiameter = hexDiameter * 0.35f;
            float shoreDiameter = pondDiameter * 1.16f;
            float top = HexPrismMesh.TopY + 0.04f;

            // Painted oasis (tools/art/make_desert_pond.py) carries its own wet-sand shore, so the
            // flat pale shore disc is gone; the quad covers the old shore footprint.
            var waterTex = Resources.Load<Texture2D>("Art/Board/TopDownDecor/desert_pond_water");
            if (waterTex != null)
            {
                ColonistFlatBoardQuad.Create(
                    hexRoot.transform,
                    "DesertLakeWater",
                    new Vector3(0f, top + 0.01f, 0f),
                    shoreDiameter * 1.08f,
                    shoreDiameter * 1.08f,
                    waterTex,
                    3190,
                    alignment: ColonistFlatBoardQuad.AlignmentMode.BoardPitchOnly);
            }
            else
            {
                ColonistFlatBoardQuad.Create(
                    hexRoot.transform,
                    "DesertLakeShore",
                    new Vector3(0f, top, 0f),
                    shoreDiameter,
                    shoreDiameter,
                    ColonistProceduralBoardTextures.CreateRoundPondMask(ShoreColor),
                    3188,
                    alignment: ColonistFlatBoardQuad.AlignmentMode.BoardPitchOnly);
            }

            var reedTex = Resources.Load<Texture2D>("Art/Board/TopDownDecor/pond_reed");
            if (reedTex == null)
                return;

            float pondRadius = pondDiameter * 0.5f;
            for (int i = 0; i < 2; i++)
            {
                float angle = (i == 0 ? 35f : 215f) * Mathf.Deg2Rad;
                float ring = pondRadius * 1.05f;
                var pos = new Vector3(Mathf.Cos(angle) * ring, top + 0.012f, Mathf.Sin(angle) * ring);
                float reedSize = hexScale * 0.20f; // square cattail clump art; 0.10 read as a speck
                ColonistFlatBoardQuad.Create(
                    hexRoot.transform,
                    $"DesertPondReed_{i}",
                    pos,
                    reedSize,
                    reedSize,
                    reedTex,
                    3191,
                    alignment: ColonistFlatBoardQuad.AlignmentMode.ColonistCamera);
            }
        }
    }
}

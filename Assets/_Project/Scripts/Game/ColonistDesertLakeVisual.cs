using CatanRoguelike.Core.Hex;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Small centred desert pond (~35% of hex) with a thin sandy shore ring on the desert face.</summary>
    public static class ColonistDesertLakeVisual
    {
        private static readonly Color PondColor = new(0.34f, 0.50f, 0.56f, 0.92f);
        private static readonly Color ShoreColor = new(0.94f, 0.84f, 0.62f, 0.95f);

        public static void Apply(GameObject hexRoot, float hexScale)
        {
            if (hexRoot == null || hexRoot.transform.Find("DesertLakeShore") != null)
                return;

            float hexDiameter = hexScale * HexMath.OuterRadius * 2f;
            float pondDiameter = hexDiameter * 0.35f;
            float shoreDiameter = pondDiameter * 1.18f;
            float top = HexPrismMesh.TopY + 0.04f;

            ColonistFlatBoardQuad.Create(
                hexRoot.transform,
                "DesertLakeShore",
                new Vector3(0f, top, 0f),
                shoreDiameter,
                shoreDiameter,
                ColonistProceduralBoardTextures.CreateRoundPondMask(ShoreColor),
                3188,
                alignment: ColonistFlatBoardQuad.AlignmentMode.BoardPitchOnly);

            ColonistFlatBoardQuad.Create(
                hexRoot.transform,
                "DesertLakeRipple",
                new Vector3(0f, top + 0.008f, 0f),
                pondDiameter,
                pondDiameter,
                ColonistProceduralBoardTextures.CreateRoundPondMask(PondColor),
                3190,
                alignment: ColonistFlatBoardQuad.AlignmentMode.BoardPitchOnly);
        }
    }
}

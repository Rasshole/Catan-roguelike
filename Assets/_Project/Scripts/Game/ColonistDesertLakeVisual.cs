using CatanRoguelike.Core.Hex;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Small desert fishing pond (~35% of hex) with sandy shoreline.</summary>
    public static class ColonistDesertLakeVisual
    {
        private static readonly Color PondColor = new(0.36f, 0.52f, 0.58f, 0.88f);
        private static readonly Color ShoreColor = new(0.88f, 0.76f, 0.50f, 0.92f);

        public static void Apply(GameObject hexRoot, float hexScale)
        {
            if (hexRoot == null || hexRoot.transform.Find("DesertLakeShore") != null)
                return;

            float hexDiameter = hexScale * HexMath.OuterRadius * 2f;
            float pondDiameter = hexDiameter * 0.35f;
            float shoreDiameter = pondDiameter * 1.22f;
            float top = HexPrismMesh.TopY + 0.035f;

            var shore = ColonistFlatBoardQuad.Create(
                hexRoot.transform,
                "DesertLakeShore",
                new Vector3(0.08f * hexScale, top, -0.06f * hexScale),
                shoreDiameter,
                shoreDiameter * 0.92f,
                ColonistProceduralBoardTextures.CreateRippleMask(),
                3188,
                alignment: ColonistFlatBoardQuad.AlignmentMode.BoardPitchOnly);
            Tint(shore, ShoreColor);

            var pond = ColonistFlatBoardQuad.Create(
                hexRoot.transform,
                "DesertLakeRipple",
                new Vector3(0.08f * hexScale, top + 0.012f, -0.06f * hexScale),
                pondDiameter,
                pondDiameter * 0.88f,
                ColonistProceduralBoardTextures.CreateRippleMask(),
                3190,
                alignment: ColonistFlatBoardQuad.AlignmentMode.BoardPitchOnly);
            Tint(pond, PondColor);
        }

        private static void Tint(GameObject quad, Color color)
        {
            var renderer = quad.GetComponent<Renderer>();
            if (renderer?.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Color"))
                renderer.sharedMaterial.color = color;
        }
    }
}

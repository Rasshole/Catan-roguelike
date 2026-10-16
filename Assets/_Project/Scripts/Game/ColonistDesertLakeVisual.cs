using CatanRoguelike.Core.Hex;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Desert-center fishing lake (Fishermen): light ripple accents on the sea-lake hex.</summary>
    public static class ColonistDesertLakeVisual
    {
        private static readonly Color RippleColor = new(0.38f, 0.58f, 0.62f, 0.72f);

        public static void Apply(GameObject hexRoot, float hexScale)
        {
            if (hexRoot == null || hexRoot.transform.Find("DesertLakeRipple") != null)
                return;

            float diameter = hexScale * HexMath.OuterRadius * 0.62f;
            var ripple = ColonistFlatBoardQuad.Create(
                hexRoot.transform,
                "DesertLakeRipple",
                new Vector3(0f, HexPrismMesh.TopY + 0.04f, 0f),
                diameter,
                diameter * 0.55f,
                ColonistProceduralBoardTextures.CreateRippleMask(),
                3190,
                alignment: ColonistFlatBoardQuad.AlignmentMode.BoardPitchOnly);
            var renderer = ripple.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Color"))
                renderer.sharedMaterial.color = RippleColor;
        }
    }
}

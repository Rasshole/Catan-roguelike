namespace CatanRoguelike.Core.Yield
{
    /// <summary>
    /// Token chip colors and label sizing for board rendering (RGB 0–1).
    /// Game layer maps <see cref="Rgb"/> to UnityEngine.Color.
    /// </summary>
    public static class NumberTokenVisualStyle
    {
        public static readonly Rgb ChipFaceStandard = new(0.92f, 0.86f, 0.72f);
        public static readonly Rgb ChipFaceRedAccent = new(0.94f, 0.84f, 0.72f);
        public static readonly Rgb ChipRimStandard = new(0.36f, 0.26f, 0.14f);
        public static readonly Rgb ChipRimRedAccent = new(0.58f, 0.12f, 0.08f);
        public static readonly Rgb LabelStandard = new(0.08f, 0.08f, 0.08f);
        public static readonly Rgb LabelRed = new(0.82f, 0.12f, 0.10f);
        public static readonly Rgb ChipFaceFishGround = new(0.14f, 0.34f, 0.58f);
        public static readonly Rgb ChipRimFishGround = new(0.08f, 0.20f, 0.42f);
        public static readonly Rgb LabelFishGround = new(0.98f, 0.98f, 0.96f);

        public static bool UsesFishGroundChip(bool isFishTile) => isFishTile;

        public static Rgb GetChipFace(int token, bool fishGround = false)
        {
            if (fishGround)
                return ChipFaceFishGround;
            return NumberTokenLibrary.IsRedNumber(token) ? ChipFaceRedAccent : ChipFaceStandard;
        }

        public static Rgb GetChipRim(int token, bool fishGround = false)
        {
            if (fishGround)
                return ChipRimFishGround;
            return NumberTokenLibrary.IsRedNumber(token) ? ChipRimRedAccent : ChipRimStandard;
        }

        public static Rgb GetLabel(int token, bool fishGround = false)
        {
            if (NumberTokenLibrary.IsRedNumber(token))
                return LabelRed;
            if (fishGround)
                return LabelFishGround;
            return LabelStandard;
        }

        /// <summary>Smaller glyphs so two-digit tokens (10–12) fit on the chip (TextMesh local units).</summary>
        public static float GetLabelCharacterSize(int token) =>
            token >= 10 ? 0.52f : 0.62f;

        public readonly struct Rgb
        {
            public readonly float R;
            public readonly float G;
            public readonly float B;

            public Rgb(float r, float g, float b)
            {
                R = r;
                G = g;
                B = b;
            }

            public override string ToString() => $"({R:F3},{G:F3},{B:F3})";
        }
    }
}

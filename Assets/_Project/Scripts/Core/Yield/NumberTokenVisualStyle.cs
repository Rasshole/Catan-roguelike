namespace CatanRoguelike.Core.Yield
{
    /// <summary>
    /// Token chip colors and label sizing for board rendering (RGB 0–1).
    /// Game layer maps <see cref="Rgb"/> to UnityEngine.Color.
    /// </summary>
    public static class NumberTokenVisualStyle
    {
        public static readonly Rgb ChipFaceStandard = new(0.95f, 0.90f, 0.78f);
        public static readonly Rgb ChipFaceRedAccent = new(0.96f, 0.86f, 0.76f);
        public static readonly Rgb ChipRimStandard = new(0.82f, 0.74f, 0.58f);
        public static readonly Rgb ChipRimRedAccent = new(0.78f, 0.38f, 0.28f);
        public static readonly Rgb LabelStandard = new(0.08f, 0.08f, 0.08f);
        public static readonly Rgb LabelRed = new(0.82f, 0.12f, 0.10f);

        public static Rgb GetChipFace(int token) =>
            NumberTokenLibrary.IsRedNumber(token) ? ChipFaceRedAccent : ChipFaceStandard;

        public static Rgb GetChipRim(int token) =>
            NumberTokenLibrary.IsRedNumber(token) ? ChipRimRedAccent : ChipRimStandard;

        public static Rgb GetLabel(int token) =>
            NumberTokenLibrary.IsRedNumber(token) ? LabelRed : LabelStandard;

        /// <summary>Smaller glyphs so two-digit tokens (10–12) fit on the chip.</summary>
        public static float GetLabelCharacterSize(int token) =>
            token >= 10 ? 0.42f : 0.5f;

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
        }
    }
}

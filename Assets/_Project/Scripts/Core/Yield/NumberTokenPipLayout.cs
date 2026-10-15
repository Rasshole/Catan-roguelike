using System;

namespace CatanRoguelike.Core.Yield
{
    /// <summary>Classic Catan pip dots under number tokens (2/12 → 1 dot … 6/8 → 5 dots).</summary>
    public static class NumberTokenPipLayout
    {
        public static int GetPipCount(int token) => NumberTokenLibrary.GetPipWeight(token);

        /// <summary>Normalized horizontal offsets (−0.5…0.5) for pip row layout.</summary>
        public static float[] GetPipOffsetsX(int pipCount)
        {
            if (pipCount <= 0)
                return Array.Empty<float>();

            const float step = 0.18f;
            float span = (pipCount - 1) * step;
            var offsets = new float[pipCount];
            for (int i = 0; i < pipCount; i++)
                offsets[i] = -span * 0.5f + i * step;
            return offsets;
        }
    }
}

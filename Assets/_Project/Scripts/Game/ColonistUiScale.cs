using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Matches <see cref="RunHudUiFactory"/> CanvasScaler (1920×1080 reference).</summary>
    public static class ColonistUiScale
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;
        public const float MatchWidthOrHeight = 0.5f;

        public static float GetScaleFactor(int screenWidth, int screenHeight)
        {
            float scaleW = screenWidth / ReferenceWidth;
            float scaleH = screenHeight / ReferenceHeight;
            float logW = Mathf.Log(Mathf.Max(scaleW, 1e-6f), 2f);
            float logH = Mathf.Log(Mathf.Max(scaleH, 1e-6f), 2f);
            float logBlend = Mathf.Lerp(logW, logH, MatchWidthOrHeight);
            return Mathf.Pow(2f, logBlend);
        }

        public static float ScaleReferencePixels(float referencePixels, int screenWidth, int screenHeight) =>
            referencePixels * GetScaleFactor(screenWidth, screenHeight);
    }
}

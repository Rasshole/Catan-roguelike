using System;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Fan geometry for the colonist roguelike card hand (screen-space).</summary>
    public static class ColonistRoguelikeCardHandLayout
    {
        public const float CardWidth = 92f;
        public const float CardHeight = 122f;
        public const float HorizontalStep = 54f;
        public const float RotationPerCard = 5f;
        public const float ArcDropPerStep = 2f;
        /// <summary>Resting fan scale; hover multiplies by <see cref="HoverScale"/>.</summary>
        public const float RestScale = 0.74f;
        /// <summary>Screen band the resting fan needs above the deck band bottom (root Y + scaled card + arc/tilt).</summary>
        public static float RestingFanHeight =>
            UnityEngine.Mathf.Max(RunHudLayout.ColonistResourceHandHeight, 12f + CardHeight * RestScale + 10f);
        public const float HoverLift = 32f;
        public const float HoverScale = 1.18f;

        public readonly struct CardPose
        {
            public readonly float X;
            public readonly float Y;
            public readonly float RotationZ;

            public CardPose(float x, float y, float rotationZ)
            {
                X = x;
                Y = y;
                RotationZ = rotationZ;
            }
        }

        public static CardPose[] ComputeFanPoses(int cardCount)
        {
            if (cardCount <= 0)
                return Array.Empty<CardPose>();

            var poses = new CardPose[cardCount];
            float mid = (cardCount - 1) * 0.5f;
            for (int i = 0; i < cardCount; i++)
            {
                float spread = i - mid;
                float x = spread * HorizontalStep;
                float y = -Mathf.Abs(spread) * ArcDropPerStep;
                float rot = -spread * RotationPerCard; // +Z is CCW: left cards lean left (fan), right cards lean right
                poses[i] = new CardPose(x, y, rot);
            }

            return poses;
        }
    }
}

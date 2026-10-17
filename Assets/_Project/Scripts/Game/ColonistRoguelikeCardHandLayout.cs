using System;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Fan geometry for the colonist roguelike card hand (screen-space).</summary>
    public static class ColonistRoguelikeCardHandLayout
    {
        public const float CardWidth = 108f;
        public const float CardHeight = 144f;
        public const float HorizontalStep = 68f; // 44 left only ~40 px of each card visible (titles cut to "Kni")
        public const float RotationPerCard = 5f;
        public const float ArcDropPerStep = 3.5f;
        public const float HoverLift = 28f;
        public const float HoverScale = 1.12f;

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

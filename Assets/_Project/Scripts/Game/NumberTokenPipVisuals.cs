using CatanRoguelike.Core.Yield;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Shared pip row layout for number tokens (colonist + table).</summary>
    public static class NumberTokenPipVisuals
    {
        public const float LabelLocalRowY = -1.75f;
        public const float LabelLocalRowZ = 0.1f;
        public const float LabelOffsetScale = 2.35f;
        public const float PipLocalScale = 0.2f;

        public static int GetExpectedPipCount(int token) => NumberTokenPipLayout.GetPipCount(token);

        public static void PlacePipOnLabelRow(Transform pip, float offsetX, Color pipColor)
        {
            pip.localPosition = new Vector3(offsetX * LabelOffsetScale, LabelLocalRowY, LabelLocalRowZ);
            pip.localScale = Vector3.one * PipLocalScale;
            var renderer = pip.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = BuiltInMaterials.Create(pipColor);
        }
    }
}

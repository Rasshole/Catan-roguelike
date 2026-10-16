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
        public const float MaxRowWidthFaceFraction = 0.7f;

        public static int GetExpectedPipCount(int token) => NumberTokenPipLayout.GetPipCount(token);

        public static void ComputeRowLayout(
            int pipCount,
            float faceDiameterWorld,
            float labelLossyScale,
            out float pipScale,
            out float step)
        {
            pipScale = 0.42f;
            step = 0.56f;
            if (pipCount <= 0)
                return;

            float maxRowLabel = MaxRowWidthFaceFraction * faceDiameterWorld / Mathf.Max(labelLossyScale, 0.001f);
            if (pipCount == 1)
            {
                pipScale = Mathf.Min(pipScale, maxRowLabel * 0.55f);
                step = 0f;
                return;
            }

            pipScale = Mathf.Min(0.42f, maxRowLabel * 0.38f);
            float spanBudget = Mathf.Max(0.05f, maxRowLabel - pipScale);
            step = Mathf.Min(0.56f, spanBudget / (pipCount - 1) / LabelOffsetScale);
        }

        public static void PlacePipOnLabelRow(Transform pip, float offsetX, Color pipColor, float pipScale)
        {
            pip.localPosition = new Vector3(offsetX * LabelOffsetScale, LabelLocalRowY, LabelLocalRowZ);
            pip.localScale = Vector3.one * pipScale;
            var renderer = pip.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = BuiltInMaterials.CreateUnlit(pipColor);
                renderer.sharedMaterial.renderQueue = 3310;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}

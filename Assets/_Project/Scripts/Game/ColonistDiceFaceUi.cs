using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    internal static class ColonistDiceFaceUi
    {
        // 3×3 pip grid indices: 0–2 top row, 3–5 middle, 6–8 bottom.
        private static readonly int[] PipMask = { 0, 16, 257, 273, 325, 341, 365 };

        public static void Build(Transform parent, out Image[] pipImages)
        {
            pipImages = new Image[9];
            float cell = 14f;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    int index = row * 3 + col;
                    var pipGo = new GameObject($"Pip_{index}");
                    pipGo.transform.SetParent(parent, false);
                    var rect = pipGo.AddComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(8f, 8f);
                    rect.anchoredPosition = new Vector2((col - 1) * cell, (1 - row) * cell);
                    var img = pipGo.AddComponent<Image>();
                    img.color = new Color(0.12f, 0.14f, 0.18f, 1f);
                    img.enabled = false;
                    pipImages[index] = img;
                }
            }
        }

        public static void SetValue(Image[] pipImages, int value)
        {
            if (pipImages == null || pipImages.Length != 9)
                return;

            value = Mathf.Clamp(value, 0, 6);
            int mask = value >= 0 && value < PipMask.Length ? PipMask[value] : 0;
            for (int i = 0; i < 9; i++)
            {
                bool on = (mask & (1 << i)) != 0;
                if (pipImages[i] != null)
                    pipImages[i].enabled = on;
            }
        }
    }
}

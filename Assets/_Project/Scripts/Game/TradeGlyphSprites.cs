using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v138 procedural glyphs for the icon-first trade board: the red "you give" arrow (pointing up, away from your
    /// hand), the green "you get" arrow (pointing down, toward your hand), and the ✓ / ✗ / counter (⇄) answer marks.
    /// Drawn as anti-aliased strokes with a dark ink outline so they read on parchment at any size.
    /// </summary>
    public static class TradeGlyphSprites
    {
        public static readonly Color GiveRed = new(0.80f, 0.20f, 0.13f, 1f);
        public static readonly Color GetGreen = new(0.24f, 0.60f, 0.18f, 1f);
        public static readonly Color CounterGold = new(0.80f, 0.56f, 0.12f, 1f);
        private static readonly Color Outline = new(0.20f, 0.12f, 0.06f, 1f);

        private static Sprite _give, _get, _check, _cross, _swap, _checkInk, _crossInk;

        public static Sprite GiveArrow => _give ??= Make("TradeGiveArrow", GiveRed, ArrowUp, 0.11f);
        public static Sprite GetArrow => _get ??= Make("TradeGetArrow", GetGreen, ArrowDown, 0.11f);
        public static Sprite Check => _check ??= Make("TradeCheck", GetGreen, CheckStrokes, 0.10f);
        public static Sprite Cross => _cross ??= Make("TradeCross", GiveRed, CrossStrokes, 0.10f);
        public static Sprite Counter => _swap ??= Make("TradeCounter", CounterGold, SwapStrokes, 0.075f);

        /// <summary>Cream marks for green / red buttons (no outline).</summary>
        public static Sprite CheckCream => _checkInk ??= Make("TradeCheckCream", ColonistUiKit.Cream, CheckStrokes, 0.10f, false);

        public static Sprite CrossCream => _crossInk ??= Make("TradeCrossCream", ColonistUiKit.Cream, CrossStrokes, 0.10f, false);

        private static readonly Vector2[][] ArrowUp =
        {
            new[] { new Vector2(0.5f, 0.14f), new Vector2(0.5f, 0.80f) },
            new[] { new Vector2(0.22f, 0.54f), new Vector2(0.5f, 0.84f), new Vector2(0.78f, 0.54f) }
        };

        private static readonly Vector2[][] ArrowDown =
        {
            new[] { new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0.20f) },
            new[] { new Vector2(0.22f, 0.46f), new Vector2(0.5f, 0.16f), new Vector2(0.78f, 0.46f) }
        };

        private static readonly Vector2[][] CheckStrokes =
        {
            new[] { new Vector2(0.18f, 0.52f), new Vector2(0.40f, 0.28f), new Vector2(0.82f, 0.74f) }
        };

        private static readonly Vector2[][] CrossStrokes =
        {
            new[] { new Vector2(0.24f, 0.24f), new Vector2(0.76f, 0.76f) },
            new[] { new Vector2(0.24f, 0.76f), new Vector2(0.76f, 0.24f) }
        };

        private static readonly Vector2[][] SwapStrokes =
        {
            new[] { new Vector2(0.16f, 0.66f), new Vector2(0.80f, 0.66f) },
            new[] { new Vector2(0.64f, 0.82f), new Vector2(0.82f, 0.66f), new Vector2(0.64f, 0.50f) },
            new[] { new Vector2(0.84f, 0.34f), new Vector2(0.20f, 0.34f) },
            new[] { new Vector2(0.36f, 0.50f), new Vector2(0.18f, 0.34f), new Vector2(0.36f, 0.18f) }
        };

        private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        private static Sprite Make(string name, Color fill, Vector2[][] strokes, float halfWidth, bool outline = true)
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, name = name };
            var px = new Color32[n * n];
            float outlineW = outline ? 0.045f : 0f;
            float aa = 1.2f / n;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                float d = float.MaxValue;
                foreach (var poly in strokes)
                    for (int i = 0; i + 1 < poly.Length; i++)
                        d = Mathf.Min(d, SegDist(p, poly[i], poly[i + 1]));
                float inner = Mathf.Clamp01((halfWidth - d) / aa + 0.5f);
                float outer = Mathf.Clamp01((halfWidth + outlineW - d) / aa + 0.5f);
                var c = Color.Lerp(Outline, fill, outline ? inner : 1f);
                c.a = outline ? outer : inner;
                px[y * n + x] = c;
            }

            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }
    }
}

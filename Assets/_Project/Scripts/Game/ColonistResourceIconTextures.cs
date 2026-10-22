using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Cached procedural resource silhouettes for colonist hex icons.</summary>
    public static class ColonistResourceIconTextures
    {
        private const int Size = 64;
        private static readonly Dictionary<ResourceType, Texture2D> BoardCache = new();
        private static readonly Dictionary<ResourceType, Texture2D> HudCache = new();
        private static readonly Dictionary<ResourceType, Texture2D> PortBadgeCache = new();

        private static readonly Color32 PortBadgeWoodInk = new(118, 74, 34, 255);

        public static Texture2D Get(ResourceType resource) => GetTexture(BoardCache, resource, forHud: false);

        public static Texture2D GetForHud(ResourceType resource) => GetTexture(HudCache, resource, forHud: true);

        private static readonly Dictionary<ResourceType, Texture2D> PortDecorCache = new();

        /// <summary>Folder of the AI-painted, keyed resource icons (tools/art/make_painted_icons.py).</summary>
        public const string PaintedIconFolder = "Art/Icons/Painted";

        /// <summary>The painted icon for <paramref name="resource"/> (Wood, Brick, Wheat, Sheep, Stone), or null.</summary>
        public static Texture2D GetPainted(ResourceType resource) =>
            Resources.Load<Texture2D>($"{PaintedIconFolder}/{resource}");

        public static Texture2D GetPaintedFish() => Resources.Load<Texture2D>($"{PaintedIconFolder}/Fish");

        /// <summary>Icon used across the UI (HUD cards, build costs, trade, port badges): the painted icon.</summary>
        public static Texture2D GetForPortBadge(ResourceType resource)
        {
            if (PortDecorCache.TryGetValue(resource, out var cached) && cached != null)
                return cached;

            var painted = GetPainted(resource);
            if (painted != null)
            {
                PortDecorCache[resource] = painted;
                return painted;
            }

            if (resource == ResourceType.Wheat)
            {
                var kenneyWheat = Resources.Load<Texture2D>("Art/Icons/Wheat");
                if (kenneyWheat != null)
                {
                    PortDecorCache[resource] = kenneyWheat;
                    return kenneyWheat;
                }
            }

            string sprite = resource switch
            {
                ResourceType.Wood => "tree_canopy_b",
                ResourceType.Wheat => "wheat_sheaf",
                ResourceType.Brick => "brick_stack",
                ResourceType.Stone => "rock_cluster_b",
                _ => null
            };

            if (sprite != null)
            {
                var decor = Resources.Load<Texture2D>($"Art/Board/TopDownDecor/{sprite}");
                if (decor != null)
                {
                    PortDecorCache[resource] = decor;
                    return decor;
                }
            }

            // No white Kenney glyph here: on the cream badge it vanished (v106 review). Sheep keeps
            // the outlined procedural sheep with a dark head, as in v105.
            return GetTexture(PortBadgeCache, resource, forHud: true, inkOverride: GetPortBadgeInk(resource));
        }

        private static Color32 GetPortBadgeInk(ResourceType resource) =>
            resource == ResourceType.Wood
                ? PortBadgeWoodInk
                : ToInk(BoardResourceColors.GetHudSwatch(resource));

        private static Color32 ToInk(Color tint) =>
            new(
                (byte)(tint.r * 255f),
                (byte)(tint.g * 255f),
                (byte)(tint.b * 255f),
                255);

        private static Texture2D GetTexture(
            Dictionary<ResourceType, Texture2D> cache,
            ResourceType resource,
            bool forHud,
            Color32? inkOverride = null)
        {
            if (cache.TryGetValue(resource, out var cached) && cached != null)
                return cached;

            // The flat grey ore silhouette on the grey stone hex read as a see-through blob: on the
            // board use the Kenney ore boulder (flecked rock) instead; HUD/port icons keep silhouettes.
            // Same for brick: the terracotta wall silhouette on the terracotta hex read as a faint
            // clay wall (v106 review), so the board corner icon uses the outlined clay mound (round,
            // so the BoardPitchOnly quad's fixed in-plane rotation doesn't turn it into a wall).
            if ((resource == ResourceType.Stone || resource == ResourceType.Brick) && !forHud && !inkOverride.HasValue)
            {
                var ore = Resources.Load<Texture2D>(resource == ResourceType.Stone
                    ? "Art/Board/TopDownDecor/rock_cluster_c"
                    : "Art/Board/TopDownDecor/clay_pit");
                if (ore != null)
                {
                    cache[resource] = ore;
                    return ore;
                }
            }

            bool isPortBadge = inkOverride.HasValue && ReferenceEquals(cache, PortBadgeCache);
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = isPortBadge
                    ? $"ColonistPortBadgeIcon_{resource}"
                    : forHud
                        ? $"ColonistHudIcon_{resource}"
                        : $"ColonistIcon_{resource}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, 0);

            var ink = inkOverride ?? ToInk(BoardResourceColors.GetHudSwatch(resource));

            switch (resource)
            {
                case ResourceType.Wood:
                    if (forHud)
                        DrawLogStack(pixels, ink);
                    else
                        DrawTree(pixels, ink);
                    break;
                case ResourceType.Brick:
                    DrawBrickStack(pixels, ink);
                    break;
                case ResourceType.Wheat:
                    DrawWheatSheaf(pixels, ink);
                    break;
                case ResourceType.Sheep:
                    DrawSheepBlob(pixels, ink);
                    break;
                case ResourceType.Stone:
                    DrawOreRock(pixels, ink);
                    break;
                default:
                    FillCircle(pixels, Size / 2, Size / 2, 14, ink);
                    break;
            }

            if (resource == ResourceType.Wheat)
                RotatePixels90Clockwise(pixels);
            if (resource == ResourceType.Wood && !forHud)
            {
                RotatePixels90Clockwise(pixels);
                RotatePixels180(pixels);
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            cache[resource] = tex;
            return tex;
        }

        private static void RotatePixels90Clockwise(Color32[] pixels)
        {
            var rotated = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int src = y * Size + x;
                    int dstX = Size - 1 - y;
                    int dstY = x;
                    rotated[dstY * Size + dstX] = pixels[src];
                }
            }

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = rotated[i];
        }

        private static void RotatePixels180(Color32[] pixels)
        {
            var rotated = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                    rotated[(Size - 1 - y) * Size + (Size - 1 - x)] = pixels[y * Size + x];
            }

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = rotated[i];
        }

        private static void DrawTree(Color32[] px, Color32 c)
        {
            FillRect(px, 29, 10, 6, 16, c);
            DrawFilledTriangle(px, 32, 50, 14, 34, 50, 34, c);
            DrawFilledTriangle(px, 32, 46, 16, 30, 46, 30, c);
            DrawFilledTriangle(px, 32, 40, 18, 26, 40, 26, c);
        }

        private static void DrawLogStack(Color32[] px, Color32 c)
        {
            var ring = Darken(c, 0.72f);
            FillRect(px, 12, 22, 40, 9, c);
            FillCircle(px, 12, 26, 5, ring);
            FillRect(px, 10, 34, 42, 9, c);
            FillCircle(px, 10, 38, 5, ring);
            FillRect(px, 14, 46, 38, 9, c);
            FillCircle(px, 52, 50, 5, ring);
        }

        private static Color32 Darken(Color32 c, float factor) =>
            new Color32(
                (byte)(c.r * factor),
                (byte)(c.g * factor),
                (byte)(c.b * factor),
                c.a);

        private static void DrawFilledTriangle(Color32[] px, int x0, int y0, int x1, int y1, int x2, int y2, Color32 c)
        {
            int minY = Mathf.Min(y0, Mathf.Min(y1, y2));
            int maxY = Mathf.Max(y0, Mathf.Max(y1, y2));
            int minX = Mathf.Min(x0, Mathf.Min(x1, x2));
            int maxX = Mathf.Max(x0, Mathf.Max(x1, x2));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (PointInTriangle(x, y, x0, y0, x1, y1, x2, y2))
                        px[y * Size + x] = c;
                }
            }
        }

        private static bool PointInTriangle(
            int px,
            int py,
            int x0,
            int y0,
            int x1,
            int y1,
            int x2,
            int y2)
        {
            float d1 = TriangleEdgeSign(px, py, x0, y0, x1, y1);
            float d2 = TriangleEdgeSign(px, py, x1, y1, x2, y2);
            float d3 = TriangleEdgeSign(px, py, x2, y2, x0, y0);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        private static float TriangleEdgeSign(int px, int py, int x0, int y0, int x1, int y1) =>
            (px - x1) * (y0 - y1) - (x0 - x1) * (py - y1);

        private static void DrawBrickStack(Color32[] px, Color32 c)
        {
            for (int row = 0; row < 3; row++)
            {
                int y = 20 + row * 10;
                int x0 = row % 2 == 0 ? 16 : 22;
                for (int b = 0; b < 3; b++)
                    FillRect(px, x0 + b * 14, y, 12, 8, c);
            }
        }

        private static void DrawWheatSheaf(Color32[] px, Color32 c)
        {
            for (int i = 0; i < 7; i++)
            {
                int x = 18 + i * 5;
                FillRect(px, x, 22, 3, 28, c);
                FillCircle(px, x + 1, 50, 3, c);
            }
            FillRect(px, 30, 16, 4, 10, c);
        }

        private static void DrawSheepBlob(Color32[] px, Color32 c)
        {
            // v114: scaled up ~1.3x about the body centre with a dark rim so the sheep fills the
            // badge/card like the other resource sprites (it read noticeably smaller on 2:1 ports).
            var wool = new Color32(245, 245, 250, 255);
            var dark = new Color32(38, 34, 40, 255);
            int[,] woolCircles = { { 32, 38, 17 }, { 22, 36, 9 }, { 44, 36, 9 }, { 18, 32, 7 }, { 48, 32, 7 } };
            for (int i = 0; i < woolCircles.GetLength(0); i++)
                SheepCircle(px, woolCircles[i, 0], woolCircles[i, 1], woolCircles[i, 2], 2, dark);
            for (int i = 0; i < woolCircles.GetLength(0); i++)
                SheepCircle(px, woolCircles[i, 0], woolCircles[i, 1], woolCircles[i, 2], 0, wool);
            SheepCircle(px, 40, 44, 7, 0, dark);
            SheepRect(px, 36, 48, 4, 8, dark);
            SheepRect(px, 42, 48, 4, 8, dark);
            SheepCircle(px, 44, 42, 4, 0, dark);
        }

        private const float SheepScale = 1.3f;
        private const float SheepCx = 33f;
        private const float SheepCy = 38.5f;

        private static void SheepCircle(Color32[] px, int cx, int cy, int r, int extra, Color32 c) =>
            FillCircle(
                px,
                Mathf.RoundToInt(SheepCx + (cx - SheepCx) * SheepScale),
                Mathf.RoundToInt(SheepCy + (cy - SheepCy) * SheepScale),
                Mathf.RoundToInt(r * SheepScale) + extra,
                c);

        private static void SheepRect(Color32[] px, int x, int y, int w, int h, Color32 c) =>
            FillRect(
                px,
                Mathf.RoundToInt(SheepCx + (x - SheepCx) * SheepScale),
                Mathf.RoundToInt(SheepCy + (y - SheepCy) * SheepScale),
                Mathf.RoundToInt(w * SheepScale),
                Mathf.Min(Size - Mathf.RoundToInt(SheepCy + (y - SheepCy) * SheepScale), Mathf.RoundToInt(h * SheepScale)),
                c);

        private static void DrawOreRock(Color32[] px, Color32 c)
        {
            FillCircle(px, 28, 38, 12, c);
            FillCircle(px, 40, 34, 10, c);
            FillCircle(px, 34, 28, 9, c);
            FillRect(px, 22, 42, 24, 6, c);
        }

        private static void FillCircle(Color32[] px, int cx, int cy, int r, Color32 c)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= Size || y < 0 || y >= Size)
                        continue;
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy <= r2)
                        px[y * Size + x] = c;
                }
            }
        }

        private static void FillRect(Color32[] px, int x, int y, int w, int h, Color32 c)
        {
            for (int py = y; py < y + h; py++)
            {
                for (int pxX = x; pxX < x + w; pxX++)
                {
                    if (pxX < 0 || pxX >= Size || py < 0 || py >= Size)
                        continue;
                    px[py * Size + pxX] = c;
                }
            }
        }
    }
}

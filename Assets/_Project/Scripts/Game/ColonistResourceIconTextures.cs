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

        public static Texture2D GetForPortBadge(ResourceType resource) =>
            GetTexture(PortBadgeCache, resource, forHud: true, inkOverride: GetPortBadgeInk(resource));

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

            var kenney = ColonistKenneyResourceIcons.TryGet(resource);
            if (kenney != null)
            {
                cache[resource] = kenney;
                return kenney;
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
            var wool = new Color32(245, 245, 250, 255);
            var dark = new Color32(38, 34, 40, 255);
            FillCircle(px, 32, 38, 17, wool);
            FillCircle(px, 22, 36, 9, wool);
            FillCircle(px, 44, 36, 9, wool);
            FillCircle(px, 18, 32, 7, wool);
            FillCircle(px, 48, 32, 7, wool);
            FillCircle(px, 40, 44, 7, dark);
            FillRect(px, 36, 48, 4, 8, dark);
            FillRect(px, 42, 48, 4, 8, dark);
            FillCircle(px, 44, 42, 4, dark);
        }

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

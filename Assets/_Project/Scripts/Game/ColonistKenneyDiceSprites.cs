using System.Collections.Generic;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Kenney Boardgame Pack dice faces (CC0).</summary>
    public static class ColonistKenneyDiceSprites
    {
        private const string DiceRoot = "Art/Boardgame/Dice";
        private static readonly Dictionary<int, Sprite> Cache = new();

        public static Sprite TryGetFace(int value)
        {
            value = Mathf.Clamp(value, 0, 6);
            if (value == 0)
                return null;

            if (Cache.TryGetValue(value, out var cached) && cached != null)
                return cached;

            var tex = Resources.Load<Texture2D>($"{DiceRoot}/DieWhite{value}");
            if (tex == null)
                return null;

            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            Cache[value] = sprite;
            return sprite;
        }

        public static Sprite TryGetCardBack()
        {
            var tex = Resources.Load<Texture2D>("Art/Boardgame/Cards/CardBack_blue");
            if (tex == null)
                return null;

            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }
    }
}
